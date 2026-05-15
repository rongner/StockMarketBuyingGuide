using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Infrastructure;

namespace StockMarketBuyingGuide.Api.Controllers;

[ApiController]
[Route("api/performance")]
[Authorize]
public class PerformanceController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var records = await db.PerformanceTrackings
            .Select(pt => new
            {
                pt.DaysAfterPick,
                pt.CurrentPrice,
                pt.Pick.Ticker,
                pt.Pick.PriceAtRecommendation
            })
            .ToListAsync(ct);

        if (records.Count == 0)
            return Ok(new { message = "No performance data yet.", byInterval = Array.Empty<object>(), topTickers = Array.Empty<object>() });

        var withReturns = records
            .Where(r => r.PriceAtRecommendation > 0)
            .Select(r => new
            {
                r.DaysAfterPick,
                r.Ticker,
                ReturnPct = (double)(r.CurrentPrice - r.PriceAtRecommendation) / (double)r.PriceAtRecommendation * 100.0
            })
            .ToList();

        var byInterval = withReturns
            .GroupBy(r => r.DaysAfterPick)
            .Select(g => new
            {
                DaysAfterPick = g.Key,
                Count         = g.Count(),
                WinRatePct    = Math.Round(g.Count(r => r.ReturnPct > 0) * 100.0 / g.Count(), 1),
                AvgReturnPct  = Math.Round(g.Average(r => r.ReturnPct), 3),
                MedianReturnPct = Math.Round(Median(g.Select(r => r.ReturnPct)), 3),
                BestPct       = Math.Round(g.Max(r => r.ReturnPct), 2),
                WorstPct      = Math.Round(g.Min(r => r.ReturnPct), 2),
            })
            .OrderBy(x => x.DaysAfterPick)
            .ToList();

        // Top and bottom tickers by 1-day average return (min 2 picks to filter noise)
        var oneDayRecords = withReturns.Where(r => r.DaysAfterPick == 1).ToList();
        var tickerStats = oneDayRecords
            .GroupBy(r => r.Ticker)
            .Where(g => g.Count() >= 2)
            .Select(g => new
            {
                Ticker       = g.Key,
                Count        = g.Count(),
                AvgReturnPct = Math.Round(g.Average(r => r.ReturnPct), 3),
                WinRatePct   = Math.Round(g.Count(r => r.ReturnPct > 0) * 100.0 / g.Count(), 1),
            })
            .ToList();

        var topTickers    = tickerStats.OrderByDescending(t => t.AvgReturnPct).Take(10).ToList();
        var bottomTickers = tickerStats.OrderBy(t => t.AvgReturnPct).Take(10).ToList();

        return Ok(new
        {
            TotalPicks = withReturns.Select(r => r.Ticker).Distinct().Count(),
            byInterval,
            topTickers,
            bottomTickers
        });
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return 0;
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}
