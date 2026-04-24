using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Broker = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    Login = table.Column<long>(type: "bigint", nullable: false),
                    PasswordEncrypted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Server = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingAccounts_Broker_AccountType",
                table: "TradingAccounts",
                columns: new[] { "Broker", "AccountType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingAccounts");
        }
    }
}
