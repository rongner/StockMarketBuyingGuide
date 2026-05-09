using FluentAssertions;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Tests.Unit;

public class SimulationServiceTests
{
    // ── GetTradingDays ────────────────────────────────────────────────────────

    [Fact]
    public void GetTradingDays_ExcludesWeekends()
    {
        var start = new DateOnly(2024, 1, 1);  // Monday
        var end = new DateOnly(2024, 1, 14);   // Sunday

        var days = SimulationService.GetTradingDays(start, end);

        days.Should().NotContain(d => d.DayOfWeek == DayOfWeek.Saturday);
        days.Should().NotContain(d => d.DayOfWeek == DayOfWeek.Sunday);
    }

    [Fact]
    public void GetTradingDays_ReturnsCorrectCount_ForTwoCalendarWeeks()
    {
        // 2024-01-01 (Mon) → 2024-01-14 (Sun) = 10 trading days
        var days = SimulationService.GetTradingDays(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 14));

        days.Should().HaveCount(10);
    }

    [Fact]
    public void GetTradingDays_ReturnsEmpty_WhenStartAfterEnd()
    {
        var days = SimulationService.GetTradingDays(
            new DateOnly(2024, 3, 1),
            new DateOnly(2024, 2, 1));

        days.Should().BeEmpty();
    }

    [Fact]
    public void GetTradingDays_IncludesSingleDay_WhenStartEqualsEnd_OnWeekday()
    {
        var monday = new DateOnly(2024, 1, 8); // Monday
        var days = SimulationService.GetTradingDays(monday, monday);
        days.Should().HaveCount(1).And.Contain(monday);
    }

    [Fact]
    public void GetTradingDays_ReturnsEmpty_WhenStartEqualsEnd_OnWeekend()
    {
        var saturday = new DateOnly(2024, 1, 6);
        var days = SimulationService.GetTradingDays(saturday, saturday);
        days.Should().BeEmpty();
    }

    [Fact]
    public void GetTradingDays_AreInAscendingOrder()
    {
        var days = SimulationService.GetTradingDays(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 3, 31));

        days.Should().BeInAscendingOrder();
    }

    // ── P&L math ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10_000, 0.01,  10_100)]   // +1% day
    [InlineData(10_000, -0.02, 9_800)]    // -2% day
    [InlineData(10_000, 0.00,  10_000)]   // flat
    [InlineData(5_000,  0.05,  5_250)]    // +5% with different capital
    public void DailyReturn_CapitalAfter_MatchesExpected(
        decimal capitalBefore, double returnPct, decimal expectedAfter)
    {
        var capitalAfter = capitalBefore * (1m + (decimal)returnPct);
        capitalAfter.Should().Be(expectedAfter);
    }

    [Fact]
    public void DailyReturn_AveragesPickReturns()
    {
        // Simulate: 3 picks with +10%, -5%, +3% → avg = 2.667%
        var pickReturns = new[] { 0.10, -0.05, 0.03 };
        var avgReturn = pickReturns.Average();

        avgReturn.Should().BeApproximately(0.02667, 0.0001);

        var capitalAfter = 10_000m * (1m + (decimal)avgReturn);
        capitalAfter.Should().BeApproximately(10_266.67m, 0.01m);
    }
}
