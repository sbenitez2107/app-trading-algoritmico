using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialBalanceToTradingAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InitialBalance",
                table: "TradingAccounts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Backfill existing accounts with the project default ($100,000) so analytics
            // endpoints have a sane baseline. New accounts must supply InitialBalance via
            // the create form (validated at the Application layer).
            migrationBuilder.Sql(@"
                UPDATE TradingAccounts
                SET InitialBalance = 100000.00
                WHERE InitialBalance IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialBalance",
                table: "TradingAccounts");
        }
    }
}
