namespace StockMarketBuyingGuide.Api.Infrastructure.Entities;

public class PerformanceTracking
{
    public Guid Id { get; set; }
    public Guid PickId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public decimal CurrentPrice { get; set; }
    public int DaysAfterPick { get; set; }

    public StockPick Pick { get; set; } = null!;
}
