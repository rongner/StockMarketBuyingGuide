namespace StockMarketBuyingGuide.Api.Models;

public record WinnerProfile(
    int SampleCount,
    double AvgMomentumPct,
    double AvgRangePosition,
    double AvgVolumePercentile,
    IReadOnlyList<WinnerExample> Examples
);

public record WinnerExample(
    string Ticker,
    string CompanyName,
    double MomentumPct,
    double RangePosition,
    double ReturnPct
);
