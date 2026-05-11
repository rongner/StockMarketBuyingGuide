using System.Text.Json;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using YahooFinanceApi;

namespace StockMarketBuyingGuide.Api.Services;

public class StockDataService(ILogger<StockDataService> logger, IHttpClientFactory httpClientFactory)
{
    public async Task<List<StockSnapshot>> FetchSnapshotsAsync(
        IEnumerable<string> tickers,
        DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var tickerList = tickers.ToList();
        var snapshotDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (asOfDate.HasValue)
            return await FetchHistoricalAsync(tickerList, asOfDate.Value, ct);

        return await FetchLiveAsync(tickerList, snapshotDate, ct);
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
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var tasks = tickers.Select(async ticker =>
        {
            try
            {
                var bar = await FetchChartBarAsync(ticker, date, ct);
                return (ticker, bar);
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

        var tasks = tickers.Select(async ticker =>
        {
            try
            {
                var (open, close, volume) = await FetchChartOhlcvAsync(ticker, asOfDate, ct);
                if (close is null) return null;

                return new StockSnapshot
                {
                    Id = Guid.NewGuid(),
                    Ticker = ticker,
                    Price = close.Value,
                    Volume = volume ?? 0,
                    PctChange = open is > 0 ? ((close.Value - open.Value) / open.Value) * 100m : 0m,
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

    // Returns the first closing price on or after the given date using Yahoo's v8 chart API.
    private async Task<decimal?> FetchChartBarAsync(string ticker, DateOnly date, CancellationToken ct)
    {
        // Start a day before to absorb timezone boundary differences in Yahoo's bar timestamps
        var period1 = new DateTimeOffset(date.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var period2 = new DateTimeOffset(date.AddDays(7).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeSeconds();

        var doc = await FetchChartDocAsync(ticker, period1, period2, ct);
        var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
        var timestamps = result.GetProperty("timestamp").EnumerateArray().ToArray();
        var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close").EnumerateArray().ToArray();

        for (int i = 0; i < timestamps.Length; i++)
        {
            var barDate = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime);
            if (barDate >= date && closes[i].ValueKind != JsonValueKind.Null)
                return (decimal)closes[i].GetDouble();
        }

        return null;
    }

    // Returns (open, close, volume) for the last bar on the given date.
    private async Task<(decimal? open, decimal? close, long? volume)> FetchChartOhlcvAsync(
        string ticker, DateOnly date, CancellationToken ct)
    {
        var period1 = new DateTimeOffset(date.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var period2 = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).ToUnixTimeSeconds();

        var doc = await FetchChartDocAsync(ticker, period1, period2, ct);
        var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
        var timestamps = result.GetProperty("timestamp").EnumerateArray().ToArray();
        var quote = result.GetProperty("indicators").GetProperty("quote")[0];
        var opens = quote.GetProperty("open").EnumerateArray().ToArray();
        var closes = quote.GetProperty("close").EnumerateArray().ToArray();
        var volumes = quote.GetProperty("volume").EnumerateArray().ToArray();

        // Find the last bar on or before the requested date
        for (int i = timestamps.Length - 1; i >= 0; i--)
        {
            var barDate = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime);
            if (barDate <= date && closes[i].ValueKind != JsonValueKind.Null)
            {
                var open = opens[i].ValueKind != JsonValueKind.Null ? (decimal?)opens[i].GetDouble() : null;
                var volume = volumes[i].ValueKind != JsonValueKind.Null ? (long?)volumes[i].GetInt64() : null;
                return (open, (decimal)closes[i].GetDouble(), volume);
            }
        }

        return (null, null, null);
    }

    private async Task<JsonDocument> FetchChartDocAsync(string ticker, long period1, long period2, CancellationToken ct)
    {
        var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(ticker)}?interval=1d&period1={period1}&period2={period2}";
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        var json = await client.GetStringAsync(url, ct);
        return JsonDocument.Parse(json);
    }
}
