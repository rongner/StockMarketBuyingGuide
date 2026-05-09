using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;

namespace StockMarketBuyingGuide.Api.Services;

public class PerformanceTrackingService(
    AppDbContext db,
    StockDataService stockDataService,
    ILogger<PerformanceTrackingService> logger)
{
    // Nominal trading-day intervals mapped to calendar-day approximations
    private static readonly (int TradingDays, int CalendarDays)[] Intervals =
    [
        (1, 1),
        (5, 7),
        (20, 28)
    ];

    public async Task RecordOutcomesAsync(
        List<StockPick> picks,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        if (picks.Count == 0) return;

        var tickers = picks.Select(p => p.Ticker).Distinct().ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var (tradingDays, calendarDays) in Intervals)
        {
            var targetDate = asOfDate.AddDays(calendarDays);

            if (targetDate >= today)
            {
                logger.LogInformation(
                    "Skipping {Days}d outcome for {AsOfDate} — target date {Target} is in the future",
                    tradingDays, asOfDate, targetDate);
                continue;
            }

            logger.LogInformation(
                "Recording {Days}d outcomes for {AsOfDate} (target: {Target})",
                tradingDays, asOfDate, targetDate);

            var prices = await stockDataService.FetchClosingPricesAsync(tickers, targetDate, ct);
            var records = new List<PerformanceTracking>();

            foreach (var pick in picks)
            {
                if (!prices.TryGetValue(pick.Ticker, out var price)) continue;

                records.Add(new PerformanceTracking
                {
                    Id = Guid.NewGuid(),
                    PickId = pick.Id,
                    RecordedAt = DateTimeOffset.UtcNow,
                    CurrentPrice = price,
                    DaysAfterPick = tradingDays
                });
            }

            db.PerformanceTrackings.AddRange(records);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Recorded {Count} {Days}d outcomes for {AsOfDate}",
                records.Count, tradingDays, asOfDate);
        }
    }
}
