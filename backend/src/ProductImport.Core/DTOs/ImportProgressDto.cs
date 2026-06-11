namespace ProductImport.Core.DTOs;

public class ImportProgressDto
{
    public Guid ImportJobId { get; set; }
    public int Processed { get; set; }
    public int Total { get; set; }
    public int Percentage { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FailedCount { get; set; }
}
