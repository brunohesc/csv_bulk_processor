using ProductImport.Core.Entities;

namespace ProductImport.Core.Interfaces;

public interface IProductRepository
{
    Task BulkInsertProductsAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Product> Products, int TotalCount)> GetProductsAsync(
        Guid? importJobId,
        string? nameFilter,
        decimal? minPrice,
        decimal? maxPrice,
        DateTime? expirationFrom,
        DateTime? expirationTo,
        string sortBy,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task DeleteProductsByImportJobIdAsync(Guid importJobId, CancellationToken cancellationToken = default);
}
