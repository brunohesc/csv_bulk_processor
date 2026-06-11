using System.ComponentModel.DataAnnotations;

namespace ProductImport.Core.DTOs;

public class ProductFilterDto
{
    public Guid? ImportJobId { get; set; }
    public string? NameFilter { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTime? ExpirationFrom { get; set; }
    public DateTime? ExpirationTo { get; set; }
    
    [RegularExpression("name|originalPrice|expirationDate", ErrorMessage = "SortBy must be name, originalPrice, or expirationDate")]
    public string SortBy { get; set; } = "";
    
    [RegularExpression("asc|desc", ErrorMessage = "SortOrder must be asc or desc")]
    public string SortOrder { get; set; } = "asc";
    
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
    public int Page { get; set; } = 1;
    
    [Range(1, 1000, ErrorMessage = "PageSize must be between 1 and 1000")]
    public int PageSize { get; set; } = 50;
}
