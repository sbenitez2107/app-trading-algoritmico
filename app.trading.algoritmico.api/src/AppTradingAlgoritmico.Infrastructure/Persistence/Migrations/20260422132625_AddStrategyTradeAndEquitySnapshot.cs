using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyTradeAndEquitySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Strategies_TradingAccountId",
                table: "Strategies");

            migrationBuilder.AddColumn<int>(
                name: "MagicNumber",
                table: "Strategies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountEquitySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Equity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FloatingPnL = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Margin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FreeMargin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedTradePnL = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountEquitySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountEquitySnapshots_TradingAccounts_TradingAccountId",
                        column: x => x.TradingAccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategyTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrategyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ticket = table.Column<long>(type: "bigint", nullable: false),
                    OpenTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Size = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Item = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OpenPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ClosePrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: true),
                    StopLoss = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Taxes = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Swap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Profit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CloseReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyTrades_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_TradingAccountId_MagicNumber",
                table: "Strategies",
                columns: new[] { "TradingAccountId", "MagicNumber" },
                unique: true,
                filter: "[TradingAccountId] IS NOT NULL AND [MagicNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountEquitySnapshots_TradingAccountId",
                table: "AccountEquitySnapshots",
                column: "TradingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyTrades_StrategyId_Ticket",
                table: "StrategyTrades",
                columns: new[] { "StrategyId", "Ticket" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountEquitySnapshots");

            migrationBuilder.DropTable(
                name: "StrategyTrades");

            migrationBuilder.DropIndex(
                name: "IX_Strategies_TradingAccountId_MagicNumber",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "MagicNumber",
                table: "Strategies");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_TradingAccountId",
                table: "Strategies",
                column: "TradingAccountId");
        }
    }
}
