using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProductImport.Core.DTOs;
using ProductImport.Core.Entities;
using ProductImport.Core.Interfaces;

namespace ProductImport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _repository;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductRepository repository,
        ILogger<ProductsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductFilterDto filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var (products, totalCount) = await _repository.GetProductsAsync(
                filter.ImportJobId,
                filter.NameFilter,
                filter.MinPrice,
                filter.MaxPrice,
                filter.ExpirationFrom,
                filter.ExpirationTo,
                filter.SortBy,
                filter.SortOrder,
                filter.Page,
                filter.PageSize,
                cancellationToken);

            var productDtos = products.Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                OriginalPrice = p.OriginalPrice,
                ExpirationDate = p.ExpirationDate,
                ConvertedPricesJson = p.ConvertedPricesJson,
                CreatedAt = p.CreatedAt,
                ImportJobId = p.ImportJobId
            }).ToList();

            var result = new PaginatedResultDto<ProductDto>
            {
                Items = productDtos,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products");
            return StatusCode(500, "An error occurred while fetching products");
        }
    }
}
