namespace StockMarketBuyingGuide.Api.Infrastructure.Entities;

public class StockPick
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public decimal PriceAtRecommendation { get; set; }

    public RecommendationRun Run { get; set; } = null!;
    public ICollection<PerformanceTracking> PerformanceRecords { get; set; } = [];
}
