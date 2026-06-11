namespace ProductImport.Application.Settings;

public class AppSettings
{
    public ExchangeRateSettings ExchangeRate { get; set; } = new();
    public ImportSettings Import { get; set; } = new();
    public CurrencySettings Currency { get; set; } = new();
}

public class ExchangeRateSettings
{
    public string PrimaryUrl { get; set; } = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.min.json";
    public string FallbackUrl { get; set; } = "https://latest.currency-api.pages.dev/v1/currencies/usd.min.json";
    public int TimeoutSeconds { get; set; } = 30;
}

public class ImportSettings
{
    public int BatchSize { get; set; } = 1000;
    public int MaxConcurrentImports { get; set; } = 5;
    public int ProgressUpdateRowCount { get; set; } = 500;
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
}

public class CurrencySettings
{
    public string[] TargetCurrencies { get; set; } = new[] { "USD", "EUR", "GBP", "CAD", "BRL", "BTC" };
}
