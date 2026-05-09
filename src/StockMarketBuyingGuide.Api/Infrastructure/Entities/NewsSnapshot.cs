namespace StockMarketBuyingGuide.Api.Infrastructure.Entities;

public class NewsSnapshot
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string RelatedTickers { get; set; } = "[]";

    public RecommendationRun Run { get; set; } = null!;
}
