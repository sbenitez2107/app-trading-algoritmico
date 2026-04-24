using System.Text;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.StrategyWorkflow;

/// <summary>
/// Unit tests for MtStatementParserService.
/// Fixture: StrategyWorkflow/Fixtures/Report Trades DW DEMO2.htm
/// (Darwinex MT4 account 2089130867, USD, 2026-04-21)
/// </summary>
public class MtStatementParserServiceTests
{
    private readonly MtStatementParserService _sut = new();

    private static Stream LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "StrategyWorkflow", "Fixtures", "Report Trades DW DEMO2.htm");
        return File.OpenRead(path);
    }

    private static Stream HtmlStream(string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        return new MemoryStream(bytes);
    }

    // -------------------------------------------------------------------------
    // R1 — Empty stream → returns null
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_EmptyStream_ReturnsNull()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // R2 / R6 / R7 — Closed trade with [sl] suffix
    // Fixture row: title="#2333376 WF_8_34_NQ_SH_LIR_H1_2_33_3[sl]", Ticket 263463718
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_ClosedTradeSl_MapsAllFieldsCorrectly()
    {
        // Arrange
        using var stream = LoadFixture();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        var trade = result!.Trades.Should().ContainSingle(t => t.Ticket == 263463718).Which;

        trade.MagicNumber.Should().Be(2333376);
        trade.StrategyNameHint.Should().Be("WF_8_34_NQ_SH_LIR_H1_2_33_3");
        trade.CloseReason.Should().Be("SL");
        trade.Type.Should().Be("buy");
        trade.Size.Should().Be(0.08m);
        trade.Item.Should().Be("ndx");
        trade.OpenPrice.Should().Be(26588.9m);
        trade.ClosePrice.Should().Be(26615.1m);
        trade.Profit.Should().Be(20.96m);
        trade.Commission.Should().Be(-0.44m);
        trade.Taxes.Should().Be(0.00m);
        trade.Swap.Should().Be(0.00m);
        trade.OpenTime.Should().Be(new DateTime(2026, 4, 20, 14, 47, 17));
        trade.CloseTime.Should().Be(new DateTime(2026, 4, 20, 17, 3, 33));
        trade.IsOpen.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // R2 / R7 — TP suffix → CloseReason=TP
    // Fixture row: title="#1272533 WF_7_28_NQ_SH_LIR_H1_1_27_2[tp]", Ticket 263004851
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_ClosedTradeTp_SetsCloseReasonTp()
    {
        // Arrange
        using var stream = LoadFixture();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        var trade = result!.Trades.Should().ContainSingle(t => t.Ticket == 263004851).Which;

        trade.CloseReason.Should().Be("TP");
        trade.MagicNumber.Should().Be(1272533);
    }

    // -------------------------------------------------------------------------
    // R2 / R7 — No bracket → CloseReason=null
    // Fixture row: title="#4533187 WF_9_26_XAUUSD_H1_KAMA_BB_4_53", Ticket 263310658
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_ClosedTradeNoBracket_SetsCloseReasonNull()
    {
        // Arrange
        using var stream = LoadFixture();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        var trade = result!.Trades.Should().ContainSingle(t => t.Ticket == 263310658).Which;

        trade.CloseReason.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // R2 / R7 — Unknown suffix → CloseReason=Other
    // Synthetic HTML: a closed row with title ending [other_value]
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_ClosedTradeUnknownSuffix_SetsCloseReasonOther()
    {
        // Arrange — minimal HTML with one closed trade row with [weird] suffix
        const string html = """
            <!DOCTYPE html><html><body>
            <table>
            <tr><td colspan=14><b>Closed Transactions:</b></td></tr>
            <tr align=right>
              <td title="#9999999 MyStrategy[weird]">777001</td>
              <td class=msdate>2026.04.01 10:00:00</td>
              <td>buy</td><td>0.01</td><td>xauusd</td>
              <td>4000.00</td><td>3900.00</td><td>4100.00</td>
              <td class=msdate>2026.04.01 12:00:00</td>
              <td>4050.00</td>
              <td>-0.10</td><td>0.00</td><td>0.00</td><td>50.00</td>
            </tr>
            <tr><td colspan=14><b>Open Trades:</b></td></tr>
            <tr><td colspan=14><b>Working Orders:</b></td></tr>
            <tr><td colspan=14><b>Summary:</b></td></tr>
            <tr align=right>
              <td colspan=2><b>Closed Trade P/L:</b></td><td colspan=2><b>50.00</b></td>
              <td colspan=4><b>Floating P/L:</b></td><td><b>0.00</b></td>
              <td colspan=3><b>Margin:</b></td><td colspan=2><b>0.00</b></td>
            </tr>
            <tr align=right>
              <td colspan=2><b>Balance:</b></td><td colspan=2><b>1000.00</b></td>
              <td colspan=4><b>Equity:</b></td><td><b>1000.00</b></td>
              <td colspan=3><b>Free Margin:</b></td><td colspan=2><b>1000.00</b></td>
            </tr>
            </table>
            </body></html>
            """;
        using var stream = HtmlStream(html);

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        var trade = result!.Trades.Should().ContainSingle(t => t.Ticket == 777001).Which;
        trade.CloseReason.Should().Be("Other");
    }

    // -------------------------------------------------------------------------
    // R4 — Cancelled row → absent from parsed result
    // Fixture: Ticket 263492812, title="#62218610 cancelled", colspan=4 cancelled
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_CancelledRow_IsAbsentFromResult()
    {
        // Arrange
        using var stream = LoadFixture();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result!.Trades.Should().NotContain(t => t.Ticket == 263492812);
    }

    // -------------------------------------------------------------------------
    // R5 — Working Orders rows → absent from parsed result
    // Fixture tickets: 263455666, 263457552, 263457603, 263466615, 263534048, 263535623
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_WorkingOrdersRows_AreAbsentFromResult()
    {
        // Arrange
        using var stream = LoadFixture();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        long[] workingOrderTickets = [263455666, 263457552, 263457603, 263466615, 263534048, 263535623];
        result!.Trades.Should().NotContain(t => workingOrderTickets.Contains(t.Ticket));
    }

    // -------------------------------------------------------------------------
    // R3 — Open trade → CloseTime=null, IsOpen=true, ClosePrice=null
    // Fixture: Ticket 263502096, XAUUSD, magic 15418111
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_OpenTrade_HasNullCloseTimeAndIsOpenTrue()
    {
        // Arrange
        using var stream = LoadFixture();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        var trade = result!.Trades.Should().ContainSingle(t => t.Ticket == 263502096).Which;

        trade.CloseTime.Should().BeNull();
        trade.IsOpen.Should().BeTrue();
        trade.ClosePrice.Should().BeNull();
        trade.OpenTime.Should().Be(new DateTime(2026, 4, 21, 1, 1, 17));
        trade.OpenPrice.Should().Be(4833.84m);
        trade.MagicNumber.Should().Be(15418111);
    }

    // -------------------------------------------------------------------------
    // R6 — Malformed title (no leading #) → row skipped, no exception
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_MalformedTitle_RowSkippedNoException()
    {
        // Arrange — one row with malformed title (no #) plus one valid row
        const string html = """
            <!DOCTYPE html><html><body>
            <table>
            <tr><td colspan=14><b>Closed Transactions:</b></td></tr>
            <tr align=right>
              <td title="NO_HASH_VALUE">888001</td>
              <td class=msdate>2026.04.01 10:00:00</td>
              <td>buy</td><td>0.01</td><td>xauusd</td>
              <td>4000.00</td><td>3900.00</td><td>4100.00</td>
              <td class=msdate>2026.04.01 12:00:00</td>
              <td>4050.00</td>
              <td>-0.10</td><td>0.00</td><td>0.00</td><td>50.00</td>
            </tr>
            <tr align=right>
              <td title="#1234567 ValidStrategy[sl]">888002</td>
              <td class=msdate>2026.04.01 10:00:00</td>
              <td>buy</td><td>0.01</td><td>xauusd</td>
              <td>4000.00</td><td>3900.00</td><td>4100.00</td>
              <td class=msdate>2026.04.01 12:00:00</td>
              <td>4050.00</td>
              <td>-0.10</td><td>0.00</td><td>0.00</td><td>50.00</td>
            </tr>
            <tr><td colspan=14><b>Open Trades:</b></td></tr>
            <tr><td colspan=14><b>Working Orders:</b></td></tr>
            <tr><td colspan=14><b>Summary:</b></td></tr>
            <tr align=right>
              <td colspan=2><b>Closed Trade P/L:</b></td><td colspan=2><b>50.00</b></td>
              <td colspan=4><b>Floating P/L:</b></td><td><b>0.00</b></td>
              <td colspan=3><b>Margin:</b></td><td colspan=2><b>0.00</b></td>
            </tr>
            <tr align=right>
              <td colspan=2><b>Balance:</b></td><td colspan=2><b>1000.00</b></td>
              <td colspan=4><b>Equity:</b></td><td><b>1000.00</b></td>
              <td colspan=3><b>Free Margin:</b></td><td colspan=2><b>1000.00</b></td>
            </tr>
            </table>
            </body></html>
            """;
        using var stream = HtmlStream(html);

        // Act
        var act = async () => await _sut.ParseAsync(stream);

        // Assert — no exception thrown
        await act.Should().NotThrowAsync();

        // Reset stream and re-parse to check result
        using var stream2 = HtmlStream(html);
        var result = await _sut.ParseAsync(stream2);

        result.Should().NotBeNull();
        result!.Trades.Should().NotContain(t => t.Ticket == 888001, "malformed title row must be skipped");
        result.Trades.Should().ContainSingle(t => t.Ticket == 888002, "valid row must still be parsed");
    }

    // -------------------------------------------------------------------------
    // R10 — Summary section parsed correctly
    // Fixture: Balance=102730.18, Equity=102918.16, FloatingPnL=187.98,
    //          Margin=9054.14, FreeMargin=93864.02, ClosedTradePnL=2897.15,
    //          ReportTime=2026-04-21T07:06:00, Currency=USD
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParseAsync_Summary_IsExtractedCorrectly()
    {
        // Arrange
        using var stream = LoadFixture();

        // Act
        var result = await _sut.ParseAsync(stream);

        // Assert
        result.Should().NotBeNull();
        var summary = result!.Summary;

        summary.Balance.Should().Be(102730.18m);
        summary.Equity.Should().Be(102918.16m);
        summary.FloatingPnL.Should().Be(187.98m);
        summary.Margin.Should().Be(9054.14m);
        summary.FreeMargin.Should().Be(93864.02m);
        summary.ClosedTradePnL.Should().Be(2897.15m);
        summary.ReportTime.Should().Be(new DateTime(2026, 4, 21, 7, 6, 0));
        summary.Currency.Should().Be("USD");
    }
}
