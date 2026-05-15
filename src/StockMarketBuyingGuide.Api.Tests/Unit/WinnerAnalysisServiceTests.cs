using FluentAssertions;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Models;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Tests.Unit;

public class WinnerAnalysisServiceTests
{
    // ── ScoreSnapshot ─────────────────────────────────────────────────────────

    [Fact]
    public void ScoreSnapshot_PerfectMomentumMatch_ReturnsHighScore()
    {
        var profile = new WinnerProfile(10, 2.0, 0, 0, []);
        var snapshot = Snap("AAPL", pctChange: 2.0m);  // exact match

        var score = WinnerAnalysisService.ScoreSnapshot(snapshot, 50.0, profile);

        score.Should().BeApproximately(1.0, 0.001); // max momentum score, no range bonus
    }

    [Fact]
    public void ScoreSnapshot_FarFromProfile_ReturnsLowerScore()
    {
        var profile  = new WinnerProfile(10, 2.0, 0, 0, []);
        var nearSnap = Snap("NEAR", pctChange: 2.1m);
        var farSnap  = Snap("FAR",  pctChange: 10.0m);

        var nearScore = WinnerAnalysisService.ScoreSnapshot(nearSnap, 50.0, profile);
        var farScore  = WinnerAnalysisService.ScoreSnapshot(farSnap, 50.0, profile);

        nearScore.Should().BeGreaterThan(farScore);
    }

    [Fact]
    public void ScoreSnapshot_WithRangeData_AddsRangeBonus()
    {
        // Profile wants ~50% range position
        var profile = new WinnerProfile(10, 2.0, 50.0, 0, []);

        // Snapshot exactly at 50% of its 52wk range
        var atFifty = Snap("X", pctChange: 2.0m, low: 50m, high: 150m, price: 100m); // (100-50)/(150-50)=50%
        var atTop   = Snap("Y", pctChange: 2.0m, low: 50m, high: 150m, price: 148m); // ~98%

        var fiftyScore = WinnerAnalysisService.ScoreSnapshot(atFifty, 50.0, profile);
        var topScore   = WinnerAnalysisService.ScoreSnapshot(atTop, 50.0, profile);

        fiftyScore.Should().BeGreaterThan(topScore);
    }

    [Fact]
    public void ScoreSnapshot_NoRangeData_DoesNotAddBonus()
    {
        var profile = new WinnerProfile(10, 2.0, 50.0, 0, []);

        // high==low means no 52wk data available
        var noRange = Snap("X", pctChange: 2.0m, low: 0m, high: 0m, price: 100m);

        var score = WinnerAnalysisService.ScoreSnapshot(noRange, 50.0, profile);

        score.Should().BeApproximately(1.0, 0.001);  // only momentum contributes
    }

    [Fact]
    public void ScoreSnapshot_ProfileRangePositionZero_SkipsRangeBonus()
    {
        // AvgRangePosition = 0 means no range data in the profile — don't apply range scoring
        var profile = new WinnerProfile(10, 1.5, 0.0, 0, []);
        var s = Snap("X", pctChange: 1.5m, low: 50m, high: 150m, price: 100m);

        var score = WinnerAnalysisService.ScoreSnapshot(s, 50.0, profile);

        score.Should().BeApproximately(1.0, 0.001);
    }

    // ── FilterToProfile ───────────────────────────────────────────────────────

    [Fact]
    public void FilterToProfile_ReturnsAll_WhenBelowCap()
    {
        var profile   = new WinnerProfile(10, 2.0, 0, 0, []);
        var snapshots = Enumerable.Range(1, 10).Select(i => Snap($"S{i}", 1.0m)).ToList();

        var result = new WinnerAnalysisService(null!, null!).FilterToProfile(snapshots, profile);

        result.Should().HaveCount(10);
    }

    [Fact]
    public void FilterToProfile_LimitsToUniverseSize_WhenAboveCap()
    {
        var profile   = new WinnerProfile(10, 2.0, 0, 0, []);
        var snapshots = Enumerable.Range(1, 100).Select(i => Snap($"S{i}", (decimal)i * 0.1m)).ToList();

        var result = new WinnerAnalysisService(null!, null!).FilterToProfile(snapshots, profile);

        result.Should().HaveCount(WinnerAnalysisService.FilteredUniverseSize);
    }

    [Fact]
    public void FilterToProfile_KeepsBestMatchingStocks()
    {
        var profile = new WinnerProfile(10, 2.0, 0, 0, []);

        // One stock exactly matches the winner profile momentum
        var ideal = Snap("IDEAL", pctChange: 2.0m);
        // Fill the rest with stocks far from the profile
        var others = Enumerable.Range(1, 100)
            .Select(i => Snap($"S{i}", pctChange: 50.0m + i))
            .ToList();
        var all = others.Prepend(ideal).ToList();

        var result = new WinnerAnalysisService(null!, null!).FilterToProfile(all, profile);

        result.Should().Contain(ideal);
    }

    // ── GroqService prompt enrichment ─────────────────────────────────────────

    [Fact]
    public void BuildSystemPrompt_WithNullProfile_ReturnsBasePrompt()
    {
        var prompt = GroqService.BuildSystemPrompt(null);
        prompt.Should().Contain("submit_stock_picks");
        prompt.Should().NotContain("Historical performance insight");
    }

    [Fact]
    public void BuildSystemPrompt_WithProfile_InjectsWinnerContext()
    {
        var examples = new List<WinnerExample>
        {
            new("MSFT", "Microsoft", 1.5, 45.0, 4.2)
        };
        var profile = new WinnerProfile(12, 1.8, 45.0, 0, examples);

        var prompt = GroqService.BuildSystemPrompt(profile);

        prompt.Should().Contain("12 past picks");
        prompt.Should().Contain("MSFT");
        prompt.Should().Contain("45");   // range position
        prompt.Should().Contain("4.2");  // return %
    }

    [Fact]
    public void BuildSystemPrompt_WithProfile_OmitsRangeLine_WhenNoRangeData()
    {
        var profile = new WinnerProfile(8, 1.5, 0.0, 0, []);

        var prompt = GroqService.BuildSystemPrompt(profile);

        prompt.Should().Contain("Historical performance insight");
        prompt.Should().NotContain("52-week range position");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StockSnapshot Snap(
        string ticker,
        decimal pctChange,
        decimal low   = 0m,
        decimal high  = 0m,
        decimal price = 100m) => new()
    {
        Id          = Guid.NewGuid(),
        Ticker      = ticker,
        PctChange   = pctChange,
        Price       = price,
        Low52Week   = low,
        High52Week  = high,
        Volume      = 1_000_000,
        SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
    };
}
