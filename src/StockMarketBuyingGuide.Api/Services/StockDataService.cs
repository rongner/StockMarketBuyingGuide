using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using YahooFinanceApi;

namespace StockMarketBuyingGuide.Api.Services;

public class StockDataService(ILogger<StockDataService> logger)
{
    public async Task<List<StockSnapshot>> FetchSnapshotsAsync(
        IEnumerable<string> tickers,
        DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var tickerList = tickers.ToList();
        var snapshots = new List<StockSnapshot>();
        var snapshotDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (asOfDate.HasValue)
        {
            snapshots = await FetchHistoricalAsync(tickerList, asOfDate.Value, ct);
        }
        else
        {
            snapshots = await FetchLiveAsync(tickerList, snapshotDate, ct);
        }

        return snapshots;
    }

    private async Task<List<StockSnapshot>> FetchLiveAsync(
        List<string> tickers, DateOnly snapshotDate, CancellationToken ct)
    {
        var snapshots = new List<StockSnapshot>();

        try
        {
            var securities = await Yahoo.Symbols(tickers.ToArray())
                .Fields(
                    Field.RegularMarketPrice,
                    Field.RegularMarketVolume,
                    Field.RegularMarketChangePercent,
                    Field.FiftyTwoWeekHigh,
                    Field.FiftyTwoWeekLow,
                    Field.LongName,
                    Field.ShortName)
                .QueryAsync(ct);

            foreach (var (ticker, security) in securities)
            {
                try
                {
                    snapshots.Add(new StockSnapshot
                    {
                        Id = Guid.NewGuid(),
                        Ticker = ticker,
                        Price = (decimal)security[Field.RegularMarketPrice],
                        Volume = (long)(double)security[Field.RegularMarketVolume],
                        PctChange = (decimal)(double)security[Field.RegularMarketChangePercent],
                        High52Week = (decimal)(double)security[Field.FiftyTwoWeekHigh],
                        Low52Week = (decimal)(double)security[Field.FiftyTwoWeekLow],
                        SnapshotDate = snapshotDate
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping ticker {Ticker} due to field parse error", ticker);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch live quotes from Yahoo Finance");
        }

        return snapshots;
    }

    public async Task<Dictionary<string, decimal>> FetchClosingPricesAsync(
        IEnumerable<string> tickers, DateOnly date, CancellationToken ct = default)
    {
        // Start one day before the target to avoid timezone boundary issues with Yahoo's bar timestamps
        var from = date.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var to = date.AddDays(7).ToDateTime(TimeOnly.MaxValue);

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var tasks = tickers.Select(async ticker =>
        {
            try
            {
                var history = await Yahoo.GetHistoricalAsync(ticker, from, to, Period.Daily, ct);
                // Find the first bar on or after the requested date
                var bar = history.FirstOrDefault(b => DateOnly.FromDateTime(b.DateTime) >= date);
                if (bar is null) return (ticker, (decimal?)null);
                return (ticker, (decimal?)bar.Close);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch closing price for {Ticker} on {Date}", ticker, date);
                return (ticker, (decimal?)null);
            }
        });

        var results = await Task.WhenAll(tasks);
        foreach (var (ticker, price) in results)
            if (price.HasValue) result[ticker] = price.Value;

        return result;
    }

    private async Task<List<StockSnapshot>> FetchHistoricalAsync(
        List<string> tickers, DateOnly asOfDate, CancellationToken ct)
    {
        var snapshots = new List<StockSnapshot>();
        var from = asOfDate.ToDateTime(TimeOnly.MinValue);
        var to = asOfDate.ToDateTime(TimeOnly.MaxValue);

        var tasks = tickers.Select(async ticker =>
        {
            try
            {
                var history = await Yahoo.GetHistoricalAsync(ticker, from, to, Period.Daily, ct);
                var bar = history.LastOrDefault();
                if (bar is null) return null;

                return new StockSnapshot
                {
                    Id = Guid.NewGuid(),
                    Ticker = ticker,
                    Price = bar.Close,
                    Volume = (long)bar.Volume,
                    PctChange = bar.Open > 0 ? ((bar.Close - bar.Open) / bar.Open) * 100m : 0m,
                    High52Week = 0m,
                    Low52Week = 0m,
                    SnapshotDate = asOfDate
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch historical data for {Ticker} on {Date}", ticker, asOfDate);
                return null;
            }
        });

        var results = await Task.WhenAll(tasks);
        snapshots.AddRange(results.Where(s => s is not null)!);
        return snapshots;
    }
}
