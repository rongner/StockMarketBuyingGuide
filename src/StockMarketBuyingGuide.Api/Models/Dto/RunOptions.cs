namespace StockMarketBuyingGuide.Api.Models.Dto;

public record RunOptions(
    bool IsBacktest = false,
    DateOnly? AsOfDate = null,
    Guid? SimulationJobId = null
);
