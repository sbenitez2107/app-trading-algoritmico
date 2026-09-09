using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReshapeFundingGuardrails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DrawdownModel",
                table: "BrokerRiskLimits",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "FtmoProduct",
                table: "BrokerRiskLimits",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FundingStageLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerRiskLimitsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageOrdinal = table.Column<int>(type: "int", nullable: false),
                    StageName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxLossLimitPct = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    ProfitTargetPct = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundingStageLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundingStageLimits_BrokerRiskLimits_BrokerRiskLimitsId",
                        column: x => x.BrokerRiskLimitsId,
                        principalTable: "BrokerRiskLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundingStageLimits_BrokerRiskLimitsId_StageOrdinal",
                table: "FundingStageLimits",
                columns: new[] { "BrokerRiskLimitsId", "StageOrdinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Every row that can be NULL here was written AFTER this migration's Up() (which wrote
            // no data), so before this change the column was NOT NULL and no pre-change row could be
            // NULL — the 0 fill is the pre-change schema's own default, not an assumption about data
            // we cannot inspect (design.md — "Migration", D6).
            migrationBuilder.Sql(
                "UPDATE [BrokerRiskLimits] SET [DrawdownModel] = 0 WHERE [DrawdownModel] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "DrawdownModel",
                table: "BrokerRiskLimits",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "FtmoProduct",
                table: "BrokerRiskLimits");

            migrationBuilder.DropTable(
                name: "FundingStageLimits");
        }
    }
}
