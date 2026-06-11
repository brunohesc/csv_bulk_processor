using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProductImport.Core.Entities;
using ProductImport.Core.Interfaces;
using ProductImport.Infrastructure.Data;

namespace ProductImport.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;
    private readonly string _connectionString;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(AppDbContext context, IConfiguration configuration, ILogger<ProductRepository> logger)
    {
        _context = context;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
    }

    public async Task BulkInsertProductsAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default)
    {
        /*
         * PostgreSQL COPY Implementation Explanation:
         *
         * PostgreSQL COPY is a high-performance bulk import mechanism that bypasses much of the
         * SQL parsing overhead. It's significantly faster than individual INSERT statements or
         * even batch INSERTs because:
         *
         * 1. Binary format: Data is sent in PostgreSQL's native binary format, avoiding text parsing
         * 2. Single round-trip: All data sent in one network call
         * 3. Minimal WAL logging: Reduced write-ahead logging overhead
         * 4. Direct table access: Bypasses some trigger and constraint checking overhead
         *
         * For 200k+ rows, COPY can be 10-100x faster than individual INSERTs.
         *
         * The implementation below:
         * - Uses Npgsql's binary import feature
         * - Streams data to avoid memory pressure
         * - Handles all column types correctly (UUID, text, numeric, date, jsonb)
         */

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await using var writer = await connection.BeginBinaryImportAsync(
                    "COPY \"Products\" (\"Id\", \"Name\", \"OriginalPrice\", \"ExpirationDate\", \"ConvertedPricesJson\", \"CreatedAt\", \"ImportJobId\") " +
                    "FROM STDIN (FORMAT BINARY)",
                    cancellationToken);


                foreach (var product in products)
                {
                    writer.StartRow();

                    // id: UUID
                    writer.Write(product.Id, NpgsqlTypes.NpgsqlDbType.Uuid);

                    // name: TEXT
                    writer.Write(product.Name, NpgsqlTypes.NpgsqlDbType.Text);

                    // original_price: NUMERIC(18,2)
                    writer.Write(product.OriginalPrice, NpgsqlTypes.NpgsqlDbType.Numeric);

                    // expiration_date: DATE (nullable)
                    DateTime? expirationDateUtc = product.ExpirationDate.HasValue
                        ? DateTime.SpecifyKind(product.ExpirationDate.Value, DateTimeKind.Utc)
                        : null;
                    writer.Write(expirationDateUtc, NpgsqlTypes.NpgsqlDbType.TimestampTz);

                    // converted_prices_json: JSONB
                    writer.Write(product.ConvertedPricesJson, NpgsqlTypes.NpgsqlDbType.Jsonb);

                    // created_at: TIMESTAMP
                    DateTime createdAtUtc = DateTime.SpecifyKind(product.CreatedAt, DateTimeKind.Utc);
                    writer.Write(createdAtUtc, NpgsqlTypes.NpgsqlDbType.TimestampTz);

                    // import_job_id: UUID
                    writer.Write(product.ImportJobId, NpgsqlTypes.NpgsqlDbType.Uuid);
                }

                await writer.CompleteAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            // Commit transaction after writer is disposed (exits Copy state)
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk insert products");
            throw;
        }
    }

    public async Task<(IEnumerable<Product> Products, int TotalCount)> GetProductsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsQueryable();

        // Apply filters
        if (importJobId.HasValue)
        {
            query = query.Where(p => p.ImportJobId == importJobId.Value);
        }

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            query = query.Where(p => p.Name.ToLower().Contains(nameFilter.ToLower()));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.OriginalPrice >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.OriginalPrice <= maxPrice.Value);
        }

        if (expirationFrom.HasValue)
        {
            var fromDate = expirationFrom.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(expirationFrom.Value, DateTimeKind.Utc)
                : expirationFrom.Value.ToUniversalTime();
            query = query.Where(p => p.ExpirationDate >= fromDate);
        }

        if (expirationTo.HasValue)
        {
            var toDate = expirationTo.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(expirationTo.Value, DateTimeKind.Utc)
                : expirationTo.Value.ToUniversalTime();
            query = query.Where(p => p.ExpirationDate <= toDate);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting only if sortBy is provided
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            query = sortBy.ToLower() switch
            {
                "name" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name),
                "originalprice" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(p => p.OriginalPrice)
                    : query.OrderBy(p => p.OriginalPrice),
                "expirationdate" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(p => p.ExpirationDate)
                    : query.OrderBy(p => p.ExpirationDate),
                _ => query.OrderBy(p => p.Name)
            };
        }

        // Apply pagination
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (products, totalCount);
    }

    public async Task DeleteProductsByImportJobIdAsync(Guid importJobId, CancellationToken cancellationToken = default)
    {
        var deletedCount = await _context.Products
            .Where(p => p.ImportJobId == importJobId)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} products for import job {ImportJobId}", deletedCount, importJobId);
    }
}
