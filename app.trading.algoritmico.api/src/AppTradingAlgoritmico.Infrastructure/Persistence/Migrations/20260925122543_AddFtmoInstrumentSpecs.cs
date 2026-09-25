using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFtmoInstrumentSpecs : Migration
    {
        /// <summary>
        /// PROVENANCE, NOT SYSTEM-DERIVED OR DEFAULTED. On 2026-09-24 Sebastian Benitez supplied
        /// these four rows' contract size, currency, lot grid and max volume from their own live
        /// FTMO MetaTrader platform, and confirmed the SQX-to-FTMO symbol mapping the same day
        /// (Engram #2766/#2769). These are USER-SUPPLIED FACTS recorded once, not derived or
        /// chosen by the system — there is no database-level default and no HasDefaultValue
        /// anywhere. BTCUSD_M1_UTC02's maximum volume is 5.00, unlike the other three rows' 1000 —
        /// that distinction is part of the same user-supplied fact, not a system inconsistency.
        /// Carried as SQL (reachable from <c>dotnet ef migrations script</c>, not only from a C#
        /// reader) and pinned by <c>FtmoInstrumentSpecMigrationProvenanceTests</c>. See design.md
        /// Decision 4. Precedent: <c>AddSourcePlatformToBacktestRun.BackfillSql</c>.
        /// </summary>
        internal const string SeedSql = """
            -- PROVENANCE, NOT SYSTEM-DERIVED OR DEFAULTED. On 2026-09-24 Sebastian Benitez supplied
            -- these four rows from their own live FTMO MetaTrader platform, and confirmed the
            -- SQX-to-FTMO symbol mapping the same day (Engram #2766/#2769). These are USER-SUPPLIED
            -- FACTS, not derived or chosen by the system. BTCUSD_M1_UTC02's MaxLots is 5.00, unlike
            -- the other three rows' 1000 -- also a user-supplied fact, not a system inconsistency.
            INSERT INTO FtmoInstrumentSpecs
                (Id, SqxSymbol, FtmoSymbol, ContractSize, ProfitCurrency, SizeDecimals, Step, MinLot, MaxLots, SourceTimeZoneId, Provenance, CapturedOn, CreatedAt)
            VALUES
                (NEWID(), 'XAUUSD_M1_UTC02', 'XAUUSD', 100.0, 'USD', 2, 0.01, 0.01, 1000.0, 'Asia/Jerusalem', 'user-supplied from the FTMO MT platform 2026-09-24, Engram #2766/#2769', '2026-09-24', SYSUTCDATETIME()),
                (NEWID(), 'DEUIDXEUR_M1_UTC02', 'GER40.cash', 1.0, 'EUR', 2, 0.01, 0.01, 1000.0, 'Asia/Jerusalem', 'user-supplied from the FTMO MT platform 2026-09-24, Engram #2766/#2769', '2026-09-24', SYSUTCDATETIME()),
                (NEWID(), 'USATECHIDXUSD_M1_UTC02', 'US100.cash', 1.0, 'USD', 2, 0.01, 0.01, 1000.0, 'Asia/Jerusalem', 'user-supplied from the FTMO MT platform 2026-09-24, Engram #2766/#2769', '2026-09-24', SYSUTCDATETIME()),
                (NEWID(), 'BTCUSD_M1_UTC02', 'BTCUSD', 1.0, 'USD', 2, 0.01, 0.01, 5.00, 'Asia/Jerusalem', 'user-supplied from the FTMO MT platform 2026-09-24, Engram #2766/#2769', '2026-09-24', SYSUTCDATETIME());
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FtmoInstrumentSpecs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SqxSymbol = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FtmoSymbol = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContractSize = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ProfitCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SizeDecimals = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MinLot = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MaxLots = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SourceTimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provenance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapturedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FtmoInstrumentSpecs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FtmoInstrumentSpecs_SqxSymbol",
                table: "FtmoInstrumentSpecs",
                column: "SqxSymbol",
                unique: true);

            migrationBuilder.Sql(SeedSql); // no defaultValue:, no defaultValueSql: on any column above -- on purpose
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FtmoInstrumentSpecs");
        }
    }
}
