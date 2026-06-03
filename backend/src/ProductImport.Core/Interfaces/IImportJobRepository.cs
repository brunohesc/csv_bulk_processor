using ProductImport.Core.Entities;

namespace ProductImport.Core.Interfaces;

public interface IImportJobRepository
{
    Task<Guid> CreateImportJobAsync(ImportJob importJob, CancellationToken cancellationToken = default);
    Task UpdateImportJobAsync(ImportJob importJob, CancellationToken cancellationToken = default);
    Task<ImportJob?> GetImportJobAsync(Guid id, CancellationToken cancellationToken = default);
}
