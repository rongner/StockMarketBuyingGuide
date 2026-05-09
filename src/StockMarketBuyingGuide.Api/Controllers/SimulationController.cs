using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Models.Dto;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Controllers;

[ApiController]
[Route("api/simulation")]
[Authorize]
public class SimulationController(
    AppDbContext db,
    ChannelWriter<Guid> jobQueue) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] SimulationRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.StartDate, out var startDate))
            return BadRequest(new { error = "Invalid startDate. Use yyyy-MM-dd." });

        if (!DateOnly.TryParse(request.EndDate, out var endDate))
            return BadRequest(new { error = "Invalid endDate. Use yyyy-MM-dd." });

        if (endDate <= startDate)
            return BadRequest(new { error = "endDate must be after startDate." });

        if (endDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { error = "endDate must be in the past." });

        if (request.StartingCapital <= 0)
            return BadRequest(new { error = "startingCapital must be positive." });

        // Idempotency: return existing completed job for same parameters
        var existing = await db.SimulationJobs
            .Where(j => j.StartDate == startDate
                     && j.EndDate == endDate
                     && j.StartingCapital == request.StartingCapital
                     && j.Status == SimulationStatus.Completed)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => j.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
            return Ok(new { jobId = existing, cached = true });

        var job = new SimulationJob
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            StartingCapital = request.StartingCapital,
            Status = SimulationStatus.Pending
        };

        db.SimulationJobs.Add(job);
        await db.SaveChangesAsync(ct);

        await jobQueue.WriteAsync(job.Id, ct);

        return Ok(new { jobId = job.Id, cached = false });
    }

    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> Get(Guid jobId, CancellationToken ct)
    {
        var job = await db.SimulationJobs
            .Where(j => j.Id == jobId)
            .Select(j => new
            {
                j.Id,
                j.CreatedAt,
                j.Status,
                j.StartDate,
                j.EndDate,
                j.StartingCapital,
                j.FinalCapital,
                j.ErrorMessage,
                TotalTradingDays = SimulationService.GetTradingDays(j.StartDate, j.EndDate).Count - 1,
                ProgressDays = j.DayResults.Count,
                DayResults = j.DayResults
                    .OrderBy(d => d.TradingDate)
                    .Select(d => new
                    {
                        d.TradingDate,
                        d.CapitalBefore,
                        d.CapitalAfter,
                        DailyReturnPct = d.CapitalBefore > 0
                            ? Math.Round((double)((d.CapitalAfter - d.CapitalBefore) / d.CapitalBefore * 100), 3)
                            : 0.0
                    })
            })
            .FirstOrDefaultAsync(ct);

        if (job is null) return NotFound();
        return Ok(job);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var jobs = await db.SimulationJobs
            .OrderByDescending(j => j.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .Select(j => new
            {
                j.Id,
                j.CreatedAt,
                j.Status,
                j.StartDate,
                j.EndDate,
                j.StartingCapital,
                j.FinalCapital,
                j.ErrorMessage,
                ProgressDays = j.DayResults.Count
            })
            .ToListAsync(ct);

        return Ok(jobs);
    }
}
