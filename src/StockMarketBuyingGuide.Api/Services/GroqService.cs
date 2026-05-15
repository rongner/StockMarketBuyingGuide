using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Models;

namespace StockMarketBuyingGuide.Api.Services;

public class GroqService(
    AppSettings settings,
    IHttpClientFactory httpClientFactory,
    ILogger<GroqService> logger)
{
    private const string Model = "llama-3.3-70b-versatile";
    private const string Url = "https://api.groq.com/openai/v1/chat/completions";

    private const string BaseSystemPrompt = """
        You are a professional stock market analyst. You will be given live market data and
        recent financial news. Your task is to select exactly 5 stocks to buy today.

        Selection criteria:
        - Strong price momentum or attractive entry point near 52-week lows
        - Positive news catalysts or sector tailwinds
        - Volume confirmation of price moves
        - Diversification across different sectors

        You MUST call the submit_stock_picks function with exactly 5 picks. Be specific
        and data-driven in your reasoning, referencing the numbers from the market data.
        """;

    public static string BuildSystemPrompt(WinnerProfile? profile)
    {
        if (profile is null) return BaseSystemPrompt;

        var sb = new StringBuilder(BaseSystemPrompt);
        sb.AppendLine();
        sb.AppendLine($"""

            Historical performance insight from {profile.SampleCount} past picks that gained on the following day:
            - Average day-of-pick momentum: {profile.AvgMomentumPct:+0.##;-0.##}% intraday change
            """);

        if (profile.AvgRangePosition > 0)
            sb.AppendLine(
                $"- Average 52-week range position at pick time: {profile.AvgRangePosition:F0}% from the annual low");

        if (profile.AvgVolumePercentile > 0)
            sb.AppendLine(
                $"- Average volume percentile at pick time: {profile.AvgVolumePercentile:F0}th percentile within the day's universe (higher = unusually active)");

        if (profile.Examples.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Recent examples of outperforming picks (do NOT re-pick these; use as a pattern reference):");
            foreach (var e in profile.Examples)
                sb.AppendLine(
                    $"  {e.Ticker} ({e.CompanyName}): momentum {e.MomentumPct:+0.##;-0.##}%, " +
                    (e.RangePosition > 0 ? $"range pos {e.RangePosition:F0}%, " : "") +
                    $"returned +{e.ReturnPct:F1}% over 20 days");
        }

        sb.AppendLine();
        sb.AppendLine("Favour stocks whose current momentum and range position resemble these historical winners.");

        return sb.ToString();
    }

    public async Task<List<StockPick>> GetPicksAsync(
        List<StockSnapshot> snapshots,
        List<NewsSnapshot> news,
        Guid runId,
        WinnerProfile? profile = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.GroqApiKey))
        {
            logger.LogWarning("GroqApiKey not configured — skipping AI picks");
            return [];
        }

        var userPrompt = $"""
            Market data for {snapshots.Count} stocks (sorted by absolute % change):

            {BuildMarketSummary(snapshots)}

            Recent financial news:

            {BuildNewsSummary(news)}

            Using the submit_stock_picks function, select the 5 best stocks to buy today.
            """;

        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = BuildSystemPrompt(profile) },
                new { role = "user",   content = userPrompt                 }
            },
            tools = new[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "submit_stock_picks",
                        description = "Submit exactly 5 stock buy recommendations with reasoning",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                picks = new
                                {
                                    type = "array",
                                    description = "Exactly 5 stock picks",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            ticker               = new { type = "string", description = "Stock ticker symbol e.g. AAPL" },
                                            company_name         = new { type = "string", description = "Full company name" },
                                            reasoning            = new { type = "string", description = "2-3 sentence explanation referencing specific market data and news" },
                                            price_at_recommendation = new { type = "number", description = "Current price from the market data provided" }
                                        },
                                        required = new[] { "ticker", "company_name", "reasoning", "price_at_recommendation" }
                                    }
                                }
                            },
                            required = new[] { "picks" }
                        }
                    }
                }
            },
            tool_choice = new { type = "function", function = new { name = "submit_stock_picks" } }
        };

        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.GroqApiKey);

            var response = await client.PostAsJsonAsync(Url, requestBody, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Groq API error {Status}: {Body}", (int)response.StatusCode, body);
                return [];
            }

            var doc = JsonNode.Parse(body);
            var usage = doc?["usage"];
            logger.LogInformation(
                "Groq usage — prompt: {Prompt}, completion: {Completion}, total: {Total}",
                usage?["prompt_tokens"], usage?["completion_tokens"], usage?["total_tokens"]);

            return ParseResponse(doc, snapshots, runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Groq API call failed");
            return [];
        }
    }

    private static List<StockPick> ParseResponse(
        JsonNode? doc,
        List<StockSnapshot> snapshots,
        Guid runId)
    {
        var picks = new List<StockPick>();
        var priceMap = snapshots.ToDictionary(s => s.Ticker, s => s.Price, StringComparer.OrdinalIgnoreCase);

        // Groq returns tool call arguments as a JSON string — deserialize it
        var argsJson = doc?["choices"]?[0]?["message"]?["tool_calls"]?[0]?["function"]?["arguments"]?.GetValue<string>();
        if (string.IsNullOrEmpty(argsJson)) return picks;

        var args = JsonNode.Parse(argsJson);
        if (args?["picks"] is not JsonArray picksArr) return picks;

        foreach (var p in picksArr)
        {
            if (p is null) continue;

            var ticker      = p["ticker"]?.GetValue<string>() ?? "";
            var companyName = p["company_name"]?.GetValue<string>() ?? "";
            var reasoning   = p["reasoning"]?.GetValue<string>() ?? "";
            decimal price   = 0;

            if (p["price_at_recommendation"] is JsonNode prNode)
                price = (decimal)prNode.GetValue<double>();

            if (price == 0 && priceMap.TryGetValue(ticker, out var snapPrice))
                price = snapPrice;

            if (!string.IsNullOrEmpty(ticker))
                picks.Add(new StockPick
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    Ticker = ticker.ToUpperInvariant(),
                    CompanyName = companyName,
                    Reasoning = reasoning,
                    PriceAtRecommendation = price
                });
        }

        return picks;
    }

    private static string BuildMarketSummary(List<StockSnapshot> snapshots)
    {
        var sb = new StringBuilder();
        foreach (var s in snapshots.OrderByDescending(x => Math.Abs((double)x.PctChange)))
        {
            var rangePos = s.High52Week > s.Low52Week
                ? (s.Price - s.Low52Week) / (s.High52Week - s.Low52Week) * 100m
                : 0m;
            sb.AppendLine(
                $"{s.Ticker}: ${s.Price:F2} ({s.PctChange:+0.##;-0.##}%) " +
                $"Vol:{s.Volume:N0} 52wk:[${s.Low52Week:F2}-${s.High52Week:F2}] " +
                $"RangePos:{rangePos:F0}%");
        }
        return sb.ToString();
    }

    private static string BuildNewsSummary(List<NewsSnapshot> news)
    {
        if (news.Count == 0) return "No news available.";
        var sb = new StringBuilder();
        foreach (var n in news.Take(20))
        {
            var tickers = string.IsNullOrEmpty(n.RelatedTickers) ? "" : $" [{n.RelatedTickers}]";
            sb.AppendLine($"- {n.Headline}{tickers}");
        }
        return sb.ToString();
    }
}
