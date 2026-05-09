using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockMarketBuyingGuide.Api.Configuration;
using StockMarketBuyingGuide.Api.Infrastructure;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;
using StockMarketBuyingGuide.Api.Models.Dto;
using StockMarketBuyingGuide.Api.Services;

namespace StockMarketBuyingGuide.Api.Tests.Unit;

public class BacktestIdempotencyTests
{
    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static RecommendationOrchestrator CreateOrchestrator(AppDbContext db)
    {
        var stockDataService = new StockDataService(new NullLogger<StockDataService>());
        var newsService = new NewsService(
            Mock.Of<IHttpClientFactory>(),
            new AppSettings(),
            new NullLogger<NewsService>());
        var claudeService = new ClaudeService(new AppSettings(), new NullLogger<ClaudeService>());
        var perfService = new PerformanceTrackingService(
            db, stockDataService, new NullLogger<PerformanceTrackingService>());

        return new RecommendationOrchestrator(
            db, stockDataService, newsService, claudeService, perfService,
            new NullLogger<RecommendationOrchestrator>());
    }

    [Fact]
    public async Task RunAsync_ReturnsSameRunId_WhenSameDateBacktestAlreadyCompleted()
    {
        // Arrange: seed a completed backtest for a specific date
        using var db = CreateDb("idempotency-same-date");
        var testDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15));
        var existingId = Guid.NewGuid();

        db.RecommendationRuns.Add(new RecommendationRun
        {
            Id = existingId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-1),
            IsBacktest = true,
            AsOfDate = testDate
        });
        await db.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(db);

        // Act
        var returnedId = await orchestrator.RunAsync(
            new RunOptions { IsBacktest = true, AsOfDate = testDate });

        // Assert: returns the existing run, not a new one
        returnedId.Should().Be(existingId);
        db.RecommendationRuns.Count().Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_CreatesNewRun_WhenDifferentDateGiven()
    {
        // Arrange: seed a completed backtest for one date
        using var db = CreateDb("idempotency-different-date");
        var existingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20));
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15));

        db.RecommendationRuns.Add(new RecommendationRun
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-1),
            IsBacktest = true,
            AsOfDate = existingDate
        });
        await db.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(db);

        // Act: run for a different date
        // (will fail mid-run since services aren't real, but a new run row gets created)
        try
        {
            await orchestrator.RunAsync(
                new RunOptions { IsBacktest = true, AsOfDate = newDate });
        }
        catch { /* expected — Yahoo/Claude calls will throw with no real config */ }

        // Assert: a second run was created for the new date
        db.RecommendationRuns.Count().Should().Be(2);
        db.RecommendationRuns.Should().Contain(r => r.AsOfDate == newDate);
    }

    [Fact]
    public async Task RunAsync_CreatesNewRun_WhenPreviousRunWasIncomplete()
    {
        // Arrange: seed an incomplete (no CompletedAt) backtest for same date
        using var db = CreateDb("idempotency-incomplete-run");
        var testDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15));

        db.RecommendationRuns.Add(new RecommendationRun
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CompletedAt = null, // not completed
            IsBacktest = true,
            AsOfDate = testDate
        });
        await db.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(db);

        // Act
        try
        {
            await orchestrator.RunAsync(
                new RunOptions { IsBacktest = true, AsOfDate = testDate });
        }
        catch { /* expected */ }

        // Assert: a second run was created (incomplete run is not reused)
        db.RecommendationRuns.Count().Should().Be(2);
    }
}
