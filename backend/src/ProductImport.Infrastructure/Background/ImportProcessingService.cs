using System.Threading.Channels;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductImport.Application.Settings;
using ProductImport.Core.Entities;
using ProductImport.Core.Interfaces;
using System.Collections.Concurrent;

namespace ProductImport.Infrastructure.Background;

public class ImportJobRequest
{
    public Guid ImportJobId { get; set; }
    public Stream CsvStream { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class ImportProcessingService : BackgroundService
{
    private readonly Channel<ImportJobRequest> _jobQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportProcessingService> _logger;
    private readonly IOptionsMonitor<ImportSettings> _importSettings;
    private readonly IOptionsMonitor<CurrencySettings> _currencySettings;
    private readonly ConcurrentDictionary<Guid, bool> _cancelledJobs = new();

    /*
     * Bounded Concurrency Strategy Explanation:
     * 
     * We use a bounded Channel to control the number of concurrent imports:
     * 
     * 1. Channel capacity = MaxConcurrentImports (default: 5)
     * 2. When channel is full, EnqueueAsync waits (backpressure)
     * 3. This prevents unbounded memory growth and database connection exhaustion
     * 4. Each import job is processed independently by a single worker
     * 5. The BackgroundService reads from the channel and processes jobs sequentially
     *    (could be parallelized with multiple workers if needed)
     * 
     * This approach:
     * - Is simple and doesn't require external queue infrastructure
     * - Provides backpressure to prevent overload
     * - Is easy to scale up by increasing MaxConcurrentImports
     * - Can be replaced with RabbitMQ/Redis for distributed processing later
     */
    public ImportProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<ImportProcessingService> logger,
        IOptionsMonitor<ImportSettings> importSettings,
        IOptionsMonitor<CurrencySettings> currencySettings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _importSettings = importSettings;
        _currencySettings = currencySettings;

        // Bounded channel to limit concurrent imports
        _jobQueue = Channel.CreateBounded<ImportJobRequest>(new BoundedChannelOptions(_importSettings.CurrentValue.MaxConcurrentImports)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task EnqueueJobAsync(ImportJobRequest job, CancellationToken cancellationToken = default)
    {
        await _jobQueue.Writer.WriteAsync(job, cancellationToken);
        _logger.LogInformation("Job {ImportJobId} enqueued for processing", job.ImportJobId);
    }

    public void CancelJob(Guid jobId)
    {
        _cancelledJobs.TryAdd(jobId, true);
        _logger.LogInformation("Job {JobId} marked as cancelled in memory", jobId);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ImportProcessingService started");

        await foreach (var job in _jobQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessImportAsync(job, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error processing job {ImportJobId}", job.ImportJobId);
                using var scope = _scopeFactory.CreateScope();
                await MarkJobAsFailedAsync(job.ImportJobId, ex.Message, cancellationToken, scope.ServiceProvider);
                var progressNotifier = scope.ServiceProvider.GetRequiredService<IImportProgressNotifier>();
                await progressNotifier.SendErrorAsync(job.ImportJobId, ex.Message, cancellationToken);
            }
            finally
            {
                // Clean up cancellation flag
                _cancelledJobs.TryRemove(job.ImportJobId, out _);
            }
        }

        _logger.LogInformation("ImportProcessingService stopped");
    }

    private async Task ProcessImportAsync(ImportJobRequest job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        using var logScope = _logger.BeginScope("ImportJobId: {ImportJobId}", job.ImportJobId);

        var importJobRepository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
        var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var exchangeRateService = scope.ServiceProvider.GetRequiredService<IExchangeRateService>();
        var csvProcessor = scope.ServiceProvider.GetRequiredService<Services.CsvProcessor>();
        var progressNotifier = scope.ServiceProvider.GetRequiredService<IImportProgressNotifier>();

        _logger.LogInformation("Starting import processing for job {ImportJobId}", job.ImportJobId);

        try
        {
            // Update job status to Processing
            var importJob = await importJobRepository.GetImportJobAsync(job.ImportJobId, cancellationToken);
            if (importJob == null)
            {
                throw new InvalidOperationException($"Import job {job.ImportJobId} not found");
            }

            importJob.Status = ImportJobStatus.Processing;
            await importJobRepository.UpdateImportJobAsync(importJob, cancellationToken);

            // Fetch exchange rates once per import
            _logger.LogInformation("Fetching exchange rates for job {ImportJobId}", job.ImportJobId);
            var allRates = await exchangeRateService.GetExchangeRatesAsync(cancellationToken);

            // Filter to target currencies
            var currencySettings = _currencySettings.CurrentValue;
            _logger.LogInformation("Current target currencies: {TargetCurrencies}", string.Join(", ", currencySettings.TargetCurrencies));
            var targetRates = allRates
                .Where(r => currencySettings.TargetCurrencies.Contains(r.Key.ToUpper()))
                .ToDictionary(r => r.Key, r => r.Value);

            importJob.ExchangeRatesJson = JsonSerializer.Serialize(targetRates);
            await importJobRepository.UpdateImportJobAsync(importJob, cancellationToken);

            // Stream and process CSV using file size for progress tracking
            var batch = new List<Product>();
            var processedCount = 0;
            var failedCount = 0;
            var lastProgressUpdate = DateTime.UtcNow;
            var criticalError = null as string;
            var totalFileSize = job.FileSize;

            _logger.LogInformation("Processing CSV file of size: {FileSize} bytes", totalFileSize);

            await foreach (var (product, error) in csvProcessor.ProcessStreamAsync(
                job.CsvStream, job.ImportJobId, targetRates, cancellationToken))
            {

                if (error != null)
                {
                    // Check if this is a critical error (not a row validation error)
                    if (error.StartsWith("CSV header error") || error.StartsWith("CSV read error"))
                    {
                        criticalError = error;
                        break;
                    }
                    failedCount++;
                    continue;
                }

                if (product != null)
                {
                    batch.Add(product);
                    processedCount++;

                    // Check if job was cancelled after every 100 rows
                    if (processedCount % 100 == 0 && _cancelledJobs.ContainsKey(job.ImportJobId))
                    {
                        _logger.LogInformation("Import job {ImportJobId} was cancelled, stopping processing at {ProcessedCount} rows", job.ImportJobId, processedCount);

                        // Delete all products for this import job
                        await productRepository.DeleteProductsByImportJobIdAsync(job.ImportJobId, cancellationToken);

                        // Reset processed rows to 0
                        importJob.ProcessedRows = 0;
                        importJob.FailedRows = 0;
                        importJob.Status = ImportJobStatus.Cancelled;
                        importJob.CompletedAt = DateTime.UtcNow;
                        await importJobRepository.UpdateImportJobAsync(importJob, cancellationToken);

                        await progressNotifier.SendCancelledAsync(job.ImportJobId, 0, cancellationToken);
                        return;
                    }

                    // Batch insert when batch size reached
                    if (batch.Count >= _importSettings.CurrentValue.BatchSize)
                    {
                        await productRepository.BulkInsertProductsAsync(batch, cancellationToken);
                        batch.Clear();

                        importJob.ProcessedRows = processedCount;
                        importJob.FailedRows = failedCount;
                        await importJobRepository.UpdateImportJobAsync(importJob, cancellationToken);
                    }

                    // Send progress update based on ProgressUpdateRowCount
                    if (processedCount % _importSettings.CurrentValue.ProgressUpdateRowCount == 0)
                    {
                        var currentStreamPosition = job.CsvStream.CanSeek ? job.CsvStream.Position : 0;
                        var percentage = totalFileSize > 0 ? (int)((double)currentStreamPosition / totalFileSize * 100) : 0;
                        await progressNotifier.SendProgressUpdateAsync(job.ImportJobId, processedCount, percentage, failedCount, cancellationToken);
                    }
                }
            }

            // If there was a critical error, fail the job
            if (criticalError != null)
            {
                throw new InvalidOperationException(criticalError);
            }

            // Insert remaining batch
            if (batch.Count > 0)
            {
                await productRepository.BulkInsertProductsAsync(batch, cancellationToken);
            }

            // Update total rows and final status
            importJob.TotalRows = processedCount + failedCount;
            importJob.ProcessedRows = processedCount;
            importJob.FailedRows = failedCount;
            importJob.Status = ImportJobStatus.Completed;
            importJob.CompletedAt = DateTime.UtcNow;
            await importJobRepository.UpdateImportJobAsync(importJob, cancellationToken);

            // Send final progress update at 100%
            await progressNotifier.SendProgressUpdateAsync(job.ImportJobId, processedCount, 100, failedCount, cancellationToken);
            await progressNotifier.SendCompleteAsync(job.ImportJobId, processedCount, failedCount, cancellationToken);

            _logger.LogInformation("Import job {ImportJobId} completed successfully. Processed: {Processed}, Failed: {Failed}",
                job.ImportJobId, processedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import job {ImportJobId} failed", job.ImportJobId);
            await MarkJobAsFailedAsync(job.ImportJobId, ex.Message, cancellationToken, scope.ServiceProvider);
            await progressNotifier.SendErrorAsync(job.ImportJobId, ex.Message, cancellationToken);
            throw;
        }
        finally
        {
            await job.CsvStream.DisposeAsync();
        }
    }

    private async Task MarkJobAsFailedAsync(Guid importJobId, string errorMessage, CancellationToken cancellationToken, IServiceProvider serviceProvider)
    {
        var importJobRepository = serviceProvider.GetRequiredService<IImportJobRepository>();
        var importJob = await importJobRepository.GetImportJobAsync(importJobId, cancellationToken);
        if (importJob != null)
        {
            importJob.Status = ImportJobStatus.Failed;
            importJob.CompletedAt = DateTime.UtcNow;
            importJob.ErrorMessage = errorMessage;
            await importJobRepository.UpdateImportJobAsync(importJob, cancellationToken);
        }
    }
}
