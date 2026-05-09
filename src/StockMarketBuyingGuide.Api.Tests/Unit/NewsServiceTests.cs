using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Services;
using StockMarketBuyingGuide.Api.Tests.Helpers;
using System.Net;
using System.Text.Json;

namespace StockMarketBuyingGuide.Api.Tests.Unit;

public class NewsServiceTests
{
    private static (NewsService service, AppSettings settings) CreateService(
        string? apiKey = "test-key",
        HttpResponseMessage? httpResponse = null)
    {
        var settings = new AppSettings { NewsApiKey = apiKey ?? "" };
        var response = httpResponse ?? new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"articles":[]}""")
        };

        var handler = new TestHttpMessageHandler(response);
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var logger = new NullLogger<NewsService>();
        return (new NewsService(factory.Object, settings, logger), settings);
    }

    [Fact]
    public async Task FetchNewsAsync_ReturnsEmpty_WhenApiKeyNotConfigured()
    {
        var (svc, _) = CreateService(apiKey: null);
        var result = await svc.FetchNewsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchNewsAsync_ReturnsEmpty_WhenDateExceedsFreeLimit()
    {
        var (svc, _) = CreateService();
        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-40));
        var result = await svc.FetchNewsAsync(oldDate);
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Apple announces record earnings", "AAPL")]
    [InlineData("Microsoft acquires gaming studio", "MSFT")]
    [InlineData("Nvidia GPU shortage continues", "NVDA")]
    [InlineData("Tesla delivers fewer cars than expected", "TSLA")]
    [InlineData("Goldman Sachs upgrades tech sector", "GS")]
    public async Task FetchNewsAsync_AssignsTicker_WhenCompanyNameInHeadline(
        string headline, string expectedTicker)
    {
        var json = $$"""
            {
                "articles": [
                    {
                        "title": "{{headline}}",
                        "url": "https://example.com/article",
                        "publishedAt": "2026-01-15T12:00:00Z",
                        "description": ""
                    }
                ]
            }
            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
        var (svc, _) = CreateService(httpResponse: response);

        var result = await svc.FetchNewsAsync();

        result.Should().HaveCount(1);
        var tickers = JsonSerializer.Deserialize<List<string>>(result[0].RelatedTickers ?? "[]");
        tickers.Should().Contain(expectedTicker);
    }

    [Fact]
    public async Task FetchNewsAsync_SkipsRemovedArticles()
    {
        var json = """
            {
                "articles": [
                    {
                        "title": "[Removed]",
                        "url": "https://example.com/removed",
                        "publishedAt": "2026-01-15T12:00:00Z",
                        "description": ""
                    },
                    {
                        "title": "Apple stock rises",
                        "url": "https://example.com/apple",
                        "publishedAt": "2026-01-15T12:00:00Z",
                        "description": ""
                    }
                ]
            }
            """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
        var (svc, _) = CreateService(httpResponse: response);

        var result = await svc.FetchNewsAsync();

        result.Should().HaveCount(1);
        result[0].Headline.Should().Be("Apple stock rises");
    }

    [Fact]
    public async Task FetchNewsAsync_ReturnsEmpty_WhenHttpFails()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var (svc, _) = CreateService(httpResponse: response);

        var result = await svc.FetchNewsAsync();

        result.Should().BeEmpty();
    }
}
