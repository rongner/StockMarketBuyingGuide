namespace StockMarketBuyingGuide.Api.Models.Dto;

public record SimulationRequest(string StartDate, string EndDate, decimal StartingCapital);
