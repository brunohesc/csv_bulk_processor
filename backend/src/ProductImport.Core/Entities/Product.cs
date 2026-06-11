using System.ComponentModel.DataAnnotations;

namespace ProductImport.Core.Entities;

public class Product
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public decimal OriginalPrice { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // JSONB column to store converted prices
    // Format: {"USD": 123.45, "EUR": 111.22, "BRL": 654.12}
    public string ConvertedPricesJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid ImportJobId { get; set; }

    // Navigation property
    public ImportJob ImportJob { get; set; } = null!;
}
