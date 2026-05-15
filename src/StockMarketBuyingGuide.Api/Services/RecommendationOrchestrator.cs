using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Models.Dto;

namespace StockMarketBuyingGuide.Api.Services;

public class RecommendationOrchestrator(
    AppDbContext db,
    StockDataService stockDataService,
    NewsService newsService,
    GroqService groqService,
    PerformanceTrackingService performanceTrackingService,
    WinnerAnalysisService winnerAnalysisService,
    ILogger<RecommendationOrchestrator> logger)
{
    public async Task<Guid> RunAsync(RunOptions options, CancellationToken ct = default)
    {
        // Idempotency: reuse existing run for same backtest date
        if (options.IsBacktest && options.AsOfDate.HasValue)
        {
            var existing = await db.RecommendationRuns
                .Where(r => r.IsBacktest && r.AsOfDate == options.AsOfDate && r.SimulationJobId == options.SimulationJobId && r.CompletedAt != null)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(ct);

            if (existing != Guid.Empty)
            {
                logger.LogInformation("Reusing existing run {RunId} for {Date}", existing, options.AsOfDate);
                return existing;
            }
        }

        var run = new RecommendationRun
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsBacktest = options.IsBacktest,
            AsOfDate = options.AsOfDate,
            SimulationJobId = options.SimulationJobId
        };

        db.RecommendationRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            // Step 1: Fetch stock snapshots
            logger.LogInformation("Fetching stock data for run {RunId}", run.Id);
            var snapshots = await stockDataService.FetchSnapshotsAsync(
                TickerUniverse.Tickers, options.AsOfDate, ct);

            foreach (var s in snapshots) s.RunId = run.Id;
            db.StockSnapshots.AddRange(snapshots);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Fetched {Count} stock snapshots for run {RunId}", snapshots.Count, run.Id);

            // Step 2: Fetch news
            logger.LogInformation("Fetching news for run {RunId}", run.Id);
            var newsItems = await newsService.FetchNewsAsync(options.AsOfDate, ct);
            foreach (var n in newsItems) n.RunId = run.Id;
            db.NewsSnapshots.AddRange(newsItems);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Fetched {Count} news articles for run {RunId}", newsItems.Count, run.Id);

            // Step 2.5: Build winner profile from historical outcomes
            // Option A — enriches the Groq system prompt with past-winner characteristics
            // Option B — pre-filters the snapshot universe to stocks resembling past winners
            var profile = await winnerAnalysisService.GetProfileAsync(ct: ct);
            var filteredSnapshots = profile is not null
                ? winnerAnalysisService.FilterToProfile(snapshots, profile)
                : snapshots;

            if (profile is not null)
                logger.LogInformation(
                    "Narrowed universe from {Before} to {After} stocks using winner profile ({Samples} samples)",
                    snapshots.Count, filteredSnapshots.Count, profile.SampleCount);

            // Step 3: Ask Groq for picks (filtered universe + enriched prompt)
            logger.LogInformation("Requesting Groq picks for run {RunId}", run.Id);
            var picks = await groqService.GetPicksAsync(filteredSnapshots, newsItems, run.Id, profile, ct);
            db.StockPicks.AddRange(picks);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Groq returned {Count} picks for run {RunId}", picks.Count, run.Id);

            // Step 4: Record performance outcomes for backtests
            if (options.IsBacktest && options.AsOfDate.HasValue && picks.Count > 0)
            {
                logger.LogInformation("Recording performance outcomes for backtest run {RunId}", run.Id);
                await performanceTrackingService.RecordOutcomesAsync(picks, options.AsOfDate.Value, ct);
            }

            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run {RunId} failed", run.Id);
            run.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(ct);
            throw;
        }

        return run.Id;
    }
}
