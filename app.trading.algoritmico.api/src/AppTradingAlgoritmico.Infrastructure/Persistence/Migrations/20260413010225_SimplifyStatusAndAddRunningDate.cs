using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyStatusAndAddRunningDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RunningStartedAt",
                table: "BatchStages",
                type: "datetime2",
                nullable: true);

            // Migrate old enum values:
            // Old: Pending=0, InProgress=1, Completed=2
            // New: Pending=0, Running=1, Completed=2
            // InProgress(1) should become Pending(0) so users can manually set Running
            migrationBuilder.Sql("UPDATE BatchStages SET Status = 0 WHERE Status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunningStartedAt",
                table: "BatchStages");
        }
    }
}
