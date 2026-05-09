using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using AnthropicTool = Anthropic.SDK.Common.Tool;
using AnthropicFunction = Anthropic.SDK.Common.Function;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StockMarketBuyingGuide.Api.Services;

public class ClaudeService(AppSettings settings, ILogger<ClaudeService> logger)
{
    private static readonly string SystemPrompt = """
        You are a professional stock market analyst. You will be given live market data and
        recent financial news. Your task is to select exactly 5 stocks to buy today.

        Selection criteria:
        - Strong price momentum or attractive entry point near 52-week lows
        - Positive news catalysts or sector tailwinds
        - Volume confirmation of price moves
        - Diversification across different sectors

        You MUST call the submit_stock_picks tool with exactly 5 picks. Be specific
        and data-driven in your reasoning, referencing the numbers from the market data.
        """;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<StockPick>> GetPicksAsync(
        List<StockSnapshot> snapshots,
        List<NewsSnapshot> news,
        Guid runId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(settings.ClaudeApiKey))
        {
            logger.LogWarning("ClaudeApiKey not configured — skipping Claude picks");
            return [];
        }

        var client = new AnthropicClient(settings.ClaudeApiKey);

        var userPrompt = $"""
            Market data for {snapshots.Count} stocks (sorted by absolute % change):

            {BuildMarketSummary(snapshots)}

            Recent financial news:

            {BuildNewsSummary(news)}

            Using the submit_stock_picks tool, select the 5 best stocks to buy today.
            """;

        var schemaJson = """
            {
                "type": "object",
                "properties": {
                    "picks": {
                        "type": "array",
                        "description": "Exactly 5 stock picks",
                        "items": {
                            "type": "object",
                            "properties": {
                                "ticker": {
                                    "type": "string",
                                    "description": "Stock ticker symbol (e.g. AAPL)"
                                },
                                "company_name": {
                                    "type": "string",
                                    "description": "Full company name"
                                },
                                "reasoning": {
                                    "type": "string",
                                    "description": "2-3 sentence explanation referencing specific market data and news"
                                },
                                "price_at_recommendation": {
                                    "type": "number",
                                    "description": "Current price from the market data provided"
                                }
                            },
                            "required": ["ticker", "company_name", "reasoning", "price_at_recommendation"]
                        }
                    }
                },
                "required": ["picks"]
            }
            """;

        var tools = new List<AnthropicTool>
        {
            new AnthropicFunction("submit_stock_picks", "Submit exactly 5 stock buy recommendations with reasoning",
                JsonNode.Parse(schemaJson))
        };

        try
        {
            var request = new MessageParameters
            {
                Model = AnthropicModels.Claude46Sonnet,
                MaxTokens = 2048,
                System =
                [
                    new SystemMessage(SystemPrompt,
                        new CacheControl { Type = CacheControlType.ephemeral })
                ],
                Messages =
                [
                    new Message(RoleType.User, userPrompt)
                ],
                Tools = tools,
                ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = "submit_stock_picks" },
                PromptCaching = PromptCacheType.FineGrained
            };

            var response = await client.Messages.GetClaudeMessageAsync(request, ct);

            logger.LogInformation(
                "Claude usage — input: {Input}, output: {Output}, cache_read: {CacheRead}, cache_write: {CacheWrite}",
                response.Usage?.InputTokens, response.Usage?.OutputTokens,
                response.Usage?.CacheReadInputTokens, response.Usage?.CacheCreationInputTokens);

            return ParseResponse(response, snapshots, runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Claude API call failed");
            return [];
        }
    }

    private static List<StockPick> ParseResponse(
        MessageResponse response,
        List<StockSnapshot> snapshots,
        Guid runId)
    {
        var picks = new List<StockPick>();
        var priceMap = snapshots.ToDictionary(s => s.Ticker, s => s.Price, StringComparer.OrdinalIgnoreCase);

        var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();
        if (toolUse?.Name != "submit_stock_picks" || toolUse.Input is null)
            return picks;

        var root = toolUse.Input["picks"];
        if (root is not JsonArray picksArr) return picks;

        foreach (var p in picksArr)
        {
            if (p is null) continue;

            var ticker = p["ticker"]?.GetValue<string>() ?? "";
            var companyName = p["company_name"]?.GetValue<string>() ?? "";
            var reasoning = p["reasoning"]?.GetValue<string>() ?? "";
            decimal price = 0;

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
