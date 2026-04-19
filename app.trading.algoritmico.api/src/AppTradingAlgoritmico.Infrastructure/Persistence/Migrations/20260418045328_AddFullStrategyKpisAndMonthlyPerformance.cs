using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFullStrategyKpisAndMonthlyPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Renames (preserve existing data) ---
            migrationBuilder.RenameColumn(
                name: "NetProfit",
                table: "Strategies",
                newName: "TotalProfit");

            migrationBuilder.RenameColumn(
                name: "WinRate",
                table: "Strategies",
                newName: "WinningPercentage");

            migrationBuilder.RenameColumn(
                name: "MaxDrawdown",
                table: "Strategies",
                newName: "Drawdown");

            migrationBuilder.RenameColumn(
                name: "TotalTrades",
                table: "Strategies",
                newName: "NumberOfTrades");

            // --- New columns on Strategies ---
            migrationBuilder.AddColumn<decimal>(
                name: "Ahpr",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualReturnMaxDdRatio",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageBarsInLosses",
                table: "Strategies",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageBarsInTrade",
                table: "Strategies",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageBarsInWins",
                table: "Strategies",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageConsecutiveLosses",
                table: "Strategies",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageConsecutiveWins",
                table: "Strategies",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageLoss",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageTrade",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageWin",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BacktestFrom",
                table: "Strategies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BacktestTo",
                table: "Strategies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cagr",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyAvgProfit",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Deviation",
                table: "Strategies",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DrawdownPercent",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Expectancy",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Exposure",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossLoss",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossProfit",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LargestLoss",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LargestWin",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxConsecutiveLosses",
                table: "Strategies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxConsecutiveWins",
                table: "Strategies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyAvgProfit",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfCancelled",
                table: "Strategies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfLosses",
                table: "Strategies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfWins",
                table: "Strategies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PayoutRatio",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitInPips",
                table: "Strategies",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RExpectancy",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RExpectancyScore",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SqnScore",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StagnationInDays",
                table: "Strategies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StagnationPercent",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StrQualityNumber",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "Strategies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timeframe",
                table: "Strategies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WinsLossesRatio",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YearlyAvgProfit",
                table: "Strategies",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YearlyAvgReturn",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZProbability",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZScore",
                table: "Strategies",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            // --- StrategyMonthlyPerformances table ---
            migrationBuilder.CreateTable(
                name: "StrategyMonthlyPerformances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Profit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyMonthlyPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyMonthlyPerformances_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyMonthlyPerformances_StrategyId_Year_Month",
                table: "StrategyMonthlyPerformances",
                columns: new[] { "StrategyId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StrategyMonthlyPerformances");

            migrationBuilder.DropColumn(name: "Ahpr", table: "Strategies");
            migrationBuilder.DropColumn(name: "AnnualReturnMaxDdRatio", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageBarsInLosses", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageBarsInTrade", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageBarsInWins", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageConsecutiveLosses", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageConsecutiveWins", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageLoss", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageTrade", table: "Strategies");
            migrationBuilder.DropColumn(name: "AverageWin", table: "Strategies");
            migrationBuilder.DropColumn(name: "BacktestFrom", table: "Strategies");
            migrationBuilder.DropColumn(name: "BacktestTo", table: "Strategies");
            migrationBuilder.DropColumn(name: "Cagr", table: "Strategies");
            migrationBuilder.DropColumn(name: "DailyAvgProfit", table: "Strategies");
            migrationBuilder.DropColumn(name: "Deviation", table: "Strategies");
            migrationBuilder.DropColumn(name: "DrawdownPercent", table: "Strategies");
            migrationBuilder.DropColumn(name: "Expectancy", table: "Strategies");
            migrationBuilder.DropColumn(name: "Exposure", table: "Strategies");
            migrationBuilder.DropColumn(name: "GrossLoss", table: "Strategies");
            migrationBuilder.DropColumn(name: "GrossProfit", table: "Strategies");
            migrationBuilder.DropColumn(name: "LargestLoss", table: "Strategies");
            migrationBuilder.DropColumn(name: "LargestWin", table: "Strategies");
            migrationBuilder.DropColumn(name: "MaxConsecutiveLosses", table: "Strategies");
            migrationBuilder.DropColumn(name: "MaxConsecutiveWins", table: "Strategies");
            migrationBuilder.DropColumn(name: "MonthlyAvgProfit", table: "Strategies");
            migrationBuilder.DropColumn(name: "NumberOfCancelled", table: "Strategies");
            migrationBuilder.DropColumn(name: "NumberOfLosses", table: "Strategies");
            migrationBuilder.DropColumn(name: "NumberOfWins", table: "Strategies");
            migrationBuilder.DropColumn(name: "PayoutRatio", table: "Strategies");
            migrationBuilder.DropColumn(name: "ProfitInPips", table: "Strategies");
            migrationBuilder.DropColumn(name: "RExpectancy", table: "Strategies");
            migrationBuilder.DropColumn(name: "RExpectancyScore", table: "Strategies");
            migrationBuilder.DropColumn(name: "SqnScore", table: "Strategies");
            migrationBuilder.DropColumn(name: "StagnationInDays", table: "Strategies");
            migrationBuilder.DropColumn(name: "StagnationPercent", table: "Strategies");
            migrationBuilder.DropColumn(name: "StrQualityNumber", table: "Strategies");
            migrationBuilder.DropColumn(name: "Symbol", table: "Strategies");
            migrationBuilder.DropColumn(name: "Timeframe", table: "Strategies");
            migrationBuilder.DropColumn(name: "WinsLossesRatio", table: "Strategies");
            migrationBuilder.DropColumn(name: "YearlyAvgProfit", table: "Strategies");
            migrationBuilder.DropColumn(name: "YearlyAvgReturn", table: "Strategies");
            migrationBuilder.DropColumn(name: "ZProbability", table: "Strategies");
            migrationBuilder.DropColumn(name: "ZScore", table: "Strategies");

            // --- Reverse renames ---
            migrationBuilder.RenameColumn(
                name: "NumberOfTrades",
                table: "Strategies",
                newName: "TotalTrades");

            migrationBuilder.RenameColumn(
                name: "Drawdown",
                table: "Strategies",
                newName: "MaxDrawdown");

            migrationBuilder.RenameColumn(
                name: "WinningPercentage",
                table: "Strategies",
                newName: "WinRate");

            migrationBuilder.RenameColumn(
                name: "TotalProfit",
                table: "Strategies",
                newName: "NetProfit");
        }
    }
}
