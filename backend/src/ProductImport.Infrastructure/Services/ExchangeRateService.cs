using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductImport.Application.Settings;
using ProductImport.Core.Interfaces;

namespace ProductImport.Infrastructure.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly HttpClient _httpClient;
    private readonly ExchangeRateSettings _settings;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(
        HttpClient httpClient,
        IOptionsMonitor<ExchangeRateSettings> settings,
        ILogger<ExchangeRateService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.CurrentValue;
        _logger = logger;
    }

    public async Task<Dictionary<string, decimal>> GetExchangeRatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching exchange rates from primary URL: {PrimaryUrl}", _settings.PrimaryUrl);

        try
        {
            var rates = await FetchFromUrlAsync(_settings.PrimaryUrl, cancellationToken);
            _logger.LogInformation("Successfully fetched exchange rates from primary URL");
            return rates;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch exchange rates from primary URL, trying fallback URL: {FallbackUrl}", _settings.FallbackUrl);
            
            try
            {
                var rates = await FetchFromUrlAsync(_settings.FallbackUrl, cancellationToken);
                _logger.LogInformation("Successfully fetched exchange rates from fallback URL");
                return rates;
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to fetch exchange rates from both primary and fallback URLs");
                throw;
            }
        }
    }

    private async Task<Dictionary<string, decimal>> FetchFromUrlAsync(string url, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        var response = await _httpClient.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(json);

        // Find the first property that is an object (the base currency, e.g., "usd")
        var baseCurrencyProp = doc.RootElement.EnumerateObject()
            .FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Object);

        if (baseCurrencyProp.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Unexpected API response format - no exchange rate object found");
        }

        var rates = new Dictionary<string, decimal>();

        foreach (var rate in baseCurrencyProp.Value.EnumerateObject())
        {
            if (decimal.TryParse(rate.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                rates[rate.Name.ToUpper()] = value;
            }
        }

        // Add the base currency itself with rate 1.0
        rates[baseCurrencyProp.Name.ToUpper()] = 1.0m;

        if (rates.Count == 0)
        {
            throw new InvalidOperationException("No exchange rates found in API response");
        }

        return rates;
    }
}
