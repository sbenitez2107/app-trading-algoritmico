using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Pure parser tests for the SQX Optimizer walk-forward export (WF-2 … WF-5, WF-9). No DB, real
/// fixture.
/// <para>
/// This file exists as a SEPARATE suite from <see cref="SqxTradeListParserTests"/> for the same
/// reason the two parsers are separate services: the two exports invert each other's conventions.
/// Every numeric column here is comma-decimal where the trade list uses dots for prices; the dates
/// are <c>dd.MM.yyyy</c> where the trade list uses <c>yyyy.MM.dd HH:mm:ss</c>; and inside the
/// <c>Parameters</c> field the roles invert AGAIN. A shared policy would silently corrupt one side.
/// </para>
/// </summary>
public class WalkForwardExportParserTests
{
    private static readonly IWalkForwardExportParser Sut = new WalkForwardExportParserService();

    private const string WfName = "WFParamsExport_XAUUSD_H1.csv";
    private const string TradeListName = "ListOfTrades_XAUUSD_H1_IST.csv";

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static Stream OpenFixture(string name) => File.OpenRead(FixturePath(name));

    private const string Header =
        "\"Period IS\";\"Period OOS\";\"Days IS\";\"Days OOS\";\"Net profit (IS)\";\"Net profit (OOS)\";" +
        "\"Ret/DD Ratio (IS)\";\"Ret/DD Ratio (OOS)\";\"Drawdown (IS)\";\"Drawdown (OOS)\";" +
        "\"Avg. Trades Per Month (IS)\";\"Avg. Trades Per Month (OOS)\";\"Parameters\"";

    private static string Row(
        string periodIs = "01.01.2016 - 19.03.2021",
        string periodOos = "20.03.2021 - 05.04.2022",
        string daysIs = "1904",
        string daysOos = "381",
        string netProfitIs = "15239,94",
        string netProfitOos = "1830,14",
        string retDdIs = "20,68",
        string retDdOos = "2,06",
        string drawdownIs = "736,89",
        string drawdownOos = "889,29",
        string avgTradesIs = "2,58",
        string avgTradesOos = "3,33",
        string parameters = "TEMAPeriod1=32,ProfitTargetCoef1=5.4,StopLossCoef1=2.05,TrailingStopCoef1=2.91,EMAPeriod1=110,")
        => $"\"{periodIs}\";\"{periodOos}\";\"{daysIs}\";\"{daysOos}\";\"{netProfitIs}\";\"{netProfitOos}\";"
           + $"\"{retDdIs}\";\"{retDdOos}\";\"{drawdownIs}\";\"{drawdownOos}\";\"{avgTradesIs}\";\"{avgTradesOos}\";\"{parameters}\"";

