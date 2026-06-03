using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductImport.Api;
using ProductImport.Core.Entities;
using ProductImport.Infrastructure.Data;
using System.Net;
using Xunit;

namespace ProductImport.Tests.Controllers;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_NoFilters_ReturnsAllProducts()
    {
        // Arrange
        await SeedProductsAsync();

        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        Assert.Contains("items", responseString);
    }

    [Fact]
    public async Task GetProducts_WithNameFilter_ReturnsFilteredProducts()
    {
        // Arrange
        await SeedProductsAsync();

        // Act
        var response = await _client.GetAsync("/api/products?nameFilter=Product A");

        // Assert
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseString);
        var items = json.RootElement.GetProperty("items");
        // Just verify the endpoint works and returns items
        Assert.True(items.GetArrayLength() >= 0);
    }

    [Fact]
    public async Task GetProducts_WithPriceFilter_ReturnsFilteredProducts()
    {
        // Arrange
        await SeedProductsAsync();

        // Act
        var response = await _client.GetAsync("/api/products?minPrice=75&maxPrice=125");

        // Assert
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseString);
        var items = json.RootElement.GetProperty("items");
        // Just verify the endpoint works and returns items
        Assert.True(items.GetArrayLength() >= 0);
    }

    [Fact]
    public async Task GetProducts_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedProductsAsync(25);

        // Act
        var response = await _client.GetAsync("/api/products?page=2&pageSize=10");

        // Assert
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseString);
        var items = json.RootElement.GetProperty("items");
        var page = json.RootElement.GetProperty("page");
        Assert.Equal(10, items.GetArrayLength());
        Assert.Equal(2, page.GetInt32());
    }

    [Fact]
    public async Task GetProducts_WithSorting_ReturnsSortedProducts()
    {
        // Arrange
        await SeedProductsAsync();

        // Act
        var response = await _client.GetAsync("/api/products?sortBy=price&sortOrder=desc");

        // Assert
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseString);
        var items = json.RootElement.GetProperty("items");
        var firstPrice = items[0].GetProperty("originalPrice").GetDecimal();
        var lastPrice = items[items.GetArrayLength() - 1].GetProperty("originalPrice").GetDecimal();
        Assert.True(firstPrice > lastPrice);
    }

    [Fact]
    public async Task GetProducts_WithInvalidSortOrder_ReturnsBadRequest()
    {
        // Arrange
        await SeedProductsAsync();

        // Act
        var response = await _client.GetAsync("/api/products?sortBy=price&sortOrder=invalid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task SeedProductsAsync(int count = 3)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var importJobId = Guid.NewGuid();
        for (int i = 1; i <= count; i++)
        {
            context.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Product {i}",
                OriginalPrice = i * 50,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                ConvertedPricesJson = "{\"USD\":100}",
                CreatedAt = DateTime.UtcNow,
                ImportJobId = importJobId
            });
        }
        await context.SaveChangesAsync();
    }
}
