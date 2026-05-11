using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Tests.Unit;

public class GroqServiceTests
{
    private static GroqService CreateService(string? apiKey = null)
    {
        var settings = new AppSettings { GroqApiKey = apiKey ?? "" };
        var factory = new Moq.Mock<IHttpClientFactory>();
        return new GroqService(settings, factory.Object, new NullLogger<GroqService>());
    }

    private static List<StockSnapshot> SampleSnapshots() =>
    [
        new StockSnapshot
        {
            Id = Guid.NewGuid(), Ticker = "AAPL", Price = 175m, Volume = 50_000_000,
            PctChange = 1.5m, High52Week = 200m, Low52Week = 130m,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow)
        },
        new StockSnapshot
        {
            Id = Guid.NewGuid(), Ticker = "MSFT", Price = 380m, Volume = 20_000_000,
            PctChange = -0.8m, High52Week = 420m, Low52Week = 310m,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }
    ];

    [Fact]
    public async Task GetPicksAsync_ReturnsEmpty_WhenApiKeyNotConfigured()
    {
        var svc = CreateService(apiKey: null);
        var result = await svc.GetPicksAsync(SampleSnapshots(), [], Guid.NewGuid());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPicksAsync_ReturnsEmpty_WhenApiKeyIsWhitespace()
    {
        var svc = CreateService(apiKey: "   ");
        var result = await svc.GetPicksAsync(SampleSnapshots(), [], Guid.NewGuid());
        result.Should().BeEmpty();
    }
}
