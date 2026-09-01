using System.Globalization;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Parses SQX Optimizer "Walk-Forward Results" exports. Pure — zero EF/DbContext references.
/// <para>
/// This service shares NOTHING with <see cref="SqxTradeListParserService"/>: not a policy table,
/// not a date format, not a culture object. The two files invert each other. Every numeric column
/// here uses a COMMA decimal where the trade list uses DOTS for prices, and the periods are
/// <c>dd.MM.yyyy</c> where the trade list is <c>yyyy.MM.dd HH:mm:ss</c>. Measured, not assumed: a
/// first cut that reused the trade-list conventions read <c>"15239,94"</c> as <c>1523994</c> — a
/// profit inflated a hundredfold — and produced <c>DateTime.MinValue</c> for every period.
/// </para>
/// <para>
/// Every guard is FILE-level. A trade list can drop one malformed row and keep the rest, because
/// rows there are independent observations. A window is not: the row ORDER carries the meaning
/// (<c>OosFromDate</c> is the second-to-last row's OOS start), so silently dropping a row would
/// move the boundary without saying so.
/// </para>
/// </summary>
public sealed class WalkForwardExportParserService : IWalkForwardExportParser
{
    private const char Delimiter = ';';
    private const string DateFormat = "dd.MM.yyyy";
    private const string PeriodSeparator = " - ";
    private const string FutureSuffix = " (future)";
    private const string NotAvailable = "N/A";

    /// <summary>An export with one window cannot produce a boundary — that is the second-to-last row's OOS start.</summary>
    private const int MinimumWindows = 2;

    private static readonly string[] ExpectedColumns =
    [
        "Period IS", "Period OOS", "Days IS", "Days OOS", "Net profit (IS)", "Net profit (OOS)",
        "Ret/DD Ratio (IS)", "Ret/DD Ratio (OOS)", "Drawdown (IS)", "Drawdown (OOS)",
        "Avg. Trades Per Month (IS)", "Avg. Trades Per Month (OOS)", "Parameters",
    ];

    private const int ColPeriodIs = 0;
    private const int ColPeriodOos = 1;
    private const int ColDaysIs = 2;
    private const int ColDaysOos = 3;
    private const int ColNetProfitIs = 4;
    private const int ColNetProfitOos = 5;
    private const int ColRetDdIs = 6;
    private const int ColRetDdOos = 7;
    private const int ColDrawdownIs = 8;
    private const int ColDrawdownOos = 9;
    private const int ColAvgTradesIs = 10;
    private const int ColAvgTradesOos = 11;
    private const int ColParameters = 12;

