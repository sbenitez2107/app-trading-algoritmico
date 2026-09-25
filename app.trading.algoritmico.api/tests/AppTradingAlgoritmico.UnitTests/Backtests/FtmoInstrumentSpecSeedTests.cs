using AppTradingAlgoritmico.Infrastructure.Persistence.Migrations;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Pins the exact seed values inside <see cref="AddFtmoInstrumentSpecs.SeedSql"/> (design.md D4
/// seed table verbatim) row by row, not merely a row count -- a test asserting only "4 rows exist"
/// cannot fail if a value is transposed between rows.
/// </summary>
public class FtmoInstrumentSpecSeedTests
{
    [Fact]
    public void SeededRows_MatchTheFourInstrumentSpecs_WithCorrectContractSizeCurrencyAndGrid()
    {
        var sql = AddFtmoInstrumentSpecs.SeedSql;

        sql.Should().Contain("'XAUUSD_M1_UTC02', 'XAUUSD', 100.0, 'USD', 2, 0.01, 0.01, 1000.0");
        sql.Should().Contain("'DEUIDXEUR_M1_UTC02', 'GER40.cash', 1.0, 'EUR', 2, 0.01, 0.01, 1000.0");
        sql.Should().Contain("'USATECHIDXUSD_M1_UTC02', 'US100.cash', 1.0, 'USD', 2, 0.01, 0.01, 1000.0");
        sql.Should().Contain("'BTCUSD_M1_UTC02', 'BTCUSD', 1.0, 'USD', 2, 0.01, 0.01, 5.00");
    }

    [Fact]
    public void SeededRows_AllUseTheJerusalemSourceTimeZone()
    {
        var sql = AddFtmoInstrumentSpecs.SeedSql;

        sql.Should().Contain("'XAUUSD_M1_UTC02', 'XAUUSD', 100.0, 'USD', 2, 0.01, 0.01, 1000.0, 'Asia/Jerusalem'");
        sql.Should().Contain("'DEUIDXEUR_M1_UTC02', 'GER40.cash', 1.0, 'EUR', 2, 0.01, 0.01, 1000.0, 'Asia/Jerusalem'");
        sql.Should().Contain("'USATECHIDXUSD_M1_UTC02', 'US100.cash', 1.0, 'USD', 2, 0.01, 0.01, 1000.0, 'Asia/Jerusalem'");
        sql.Should().Contain("'BTCUSD_M1_UTC02', 'BTCUSD', 1.0, 'USD', 2, 0.01, 0.01, 5.00, 'Asia/Jerusalem'");
    }
}
