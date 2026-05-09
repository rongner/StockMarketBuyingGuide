using System.Text.Json;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;

namespace StockMarketBuyingGuide.Api.Services;

public class NewsService(
    IHttpClientFactory httpClientFactory,
    AppSettings settings,
    ILogger<NewsService> logger)
{
    private const int FreeHistoryDays = 30;

    public async Task<List<NewsSnapshot>> FetchNewsAsync(
        DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(settings.NewsApiKey))
        {
            logger.LogWarning("NewsApiKey not configured — skipping news fetch");
            return [];
        }

        if (asOfDate.HasValue)
        {
            var daysAgo = (DateTime.UtcNow.Date - asOfDate.Value.ToDateTime(TimeOnly.MinValue)).Days;
            if (daysAgo > FreeHistoryDays)
            {
                logger.LogInformation("Date {Date} is older than {Days} days — news unavailable on free tier", asOfDate, FreeHistoryDays);
                return [];
            }

            return await FetchByDateAsync(asOfDate.Value, ct);
        }

        return await FetchCurrentAsync(ct);
    }

    private async Task<List<NewsSnapshot>> FetchCurrentAsync(CancellationToken ct)
    {
        var url = $"https://newsapi.org/v2/everything?q=stock+market+investing&language=en&sortBy=publishedAt&pageSize=50&apiKey={settings.NewsApiKey}";
        return await QueryNewsApiAsync(url, ct);
    }

    private async Task<List<NewsSnapshot>> FetchByDateAsync(DateOnly date, CancellationToken ct)
    {
        var from = date.ToString("yyyy-MM-dd");
        var to = date.AddDays(1).ToString("yyyy-MM-dd");
        var url = $"https://newsapi.org/v2/everything?q=stock+market+investing&language=en&from={from}&to={to}&sortBy=publishedAt&pageSize=50&apiKey={settings.NewsApiKey}";
        return await QueryNewsApiAsync(url, ct);
    }

    private async Task<List<NewsSnapshot>> QueryNewsApiAsync(string url, CancellationToken ct)
    {
        var snapshots = new List<NewsSnapshot>();

        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("articles", out var articles))
                return snapshots;

            foreach (var article in articles.EnumerateArray())
            {
                var headline = article.TryGetProperty("title", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(headline) || headline == "[Removed]") continue;

                var url2 = article.TryGetProperty("url", out var u) ? u.GetString() : null;
                var publishedAt = article.TryGetProperty("publishedAt", out var p)
                    ? DateTimeOffset.Parse(p.GetString()!)
                    : DateTimeOffset.UtcNow;

                var description = article.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                var relatedTickers = FindRelatedTickers(headline + " " + description);

                snapshots.Add(new NewsSnapshot
                {
                    Id = Guid.NewGuid(),
                    Headline = headline,
                    Url = url2,
                    PublishedAt = publishedAt,
                    RelatedTickers = JsonSerializer.Serialize(relatedTickers)
                });
            }

            logger.LogInformation("Fetched {Count} news articles", snapshots.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch news from NewsAPI");
        }

        return snapshots;
    }

    private static List<string> FindRelatedTickers(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var found = new List<string>();
        foreach (var (keyword, ticker) in TickerKeywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                found.Add(ticker);
        }

        return found.Distinct().ToList();
    }

    // Maps common company name keywords to tickers
    private static readonly Dictionary<string, string> TickerKeywords = new()
    {
        ["Apple"] = "AAPL", ["Microsoft"] = "MSFT", ["Nvidia"] = "NVDA",
        ["Amazon"] = "AMZN", ["Google"] = "GOOGL", ["Alphabet"] = "GOOGL",
        ["Meta"] = "META", ["Facebook"] = "META", ["Tesla"] = "TSLA",
        ["Berkshire"] = "BRK-B", ["JPMorgan"] = "JPM", ["Eli Lilly"] = "LLY",
        ["Visa"] = "V", ["UnitedHealth"] = "UNH", ["ExxonMobil"] = "XOM",
        ["Broadcom"] = "AVGO", ["Mastercard"] = "MA", ["Johnson & Johnson"] = "JNJ",
        ["Home Depot"] = "HD", ["Procter"] = "PG", ["Costco"] = "COST",
        ["Merck"] = "MRK", ["AbbVie"] = "ABBV", ["Chevron"] = "CVX",
        ["Coca-Cola"] = "KO", ["PepsiCo"] = "PEP", ["Adobe"] = "ADBE",
        ["Walmart"] = "WMT", ["Bank of America"] = "BAC", ["Salesforce"] = "CRM",
        ["McDonald"] = "MCD", ["Netflix"] = "NFLX", ["AMD"] = "AMD",
        ["Intel"] = "INTC", ["Oracle"] = "ORCL", ["Cisco"] = "CSCO",
        ["Goldman Sachs"] = "GS", ["Morgan Stanley"] = "MS", ["BlackRock"] = "BLK",
        ["Qualcomm"] = "QCOM", ["Honeywell"] = "HON", ["IBM"] = "IBM",
        ["Boeing"] = "BA", ["Ford"] = "F", ["General Motors"] = "GM",
        ["Walt Disney"] = "DIS", ["Disney"] = "DIS", ["Pfizer"] = "PFE",
        ["Moderna"] = "MRNA", ["Uber"] = "UBER", ["Airbnb"] = "ABNB",
        ["Shopify"] = "SHOP", ["Palantir"] = "PLTR", ["CrowdStrike"] = "CRWD",
        ["Palo Alto"] = "PANW", ["ServiceNow"] = "NOW", ["Snowflake"] = "SNOW"
    };
}
