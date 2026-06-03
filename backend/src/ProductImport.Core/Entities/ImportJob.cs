using System.ComponentModel.DataAnnotations;

namespace ProductImport.Core.Entities;

public enum ImportJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public class ImportJob
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public long FileSizeBytes { get; set; }

    [Required]
    public ImportJobStatus Status { get; set; } = ImportJobStatus.Pending;

    public int TotalRows { get; set; }

    public int ProcessedRows { get; set; }

    public int FailedRows { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    // JSONB column to store exchange rates used for this import
    // Format: {"USD": 1.0, "EUR": 0.92, "BRL": 5.15, ...}
    public string ExchangeRatesJson { get; set; } = "{}";

    // Navigation property
    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ImportJob() { }

    public ImportJob(string fileName, long fileSizeBytes)
    {
        Id = Guid.NewGuid();
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        Status = ImportJobStatus.Pending;
        TotalRows = 0;
        ProcessedRows = 0;
        FailedRows = 0;
        StartedAt = DateTime.UtcNow;
    }
}
