namespace StockMarketBuyingGuide.Api.Infrastructure.Entities;

public class SimulationJob
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal StartingCapital { get; set; }
    public decimal? FinalCapital { get; set; }
    public string Status { get; set; } = SimulationStatus.Pending;
    public string? ErrorMessage { get; set; }

    public ICollection<RecommendationRun> Runs { get; set; } = [];
    public ICollection<SimulationDayResult> DayResults { get; set; } = [];
}

public static class SimulationStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
