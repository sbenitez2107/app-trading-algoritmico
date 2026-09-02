using System.Globalization;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Constants;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Parses SQX/AlgoWizard trade-list CSV exports. Pure — zero EF/DbContext references, so it is
/// unit-tested directly against fixtures with no database. See design.md "Parser Contract" for
/// the load-bearing gotcha: decimal separators are fixed PER COLUMN, never sniffed. A value
/// arriving in the wrong format is a rejected row naming the column, never silently reinterpreted.
/// </summary>
public sealed class SqxTradeListParserService : ISqxTradeListParser
{
    private const char Delimiter = ';';
    private const string DateFormat = "yyyy.MM.dd HH:mm:ss";

    // Column policy — fixed positions, fixed separators. Never sniffed at runtime.
    private static readonly string[] ExpectedColumns =
    [
        "Ticket", "Symbol", "Type", "Open time", "Open price", "Size", "Close time", "Close price",
        "Profit/Loss", "Balance", "Sample type", "Close type", "MAE ($)", "MFE ($)", "Time in trade", "Comment",
    ];

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

    private static readonly NumberFormatInfo CommaDecimal = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = string.Empty,
    };

    /// <summary>
    /// THE single column→separator policy table (task 1.13 refactor target). Fixed per column,
    /// never sniffed at runtime — see design.md "Parser Contract". Every decimal-bearing column
    /// parsed by <see cref="ParseAllDecimals"/> is declared here exactly once.
    /// </summary>
    private static readonly (int Index, string Name, bool UseCommaDecimal)[] DecimalColumns =
    [
        (ColOpenPrice, "Open price", false),
        (ColClosePrice, "Close price", false),
        (ColSize, "Size", true),
        (ColProfit, "Profit/Loss", true),
        (ColBalance, "Balance", true),
        (ColMae, "MAE ($)", true),
    ];

    /// <summary>
    /// THE single column→length policy table. Text arrives from a CSV and is persisted into
    /// length-bounded columns, so the limit is a PARSING rule, not a database one — the widths are
    /// shared with the EF configurations via <see cref="BacktestFieldLengths"/> precisely so the
    /// two cannot drift. Without this guard an over-length field reached <c>SaveChanges</c> as a
    /// "String or binary data would be truncated" error, which is not transient and therefore
    /// defeats the retry strategy entirely.
    /// </summary>
    private static readonly (int Index, string Name, int MaxLength)[] TextColumns =
    [
        (ColSymbol, "Symbol", BacktestFieldLengths.Symbol),
        (ColType, "Type", BacktestFieldLengths.TradeType),
        (ColSampleType, "Sample type", BacktestFieldLengths.SampleTypeRaw),
        (ColCloseType, "Close type", BacktestFieldLengths.CloseType),
        (ColComment, "Comment", BacktestFieldLengths.Comment),
    ];

    public Task<ParsedBacktestFileDto> ParseAsync(Stream csv, string fileName, CancellationToken ct)
    {
        // Path traversal is stripped here and not re-derived anywhere: the run's attribution comes
        // from the import route, so the file name is stored for display only.
        var sanitizedFileName = Path.GetFileName(fileName);

        // FILE-LEVEL length guard on the one column the name feeds.
        if (sanitizedFileName.Length > BacktestFieldLengths.FileNameOrKey)
        {
            return Task.FromResult(Rejected(
                sanitizedFileName,
                $"file name is {sanitizedFileName.Length} characters, exceeding the {BacktestFieldLengths.FileNameOrKey}-character limit"));
        }

        using var reader = new StreamReader(csv);
        var headerLine = reader.ReadLine();

        if (headerLine is null)
        {
            return Task.FromResult(Rejected(sanitizedFileName, "empty file"));
        }

        var headerFields = SplitLine(headerLine);
        if (headerFields.Length <= 1)
        {
            return Task.FromResult(Rejected(sanitizedFileName, "invalid delimiter"));
        }

        // FILE-SHAPE guard, before the per-column check. The two files this system accepts are
        // structurally different documents, and the user declares which is which by choosing a
        // slot — so a walk-forward export dropped into a trade-list slot must be named as the
        // wrong KIND of file, not reported as a trade list that happens to be missing its first
        // column. The distinction is drawn by how much of the expected header survives: a genuine
        // trade list with one column dropped still matches almost all of them, while a different
        // document matches almost none.
        var recognisedColumns = headerFields.Intersect(ExpectedColumns, StringComparer.Ordinal).Count();
        if (recognisedColumns * 2 < ExpectedColumns.Length)
        {
            return Task.FromResult(Rejected(
                sanitizedFileName,
                $"expected trade-list header, found a different column shape: {string.Join("; ", headerFields)}"));
        }

        for (var i = 0; i < ExpectedColumns.Length; i++)
        {
            if (i >= headerFields.Length || headerFields[i] != ExpectedColumns[i])
            {
                return Task.FromResult(Rejected(
                    sanitizedFileName, $"missing column: {ExpectedColumns[i]}"));
            }
        }

        var trades = new List<ParsedBacktestTradeDto>();
        var rejectedRows = new List<RejectedBacktestRowDto>();
        var distinctSymbols = new HashSet<string>(StringComparer.Ordinal);
        var distinctSampleTypes = new HashSet<string>(StringComparer.Ordinal);

        var rowIndex = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (line.Length == 0)
            {
                rowIndex++;
                continue;
            }

            var fields = SplitLine(line);
            if (fields.Length < ExpectedColumns.Length)
            {
                rejectedRows.Add(new RejectedBacktestRowDto(rowIndex, "row has fewer columns than expected"));
                rowIndex++;
                continue;
            }

            // ROW-LEVEL length guard, BEFORE the symbol is registered: an over-length Symbol must
            // not first be counted as a second distinct symbol and fail the whole file over a row
            // that is already being rejected.
            if (!TryValidateLengths(fields, out var lengthRejectionReason))
            {
                rejectedRows.Add(new RejectedBacktestRowDto(rowIndex, lengthRejectionReason!));
                rowIndex++;
                continue;
            }

            var symbol = fields[ColSymbol];
            distinctSymbols.Add(symbol);

            // Dates are a FILE-LEVEL guard (design: "unparseable date rejects the whole file").
            if (!TryParseDate(fields[ColOpenTime], out var openTime))
            {
                return Task.FromResult(Rejected(
                    sanitizedFileName,
                    $"invalid date at row {rowIndex}, column 'Open time'"));
            }

            if (!TryParseDate(fields[ColCloseTime], out var closeTime))
            {
                return Task.FromResult(Rejected(
                    sanitizedFileName,
                    $"invalid date at row {rowIndex}, column 'Close time'"));
            }

            // Decimal parsing is a ROW-LEVEL guard, driven by the single DecimalColumns table —
            // a mismatched separator rejects only this row, never the whole file.
            if (!ParseAllDecimals(fields, out var decimals, out var decimalRejectionReason))
            {
                rejectedRows.Add(new RejectedBacktestRowDto(rowIndex, decimalRejectionReason!));
                rowIndex++;
                continue;
            }

            var openPrice = decimals[ColOpenPrice];
            var closePrice = decimals[ColClosePrice];
            var size = decimals[ColSize];
            var profit = decimals[ColProfit];
            var balance = decimals[ColBalance];
            var mae = decimals[ColMae];

            // Degenerate row — ROW-LEVEL rejection, never a division-by-zero exception downstream.
            if (closePrice == openPrice)
            {
                rejectedRows.Add(new RejectedBacktestRowDto(rowIndex, "degenerate row: ClosePrice equals OpenPrice"));
                rowIndex++;
                continue;
            }

            if (!long.TryParse(fields[ColTicket], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticket))
            {
                rejectedRows.Add(new RejectedBacktestRowDto(rowIndex, "invalid ticket"));
                rowIndex++;
                continue;
            }

            var closeType = fields[ColCloseType];
            var sampleTypeRaw = fields[ColSampleType];
            distinctSampleTypes.Add(sampleTypeRaw);
            var (segment, segmentIndex) = ClassifySegment(sampleTypeRaw);

            var realizedRisk = closeType == "SL" ? Math.Abs(mae) : (decimal?)null;

            var comment = fields.Length > ColComment ? fields[ColComment] : string.Empty;

            trades.Add(new ParsedBacktestTradeDto(
                RowIndex: rowIndex,
                Ticket: ticket,
                Symbol: symbol,
                Type: fields[ColType],
                OpenTime: openTime,
                OpenPrice: openPrice,
                Size: size,
                CloseTime: closeTime,
                ClosePrice: closePrice,
                Profit: profit,
                Balance: balance,
                SampleTypeRaw: sampleTypeRaw,
                Segment: segment,
                SegmentIndex: segmentIndex,
                CloseType: closeType,
                RealizedRisk: realizedRisk,
                StopLoss: null,
                Comment: string.IsNullOrEmpty(comment) ? null : comment));

            rowIndex++;
        }

        if (distinctSymbols.Count > 1)
        {
            return Task.FromResult(Rejected(
                sanitizedFileName,
                $"multiple symbols in one file (Symbol column): {string.Join(", ", distinctSymbols.OrderBy(s => s))}"));
        }

        // FILE-LEVEL single-sample-type guard, same shape and same justification as the
        // single-symbol guard above: a Deploy or Evaluation run answers a question about ONE
        // coherent sample, and a file carrying both in-sample and out-of-sample rows cannot answer
        // it for either. Rejection is structural — it observes two distinct literals in a column,
        // it does not infer what the user meant. Classification of the individual labels is
        // untouched, so a file that is wholly IS, wholly OOSn or wholly IST still imports.
        if (distinctSampleTypes.Count > 1)
        {
            return Task.FromResult(Rejected(
                sanitizedFileName,
                $"multiple values in the 'Sample type' column: {string.Join(", ", distinctSampleTypes.OrderBy(v => v, StringComparer.Ordinal))}"));
        }

        // FILE-LEVEL zero-usable-row guard, LAST so the two guards above still give the more
        // specific diagnosis when they apply. An accepted zero-trade file is not a harmless empty
        // import: it REPLACES whatever occupied the slot with nothing while reporting success, and
        // the run it leaves behind holds no trades at all — which every downstream reader, the
        // readiness marker included, would otherwise treat as evidence that exists.
        if (trades.Count == 0)
        {
            return Task.FromResult(Rejected(
                sanitizedFileName,
                rejectedRows.Count == 0
                    ? "no trade rows: the header is a trade list but the file carries no data"
                    : $"no usable trade rows: all {rejectedRows.Count} data rows were rejected"));
        }

        var fileSymbol = distinctSymbols.SingleOrDefault();

        return Task.FromResult(new ParsedBacktestFileDto(
            FileName: sanitizedFileName,
            Symbol: fileSymbol,
            IsRejected: false,
            RejectionReason: null,
            Trades: trades,
            RejectedRows: rejectedRows));
    }

    private static ParsedBacktestFileDto Rejected(string fileName, string reason)
        => new(
            FileName: fileName,
            Symbol: null,
            IsRejected: true,
            RejectionReason: reason,
            Trades: [],
            RejectedRows: []);

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

    private static bool TryParseDate(string raw, out DateTime value)
        => DateTime.TryParseExact(raw, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    /// <summary>
    /// Checks every length-bounded text column of one row against <see cref="TextColumns"/>.
    /// Stops at the first violation — the row is rejected naming the column and its limit, the
    /// file is not.
    /// </summary>
    private static bool TryValidateLengths(string[] fields, out string? rejectionReason)
    {
        foreach (var (index, name, maxLength) in TextColumns)
        {
            var length = fields[index].Length;
            if (length > maxLength)
            {
                rejectionReason = $"value in column '{name}' is {length} characters, exceeding the {maxLength}-character limit";
                return false;
            }
        }

        rejectionReason = null;
        return true;
    }

    /// <summary>
    /// Parses every decimal-bearing column of one row in a single pass, driven by
    /// <see cref="DecimalColumns"/>. Stops at the first mismatch — the row is rejected, the file
    /// is not.
    /// </summary>
    private static bool ParseAllDecimals(string[] fields, out Dictionary<int, decimal> values, out string? rejectionReason)
    {
        values = new Dictionary<int, decimal>(DecimalColumns.Length);
        rejectionReason = null;

        foreach (var (index, name, useCommaDecimal) in DecimalColumns)
        {
            if (!TryParseDecimal(fields[index], useCommaDecimal, out var value, out var error))
            {
                rejectionReason = $"invalid decimal in column '{name}': {error}";
                return false;
            }

            values[index] = value;
        }

        return true;
    }

    private static bool TryParseDecimal(string raw, bool useCommaDecimal, out decimal value, out string? error)
    {
        value = 0m;
        error = null;

        if (useCommaDecimal)
        {
            if (raw.Contains('.'))
            {
                error = "mismatched separator: expected comma decimal, found a dot";
                return false;
            }

            if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CommaDecimal, out value))
            {
                error = "unparseable value";
                return false;
            }

            return true;
        }

        if (raw.Contains(','))
        {
            error = "mismatched separator: expected dot decimal, found a comma";
            return false;
        }

        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            error = "unparseable value";
            return false;
        }

        return true;
    }

    private static (BacktestSegment Segment, int? SegmentIndex) ClassifySegment(string raw)
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
