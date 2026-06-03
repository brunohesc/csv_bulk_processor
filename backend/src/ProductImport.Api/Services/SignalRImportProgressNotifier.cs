using Microsoft.AspNetCore.SignalR;
using ProductImport.Api.Hubs;
using ProductImport.Core.Interfaces;

namespace ProductImport.Api.Services;

public class SignalRImportProgressNotifier : IImportProgressNotifier
{
    private readonly IHubContext<ImportProgressHub> _hubContext;

    public SignalRImportProgressNotifier(IHubContext<ImportProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendProgressUpdateAsync(Guid importJobId, int processed, int percentage, int failed, CancellationToken cancellationToken = default)
    {
        var progress = new Core.DTOs.ImportProgressDto
        {
            ImportJobId = importJobId,
            Processed = processed,
            Total = processed, // Use processed as total since we don't know the final count yet
            Percentage = percentage,
            Status = "Processing",
            FailedCount = failed
        };

        await _hubContext.Clients.Group(importJobId.ToString()).SendAsync("ProgressUpdate", progress, cancellationToken);
    }

    public async Task SendCompleteAsync(Guid importJobId, int successCount, int failedCount, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(importJobId.ToString()).SendAsync("ImportComplete", new
        {
            ImportJobId = importJobId,
            SuccessCount = successCount,
            FailedCount = failedCount
        }, cancellationToken);
    }

    public async Task SendErrorAsync(Guid importJobId, string errorMessage, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(importJobId.ToString()).SendAsync("ImportError", new
        {
            ImportJobId = importJobId,
            ErrorMessage = errorMessage
        }, cancellationToken);
    }

    public async Task SendCancelledAsync(Guid importJobId, int processedCount, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(importJobId.ToString()).SendAsync("ImportCancelled", new
        {
            ImportJobId = importJobId,
            ProcessedCount = processedCount
        }, cancellationToken);
    }
}
