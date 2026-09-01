using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReshapeBacktestRunsForStrategyScopedImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestRunStrategies");

            migrationBuilder.DropIndex(
                name: "IX_BacktestRuns_ContentHash",
                table: "BacktestRuns");

            migrationBuilder.DropIndex(
                name: "IX_BacktestRuns_StrategyNameKey_RunLabel",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "RunLabel",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "StrategyNameKey",
                table: "BacktestRuns");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "BacktestRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "StrategyId",
                table: "BacktestRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "StrategyWalkForwardExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OosFromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeployParameters = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvaluationParameters = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyWalkForwardExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyWalkForwardExports_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalkForwardWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowIndex = table.Column<int>(type: "int", nullable: false),
                    PeriodIsStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodIsEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodOosStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodOosEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DaysIs = table.Column<int>(type: "int", nullable: false),
                    DaysOos = table.Column<int>(type: "int", nullable: false),
                    NetProfitIs = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RetDdRatioIs = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DrawdownIs = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AvgTradesPerMonthIs = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetProfitOos = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RetDdRatioOos = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    DrawdownOos = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    AvgTradesPerMonthOos = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsFutureWindow = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalkForwardWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalkForwardWindows_StrategyWalkForwardExports_ExportId",
                        column: x => x.ExportId,
                        principalTable: "StrategyWalkForwardExports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestTrades_BacktestRunId_CloseTime",
                table: "BacktestTrades",
                columns: new[] { "BacktestRunId", "CloseTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_ContentHash",
                table: "BacktestRuns",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_StrategyId_Kind",
                table: "BacktestRuns",
                columns: new[] { "StrategyId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategyWalkForwardExports_StrategyId",
                table: "StrategyWalkForwardExports",
                column: "StrategyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalkForwardWindows_ExportId_RowIndex",
                table: "WalkForwardWindows",
                columns: new[] { "ExportId", "RowIndex" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BacktestRuns_Strategies_StrategyId",
                table: "BacktestRuns",
                column: "StrategyId",
                principalTable: "Strategies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BacktestRuns_Strategies_StrategyId",
                table: "BacktestRuns");

            migrationBuilder.DropTable(
                name: "WalkForwardWindows");

            migrationBuilder.DropTable(
                name: "StrategyWalkForwardExports");

            migrationBuilder.DropIndex(
                name: "IX_BacktestTrades_BacktestRunId_CloseTime",
                table: "BacktestTrades");

            migrationBuilder.DropIndex(
                name: "IX_BacktestRuns_ContentHash",
                table: "BacktestRuns");

            migrationBuilder.DropIndex(
                name: "IX_BacktestRuns_StrategyId_Kind",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "BacktestRuns");

            migrationBuilder.DropColumn(
                name: "StrategyId",
                table: "BacktestRuns");

            migrationBuilder.AddColumn<string>(
                name: "RunLabel",
                table: "BacktestRuns",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StrategyNameKey",
                table: "BacktestRuns",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BacktestRunStrategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BacktestRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
        }
    }
}
