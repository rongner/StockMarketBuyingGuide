namespace StockMarketBuyingGuide.Api.Infrastructure.Entities;

public class SimulationDayResult
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid RunId { get; set; }
    public DateOnly TradingDate { get; set; }
    public decimal CapitalBefore { get; set; }
    public decimal CapitalAfter { get; set; }

    public SimulationJob Job { get; set; } = null!;
    public RecommendationRun Run { get; set; } = null!;
}
