using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Models.Dto;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController(
    RecommendationOrchestrator orchestrator,
    AppDbContext db) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var runId = await orchestrator.RunAsync(new RunOptions(), ct);
        return Ok(new { runId });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 20, [FromQuery] int offset = 0, CancellationToken ct = default)
    {
        var runs = await db.RecommendationRuns
            .Where(r => r.SimulationJobId == null)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                r.CreatedAt,
                r.CompletedAt,
                r.IsBacktest,
                r.AsOfDate,
                r.ErrorMessage,
                PickCount = r.StockPicks.Count
            })
            .ToListAsync(ct);

        return Ok(runs);
    }

    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> Get(Guid runId, CancellationToken ct)
    {
        var run = await db.RecommendationRuns
            .Where(r => r.Id == runId)
            .Select(r => new
            {
                r.Id,
                r.CreatedAt,
                r.CompletedAt,
                r.IsBacktest,
                r.AsOfDate,
                r.ErrorMessage,
                Picks = r.StockPicks.Select(p => new
                {
                    p.Id,
                    p.Ticker,
                    p.CompanyName,
                    p.Reasoning,
                    p.PriceAtRecommendation
                }),
                Snapshots = r.StockSnapshots.Select(s => new
                {
                    s.Ticker,
                    s.Price,
                    s.Volume,
                    s.PctChange,
                    s.High52Week,
                    s.Low52Week
                }),
                News = r.NewsSnapshots.Select(n => new
                {
                    n.Headline,
                    n.Url,
                    n.PublishedAt,
                    n.RelatedTickers
                })
            })
            .FirstOrDefaultAsync(ct);

        if (run is null) return NotFound();
        return Ok(run);
    }
}
