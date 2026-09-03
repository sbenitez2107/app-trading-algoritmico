using System.Globalization;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Task 1.8 — loads a committed trade-list CSV straight into <see cref="BacktestTrade"/> rows,
/// deliberately BYPASSING <c>SqxTradeListParserService</c>.
/// <para>
/// This is not a convenience. <c>ListOfTrades_XAUUSD_H1_OOST.csv</c> carries two <c>Sample type</c>
/// values (<c>IS</c> 151 and <c>OOS1</c> 186), so the importer's file-level single-sample-type guard
/// rejects it WHOLESALE — it is a NEGATIVE fixture for import, and routing it through the parser
/// yields zero trades, not 337. The calculators under test take already-persisted trades rather than
/// a CSV, so the 1-decimal population is legitimately reachable this way and only this way.
/// </para>
/// <para>
/// It intentionally applies NO parser policy beyond the column separators: no degenerate-row
/// rejection, no length guards, no single-symbol guard. Its job is to reproduce the file's rows, so
/// that a count asserted in a test is the count in the file.
/// </para>
/// </summary>
internal static class RawTradeListFixture
{
    internal const string IstFileName = "ListOfTrades_XAUUSD_H1_IST.csv";
    internal const string OostFileName = "ListOfTrades_XAUUSD_H1_OOST.csv";

    private const char Delimiter = ';';
    private const string DateFormat = "yyyy.MM.dd HH:mm:ss";

    private const int ColTicket = 0;
    private const int ColSymbol = 1;
    private const int ColType = 2;
    private const int ColOpenTime = 3;
    private const int ColOpenPrice = 4;
    private const int ColSize = 5;
    private const int ColCloseTime = 6;
    private const int ColClosePrice = 7;
    private const int ColProfit = 8;
    private const int ColBalance = 9;
    private const int ColSampleType = 10;
    private const int ColCloseType = 11;
    private const int ColMae = 12;
    private const int ColComment = 15;

    /// <summary>Comma is the decimal separator for Size/Profit/Balance/MAE; dot for the two prices.</summary>
    private static readonly NumberFormatInfo CommaDecimal = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = string.Empty,
    };

    /// <summary>
    /// One file row: the entity the calculators consume, plus the raw <c>MAE ($)</c> column.
    /// <para>
    /// The raw MAE is carried SEPARATELY because <see cref="BacktestTrade.RealizedRisk"/> is
    /// deliberately SL-only and never defaulted — a trailing stop's MAE is not the risk the trade
    /// was sized on, so the entity must not hold it. Some of D5's supporting evidence is
    /// nevertheless a statement about that column on NON-SL rows, so the test needs it without the
    /// entity gaining it.
    /// </para>
    /// </summary>
    internal readonly record struct RawRow(BacktestTrade Trade, decimal RawMae);

    internal static List<BacktestTrade> Load(string fileName, Guid? runId = null)
        => LoadRows(fileName, runId).Select(r => r.Trade).ToList();

    internal static List<RawRow> LoadRows(string fileName, Guid? runId = null)
    {
        var backtestRunId = runId ?? Guid.NewGuid();
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var trades = new List<RawRow>();

        using var reader = new StreamReader(path);
        reader.ReadLine(); // header — positions are fixed, never sniffed

        var rowIndex = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                rowIndex++;
                continue;
            }

            var fields = Split(line);
            var closeType = fields[ColCloseType];
            var mae = Dec(fields[ColMae], commaDecimal: true);
            var sampleTypeRaw = fields[ColSampleType];
            var comment = fields.Length > ColComment ? fields[ColComment] : string.Empty;

            trades.Add(new RawRow(new BacktestTrade
            {
                Id = Guid.NewGuid(),
                BacktestRunId = backtestRunId,
                RowIndex = rowIndex,
                Ticket = long.Parse(fields[ColTicket], CultureInfo.InvariantCulture),
                Symbol = fields[ColSymbol],
                Type = fields[ColType],
                OpenTime = Date(fields[ColOpenTime]),
                OpenPrice = Dec(fields[ColOpenPrice], commaDecimal: false),
                Size = Dec(fields[ColSize], commaDecimal: true),
                CloseTime = Date(fields[ColCloseTime]),
                ClosePrice = Dec(fields[ColClosePrice], commaDecimal: false),
                Profit = Dec(fields[ColProfit], commaDecimal: true),
                Balance = Dec(fields[ColBalance], commaDecimal: true),
                SampleTypeRaw = sampleTypeRaw,
                Segment = ClassifySegment(sampleTypeRaw).Segment,
                SegmentIndex = ClassifySegment(sampleTypeRaw).Index,
                CloseType = closeType,

                // Same rule the shipped parser applies: |MAE| for SL closes, null otherwise.
                RealizedRisk = closeType == "SL" ? Math.Abs(mae) : null,
                StopLoss = null,
                Comment = string.IsNullOrEmpty(comment) ? null : comment,
                CreatedAt = DateTime.UtcNow,
            }, Math.Abs(mae)));

            rowIndex++;
        }

        return trades;
    }

    private static string[] Split(string line)
    {
        var raw = line.Split(Delimiter);
        var fields = new string[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var f = raw[i].Trim();
            if (f.Length >= 2 && f[0] == '"' && f[^1] == '"')
                f = f[1..^1];
            fields[i] = f;
        }

        return fields;
    }

    private static DateTime Date(string raw)
        => DateTime.ParseExact(raw, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static decimal Dec(string raw, bool commaDecimal)
        => decimal.Parse(
            raw,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            commaDecimal ? CommaDecimal : CultureInfo.InvariantCulture);

    private static (BacktestSegment Segment, int? Index) ClassifySegment(string raw)
    {
        if (raw == "IST")
            return (BacktestSegment.InSampleTest, null);

        if (raw == "IS")
            return (BacktestSegment.InSample, null);

        if (raw.StartsWith("OOS", StringComparison.Ordinal)
            && int.TryParse(raw.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return (BacktestSegment.OutOfSample, index);
        }

        return (BacktestSegment.Unknown, null);
    }
}
