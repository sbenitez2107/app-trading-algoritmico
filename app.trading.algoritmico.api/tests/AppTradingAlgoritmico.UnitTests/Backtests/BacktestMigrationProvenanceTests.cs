using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Infrastructure.Persistence.Migrations;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Pins the provenance comment inside <see cref="AddSourcePlatformToBacktestRun.BackfillSql"/>
/// (design.md D2/D9). The comment is load-bearing, not decoration: a bare
/// <c>UPDATE ... SET SourcePlatform = 0</c> is indistinguishable from the system fabricating a
/// domain fact, so this test fences the asserter's name, the assertion date, and the "not a
/// default" framing into the migration itself — reachable from <c>dotnet ef migrations script</c>,
/// not just from a C# reader.
/// </summary>
public class BacktestMigrationProvenanceTests
{
    [Fact]
    public void BackfillSql_CarriesTheUserSuppliedProvenanceAndNoDefaultLanguage()
    {
        AddSourcePlatformToBacktestRun.BackfillSql.Should().Contain("WHERE SourcePlatform IS NULL");
        AddSourcePlatformToBacktestRun.BackfillSql.Should().Contain("Sebastian Benitez");
        AddSourcePlatformToBacktestRun.BackfillSql.Should().Contain("2026-09-21");
        AddSourcePlatformToBacktestRun.BackfillSql.Should().Contain("USER-SUPPLIED");
        AddSourcePlatformToBacktestRun.BackfillSql.Should().NotContain("DEFAULT");
        AddSourcePlatformToBacktestRun.BackfillSql.Should().NotContain("defaultValue");
    }

    [Fact]
    public void RowCreatedAfterMigration_DoesNotInheritTheBackfill()
    {
        var run = new BacktestRun { SourceFileName = "f.csv", ContentHash = "h" };

        run.SourcePlatform.Should().BeNull("a row created after the migration must start undeclared, not MT4");
    }
}
