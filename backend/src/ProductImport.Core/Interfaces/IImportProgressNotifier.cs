namespace ProductImport.Core.Interfaces;

public interface IImportProgressNotifier
{
    Task SendProgressUpdateAsync(Guid importJobId, int processed, int percentage, int failed, CancellationToken cancellationToken = default);
    Task SendCompleteAsync(Guid importJobId, int successCount, int failedCount, CancellationToken cancellationToken = default);
    Task SendErrorAsync(Guid importJobId, string errorMessage, CancellationToken cancellationToken = default);
    Task SendCancelledAsync(Guid importJobId, int processedCount, CancellationToken cancellationToken = default);
}
