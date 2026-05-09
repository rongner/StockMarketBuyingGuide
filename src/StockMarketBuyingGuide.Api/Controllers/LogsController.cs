using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockMarketBuyingGuide.Api.Models.Dto;

namespace StockMarketBuyingGuide.Api.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize]
public class LogsController(ILogger<LogsController> logger) : ControllerBase
{
    private static readonly HashSet<string> ValidLevels =
        ["debug", "info", "warn", "error"];

    [HttpPost]
    public IActionResult Ingest([FromBody] List<ClientLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            var level = entry.Level?.ToLowerInvariant() ?? "info";
            if (!ValidLevels.Contains(level)) level = "info";

            var context = entry.Context ?? "client";
            var message = $"[{context}] {entry.Message}";

            switch (level)
            {
                case "error": logger.LogError("{ClientMessage} {@Data}", message, entry.Data); break;
                case "warn":  logger.LogWarning("{ClientMessage} {@Data}", message, entry.Data); break;
                case "debug": logger.LogDebug("{ClientMessage} {@Data}", message, entry.Data); break;
                default:      logger.LogInformation("{ClientMessage} {@Data}", message, entry.Data); break;
            }
        }

        return NoContent();
    }
}
