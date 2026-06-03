using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ProductImport.Application.Settings;
using ProductImport.Infrastructure.Services;
using System.Net;
using Xunit;

namespace ProductImport.Tests.Services;

public class ExchangeRateServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly ExchangeRateSettings _settings;

    public ExchangeRateServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _settings = new ExchangeRateSettings
        {
            PrimaryUrl = "https://example.com/primary.json",
            FallbackUrl = "https://example.com/fallback.json",
            TimeoutSeconds = 30
        };
    }

    [Fact]
    public async Task GetExchangeRatesAsync_PrimaryUrlSucceeds_ReturnsRates()
    {
        // Arrange
        var responseContent = @"{""usd"":{""eur"":0.92,""gbp"":0.79,""brl"":5.0}}";
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("primary")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var optionsMock = new Mock<IOptionsMonitor<ExchangeRateSettings>>();
        optionsMock.Setup(x => x.CurrentValue).Returns(_settings);
        var logger = new NullLogger<ExchangeRateService>();
        var service = new ExchangeRateService(_httpClient, optionsMock.Object, logger);

        // Act
        var result = await service.GetExchangeRatesAsync(default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
        Assert.Equal(0.92m, result["EUR"]);
        Assert.Equal(0.79m, result["GBP"]);
        Assert.Equal(5.0m, result["BRL"]);
    }

    [Fact]
    public async Task GetExchangeRatesAsync_PrimaryUrlFails_UsesFallback()
    {
        // Arrange
        var fallbackContent = @"{""usd"":{""eur"":0.92,""gbp"":0.79}}";
        _httpMessageHandlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(fallbackContent)
            });

        var optionsMock = new Mock<IOptionsMonitor<ExchangeRateSettings>>();
        optionsMock.Setup(x => x.CurrentValue).Returns(_settings);
        var logger = new NullLogger<ExchangeRateService>();
        var service = new ExchangeRateService(_httpClient, optionsMock.Object, logger);

        // Act
        var result = await service.GetExchangeRatesAsync(default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(0.92m, result["EUR"]);
        Assert.Equal(0.79m, result["GBP"]);
    }

    [Fact]
    public async Task GetExchangeRatesAsync_BothUrlsFail_ThrowsException()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });

        var optionsMock = new Mock<IOptionsMonitor<ExchangeRateSettings>>();
        optionsMock.Setup(x => x.CurrentValue).Returns(_settings);
        var logger = new NullLogger<ExchangeRateService>();
        var service = new ExchangeRateService(_httpClient, optionsMock.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetExchangeRatesAsync(default));
    }

    [Fact]
    public async Task GetExchangeRatesAsync_InvalidJson_ThrowsException()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("invalid json")
            });

        var optionsMock = new Mock<IOptionsMonitor<ExchangeRateSettings>>();
        optionsMock.Setup(x => x.CurrentValue).Returns(_settings);
        var logger = new NullLogger<ExchangeRateService>();
        var service = new ExchangeRateService(_httpClient, optionsMock.Object, logger);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => service.GetExchangeRatesAsync(default));
    }
}
