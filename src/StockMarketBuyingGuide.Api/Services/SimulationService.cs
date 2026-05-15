using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Models.Dto;

namespace StockMarketBuyingGuide.Api.Services;

public class SimulationService(
    AppDbContext db,
    RecommendationOrchestrator orchestrator,
    StockDataService stockDataService,
    WinnerAnalysisService winnerAnalysisService,
    AppSettings settings,
    ILogger<SimulationService> logger)
{
    public static List<DateOnly> GetTradingDays(DateOnly start, DateOnly end)
    {
        var days = new List<DateOnly>();
        var current = start;
        while (current <= end)
        {
            if (current.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days.Add(current);
            current = current.AddDays(1);
        }
        return days;
    }

    public async Task RunSimulationAsync(Guid jobId, CancellationToken ct)
    {
        var job = await db.SimulationJobs.FindAsync([jobId], ct);
        if (job is null)
        {
            logger.LogError("Simulation job {JobId} not found", jobId);
            return;
        }

        job.Status = SimulationStatus.Running;
        await db.SaveChangesAsync(ct);

        try
        {
            var tradingDays = GetTradingDays(job.StartDate, job.EndDate);
            decimal capital = job.StartingCapital;

            // Process days 0..N-2 — each day we buy at close, sell next day at close
            for (int i = 0; i < tradingDays.Count - 1; i++)
            {
                if (ct.IsCancellationRequested) break;

                var today = tradingDays[i];
                var tomorrow = tradingDays[i + 1];

                logger.LogInformation(
                    "Simulation {JobId}: day {Day} ({I}/{Total})",
                    jobId, today, i + 1, tradingDays.Count - 1);

                // Get picks (idempotent — reuses existing run for same date)
                Guid runId;
                try
                {
                    runId = await orchestrator.RunAsync(
                        new RunOptions { IsBacktest = true, AsOfDate = today, SimulationJobId = jobId }, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Simulation {JobId}: skipping {Day} — orchestrator failed", jobId, today);
                    continue;
                }

                var picks = await db.StockPicks
                    .Where(p => p.RunId == runId)
                    .ToListAsync(ct);

                if (picks.Count == 0)
                {
                    logger.LogWarning("Simulation {JobId}: no picks for {Day} — skipping", jobId, today);
                    continue;
                }

                // Fetch next-day closing prices (5-day window handles weekends/holidays)
                var tickers = picks.Select(p => p.Ticker).ToList();
                var nextPrices = await stockDataService.FetchClosingPricesAsync(tickers, tomorrow, ct);

                var validPicks = picks
                    .Where(p => nextPrices.ContainsKey(p.Ticker) && p.PriceAtRecommendation > 0)
                    .ToList();

                if (validPicks.Count == 0)
                {
                    logger.LogWarning("Simulation {JobId}: no next-day data for {Day} — skipping", jobId, today);
                    continue;
                }

                var avgReturn = (decimal)await ComputeWeightedReturnAsync(validPicks, nextPrices, runId, ct);

                var capitalAfter = capital * (1m + avgReturn);

                db.SimulationDayResults.Add(new SimulationDayResult
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    RunId = runId,
                    TradingDate = today,
                    CapitalBefore = capital,
                    CapitalAfter = capitalAfter
                });
                await db.SaveChangesAsync(ct);

                capital = capitalAfter;

                // Configurable delay to respect Anthropic rate limits
                var delayMs = settings.SimulationDelaySeconds * 1_000;
                if (i < tradingDays.Count - 2 && delayMs > 0)
                    await Task.Delay(delayMs, ct);
            }

            job.FinalCapital = capital;
            job.Status = SimulationStatus.Completed;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Simulation {JobId} completed. Final capital: {Capital:C}", jobId, capital);
        }
        catch (OperationCanceledException)
        {
            job.Status = SimulationStatus.Failed;
            job.ErrorMessage = "Job was cancelled";
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Simulation {JobId} failed", jobId);
            job.Status = SimulationStatus.Failed;
            job.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<double> ComputeWeightedReturnAsync(
        List<StockPick> picks,
        Dictionary<string, decimal> nextPrices,
        Guid runId,
        CancellationToken ct)
    {
        var profile = await winnerAnalysisService.GetProfileAsync(ct: ct);

        if (profile is null)
            return picks.Average(p =>
                (double)(nextPrices[p.Ticker] - p.PriceAtRecommendation) / (double)p.PriceAtRecommendation);

        // Fetch all snapshots for this run to compute volume percentiles in context
        var runSnapshots = await db.StockSnapshots
            .Where(s => s.RunId == runId)
            .ToListAsync(ct);

        var sortedVols = runSnapshots.Select(s => s.Volume).OrderBy(v => v).ToList();
        var snapMap    = runSnapshots.ToDictionary(s => s.Ticker, StringComparer.OrdinalIgnoreCase);

        var scores = picks
            .Select(p =>
            {
                if (!snapMap.TryGetValue(p.Ticker, out var snap)) return 1.0;
                var volPct = WinnerAnalysisService.ComputeVolumePercentile(snap.Volume, sortedVols);
                return WinnerAnalysisService.ScoreSnapshot(snap, volPct, profile);
            })
            .ToList();

        var totalScore = scores.Sum();
        if (totalScore <= 0)
            return picks.Average(p =>
                (double)(nextPrices[p.Ticker] - p.PriceAtRecommendation) / (double)p.PriceAtRecommendation);

        return picks.Zip(scores, (p, w) =>
        {
            var ret = (double)(nextPrices[p.Ticker] - p.PriceAtRecommendation) / (double)p.PriceAtRecommendation;
            return ret * (w / totalScore);
        }).Sum();
    }
}
