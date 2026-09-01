using System.Globalization;
using System.Text;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Pure parser tests — no DB, real fixtures (F1 = ListOfTrades_XAUUSD_H1_IST.csv, 329 rows;
/// F2 = ListOfTrades_XAUUSD_H1_OOST.csv, 337 rows). Covers SBI-1, SBI-2, SBI-3, SBI-5, SBI-6,
/// SBI-7, SBI-8, SBI-9.
/// </summary>
public class SqxTradeListParserTests
{
    private static readonly ISqxTradeListParser Sut = new SqxTradeListParserService();

    private const string F1Name = "ListOfTrades_XAUUSD_H1_IST.csv";

    /// <summary>The NEGATIVE fixture: two distinct <c>Sample type</c> values, so it must be refused whole.</summary>
    private const string F3Name = "ListOfTrades_XAUUSD_H1_OOST.csv";

    /// <summary>A walk-forward export — the wrong column shape for this parser.</summary>
    private const string WfName = "WFParamsExport_XAUUSD_H1.csv";

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static Stream OpenFixture(string name) => File.OpenRead(FixturePath(name));

    private const string Header =
        "\"Ticket\";\"Symbol\";\"Type\";\"Open time\";\"Open price\";\"Size\";\"Close time\";\"Close price\";" +
        "\"Profit/Loss\";\"Balance\";\"Sample type\";\"Close type\";\"MAE ($)\";\"MFE ($)\";\"Time in trade\";\"Comment\"";

    private static string Row(
        long ticket = 1, string symbol = "XAUUSD_M1_UTC02", string type = "Buy",
        string openTime = "2016.01.04 07:16:00", string openPrice = "1066.19", string size = "0,44000",
        string closeTime = "2016.01.04 15:25:00", string closePrice = "1077.86", string profit = "511,13",
        string balance = "100511,13", string sampleType = "IST", string closeType = "PT",
        string mae = "-27,37", string mfe = "513,48", string timeInTrade = "8h 9m", string comment = "")
        => $"\"{ticket}\";\"{symbol}\";\"{type}\";\"{openTime}\";\"{openPrice}\";\"{size}\";\"{closeTime}\";" +
           $"\"{closePrice}\";\"{profit}\";\"{balance}\";\"{sampleType}\";\"{closeType}\";\"{mae}\";\"{mfe}\";" +
           $"\"{timeInTrade}\";\"{comment}\"";

    private static Stream CsvStream(params string[] lines)
        => new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\r\n", lines)));

    // ---- 1.2: row counts ----

    [Fact]
    public async Task ParseAsync_F1Fixture_Parses329Rows()
    {
        await using var stream = OpenFixture(F1Name);

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeFalse();
        result.Trades.Should().HaveCount(329);
        result.RejectedRows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_F3Fixture_IsRejectedWholeNamingBothSampleTypes()
    {
        // F3 mixes IS (151 rows) and OOS1 (186). A Deploy or Evaluation run must be ONE coherent
        // sample, so the file is refused outright rather than silently importing a run whose trades
        // came from two different walk-forward phases. This is the fixture\'s only remaining job.
        await using var stream = OpenFixture(F3Name);

        var result = await Sut.ParseAsync(stream, F3Name, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("Sample type");
        result.RejectionReason.Should().Contain("IS");
        result.RejectionReason.Should().Contain("OOS1");
        result.Trades.Should().BeEmpty();
        result.RejectedRows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WalkForwardExportHeader_IsRejectedAsTheWrongColumnShape()
    {
        // The detectable half of D11: a WF export and a trade list are structurally different, so
        // dropping one into the other\'s slot is caught, unlike Deploy-vs-Evaluation which is not.
        await using var stream = OpenFixture(WfName);

        var result = await Sut.ParseAsync(stream, WfName, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("trade-list header");
        result.Trades.Should().BeEmpty();
    }

    // ---- 1.3: per-column decimal separators ----

    [Fact]
    public async Task ParseAsync_F1FirstRow_ParsesDotColumnsAndCommaColumnsCorrectly()
    {
        await using var stream = OpenFixture(F1Name);

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        var first = result.Trades.Single(t => t.Ticket == 5);
        first.OpenPrice.Should().Be(1066.19m);
        first.ClosePrice.Should().Be(1077.86m);
        first.Size.Should().Be(0.44000m);
        first.Profit.Should().Be(511.13m);
        first.Balance.Should().Be(100511.13m);
    }

    // ---- 1.4: separator mismatch FAILS the row, never reinterpreted ----

    [Fact]
    public async Task ParseAsync_CommaInDotColumn_RejectsRowNamingColumn()
    {
        await using var stream = CsvStream(Header, Row(openPrice: "1066,19"));

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeFalse("a bad row must not fail the whole file");
        result.Trades.Should().BeEmpty();
        result.RejectedRows.Should().ContainSingle();
        result.RejectedRows[0].Reason.Should().Contain("Open price");
    }

    // ---- 1.5: culture independence ----

    [Fact]
    public async Task ParseAsync_UnderDeDeCulture_ProducesIdenticalResult()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            await using var streamInvariant = OpenFixture(F1Name);
            var baseline = await Sut.ParseAsync(streamInvariant, F1Name, CancellationToken.None);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            await using var streamDeDe = OpenFixture(F1Name);
            var underDeDe = await Sut.ParseAsync(streamDeDe, F1Name, CancellationToken.None);

            underDeDe.Trades.Should().HaveCount(baseline.Trades.Count);
            var baselineFirst = baseline.Trades.Single(t => t.Ticket == 5);
            var deDeFirst = underDeDe.Trades.Single(t => t.Ticket == 5);
            deDeFirst.OpenPrice.Should().Be(baselineFirst.OpenPrice);
            deDeFirst.Size.Should().Be(baselineFirst.Size);
            deDeFirst.Profit.Should().Be(baselineFirst.Profit);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ---- 1.6: whole-file rejects ----

    [Fact]
    public async Task ParseAsync_WrongDelimiter_RejectsWholeFile()
    {
        var header = Header.Replace(';', ',');
        var row = Row().Replace(';', ',');
        await using var stream = CsvStream(header, row);

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("delimiter");
        result.Trades.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_MissingCloseTypeColumn_RejectsWholeFile()
    {
        const string headerMissingCloseType =
            "\"Ticket\";\"Symbol\";\"Type\";\"Open time\";\"Open price\";\"Size\";\"Close time\";\"Close price\";" +
            "\"Profit/Loss\";\"Balance\";\"Sample type\";\"MAE ($)\";\"MFE ($)\";\"Time in trade\";\"Comment\"";
        await using var stream = CsvStream(headerMissingCloseType, Row());

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("Close type");
        result.Trades.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_UnparseableDate_RejectsWholeFileNamingRowAndColumn()
    {
        await using var stream = CsvStream(Header, Row(openTime: "04/01/2016 07:16:00"));

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("Open time");
        result.RejectionReason.Should().Contain("0");
        result.Trades.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_MoreThanOneSymbol_RejectsWholeFile()
    {
        await using var stream = CsvStream(
            Header,
            Row(ticket: 1, symbol: "XAUUSD_M1_UTC02"),
            Row(ticket: 2, symbol: "EURUSD_M1_UTC02"));

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeTrue();
        result.RejectionReason.Should().Contain("Symbol");
        result.Trades.Should().BeEmpty();
    }

    // ---- 1.7: segments ----

    [Fact]
    public async Task ParseAsync_AllOutOfSampleFile_ClassifiesSegmentAndIndex()
    {
        // Segment classification survives the single-sample-type guard: the guard rejects a file
        // that MIXES phases, it does not stop a file that is wholly one of them from being
        // classified. Slice 3 filters on Segment/SegmentIndex, so both must still be derived.
        await using var stream = CsvStream(Header, Row(ticket: 1, sampleType: "OOS1"), Row(ticket: 2, sampleType: "OOS1"));

        var result = await Sut.ParseAsync(stream, "Oos.csv", CancellationToken.None);

        result.IsRejected.Should().BeFalse();
        result.Trades.Should().HaveCount(2);
        result.Trades.Should().OnlyContain(t => t.Segment == BacktestSegment.OutOfSample);
        result.Trades.Should().OnlyContain(t => t.SegmentIndex == 1);
        result.Trades.Should().OnlyContain(t => t.SampleTypeRaw == "OOS1");
    }

    [Fact]
    public async Task ParseAsync_AllInSampleFile_ClassifiesSegmentWithNoIndex()
    {
        await using var stream = CsvStream(Header, Row(ticket: 1, sampleType: "IS"), Row(ticket: 2, sampleType: "IS"));

        var result = await Sut.ParseAsync(stream, "Is.csv", CancellationToken.None);

        result.IsRejected.Should().BeFalse();
        result.Trades.Should().HaveCount(2);
        result.Trades.Should().OnlyContain(t => t.Segment == BacktestSegment.InSample);
        result.Trades.Should().OnlyContain(t => t.SegmentIndex == null);
        result.Trades.Should().OnlyContain(t => t.SampleTypeRaw == "IS");
    }

    [Fact]
    public async Task ParseAsync_F1Fixture_AllRowsAreInSampleTest()
    {
        await using var stream = OpenFixture(F1Name);

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.Trades.Should().HaveCount(329);
        result.Trades.Should().OnlyContain(t => t.Segment == BacktestSegment.InSampleTest);
        result.Trades.Should().OnlyContain(t => t.SampleTypeRaw == "IST");
    }

    [Fact]
    public async Task ParseAsync_UnknownSampleTypeLabel_MapsToUnknownButFileSurvives()
    {
        await using var stream = CsvStream(Header, Row(sampleType: "OOS-WEIRD"));

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeFalse();
        result.Trades.Should().ContainSingle();
        result.Trades[0].Segment.Should().Be(BacktestSegment.Unknown);
        result.Trades[0].SampleTypeRaw.Should().Be("OOS-WEIRD");
    }

    // ---- 1.8: close type verbatim ----

    [Fact]
    public async Task ParseAsync_F1Fixture_CloseTypeIsVerbatimForAllFiveObservedValues()
    {
        await using var f1 = OpenFixture(F1Name);
        var r1 = await Sut.ParseAsync(f1, F1Name, CancellationToken.None);

        var observed = r1.Trades.Select(t => t.CloseType).Distinct().ToList();

        observed.Should().Contain(["SL", "PT", "TrailingStop", "End Of Friday", "End Of Friday (Time)"]);
    }

    // ---- 1.9: RealizedRisk / StopLoss ----

    [Fact]
    public async Task ParseAsync_SlClosedRow_RealizedRiskIsAbsoluteMae()
    {
        await using var stream = CsvStream(Header, Row(closeType: "SL", mae: "-152.77".Replace('.', ',')));

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.Trades.Should().ContainSingle();
        result.Trades[0].RealizedRisk.Should().Be(152.77m);
    }

    [Fact]
    public async Task ParseAsync_F1Fixture_StopLossIsNullForEveryRow()
    {
        await using var f1 = OpenFixture(F1Name);
        var r1 = await Sut.ParseAsync(f1, F1Name, CancellationToken.None);

        r1.Trades.Should().HaveCount(329);
        r1.Trades.Should().OnlyContain(t => t.StopLoss == null);
    }

    [Fact]
    public async Task ParseAsync_NonSlClosedRow_RealizedRiskIsNull()
    {
        await using var stream = CsvStream(Header, Row(closeType: "TrailingStop"));

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.Trades.Should().ContainSingle();
        result.Trades[0].RealizedRisk.Should().BeNull();
    }

    // ---- 1.10: degenerate row rejected at row level ----

    [Fact]
    public async Task ParseAsync_DegenerateRowAmongValidOnes_RejectsOnlyThatRow()
    {
        var lines = new List<string> { Header };
        for (var i = 0; i < 100; i++)
            lines.Add(Row(ticket: i + 1));
        lines.Add(Row(ticket: 999, openPrice: "1000.00", closePrice: "1000.00"));

        await using var stream = CsvStream(lines.ToArray());

        var result = await Sut.ParseAsync(stream, F1Name, CancellationToken.None);

        result.IsRejected.Should().BeFalse();
        result.Trades.Should().HaveCount(100);
        result.RejectedRows.Should().ContainSingle();
        result.RejectedRows[0].Reason.Should().Contain("degenerate");
    }

    // ---- File name is display data only: no convention is parsed out of it any more ----

    [Fact]
    public async Task ParseAsync_PathTraversalInFileName_UsesBareFileNameOnly()
    {
        await using var stream = CsvStream(Header, Row());

        var result = await Sut.ParseAsync(stream, "..\\..\\evil\\My Strategy.csv", CancellationToken.None);

        result.FileName.Should().Be("My Strategy.csv");
    }

    // ---- Field length is a PARSER guard, not a database guard ----
    // Text fields were previously copied verbatim into length-bounded columns. An over-length
    // value therefore failed at SaveChanges ("String or binary data would be truncated"), which is
    // NOT transient — it defeats the retry strategy and, before the per-file boundary existed,
    // killed the whole batch. Length is now the same class of defect as a bad separator or a
    // degenerate row: a named rejection carrying the offending column and its limit.

    [Fact]
    public async Task ParseAsync_CommentLongerThanTheColumnLimit_RejectsThatRowNamingTheColumn()
    {
        var overLength = new string('c', BacktestFieldLengths.Comment + 1);
        await using var stream = CsvStream(Header, Row(ticket: 1), Row(ticket: 2, comment: overLength), Row(ticket: 3));

        var result = await Sut.ParseAsync(stream, "Lengths.csv", CancellationToken.None);

        result.IsRejected.Should().BeFalse("one over-length row must not fail the whole file");
        result.Trades.Should().HaveCount(2);
        var rejected = result.RejectedRows.Should().ContainSingle().Subject;
        rejected.RowIndex.Should().Be(1);
        rejected.Reason.Should().Contain("Comment").And.Contain(BacktestFieldLengths.Comment.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ParseAsync_SymbolLongerThanTheColumnLimit_RejectsTheRowNotTheFile()
    {
        var overLength = new string('S', BacktestFieldLengths.Symbol + 1);
        await using var stream = CsvStream(Header, Row(ticket: 1), Row(ticket: 2, symbol: overLength));

        var result = await Sut.ParseAsync(stream, "Lengths.csv", CancellationToken.None);

        result.IsRejected.Should().BeFalse(
            "an over-length Symbol must be rejected as a ROW before it can masquerade as a second symbol and fail the file");
        result.Symbol.Should().Be("XAUUSD_M1_UTC02");
        result.Trades.Should().ContainSingle();
        result.RejectedRows.Should().ContainSingle().Subject.Reason.Should().Contain("Symbol");
    }

    [Theory]
    [InlineData("Sample type", "SampleType")]
    [InlineData("Close type", "CloseType")]
    [InlineData("Type", "Type")]
    public async Task ParseAsync_TextColumnLongerThanItsLimit_RejectsThatRow(string columnName, string field)
    {
        var length = field switch
        {
            "SampleType" => BacktestFieldLengths.SampleTypeRaw,
            "CloseType" => BacktestFieldLengths.CloseType,
            _ => BacktestFieldLengths.TradeType,
        };
        var overLength = new string('x', length + 1);
        var badRow = field switch
        {
            "SampleType" => Row(ticket: 2, sampleType: overLength),
            "CloseType" => Row(ticket: 2, closeType: overLength),
            _ => Row(ticket: 2, type: overLength),
        };
        await using var stream = CsvStream(Header, Row(ticket: 1), badRow);

        var result = await Sut.ParseAsync(stream, "Lengths.csv", CancellationToken.None);

        result.IsRejected.Should().BeFalse();
        result.Trades.Should().ContainSingle();
        result.RejectedRows.Should().ContainSingle().Subject.Reason.Should().Contain(columnName);
    }

    [Fact]
    public async Task ParseAsync_FileNameLongerThanTheColumnLimit_RejectsTheWholeFile()
    {
        var overLengthName = new string('n', BacktestFieldLengths.FileNameOrKey) + ".csv";
        await using var stream = CsvStream(Header, Row());

        var result = await Sut.ParseAsync(stream, overLengthName, CancellationToken.None);

        result.IsRejected.Should().BeTrue("the file name is persisted into a length-bounded column");
        result.RejectionReason.Should().Contain(BacktestFieldLengths.FileNameOrKey.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ParseAsync_TextFieldsExactlyAtTheirLimit_AreAccepted()
    {
        await using var stream = CsvStream(Header, Row(comment: new string('c', BacktestFieldLengths.Comment)));

        var result = await Sut.ParseAsync(stream, "Lengths.csv", CancellationToken.None);

        result.IsRejected.Should().BeFalse();
        result.RejectedRows.Should().BeEmpty("the limit is inclusive — a value exactly at the column width still fits");
        result.Trades.Should().ContainSingle();
    }
}
