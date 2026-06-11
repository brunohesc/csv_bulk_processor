using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductImport.Core.Entities;
using ProductImport.Infrastructure.Data;
using ProductImport.Infrastructure.Repositories;
using Xunit;

namespace ProductImport.Tests.Repositories;

public class ProductRepositoryTests
{
    private readonly AppDbContext _context;
    private readonly ProductRepository _repository;

    public ProductRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        var logger = new NullLogger<ProductRepository>();
        var configuration = new ConfigurationBuilder().Build();
        _repository = new ProductRepository(_context, configuration, logger);
    }

    [Fact]
    public async Task GetProductsAsync_WithNoFilters_ReturnsAllProducts()
    {
        // Arrange
        var importJobId = Guid.NewGuid();
        _context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "Product A", OriginalPrice = 100, ImportJobId = importJobId },
            new Product { Id = Guid.NewGuid(), Name = "Product B", OriginalPrice = 200, ImportJobId = importJobId }
        );
        await _context.SaveChangesAsync();

        // Act
        var (products, totalCount) = await _repository.GetProductsAsync(
            importJobId, null, null, null, null, null, "name", "asc", 1, 10, default);

        // Assert
        Assert.Equal(2, totalCount);
        Assert.Equal(2, products.Count());
    }

    [Fact]
    public async Task GetProductsAsync_WithNameFilter_ReturnsFilteredProducts()
    {
        // Arrange
        var importJobId = Guid.NewGuid();
        _context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "Apple", OriginalPrice = 100, ImportJobId = importJobId },
            new Product { Id = Guid.NewGuid(), Name = "Banana", OriginalPrice = 200, ImportJobId = importJobId },
            new Product { Id = Guid.NewGuid(), Name = "Apricot", OriginalPrice = 150, ImportJobId = importJobId }
        );
        await _context.SaveChangesAsync();

        // Act
        var (products, totalCount) = await _repository.GetProductsAsync(
            importJobId, "App", null, null, null, null, "name", "asc", 1, 10, default);

        // Assert
        Assert.Equal(2, totalCount);
        Assert.All(products, p => Assert.Contains("App", p.Name));
    }

    [Fact]
    public async Task GetProductsAsync_WithPriceFilter_ReturnsFilteredProducts()
    {
        // Arrange
        var importJobId = Guid.NewGuid();
        _context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "Product A", OriginalPrice = 50, ImportJobId = importJobId },
            new Product { Id = Guid.NewGuid(), Name = "Product B", OriginalPrice = 100, ImportJobId = importJobId },
            new Product { Id = Guid.NewGuid(), Name = "Product C", OriginalPrice = 150, ImportJobId = importJobId }
        );
        await _context.SaveChangesAsync();

        // Act
        var (products, totalCount) = await _repository.GetProductsAsync(
            importJobId, null, 75, 125, null, null, "name", "asc", 1, 10, default);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Equal(100, products.First().OriginalPrice);
    }

    [Fact]
    public async Task GetProductsAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var importJobId = Guid.NewGuid();
        for (int i = 1; i <= 25; i++)
        {
            _context.Products.Add(
                new Product { Id = Guid.NewGuid(), Name = $"Product {i}", OriginalPrice = i * 10, ImportJobId = importJobId });
        }
        await _context.SaveChangesAsync();

        // Act
        var (products, totalCount) = await _repository.GetProductsAsync(
            importJobId, null, null, null, null, null, "name", "asc", 2, 10, default);

        // Assert
        Assert.Equal(25, totalCount);
        Assert.Equal(10, products.Count());
        Assert.Equal("Product 11", products.First().Name);
    }

    [Fact]
    public async Task GetProductsAsync_WithSorting_ReturnsSortedProducts()
    {
        // Arrange
        var importJobId = Guid.NewGuid();
        _context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "Product C", OriginalPrice = 300, ImportJobId = importJobId },
            new Product { Id = Guid.NewGuid(), Name = "Product A", OriginalPrice = 100, ImportJobId = importJobId },
            new Product { Id = Guid.NewGuid(), Name = "Product B", OriginalPrice = 200, ImportJobId = importJobId }
        );
        await _context.SaveChangesAsync();

        // Act
        var (products, _) = await _repository.GetProductsAsync(
            importJobId, null, null, null, null, null, "price", "desc", 1, 10, default);

        // Assert
        Assert.Equal(3, products.Count());
        Assert.Equal("Product C", products.First().Name);
        Assert.Equal("Product A", products.Last().Name);
    }

    [Fact]
    public async Task DeleteProductsByImportJobIdAsync_DeletesCorrectProducts()
    {
        // Arrange
        var importJobId1 = Guid.NewGuid();
        var importJobId2 = Guid.NewGuid();
        _context.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "Product A", ImportJobId = importJobId1 },
            new Product { Id = Guid.NewGuid(), Name = "Product B", ImportJobId = importJobId1 },
            new Product { Id = Guid.NewGuid(), Name = "Product C", ImportJobId = importJobId2 }
        );
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteProductsByImportJobIdAsync(importJobId1, default);

        // Assert
        var remainingProducts = await _context.Products.ToListAsync();
        Assert.Single(remainingProducts);
        Assert.Equal("Product C", remainingProducts.First().Name);
    }
}
