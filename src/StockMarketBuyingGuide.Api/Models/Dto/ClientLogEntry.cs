namespace StockMarketBuyingGuide.Api.Models.Dto;

public class ClientLogEntry
{
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Context { get; set; }
    public string? Timestamp { get; set; }
    public object? Data { get; set; }
}
