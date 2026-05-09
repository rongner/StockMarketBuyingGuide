namespace StockMarketBuyingGuide.Api.Infrastructure.Entities;

public class RecommendationRun
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsBacktest { get; set; }
    public DateOnly? AsOfDate { get; set; }
    public Guid? SimulationJobId { get; set; }
    public string? ErrorMessage { get; set; }

    public SimulationJob? SimulationJob { get; set; }
    public ICollection<StockPick> StockPicks { get; set; } = [];
    public ICollection<StockSnapshot> StockSnapshots { get; set; } = [];
    public ICollection<NewsSnapshot> NewsSnapshots { get; set; } = [];
}
