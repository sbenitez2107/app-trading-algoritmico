using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerToPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Broker",
                table: "Portfolios",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill existing portfolios' broker from the broker of their members' accounts.
            migrationBuilder.Sql(@"
                UPDATE p SET p.Broker = ISNULL((
                    SELECT TOP 1 ta.Broker
                    FROM PortfolioStrategies ps
                    JOIN Strategies s ON ps.StrategyId = s.Id
                    JOIN TradingAccounts ta ON s.TradingAccountId = ta.Id
                    WHERE ps.PortfolioId = p.Id
                ), '')
                FROM Portfolios p
                WHERE p.Broker = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Broker",
                table: "Portfolios");
        }
    }
}
