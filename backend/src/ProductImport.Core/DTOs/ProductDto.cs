namespace ProductImport.Core.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string ConvertedPricesJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid ImportJobId { get; set; }
}
