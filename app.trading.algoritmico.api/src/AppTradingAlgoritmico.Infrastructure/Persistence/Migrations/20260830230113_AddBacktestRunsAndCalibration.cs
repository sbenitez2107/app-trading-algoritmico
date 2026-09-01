using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBacktestRunsAndCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StrategyNameKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RunLabel = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    AttributionStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SymbolCalibrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PointValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    MinObserved = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    MaxObserved = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CalibratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolCalibrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BacktestRunStrategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BacktestRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRunStrategies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestRunStrategies_BacktestRuns_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "BacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BacktestRunStrategies_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BacktestTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BacktestRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowIndex = table.Column<int>(type: "int", nullable: false),
                    Ticket = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpenTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Size = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    CloseTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosePrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Profit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SampleTypeRaw = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Segment = table.Column<int>(type: "int", nullable: false),
                    SegmentIndex = table.Column<int>(type: "int", nullable: true),
                    CloseType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RealizedRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StopLoss = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestTrades_BacktestRuns_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "BacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_ContentHash",
                table: "BacktestRuns",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_StrategyNameKey_RunLabel",
                table: "BacktestRuns",
                columns: new[] { "StrategyNameKey", "RunLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRunStrategies_BacktestRunId_StrategyId",
                table: "BacktestRunStrategies",
                columns: new[] { "BacktestRunId", "StrategyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRunStrategies_StrategyId",
                table: "BacktestRunStrategies",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTrades_BacktestRunId_RowIndex",
                table: "BacktestTrades",
                columns: new[] { "BacktestRunId", "RowIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTrades_BacktestRunId_Segment",
                table: "BacktestTrades",
                columns: new[] { "BacktestRunId", "Segment" });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTrades_Ticket",
                table: "BacktestTrades",
                column: "Ticket");

            migrationBuilder.CreateIndex(
                name: "IX_SymbolCalibrations_Symbol",
                table: "SymbolCalibrations",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestRunStrategies");

            migrationBuilder.DropTable(
                name: "BacktestTrades");

            migrationBuilder.DropTable(
                name: "SymbolCalibrations");

            migrationBuilder.DropTable(
                name: "BacktestRuns");
        }
    }
}
