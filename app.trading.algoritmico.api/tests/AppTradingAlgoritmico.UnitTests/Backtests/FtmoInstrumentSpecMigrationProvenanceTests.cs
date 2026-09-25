using AppTradingAlgoritmico.Infrastructure.Persistence.Migrations;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Pins the provenance comment inside <see cref="AddFtmoInstrumentSpecs.SeedSql"/> (design.md D4).
/// Mirrors <see cref="BacktestMigrationProvenanceTests"/>: the seed values are a user-supplied fact
/// from the FTMO MT platform, not a system-derived or default value, so the comment is fenced into
/// the migration itself — reachable from <c>dotnet ef migrations script</c>, not just a C# reader.
/// </summary>
public class FtmoInstrumentSpecMigrationProvenanceTests
{
    [Fact]
    public void SeedSql_CarriesAllFourSqxSymbols()
    {
        AddFtmoInstrumentSpecs.SeedSql.Should().Contain("XAUUSD_M1_UTC02");
        AddFtmoInstrumentSpecs.SeedSql.Should().Contain("DEUIDXEUR_M1_UTC02");
        AddFtmoInstrumentSpecs.SeedSql.Should().Contain("USATECHIDXUSD_M1_UTC02");
        AddFtmoInstrumentSpecs.SeedSql.Should().Contain("BTCUSD_M1_UTC02");
    }

    [Fact]
    public void SeedSql_CarriesTheUserSuppliedProvenanceAndDate()
    {
        AddFtmoInstrumentSpecs.SeedSql.Should().Contain("user-supplied");
        AddFtmoInstrumentSpecs.SeedSql.Should().Contain("2026-09-24");
    }

    [Fact]
    public void SeedSql_CarriesTheDistinctBtcMaxLots()
    {
        AddFtmoInstrumentSpecs.SeedSql.Should().Contain("5.00");
    }
}