    /// <summary>
    /// THIS parser's own comma-decimal format. A separate instance from the trade-list parser's on
    /// purpose — the two must never be able to drift into sharing one.
    /// </summary>
    private static readonly NumberFormatInfo CommaDecimal = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = string.Empty,
    };

    /// <summary>
    /// THE column-to-separator policy for this file. <c>Parameters</c> is deliberately ABSENT:
    /// inside that field the punctuation roles invert, so applying this table to it would destroy
    /// it. See <see cref="SplitParameters"/>.
    /// </summary>
    private static readonly (int Index, string Name)[] CommaDecimalColumns =
    [
        (ColNetProfitIs, "Net profit (IS)"),
        (ColNetProfitOos, "Net profit (OOS)"),
        (ColRetDdIs, "Ret/DD Ratio (IS)"),
        (ColRetDdOos, "Ret/DD Ratio (OOS)"),
        (ColDrawdownIs, "Drawdown (IS)"),
        (ColDrawdownOos, "Drawdown (OOS)"),
        (ColAvgTradesIs, "Avg. Trades Per Month (IS)"),
        (ColAvgTradesOos, "Avg. Trades Per Month (OOS)"),
    ];

    /// <summary>
    /// The four columns SQX leaves as the literal <c>N/A</c> on the window whose out-of-sample
    /// period has not elapsed. <c>Days OOS</c> is NOT one of them — it carries the planned span —
    /// and neither is any IS column, which is what makes "exactly these four are N/A" a usable
    /// second signal rather than a guess.
    /// </summary>
    private static readonly int[] OosValueColumns = [ColNetProfitOos, ColRetDdOos, ColDrawdownOos, ColAvgTradesOos];

    public Task<ParsedWalkForwardExportDto> ParseAsync(Stream csv, string fileName, CancellationToken ct)
    {
        var sanitizedFileName = Path.GetFileName(fileName);

        using var reader = new StreamReader(csv);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
            return Task.FromResult(Rejected(sanitizedFileName, "empty file"));

        var headerFields = SplitLine(headerLine);
        if (headerFields.Length <= 1)
            return Task.FromResult(Rejected(sanitizedFileName, "invalid delimiter"));

        // FILE-SHAPE guard before the per-column check, mirroring the trade-list parser: a document
        // of the wrong KIND must be named as such, not reported as this one missing its first
        // column. See SqxTradeListParserService for the same rule from the other side.
        var recognisedColumns = headerFields.Intersect(ExpectedColumns, StringComparer.Ordinal).Count();
        if (recognisedColumns * 2 < ExpectedColumns.Length)
        {
            return Task.FromResult(Rejected(
                sanitizedFileName,
                $"expected walk-forward-export header, found a different column shape: {string.Join("; ", headerFields)}"));
        }

        for (var i = 0; i < ExpectedColumns.Length; i++)
        {
            if (i >= headerFields.Length || headerFields[i] != ExpectedColumns[i])
                return Task.FromResult(Rejected(sanitizedFileName, $"missing column: {ExpectedColumns[i]}"));
        }

        var rows = new List<string[]>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (line.Length == 0)
                continue;

            var fields = SplitLine(line);
            if (fields.Length < ExpectedColumns.Length)
            {
                return Task.FromResult(Rejected(
                    sanitizedFileName, $"row {rows.Count} has fewer columns than expected"));
            }

            rows.Add(fields);
        }

        if (rows.Count < MinimumWindows)
        {
            return Task.FromResult(Rejected(
                sanitizedFileName,
                $"at least 2 windows required, found {rows.Count} — the out-of-sample boundary is the second-to-last window's start"));
        }

        var windows = new List<ParsedWalkForwardWindowDto>(rows.Count);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (!TryParseRow(rows[rowIndex], rowIndex, rowIndex == rows.Count - 1, out var window, out var reason))
                return Task.FromResult(Rejected(sanitizedFileName, reason!));

            windows.Add(window!);
        }

        return Task.FromResult(new ParsedWalkForwardExportDto(sanitizedFileName, false, null, windows));
    }

    private static bool TryParseRow(
        string[] fields, int rowIndex, bool isLastRow, out ParsedWalkForwardWindowDto? window, out string? rejectionReason)
    {
        window = null;
        rejectionReason = null;

        var periodOosRaw = fields[ColPeriodOos];
        var hasFutureSuffix = periodOosRaw.EndsWith(FutureSuffix, StringComparison.Ordinal);
        var naCount = OosValueColumns.Count(c => fields[c] == NotAvailable);

        // TWO INDEPENDENT SIGNALS, and they must agree. Either one alone could be a format change
        // rather than a future window, and guessing which would put a fabricated number — or a
        // fabricated date — into the evidence the whole evaluation rests on.
        var isFutureWindow = hasFutureSuffix && naCount == OosValueColumns.Length;
        if (!isFutureWindow && (hasFutureSuffix || naCount > 0))
        {
            rejectionReason =
                $"future-window signal mismatch at row {rowIndex}: the ' (future)' suffix is "
                + $"{(hasFutureSuffix ? "present" : "absent")} while {naCount} of {OosValueColumns.Length} "
                + "out-of-sample columns are 'N/A' — both signals must agree or neither";
            return false;
        }

        if (isFutureWindow && !isLastRow)
        {
            rejectionReason =
                $"'N/A' out-of-sample values at row {rowIndex}: only the last window may be un-elapsed";
            return false;
        }

        if (!TryParsePeriod(fields[ColPeriodIs], out var isStart, out var isEnd))
        {
            rejectionReason = $"invalid date at row {rowIndex}, column 'Period IS': expected '{DateFormat}{PeriodSeparator}{DateFormat}'";
            return false;
        }

        if (!TryParsePeriod(StripFutureSuffix(periodOosRaw), out var oosStart, out var oosEnd))
        {
            rejectionReason = $"invalid date at row {rowIndex}, column 'Period OOS': expected '{DateFormat}{PeriodSeparator}{DateFormat}'";
            return false;
        }

        if (!TryParseInt(fields[ColDaysIs], out var daysIs))
        {
            rejectionReason = $"invalid integer at row {rowIndex}, column 'Days IS'";
            return false;
        }

        // Days OOS stays populated on the future window — it is the PLANNED span, not a measurement.
        if (!TryParseInt(fields[ColDaysOos], out var daysOos))
        {
            rejectionReason = $"invalid integer at row {rowIndex}, column 'Days OOS'";
            return false;
        }

        var values = new Dictionary<int, decimal?>(CommaDecimalColumns.Length);
        foreach (var (index, name) in CommaDecimalColumns)
        {
            var raw = fields[index];
            if (raw == NotAvailable)
            {
                // NULL, never 0. Zero would make the window that has not happened yet look like the
                // worst evidence in the file — the fixture's true minimum out-of-sample Ret/DD is
                // 0.52, and a zero would silently replace it.
                values[index] = null;
                continue;
            }

            if (!TryParseCommaDecimal(raw, out var value))
            {
                rejectionReason = $"invalid decimal at row {rowIndex}, column '{name}': expected a comma decimal separator";
                return false;
            }

            values[index] = value;
        }

        if (values[ColNetProfitIs] is not { } netProfitIs
            || values[ColRetDdIs] is not { } retDdIs
            || values[ColDrawdownIs] is not { } drawdownIs
            || values[ColAvgTradesIs] is not { } avgTradesIs)
        {
            rejectionReason = $"row {rowIndex} has 'N/A' in an in-sample column — the in-sample half of every window has always elapsed";
            return false;
        }

        window = new ParsedWalkForwardWindowDto(
            RowIndex: rowIndex,
            PeriodIsStart: isStart,
            PeriodIsEnd: isEnd,
            PeriodOosStart: oosStart,
            PeriodOosEnd: oosEnd,
            DaysIs: daysIs,
            DaysOos: daysOos,
            NetProfitIs: netProfitIs,
            RetDdRatioIs: retDdIs,
            DrawdownIs: drawdownIs,
            AvgTradesPerMonthIs: avgTradesIs,
            NetProfitOos: values[ColNetProfitOos],
            RetDdRatioOos: values[ColRetDdOos],
            DrawdownOos: values[ColDrawdownOos],
            AvgTradesPerMonthOos: values[ColAvgTradesOos],
            Parameters: fields[ColParameters],
            IsFutureWindow: isFutureWindow);

        return true;
    }

    /// <summary>
    /// THE inversion rule, and the only place allowed to read inside the <c>Parameters</c> field.
    /// Within it a comma SEPARATES pairs and a dot is the DECIMAL point — the exact opposite of
    /// every other column in this file. Applying the file's own comma-decimal rule here would turn
    /// <c>ProfitTargetCoef1=5.4</c> into <c>ProfitTargetCoef1=5</c> plus a stray <c>4</c>.
    /// <para>
    /// SQX emits a trailing comma, so the last token is empty. That is normal output, not a
    /// malformed file: the empty token is dropped and the row stands.
    /// </para>
    /// <para>
    /// Slice 1 persists the raw text and never needs the pairs — the text is the manual audit trail
    /// against a run's declared kind. This method is the encoded contract for that text, exercised
    /// by <c>WalkForwardExportParserTests</c> and consumed by the later slice that compares
    /// parameter sets.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> SplitParameters(string parameters)
    {
        var pairs = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var token in parameters.Split(','))
        {
            if (token.Length == 0)
                continue;

            var separator = token.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
                continue;

            var key = token[..separator];
            var raw = token[(separator + 1)..];

            // DOT decimal, invariant — the inversion, stated once.
            if (decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                pairs[key] = value;
        }

        return pairs;
    }

    private static string StripFutureSuffix(string periodOos)
        => periodOos.EndsWith(FutureSuffix, StringComparison.Ordinal)
            ? periodOos[..^FutureSuffix.Length]
            : periodOos;

    private static bool TryParsePeriod(string raw, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;

        var separator = raw.IndexOf(PeriodSeparator, StringComparison.Ordinal);
        if (separator < 0)
            return false;

        return TryParseDate(raw[..separator], out start)
            && TryParseDate(raw[(separator + PeriodSeparator.Length)..], out end);
    }

    private static bool TryParseDate(string raw, out DateTime value)
        => DateTime.TryParseExact(raw.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    private static bool TryParseInt(string raw, out int value)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseCommaDecimal(string raw, out decimal value)
    {
        value = 0m;

        // A dot here means the file changed convention. Reinterpreting it silently is how a
        // hundredfold error gets persisted as evidence.
        if (raw.Contains('.', StringComparison.Ordinal))
            return false;

        return decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CommaDecimal, out value);
    }

    private static ParsedWalkForwardExportDto Rejected(string fileName, string reason)
        => new(fileName, true, reason, []);

    private static string[] SplitLine(string line)
    {
        var rawFields = line.Split(Delimiter);
        var fields = new string[rawFields.Length];
        for (var i = 0; i < rawFields.Length; i++)
        {
            var f = rawFields[i].Trim();
            if (f.Length >= 2 && f[0] == '"' && f[^1] == '"')
                f = f[1..^1];
            fields[i] = f;
        }

        return fields;
    }
}
