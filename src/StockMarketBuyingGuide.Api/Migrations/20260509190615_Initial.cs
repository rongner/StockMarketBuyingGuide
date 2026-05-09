using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockMarketBuyingGuide.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SimulationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartingCapital = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalCapital = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecommendationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsBacktest = table.Column<bool>(type: "boolean", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SimulationJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecommendationRuns_SimulationJobs_SimulationJobId",
                        column: x => x.SimulationJobId,
                        principalTable: "SimulationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NewsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RelatedTickers = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsSnapshots_RecommendationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "RecommendationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SimulationDayResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CapitalBefore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationDayResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulationDayResults_RecommendationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "RecommendationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimulationDayResults_SimulationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "SimulationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockPicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: false),
                    PriceAtRecommendation = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockPicks_RecommendationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "RecommendationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    PctChange = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    High52Week = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Low52Week = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockSnapshots_RecommendationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "RecommendationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceTrackings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DaysAfterPick = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceTrackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceTrackings_StockPicks_PickId",
                        column: x => x.PickId,
                        principalTable: "StockPicks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsSnapshots_RunId",
                table: "NewsSnapshots",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceTrackings_PickId",
                table: "PerformanceTrackings",
                column: "PickId");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationRuns_AsOfDate",
                table: "RecommendationRuns",
                column: "AsOfDate");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationRuns_SimulationJobId",
                table: "RecommendationRuns",
                column: "SimulationJobId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationDayResults_JobId",
                table: "SimulationDayResults",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationDayResults_RunId",
                table: "SimulationDayResults",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationJobs_StartDate_EndDate_StartingCapital",
                table: "SimulationJobs",
                columns: new[] { "StartDate", "EndDate", "StartingCapital" });

            migrationBuilder.CreateIndex(
                name: "IX_StockPicks_RunId",
                table: "StockPicks",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_StockSnapshots_RunId_Ticker",
                table: "StockSnapshots",
                columns: new[] { "RunId", "Ticker" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsSnapshots");

            migrationBuilder.DropTable(
                name: "PerformanceTrackings");

            migrationBuilder.DropTable(
                name: "SimulationDayResults");

            migrationBuilder.DropTable(
                name: "StockSnapshots");

            migrationBuilder.DropTable(
                name: "StockPicks");

            migrationBuilder.DropTable(
                name: "RecommendationRuns");

            migrationBuilder.DropTable(
                name: "SimulationJobs");
        }
    }
}
