using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductImport.Application.Settings;
using ProductImport.Core.Entities;
using ProductImport.Core.Interfaces;
using ProductImport.Infrastructure.Background;

namespace ProductImport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly IImportJobRepository _importJobRepository;
    private readonly ImportProcessingService _processingService;
    private readonly ILogger<ImportController> _logger;
    private readonly long _maxFileSizeBytes;

    public ImportController(
        IImportJobRepository importJobRepository,
        ImportProcessingService processingService,
        ILogger<ImportController> logger,
        IOptions<AppSettings> appSettings)
    {
        _importJobRepository = importJobRepository;
        _processingService = processingService;
        _logger = logger;
        _maxFileSizeBytes = appSettings.Value.Import.MaxFileSizeBytes;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only CSV files are allowed");
        }

        if (file.Length > _maxFileSizeBytes)
        {
            return BadRequest($"File size exceeds maximum allowed size of {_maxFileSizeBytes / (1024 * 1024)}MB");
        }

        using var scope = _logger.BeginScope("File: {FileName}, Size: {FileSize}", file.FileName, file.Length);

        try
        {
            // Create import job
            var importJob = new ImportJob(file.FileName, file.Length);

            var jobId = await _importJobRepository.CreateImportJobAsync(importJob, cancellationToken);
            _logger.LogInformation("Created import job {JobId} for file {FileName}", jobId, file.FileName);

            // Copy stream to MemoryStream to prevent disposal issues
            using var originalStream = file.OpenReadStream();
            var memoryStream = new MemoryStream();
            await originalStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0; // Reset position for reading

            // Enqueue job for background processing (background service will dispose the stream)
            var job = new ImportJobRequest
            {
                ImportJobId = jobId,
                CsvStream = memoryStream,
                FileName = file.FileName,
                FileSize = file.Length
            };

            await _processingService.EnqueueJobAsync(job, cancellationToken);

            return Ok(new { importJobId = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
            return StatusCode(500, $"An error occurred while processing the upload: {ex.GetBaseException().Message}");
        }
    }

    [HttpPost("{jobId}/cancel")]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var importJob = await _importJobRepository.GetImportJobAsync(jobId, cancellationToken);
            if (importJob == null)
            {
                return NotFound("Import job not found");
            }

            if (importJob.Status != ImportJobStatus.Processing)
            {
                return BadRequest("Import job is not currently processing");
            }

            // Mark job as cancelled in memory for immediate effect
            _processingService.CancelJob(jobId);

            // Also update database for persistence
            importJob.Status = ImportJobStatus.Cancelled;
            importJob.CompletedAt = DateTime.UtcNow;
            await _importJobRepository.UpdateImportJobAsync(importJob, cancellationToken);

            _logger.LogInformation("Import job {JobId} cancelled", jobId);

            return Ok(new { message = "Import job cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling import job {JobId}", jobId);
            return StatusCode(500, $"An error occurred while cancelling the import: {ex.GetBaseException().Message}");
        }
    }
}
