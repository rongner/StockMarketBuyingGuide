using Microsoft.EntityFrameworkCore;
using StockMarketBuyingGuide.Api.Infrastructure.Entities;

namespace StockMarketBuyingGuide.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RecommendationRun> RecommendationRuns => Set<RecommendationRun>();
    public DbSet<StockPick> StockPicks => Set<StockPick>();
    public DbSet<StockSnapshot> StockSnapshots => Set<StockSnapshot>();
    public DbSet<NewsSnapshot> NewsSnapshots => Set<NewsSnapshot>();
    public DbSet<PerformanceTracking> PerformanceTrackings => Set<PerformanceTracking>();
    public DbSet<SimulationJob> SimulationJobs => Set<SimulationJob>();
    public DbSet<SimulationDayResult> SimulationDayResults => Set<SimulationDayResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecommendationRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => x.AsOfDate);
            e.HasIndex(x => x.SimulationJobId);
            e.HasMany(x => x.StockPicks).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.StockSnapshots).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.NewsSnapshots).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockPick>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => x.RunId);
            e.Property(x => x.PriceAtRecommendation).HasPrecision(18, 4);
            e.HasMany(x => x.PerformanceRecords).WithOne(x => x.Pick).HasForeignKey(x => x.PickId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockSnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.RunId, x.Ticker });
            e.Property(x => x.Price).HasPrecision(18, 4);
            e.Property(x => x.PctChange).HasPrecision(8, 4);
            e.Property(x => x.High52Week).HasPrecision(18, 4);
            e.Property(x => x.Low52Week).HasPrecision(18, 4);
        });

        modelBuilder.Entity<NewsSnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PerformanceTracking>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.CurrentPrice).HasPrecision(18, 4);
        });

        modelBuilder.Entity<SimulationJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.StartDate, x.EndDate, x.StartingCapital });
            e.Property(x => x.StartingCapital).HasPrecision(18, 2);
            e.Property(x => x.FinalCapital).HasPrecision(18, 2);
            e.HasMany(x => x.Runs).WithOne(x => x.SimulationJob).HasForeignKey(x => x.SimulationJobId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.DayResults).WithOne(x => x.Job).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SimulationDayResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.CapitalBefore).HasPrecision(18, 2);
            e.Property(x => x.CapitalAfter).HasPrecision(18, 2);
            e.HasOne(x => x.Run).WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
