using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Models;

namespace StockMarketBuyingGuide.Api.Services;

public class WinnerAnalysisService(AppDbContext db, ILogger<WinnerAnalysisService> logger)
{
    public const int MinSamples = 5;
    public const int FilteredUniverseSize = 60;
    private const double MomentumSigma = 3.0;
    private const double RangeSigma    = 20.0;
    private const double VolumeSigma   = 20.0;  // percentile points

    public async Task<WinnerProfile?> GetProfileAsync(
        int daysAfterPick = 1,
        double minReturnPct = 0.5,
        CancellationToken ct = default)
    {
        var rawWinners = await db.StockPicks
            .SelectMany(p => p.PerformanceRecords
                .Where(pt => pt.DaysAfterPick == daysAfterPick)
                .Select(pt => new
                {
                    p.RunId,
                    p.Ticker,
                    p.CompanyName,
                    p.PriceAtRecommendation,
                    pt.CurrentPrice,
                }))
            .Where(x => x.PriceAtRecommendation > 0)
            .ToListAsync(ct);

        var winners = rawWinners
            .Where(x => ReturnPct(x.PriceAtRecommendation, x.CurrentPrice) >= minReturnPct)
            .ToList();

        if (winners.Count < MinSamples)
        {
            logger.LogInformation(
                "Only {Count} winners (>{Threshold}% in {Days}d) found — need {Min} to build profile",
                winners.Count, minReturnPct, daysAfterPick, MinSamples);
            return null;
        }

        var runIds  = winners.Select(w => w.RunId).Distinct().ToList();
        var tickers = winners.Select(w => w.Ticker).Distinct().ToList();

        // Fetch winner snapshots for momentum/range/volume features
        var winnerSnapshots = await db.StockSnapshots
            .Where(s => runIds.Contains(s.RunId) && tickers.Contains(s.Ticker))
            .ToListAsync(ct);

        var snapshotMap = winnerSnapshots
            .GroupBy(s => (s.RunId, s.Ticker))
            .ToDictionary(g => g.Key, g => g.First());

        // Fetch all snapshots for the same runs to compute volume percentiles
        var allRunSnapshots = await db.StockSnapshots
            .Where(s => runIds.Contains(s.RunId))
            .Select(s => new { s.RunId, s.Volume })
            .ToListAsync(ct);

        var volumesByRun = allRunSnapshots
            .GroupBy(s => s.RunId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Volume).OrderBy(v => v).ToList());

        var features = winners.Select(w =>
        {
            snapshotMap.TryGetValue((w.RunId, w.Ticker), out var snap);
            var momentum = snap is not null ? (double)snap.PctChange : 0.0;
            var rangePos = snap is not null && snap.High52Week > snap.Low52Week
                ? (double)((snap.Price - snap.Low52Week) / (snap.High52Week - snap.Low52Week) * 100m)
                : -1.0;
            var volPct = snap is not null && volumesByRun.TryGetValue(w.RunId, out var runVols)
                ? ComputeVolumePercentile(snap.Volume, runVols)
                : -1.0;

            return (
                momentum,
                rangePos,
                volPct,
                ret: ReturnPct(w.PriceAtRecommendation, w.CurrentPrice),
                w.Ticker,
                w.CompanyName
            );
        }).ToList();

        var avgMomentum = features.Average(f => f.momentum);
        var rangePoints = features.Where(f => f.rangePos >= 0).ToList();
        var avgRangePos = rangePoints.Count > 0 ? rangePoints.Average(f => f.rangePos) : 0.0;
        var volPoints   = features.Where(f => f.volPct >= 0).ToList();
        var avgVolPct   = volPoints.Count > 0 ? volPoints.Average(f => f.volPct) : 0.0;

        var examples = features
            .OrderByDescending(f => f.ret)
            .Take(5)
            .Select(f => new WinnerExample(
                f.Ticker, f.CompanyName, f.momentum,
                f.rangePos >= 0 ? f.rangePos : 0.0, f.ret))
            .ToList();

        logger.LogInformation(
            "Winner profile built ({Days}d): {Count} samples, momentum={Momentum:+0.##;-0.##}%, rangePos={Range:F0}%, volPct={Vol:F0}th",
            daysAfterPick, features.Count, avgMomentum, avgRangePos, avgVolPct);

        return new WinnerProfile(features.Count, avgMomentum, avgRangePos, avgVolPct, examples);
    }

    public List<StockSnapshot> FilterToProfile(List<StockSnapshot> snapshots, WinnerProfile profile)
    {
        if (snapshots.Count <= FilteredUniverseSize)
            return snapshots;

        var sortedVols = snapshots.Select(s => s.Volume).OrderBy(v => v).ToList();

        var scored = snapshots
            .Select(s => (s, score: ScoreSnapshot(s, ComputeVolumePercentile(s.Volume, sortedVols), profile)))
            .OrderByDescending(x => x.score)
            .Take(FilteredUniverseSize)
            .Select(x => x.s)
            .ToList();

        return scored;
    }

    public static double ScoreSnapshot(StockSnapshot s, double volumePercentile, WinnerProfile profile)
    {
        var momentumDiff  = (double)s.PctChange - profile.AvgMomentumPct;
        var momentumScore = Math.Exp(-(momentumDiff * momentumDiff) / (2 * MomentumSigma * MomentumSigma));

        double rangeScore = 0;
        if (s.High52Week > s.Low52Week && profile.AvgRangePosition > 0)
        {
            var pos       = (double)((s.Price - s.Low52Week) / (s.High52Week - s.Low52Week) * 100m);
            var rangeDiff = pos - profile.AvgRangePosition;
            rangeScore    = Math.Exp(-(rangeDiff * rangeDiff) / (2 * RangeSigma * RangeSigma));
        }

        double volumeScore = 0;
        if (volumePercentile >= 0 && profile.AvgVolumePercentile > 0)
        {
            var volDiff  = volumePercentile - profile.AvgVolumePercentile;
            volumeScore  = Math.Exp(-(volDiff * volDiff) / (2 * VolumeSigma * VolumeSigma));
        }

        return momentumScore + rangeScore + volumeScore;
    }

    public static double ComputeVolumePercentile(long volume, List<long> sortedVolumes)
    {
        if (sortedVolumes.Count <= 1) return 50.0;
        var idx = sortedVolumes.BinarySearch(volume);
        if (idx < 0) idx = ~idx;
        return (double)idx / (sortedVolumes.Count - 1) * 100.0;
    }

    private static double ReturnPct(decimal entryPrice, decimal exitPrice) =>
        (double)(exitPrice - entryPrice) / (double)entryPrice * 100.0;
}
