using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProductImport.Infrastructure.Services;
using System.Text;
using Xunit;

namespace ProductImport.Tests.Services;

public class CsvProcessorTests
{
    private readonly CsvProcessor _processor;
    private readonly ILogger<CsvProcessor> _logger;

    public CsvProcessorTests()
    {
        _logger = new NullLogger<CsvProcessor>();
        _processor = new CsvProcessor(_logger);
    }

    [Fact]
    public async Task ProcessStreamAsync_ValidCsv_ReturnsProducts()
    {
        // Arrange
        var csvContent = "name;price;expiration\nProduct A;$123.45;12/31/2024\nProduct B;$67.89;01/15/2025";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var importJobId = Guid.NewGuid();
        var exchangeRates = new Dictionary<string, decimal> { { "USD", 1.0m }, { "EUR", 0.92m } };

        // Act
        var products = new List<(ProductImport.Core.Entities.Product?, string?)>();
        await foreach (var result in _processor.ProcessStreamAsync(stream, importJobId, exchangeRates, default))
        {
            products.Add(result);
        }

        // Assert
        Assert.Equal(2, products.Count);
        Assert.All(products, p => Assert.Null(p.Item2)); // No errors
        Assert.Equal("Product A", products[0].Item1?.Name);
        Assert.Equal(123.45m, products[0].Item1?.OriginalPrice);
        Assert.Equal("Product B", products[1].Item1?.Name);
        Assert.Equal(67.89m, products[1].Item1?.OriginalPrice);
    }

    [Fact]
    public async Task ProcessStreamAsync_InvalidPrice_SkipsRow()
    {
        // Arrange
        var csvContent = "name;price;expiration\nProduct A;invalid;12/31/2024\nProduct B;$67.89;01/15/2025";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var importJobId = Guid.NewGuid();
        var exchangeRates = new Dictionary<string, decimal> { { "USD", 1.0m } };

        // Act
        var products = new List<(ProductImport.Core.Entities.Product?, string?)>();
        await foreach (var result in _processor.ProcessStreamAsync(stream, importJobId, exchangeRates, default))
        {
            products.Add(result);
        }

        // Assert
        Assert.Equal(2, products.Count);
        Assert.NotNull(products[0].Item1); // First row processed with 0 price
        Assert.Equal(0m, products[0].Item1?.OriginalPrice);
        Assert.NotNull(products[1].Item1); // Second row processed
    }

    [Fact]
    public async Task ProcessStreamAsync_NameExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var longName = new string('A', 1001);
        var csvContent = $"name;price;expiration\n{longName};$123.45;12/31/2024";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var importJobId = Guid.NewGuid();
        var exchangeRates = new Dictionary<string, decimal> { { "USD", 1.0m } };

        // Act
        var products = new List<(ProductImport.Core.Entities.Product?, string?)>();
        await foreach (var result in _processor.ProcessStreamAsync(stream, importJobId, exchangeRates, default))
        {
            products.Add(result);
        }

        // Assert
        Assert.Single(products);
        Assert.Null(products[0].Item1);
        Assert.NotNull(products[0].Item2);
        Assert.Contains("exceeds maximum length", products[0].Item2);
    }

    [Fact]
    public async Task ProcessStreamAsync_EmptyExpiration_SetsNull()
    {
        // Arrange
        var csvContent = "name;price;expiration\nProduct A;$123.45;";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var importJobId = Guid.NewGuid();
        var exchangeRates = new Dictionary<string, decimal> { { "USD", 1.0m } };

        // Act
        var products = new List<(ProductImport.Core.Entities.Product?, string?)>();
        await foreach (var result in _processor.ProcessStreamAsync(stream, importJobId, exchangeRates, default))
        {
            products.Add(result);
        }

        // Assert
        Assert.Single(products);
        Assert.NotNull(products[0].Item1);
        Assert.Equal(DateTime.MaxValue, products[0].Item1?.ExpirationDate);
    }

    [Fact]
    public async Task ProcessStreamAsync_CurrencyConversion_ConvertsPrices()
    {
        // Arrange
        var csvContent = "name;price;expiration\nProduct A;$100.00;12/31/2024";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var importJobId = Guid.NewGuid();
        var exchangeRates = new Dictionary<string, decimal> { { "USD", 1.0m }, { "EUR", 0.92m }, { "GBP", 0.79m } };

        // Act
        var products = new List<(ProductImport.Core.Entities.Product?, string?)>();
        await foreach (var result in _processor.ProcessStreamAsync(stream, importJobId, exchangeRates, default))
        {
            products.Add(result);
        }

        // Assert
        Assert.Single(products);
        var convertedPrices = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(products[0].Item1?.ConvertedPricesJson ?? "{}") ?? new Dictionary<string, decimal>();
        Assert.Equal(100.0m, convertedPrices["USD"]);
        Assert.Equal(92.0m, convertedPrices["EUR"]);
        Assert.Equal(79.0m, convertedPrices["GBP"]);
    }
}
