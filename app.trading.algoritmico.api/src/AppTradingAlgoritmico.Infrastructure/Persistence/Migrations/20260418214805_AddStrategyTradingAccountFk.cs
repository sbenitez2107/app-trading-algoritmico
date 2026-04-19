using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyTradingAccountFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Strategies_BatchStages_BatchStageId",
                table: "Strategies");

            migrationBuilder.AlterColumn<Guid>(
                name: "BatchStageId",
                table: "Strategies",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "TradingAccountId",
                table: "Strategies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_TradingAccountId",
                table: "Strategies",
                column: "TradingAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Strategies_BatchStages_BatchStageId",
                table: "Strategies",
                column: "BatchStageId",
                principalTable: "BatchStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Strategies_TradingAccounts_TradingAccountId",
                table: "Strategies",
                column: "TradingAccountId",
                principalTable: "TradingAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Strategies_BatchStages_BatchStageId",
                table: "Strategies");

            migrationBuilder.DropForeignKey(
                name: "FK_Strategies_TradingAccounts_TradingAccountId",
                table: "Strategies");

            migrationBuilder.DropIndex(
                name: "IX_Strategies_TradingAccountId",
                table: "Strategies");

            migrationBuilder.DropColumn(
                name: "TradingAccountId",
                table: "Strategies");

            migrationBuilder.AlterColumn<Guid>(
                name: "BatchStageId",
                table: "Strategies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Strategies_BatchStages_BatchStageId",
                table: "Strategies",
                column: "BatchStageId",
                principalTable: "BatchStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
