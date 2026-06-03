using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.Logging;
using ProductImport.Core.Entities;

namespace ProductImport.Infrastructure.Services;

public class CsvRow
{
    [Name("name")]
    public string Name { get; set; } = string.Empty;

    [Name("price")]
    public string Price { get; set; } = string.Empty;

    [Name("expiration")]
    public string Expiration { get; set; } = string.Empty;
}

public class CsvProcessor
{
    private readonly ILogger<CsvProcessor> _logger;

    public CsvProcessor(ILogger<CsvProcessor> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<(Product? Product, string? Error)> ProcessStreamAsync(
        Stream stream,
        Guid importJobId,
        Dictionary<string, decimal> exchangeRates,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.ToLower()
        });

        var rowCount = 0;
        string? headerError = null;

        try
        {
            await csv.ReadAsync(); // Skip header
            csv.ReadHeader();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading CSV header");
            headerError = $"CSV header error: {ex.Message}";
        }

        if (headerError != null)
        {
            yield return (null, headerError);
            yield break;
        }

        while (true)
        {
            (Product? product, string? error) result = (null, null);
            bool readSuccess = false;

            try
            {
                readSuccess = await csv.ReadAsync();
                if (!readSuccess)
                {
                    break; // End of file
                }

                cancellationToken.ThrowIfCancellationRequested();
                rowCount++;

                result = ProcessRow(csv, importJobId, exchangeRates, rowCount);

                if (result.error != null)
                {
                    _logger.LogWarning("Row {RowCount} validation failed: {Error}", rowCount, result.error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading CSV row {RowCount}", rowCount + 1);
                result = (null, $"CSV read error at row {rowCount + 1}: {ex.Message}");
            }

            if (readSuccess)
            {
                yield return result;
            }
        }

        _logger.LogInformation("Finished processing {RowCount} rows", rowCount);
    }

    private (Product? Product, string? Error) ProcessRow(CsvHelper.CsvReader csv, Guid importJobId, Dictionary<string, decimal> exchangeRates, int rowNumber)
    {
        try
        {
            var row = csv.GetRecord<CsvRow>();

            // Parse name (allow empty, but validate length)
            var name = row.Name ?? string.Empty;
            if (name.Length > 1000)
            {
                return (null, $"Name exceeds maximum length of 1000 characters (current: {name.Length})");
            }

            // Parse price (default to 0 if empty or invalid)
            decimal price = 0;
            if (!string.IsNullOrWhiteSpace(row.Price))
            {
                var priceStr = row.Price.Replace("$", "").Trim();
                if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPrice))
                {
                    price = parsedPrice;
                }
            }

            // Parse expiration date (default to DateTime.MaxValue if empty or invalid)
            DateTime? expirationDate = DateTime.MaxValue;
            if (!string.IsNullOrWhiteSpace(row.Expiration))
            {
                // Try M/d/yyyy format first
                if (DateTime.TryParseExact(row.Expiration, "M/d/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expDate))
                {
                    expirationDate = expDate;
                }
                // Fallback to general parsing
                else if (DateTime.TryParse(row.Expiration, CultureInfo.InvariantCulture, DateTimeStyles.None, out expDate))
                {
                    expirationDate = expDate;
                }
            }

            // Calculate currency conversions using LINQ
            var convertedPrices = exchangeRates
                .ToDictionary(kvp => kvp.Key, kvp => price * kvp.Value);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                OriginalPrice = price,
                ExpirationDate = expirationDate,
                ConvertedPricesJson = System.Text.Json.JsonSerializer.Serialize(convertedPrices),
                ImportJobId = importJobId
            };

            return (product, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing row {RowCount}", rowNumber);
            return (null, $"Unexpected error: {ex.Message}");
        }
    }
}
