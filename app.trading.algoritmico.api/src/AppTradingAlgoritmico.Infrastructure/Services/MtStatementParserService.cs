using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AppTradingAlgoritmico.Application.DTOs.Trades;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// Parses a Darwinex MT4 "Detailed Statement" HTML file into a structured DTO.
/// Uses AngleSharp for HTML parsing, mirrors the approach of HtmlReportParserService.
/// </summary>
public sealed class MtStatementParserService : IMtStatementParserService
{
    // Matches: #MagicNumber StrategyName or #MagicNumber StrategyName[suffix]
    // Group 1 = magic number, Group 2 = strategy name hint, Group 3 = optional suffix
    private static readonly Regex TitleRegex = new(
        @"^#(\d+)\s+(.+?)(?:\[(\w+)\])?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Trade date format used by Darwinex in trade rows: "2026.04.20 14:47:17"
    private const string TradeDateFormat = "yyyy.MM.dd HH:mm:ss";

    // Report time format in the header: "2026 April 21, 07:06"
    private const string ReportTimeFormat = "yyyy MMMM d, HH:mm";

    private readonly ILogger<MtStatementParserService>? _logger;

    public MtStatementParserService(ILogger<MtStatementParserService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<ParsedMtStatementDto?> ParseAsync(Stream htmlStream, CancellationToken ct = default)
    {
        if (htmlStream.Length == 0)
            return null;

        // Read the full stream so we can check for content before parsing
        using var reader = new StreamReader(htmlStream, leaveOpen: true);
        var html = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(html))
            return null;

        // Use AngleSharp BrowsingContext approach (matches HtmlReportParserService style)
        var config = Configuration.Default;
        using var context = BrowsingContext.New(config);
        using var document = await context.OpenAsync(req => req.Content(html), ct);

        // Sanity check — must look like a Darwinex MT4 statement
        if (!HasDarwinexMarkers(document))
            return null;

        var currency = ExtractCurrency(document);
        var reportTime = ExtractReportTime(document);
        var trades = ExtractTrades(document);
        var summary = ExtractSummary(document, reportTime, currency);

        if (summary is null)
            return null;

        return new ParsedMtStatementDto(trades, summary);
    }

    // -------------------------------------------------------------------------
    // Section detection helpers
    // -------------------------------------------------------------------------

    private static bool HasDarwinexMarkers(IDocument document)
    {
        var boldTags = document.QuerySelectorAll("b");
        foreach (var b in boldTags)
        {
            var text = b.TextContent.Trim();
            if (text is "Closed Transactions:" or "Open Trades:" or "Summary:")
                return true;
        }
        return false;
    }

    private static string ExtractCurrency(IDocument document)
    {
        // Header row: <td colspan=2><b>Currency: USD</b></td>
        var boldTags = document.QuerySelectorAll("b");
        foreach (var b in boldTags)
        {
            var text = b.TextContent.Trim();
            if (text.StartsWith("Currency:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = text.Split(':', 2);
                if (parts.Length == 2)
                    return parts[1].Trim();
            }
        }
        return string.Empty;
    }

    private static DateTime ExtractReportTime(IDocument document)
    {
        // Header row last <td>: "2026 April 21, 07:06"
        var boldTags = document.QuerySelectorAll("td > b");
        foreach (var b in boldTags)
        {
            var text = b.TextContent.Trim();
            // Matches: "YYYY Month DD, HH:MM"
            if (DateTime.TryParseExact(
                    text,
                    ReportTimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return dt;
            }
        }
        return default;
    }

    // -------------------------------------------------------------------------
    // Trade extraction
    // -------------------------------------------------------------------------

    private List<ParsedMtTradeDto> ExtractTrades(IDocument document)
    {
        var trades = new List<ParsedMtTradeDto>();

        // Walk all <tr> rows in the document, tracking which section we are in
        var rows = document.QuerySelectorAll("tr");

        var currentSection = Section.None;

        foreach (var row in rows)
        {
            // Check if this row is a section header
            var sectionMarker = GetSectionMarker(row);
            if (sectionMarker != Section.None)
            {
                currentSection = sectionMarker;
                continue;
            }

            // Skip header/totals rows and rows outside a trade section
            if (currentSection is Section.None or Section.Working or Section.Summary)
                continue;

            // Skip column header rows (bgcolor="#C0C0C0")
            var bgColor = row.GetAttribute("bgcolor");
            if (bgColor is not null && bgColor.Equals("#C0C0C0", StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip totals/summary rows with colspan≥10 in first td
            var cells = row.QuerySelectorAll("td").ToArray();
            if (cells.Length == 0)
                continue;

            // Skip total rows: first cell has colspan >= 10
            var firstColspan = cells[0].GetAttribute("colspan");
            if (firstColspan is not null && int.TryParse(firstColspan, out var span) && span >= 10)
                continue;

            // Check for cancelled row: has a <td colspan=4> with text "cancelled"
            if (IsCancelledRow(cells))
                continue;

            // The first cell must have a title attribute with a valid trade title
            var title = cells[0].GetAttribute("title");
            if (string.IsNullOrWhiteSpace(title))
                continue;

            // Check if this is a "cancelled" title (title text contains "cancelled" without #number pattern)
            var titleTrimmed = title.Trim();

            var match = TitleRegex.Match(titleTrimmed);
            if (!match.Success)
            {
                _logger?.LogWarning("Skipping row with malformed title: {Title}", title);
                continue;
            }

            var magicNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var strategyNameHint = match.Groups[2].Value.Trim();
            var suffix = match.Groups[3].Success ? match.Groups[3].Value : null;
            var closeReason = MapCloseReason(suffix);

            var isOpen = currentSection == Section.Open;

            var trade = ParseTradeRow(cells, magicNumber, strategyNameHint, closeReason, isOpen);
            if (trade is not null)
                trades.Add(trade);
        }

        return trades;
    }

    private static Section GetSectionMarker(IElement row)
    {
        var b = row.QuerySelector("b");
        if (b is null)
            return Section.None;

        var text = b.TextContent.Trim();
        return text switch
        {
            "Closed Transactions:" => Section.Closed,
            "Open Trades:" => Section.Open,
            "Working Orders:" => Section.Working,
            "Summary:" => Section.Summary,
            _ => Section.None
        };
    }

    private static bool IsCancelledRow(IElement[] cells)
    {
        // Cancelled rows have a <td colspan=4> containing "cancelled"
        foreach (var cell in cells)
        {
            var colspan = cell.GetAttribute("colspan");
            if (colspan == "4" && cell.TextContent.Trim().Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? MapCloseReason(string? suffix)
    {
        if (suffix is null)
            return null;

        return suffix.ToUpperInvariant() switch
        {
            "SL" => "SL",
            "TP" => "TP",
            _ => "Other"
        };
    }

    private ParsedMtTradeDto? ParseTradeRow(
        IElement[] cells,
        int magicNumber,
        string strategyNameHint,
        string? closeReason,
        bool isOpen)
    {
        // Closed Transactions columns (14):
        // 0: Ticket(title) | 1: OpenTime | 2: Type | 3: Size | 4: Item |
        // 5: OpenPrice | 6: S/L | 7: T/P | 8: CloseTime | 9: ClosePrice |
        // 10: Commission | 11: Taxes | 12: Swap | 13: Profit
        //
        // Open Trades columns (14 — same layout but col 8 is empty &nbsp;, col 9 is market price not close price):
        // 0: Ticket | 1: OpenTime | 2: Type | 3: Size | 4: Item |
        // 5: OpenPrice | 6: S/L | 7: T/P | 8: (empty) | 9: (market price, ignored) |
        // 10: Commission | 11: Taxes | 12: Swap | 13: Profit

        if (cells.Length < 14)
        {
            _logger?.LogWarning("Skipping row with unexpected cell count: {Count}", cells.Length);
            return null;
        }

        try
        {
            var ticket = ParseLong(cells[0].TextContent);
            var openTime = ParseTradeDate(cells[1].TextContent.Trim());
            var type = cells[2].TextContent.Trim();
            var size = ParseDecimal(cells[3].TextContent) ?? 0m;
            var item = cells[4].TextContent.Trim();
            var openPrice = ParseDecimal(cells[5].TextContent) ?? 0m;
            var stopLoss = ParseDecimal(cells[6].TextContent) ?? 0m;
            var takeProfit = ParseDecimal(cells[7].TextContent) ?? 0m;

            DateTime? closeTime = null;
            decimal? closePrice = null;

            if (!isOpen)
            {
                var closeTimeText = cells[8].TextContent.Trim();
                if (!string.IsNullOrWhiteSpace(closeTimeText) && closeTimeText != " ")
                    closeTime = ParseTradeDate(closeTimeText);

                closePrice = ParseDecimal(cells[9].TextContent);
            }

            var commission = ParseDecimal(cells[10].TextContent) ?? 0m;
            var taxes = ParseDecimal(cells[11].TextContent) ?? 0m;
            var swap = ParseDecimal(cells[12].TextContent) ?? 0m;
            var profit = ParseDecimal(cells[13].TextContent) ?? 0m;

            if (ticket == 0)
            {
                _logger?.LogWarning("Skipping row with zero ticket");
                return null;
            }

            return new ParsedMtTradeDto(
                Ticket: ticket,
                MagicNumber: magicNumber,
                StrategyNameHint: strategyNameHint,
                CloseReason: closeReason,
                OpenTime: openTime,
                CloseTime: closeTime,
                Type: type,
                Size: size,
                Item: item,
                OpenPrice: openPrice,
                ClosePrice: closePrice,
                StopLoss: stopLoss,
                TakeProfit: takeProfit,
                Commission: commission,
                Taxes: taxes,
                Swap: swap,
                Profit: profit,
                IsOpen: isOpen);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse trade row, skipping");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Summary extraction
    // -------------------------------------------------------------------------

    private static ParsedSummaryDto? ExtractSummary(IDocument document, DateTime reportTime, string currency)
    {
        // Summary section layout (from fixture):
        // Row 1: Closed Trade P/L + Floating P/L + Margin
        // Row 2: Balance + Equity + Free Margin
        //
        // Values are inside <b> tags within <td> cells.
        // Numbers may have spaces as thousands separators (e.g., "102 730.18").

        decimal balance = 0, equity = 0, floatingPnL = 0;
        decimal margin = 0, freeMargin = 0, closedTradePnL = 0;

        var inSummary = false;

        var rows = document.QuerySelectorAll("tr");
        foreach (var row in rows)
        {
            var b = row.QuerySelector("b");
            if (b is not null && b.TextContent.Trim() == "Summary:")
            {
                inSummary = true;
                continue;
            }

            if (!inSummary)
                continue;

            // Extract all bold cells in this row
            var boldCells = row.QuerySelectorAll("b").ToArray();
            if (boldCells.Length == 0)
                continue;

            // Parse label-value pairs from bold text
            for (var i = 0; i < boldCells.Length - 1; i++)
            {
                var label = boldCells[i].TextContent.Trim().TrimEnd(':').Trim();
                var valueText = boldCells[i + 1].TextContent.Trim();
                var value = ParseDecimalSummary(valueText);

                switch (label.ToUpperInvariant())
                {
                    case "BALANCE":
                        balance = value ?? 0;
                        break;
                    case "EQUITY":
                        equity = value ?? 0;
                        break;
                    case "FLOATING P/L":
                        floatingPnL = value ?? 0;
                        break;
                    case "MARGIN":
                        margin = value ?? 0;
                        break;
                    case "FREE MARGIN":
                        freeMargin = value ?? 0;
                        break;
                    case "CLOSED TRADE P/L":
                        closedTradePnL = value ?? 0;
                        break;
                }
            }
        }

        if (balance == 0 && equity == 0)
            return null;

        return new ParsedSummaryDto(
            ReportTime: reportTime,
            Balance: balance,
            Equity: equity,
            FloatingPnL: floatingPnL,
            Margin: margin,
            FreeMargin: freeMargin,
            ClosedTradePnL: closedTradePnL,
            Currency: string.IsNullOrWhiteSpace(currency) ? "USD" : currency);
    }

    // -------------------------------------------------------------------------
    // Parsing helpers
    // -------------------------------------------------------------------------

    private static DateTime ParseTradeDate(string raw)
    {
        var cleaned = raw.Trim();
        if (DateTime.TryParseExact(
                cleaned,
                TradeDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
        {
            return dt;
        }
        return default;
    }

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == " ")
            return null;

        var cleaned = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    /// <summary>
    /// ParseDecimal variant for Summary bold values — handles spaces as thousands separators.
    /// e.g. "102 730.18" → 102730.18
    /// </summary>
    private static decimal? ParseDecimalSummary(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var cleaned = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal) // non-breaking space
            .Replace(" ", string.Empty, StringComparison.Ordinal)       // regular space (thousands sep)
            .Replace(",", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static long ParseLong(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var cleaned = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return long.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }

    // -------------------------------------------------------------------------
    // Section enum
    // -------------------------------------------------------------------------

    private enum Section
    {
        None,
        Closed,
        Open,
        Working,
        Summary
    }
}
