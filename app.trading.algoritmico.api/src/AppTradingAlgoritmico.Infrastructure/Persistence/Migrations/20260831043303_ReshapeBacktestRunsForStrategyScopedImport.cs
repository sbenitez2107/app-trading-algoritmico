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
        /// <summary>
        /// Rolls back to the rev-1 shape. THIS ROLLBACK IS LOSSY BY CONSTRUCTION, and discards
        /// every imported backtest run, every trade under it, and every walk-forward export.
        /// <para>
        /// It cannot be otherwise. Rev-1 identified a run by <c>(StrategyNameKey, RunLabel)</c>,
        /// both parsed out of a file-name convention that no longer exists anywhere in the system,
        /// and the rev-2 rows carry no value to derive them from — which is why the columns come
        /// back with <c>defaultValue: ""</c>. Recreating the UNIQUE index over that pair with two
        /// or more rows present would therefore compare <c>("", "")</c> against <c>("", "")</c> and
        /// fail, and two rows is the ordinary steady state rather than an edge case: one Deploy
        /// slot plus one Evaluation slot. The UNIQUE <c>ContentHash</c> index below has the same
        /// problem from the other side — the forward design dropped it precisely because identical
        /// bytes legitimately back runs under two different strategies.
        /// </para>
        /// <para>
        /// So the rows are discarded here, deliberately and in the open. The alternative is not a
        /// faithful rollback, it is a <c>Down</c> that throws — and a migration whose rollback
        /// cannot execute is not a rollback at all. Re-import the trade lists and walk-forward
        /// exports after rolling back; every one of them is reproducible from its source file,
        /// which is the reason this is an acceptable loss and live trade data would not be.
        /// </para>
        /// <para>
        /// <c>StrategyTrades</c>, <c>Strategies</c> and every other table are untouched.
        /// </para>
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ordered child-first: explicit rather than relying on the cascade, so the intent is
            // visible at the point of loss instead of implied by a foreign-key setting elsewhere.
            // This MUST precede the CreateIndex calls below — they are the constraints these rows
            // cannot satisfy.
            migrationBuilder.Sql("DELETE FROM [BacktestTrades];");
            migrationBuilder.Sql("DELETE FROM [BacktestRuns];");

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
