using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Models.Dto;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Controllers;

[ApiController]
[Route("api/backtest")]
[Authorize]
public class BacktestController(
    RecommendationOrchestrator orchestrator,
    AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Run([FromBody] BacktestRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.Date, out var date))
            return BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

        if (date >= DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { error = "Backtest date must be in the past." });

        var runId = await orchestrator.RunAsync(
            new RunOptions { IsBacktest = true, AsOfDate = date }, ct);

        return Ok(new { runId });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var runs = await db.RecommendationRuns
            .Where(r => r.IsBacktest && r.SimulationJobId == null)
            .OrderByDescending(r => r.AsOfDate)
            .Skip(offset)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                r.CreatedAt,
                r.CompletedAt,
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
            .Where(r => r.Id == runId && r.IsBacktest)
            .Select(r => new
            {
                r.Id,
                r.CreatedAt,
                r.CompletedAt,
                r.AsOfDate,
                r.ErrorMessage,
                Picks = r.StockPicks.Select(p => new
                {
                    p.Id,
                    p.Ticker,
                    p.CompanyName,
                    p.Reasoning,
                    p.PriceAtRecommendation,
                    Performance = p.PerformanceRecords
                        .OrderBy(x => x.DaysAfterPick)
                        .Select(x => new
                        {
                            x.DaysAfterPick,
                            x.CurrentPrice,
                            PctChange = p.PriceAtRecommendation > 0
                                ? Math.Round((double)((x.CurrentPrice - p.PriceAtRecommendation)
                                    / p.PriceAtRecommendation * 100), 2)
                                : 0.0
                        })
                })
            })
            .FirstOrDefaultAsync(ct);

        if (run is null) return NotFound();
        return Ok(run);
    }
}
