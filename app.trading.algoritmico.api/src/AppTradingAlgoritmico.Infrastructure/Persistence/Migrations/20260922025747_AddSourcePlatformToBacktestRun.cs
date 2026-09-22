using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTradingAlgoritmico.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePlatformToBacktestRun : Migration
    {
        /// <summary>
        /// PROVENANCE, NOT A SYSTEM-CHOSEN VALUE. Carried as SQL (reachable from
        /// <c>dotnet ef migrations script</c>, not only from a C# reader) and pinned by
        /// <c>BacktestMigrationProvenanceTests</c>. See design.md D2.
        /// <para>
        /// The wording below deliberately avoids the bare word the design draft used ("NOT A
        /// DEFAULT") because <c>BacktestMigrationProvenanceTests</c> asserts this string contains
        /// neither "DEFAULT" nor "defaultValue" anywhere — a guard against a SQL/EF default clause
        /// being reintroduced. Design.md's own illustrative text uses "DEFAULT" as English prose
        /// emphasis, which collides with that exact-substring guard; this is a task/design
        /// inconsistency flagged in the apply report, resolved here by keeping the substance
        /// (asserter, date, "user-supplied historical fact", the coincidence note) and rephrasing
        /// only the one colliding word.
        /// </para>
        /// </summary>
        internal const string BackfillSql = """
            -- PROVENANCE, NOT SYSTEM-CHOSEN. On 2026-09-21 Sebastian Benitez asserted, from his own
            -- SQX build history, that every BacktestRun loaded up to that date came from MT4-era
            -- work. This is a USER-SUPPLIED HISTORICAL FACT recorded once — not derived, inferred or
            -- chosen by the system. The WHERE clause is point-in-time and load-bearing: rows created
            -- after this migration stay NULL until a caller declares a platform. There is no
            -- database-level fallback and no HasDefaultValue anywhere. That the written 0 equals
            -- PlatformType's CLR zero value is a COINCIDENCE: 0 is MT4 because the user says the
            -- files were MT4; if MT4 were 7, this would write 7.
            UPDATE BacktestRuns SET SourcePlatform = 0 WHERE SourcePlatform IS NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourcePlatform",
                table: "BacktestRuns",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(BackfillSql); // no defaultValue:, no defaultValueSql: on the AddColumn — on purpose
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourcePlatform",
                table: "BacktestRuns");
        }
    }
}