    private static Stream CsvStream(params string[] lines)
        => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines)));

    // ---- Length is a PARSING rule, shared with the columns via BacktestFieldLengths ----

    [Fact]
    public async Task ParseAsync_OverLengthParameters_RejectsTheWholeFileNamingTheRowAndTheLimit()
    {
        // Parameters is an opaque key=value list whose length grows with the number of optimised
        // inputs, and it is persisted into three nvarchar(1000) columns. Left unchecked it reaches
        // SaveChanges as "String or binary data would be truncated" — an error that is NOT
        // transient, so the retry strategy cannot recover from it and the whole import dies with a
        // provider message that names a column, not a file.
        var tooLong = new string('x', BacktestFieldLengths.WalkForwardParameters + 1);

        await using var stream = CsvStream(Header, Row(), Row(parameters: tooLong));

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("Parameters");
        result.RejectionReason.Should().Contain("row 1");
        result.RejectionReason.Should().Contain(BacktestFieldLengths.WalkForwardParameters.ToString());
        result.Windows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_ParametersExactlyAtTheLimit_IsAccepted()
    {
        // The boundary belongs to the accepting side — an off-by-one here would refuse a file the
        // column can hold.
        var atLimit = new string('x', BacktestFieldLengths.WalkForwardParameters);

        await using var stream = CsvStream(Header, Row(), Row(parameters: atLimit));

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeFalse(result.RejectionReason);
        result.Windows.Should().HaveCount(2);
        result.Windows[1].Parameters.Should().Be(atLimit);
    }

    [Fact]
    public async Task ParseAsync_OverLengthFileName_RejectsTheWholeFile()
    {
        // The sanitised name is stored in an nvarchar(260) column, same as the trade-list side.
        var tooLong = new string('n', BacktestFieldLengths.FileNameOrKey + 1) + ".csv";

        await using var stream = CsvStream(Header, Row(), Row());

        var result = await Sut.ParseAsync(stream, tooLong, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain(BacktestFieldLengths.FileNameOrKey.ToString());
        result.Windows.Should().BeEmpty();
    }

    // ---- 4.1: comma decimals ----

    [Fact]
    public async Task ParseAsync_Fixture_ParsesSixWindows()
    {
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeFalse(result.RejectionReason);
        result.Windows.Should().HaveCount(6);
        result.Windows.Select(w => w.RowIndex).Should().Equal(0, 1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task ParseAsync_Fixture_TreatsTheCommaAsADecimalPointNotAThousandsSeparator()
    {
        // "15239,94" is fifteen thousand, not one and a half million. The trade list parses
        // Open/Close price with a DOT, so a shared policy would read this column as 1523994.
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        var first = result.Windows[0];
        first.NetProfitIs.Should().Be(15239.94m);
        first.RetDdRatioIs.Should().Be(20.68m);
        first.DrawdownIs.Should().Be(736.89m);
        first.AvgTradesPerMonthIs.Should().Be(2.58m);
        first.NetProfitOos.Should().Be(1830.14m);
        first.RetDdRatioOos.Should().Be(2.06m);
    }

    [Fact]
    public async Task ParseAsync_Fixture_ParsesIntegerDayColumns()
    {
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.Windows[0].DaysIs.Should().Be(1904);
        result.Windows[0].DaysOos.Should().Be(381);
        result.Windows[4].DaysOos.Should().Be(382);
    }

    // ---- 4.2: dd.MM.yyyy, not the trade list's yyyy.MM.dd ----

    [Fact]
    public async Task ParseAsync_Fixture_ParsesPeriodsAsDayFirstDates()
    {
        // Row 4's "20.02.2019 - 08.05.2024" is unparseable under the trade list's yyyy.MM.dd, so
        // this fails outright rather than quietly transposing day and month if the format leaks.
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        var row = result.Windows[3];
        row.PeriodIsStart.Should().Be(new DateTime(2019, 2, 20));
        row.PeriodIsEnd.Should().Be(new DateTime(2024, 5, 8));
        row.PeriodOosStart.Should().Be(new DateTime(2024, 5, 9));
        row.PeriodOosEnd.Should().Be(new DateTime(2025, 5, 25));
    }

    [Fact]
    public async Task ParseAsync_UnparseableDate_RejectsTheWholeFileNamingTheRow()
    {
        await using var stream = CsvStream(Header, Row(), Row(periodIs: "2019.02.20 - 2024.05.08"));

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("row 1");
        result.RejectionReason.Should().Contain("Period IS");
        result.Windows.Should().BeEmpty();
    }

    // ---- 4.3: the Parameters field inverts punctuation roles ----

    [Fact]
    public async Task ParseAsync_ParametersField_KeepsDotsAsDecimalsAndDropsTheTrailingComma()
    {
        // Applying this file's own comma-decimal rule inside Parameters would split
        // "ProfitTargetCoef1=5.4" into "ProfitTargetCoef1=5" and a stray "4".
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        var pairs = WalkForwardExportParserService.SplitParameters(result.Windows[0].Parameters);

        pairs.Should().HaveCount(5, "the trailing comma yields an empty token that is dropped, not an error");
        pairs.Should().ContainKey("ProfitTargetCoef1");
        pairs["ProfitTargetCoef1"].Should().Be(5.4m);
        pairs["TEMAPeriod1"].Should().Be(32m);
        pairs["StopLossCoef1"].Should().Be(2.05m);
        pairs["TrailingStopCoef1"].Should().Be(2.91m);
        pairs["EMAPeriod1"].Should().Be(110m);
    }

    [Fact]
    public async Task ParseAsync_ParametersField_IsStoredVerbatim()
    {
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.Windows[0].Parameters.Should().Be(
            "TEMAPeriod1=32,ProfitTargetCoef1=5.4,StopLossCoef1=2.05,TrailingStopCoef1=2.91,EMAPeriod1=110,",
            "the field is the only manual audit trail against a run's declared kind, so nothing is normalised out of it");
    }

    // ---- 4.4: the future window, recognised by two signals ----

    [Fact]
    public async Task ParseAsync_Fixture_LastRowIsTheFutureWindowWithNullOosValues()
    {
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        var future = result.Windows[^1];
        future.IsFutureWindow.Should().BeTrue();
        future.NetProfitOos.Should().BeNull();
        future.RetDdRatioOos.Should().BeNull();
        future.DrawdownOos.Should().BeNull();
        future.AvgTradesPerMonthOos.Should().BeNull();

        future.DaysOos.Should().Be(381, "SQX still reports the planned span for the window that has not run");
        future.NetProfitIs.Should().Be(10276.27m);
        future.RetDdRatioIs.Should().Be(13.38m);
        future.DrawdownIs.Should().Be(768.24m);
        future.AvgTradesPerMonthIs.Should().Be(2.44m);
    }

    [Fact]
    public async Task ParseAsync_Fixture_MinimumElapsedOosRetDdIsNotZero()
    {
        // The must-fail guard for N/A-as-zero: the five elapsed windows are 2.06, 1.16, 0.96, 0.52
        // and 1.27, so the worst window is 0.52. Parsing the future row's "N/A" as 0 would make the
        // window that has not happened yet look like the worst evidence in the file.
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        var elapsed = result.Windows.Where(w => !w.IsFutureWindow).ToList();
        elapsed.Should().HaveCount(5);
        elapsed.Min(w => w.RetDdRatioOos).Should().Be(0.52m);
        result.Windows.Where(w => w.RetDdRatioOos is not null).Min(w => w.RetDdRatioOos).Should().Be(0.52m);
    }

    [Fact]
    public async Task ParseAsync_FutureSuffixWithoutTheNaValues_RejectsTheFile()
    {
        await using var stream = CsvStream(
            Header,
            Row(),
            Row(periodOos: "12.06.2026 - 28.06.2027 (future)"));

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("future-window signal mismatch");
    }

    [Fact]
    public async Task ParseAsync_NaValuesWithoutTheFutureSuffix_RejectsTheFile()
    {
        await using var stream = CsvStream(
            Header,
            Row(),
            Row(netProfitOos: "N/A", retDdOos: "N/A", drawdownOos: "N/A", avgTradesOos: "N/A"));

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("future-window signal mismatch");
    }

    [Fact]
    public async Task ParseAsync_NaOnANonLastRow_RejectsTheFileNamingThatRow()
    {
        await using var stream = CsvStream(
            Header,
            Row(netProfitOos: "N/A", retDdOos: "N/A", drawdownOos: "N/A", avgTradesOos: "N/A",
                periodOos: "20.03.2021 - 05.04.2022 (future)"),
            Row());

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("row 0");
        result.RejectionReason.Should().Contain("only the last window");
    }

    // ---- 4.5: whole-file rejections ----

    [Fact]
    public async Task ParseAsync_WrongDelimiter_RejectsTheWholeFile()
    {
        await using var stream = CsvStream(Header.Replace(';', ','), Row().Replace(';', ','));

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("invalid delimiter");
        result.Windows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_MissingParametersColumn_RejectsTheWholeFile()
    {
        var headerWithoutParameters = Header.Replace(";\"Parameters\"", string.Empty);
        await using var stream = CsvStream(headerWithoutParameters, Row(), Row());

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("Parameters");
        result.Windows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_SingleDataRow_RejectsTheWholeFile()
    {
        // OosFromDate is the SECOND-TO-LAST row's OOS start, so a one-window export cannot produce
        // a boundary at all.
        await using var stream = CsvStream(Header, Row());

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("at least 2 windows");
        result.Windows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_TradeListFile_RejectsTheWholeFileNamingTheShapeMismatch()
    {
        await using var stream = OpenFixture(TradeListName);

        var result = await Sut.ParseAsync(stream, TradeListName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("walk-forward-export header");
        result.Windows.Should().BeEmpty();
    }
}
