namespace ProductImport.Core.Interfaces;

public interface IExchangeRateService
{
    Task<Dictionary<string, decimal>> GetExchangeRatesAsync(CancellationToken cancellationToken = default);
}
