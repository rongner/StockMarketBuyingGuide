namespace StockMarketBuyingGuide.Api.Infrastructure.Entities;

public class StockSnapshot
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public long Volume { get; set; }
    public decimal PctChange { get; set; }
    public decimal High52Week { get; set; }
    public decimal Low52Week { get; set; }
    public DateOnly SnapshotDate { get; set; }

    public RecommendationRun Run { get; set; } = null!;
}
