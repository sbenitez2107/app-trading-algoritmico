using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public sealed class HtmlReportParserService : IHtmlReportParserService
{
    private static readonly HtmlParser Parser = new();

    private static readonly Regex HeaderRegex = new(
        @"^\s*(?<symbol>[^,]+?)\s*,\s*(?<tf>[^,]+?)\s*,\s*(?<from>\d{4}\.\d{2}\.\d{2})\s*-\s*(?<to>\d{4}\.\d{2}\.\d{2})\s*$",
        RegexOptions.Compiled);

    public async Task<ParsedReportDto?> ParseAsync(Stream htmlStream, CancellationToken ct = default)
    {
        var doc = await Parser.ParseDocumentAsync(htmlStream, ct);

        // Sanity check — must look like an SQX report
        if (doc.QuerySelector("div#summaryBox") is null)
            return null;

        var (symbol, timeframe, from, to) = ExtractMetadata(doc);
        var labels = ExtractLabels(doc);
        var monthly = ExtractMonthly(doc);
        var kpis = MapLabelsToKpis(labels);

        return new ParsedReportDto(symbol, timeframe, from, to, kpis, monthly);
    }

    private static (string? Symbol, string? Timeframe, DateTime? From, DateTime? To) ExtractMetadata(IDocument doc)
    {
        var h3 = doc.QuerySelector("div.title > h3")?.TextContent?.Trim();
        if (string.IsNullOrEmpty(h3))
            return (null, null, null, null);

        var match = HeaderRegex.Match(h3);
        if (!match.Success)
            return (null, null, null, null);

        var symbol = match.Groups["symbol"].Value.Trim();
        var timeframe = match.Groups["tf"].Value.Trim();
        var from = TryParseSqxDate(match.Groups["from"].Value);
        var to = TryParseSqxDate(match.Groups["to"].Value);

        return (symbol, timeframe, from, to);
    }

    private static DateTime? TryParseSqxDate(string raw)
    {
        return DateTime.TryParseExact(
            raw,
            "yyyy.MM.dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dt)
                ? dt
                : null;
    }

    private static Dictionary<string, string> ExtractLabels(IDocument doc)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Summary top-left: .plH1 → .name + sibling value span
        foreach (var plH1 in doc.QuerySelectorAll("div.plPart div.plH1"))
        {
            var nameEl = plH1.QuerySelector(".name");
            var valueEl = plH1.Children.FirstOrDefault(c =>
                c.ClassList.Contains("positiveNum") || c.ClassList.Contains("negativeNum"));
            AddLabel(map, nameEl?.TextContent, valueEl?.TextContent);
        }

        // Summary top-left: .plH2 → .name2 + .value2
        foreach (var plH2 in doc.QuerySelectorAll("div.plPart div.plH2"))
        {
            var nameEl = plH2.QuerySelector(".name2");
            var valueEl = plH2.QuerySelector(".value2");
            AddLabel(map, nameEl?.TextContent, valueEl?.TextContent);
        }

        // Summary grid: .sumH2 → .name + .value
        foreach (var sum in doc.QuerySelectorAll("div.sumH2"))
        {
            var nameEl = sum.QuerySelector(".name");
            var valueEl = sum.QuerySelector(".value");
            AddLabel(map, nameEl?.TextContent, valueEl?.TextContent);
        }

        // Stats tables: td.key + adjacent td.val, td.keyT + td.valT
        foreach (var keyCell in doc.QuerySelectorAll("td.key, td.keyT"))
        {
            var valueCell = keyCell.NextElementSibling;
            if (valueCell is null)
                continue;
            if (!(valueCell.ClassList.Contains("val") || valueCell.ClassList.Contains("valT")))
                continue;
            AddLabel(map, keyCell.TextContent, valueCell.TextContent);
        }

        return map;
    }

    private static void AddLabel(Dictionary<string, string> map, string? label, string? value)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;
        var key = label.Trim().ToUpperInvariant();
        if (key.Length == 0 || map.ContainsKey(key))
            return;
        map[key] = value?.Trim() ?? string.Empty;
    }

    private static List<MonthlyPerformanceDto> ExtractMonthly(IDocument doc)
    {
        var result = new List<MonthlyPerformanceDto>();

        var table = doc.QuerySelector("div.performance table.calendar");
        if (table is null)
            return result;

        foreach (var row in table.QuerySelectorAll("tr.oddrow, tr.evenrow"))
        {
            var cells = row.QuerySelectorAll("td").ToArray();
            // Expect: year + 12 months + YTD = 14 cells
            if (cells.Length < 13)
                continue;

            if (!int.TryParse(
                    cells[0].TextContent.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var year))
                continue;

            for (var month = 1; month <= 12; month++)
            {
                var profit = ParseDecimal(cells[month].TextContent);
                if (!profit.HasValue)
                    continue;
                result.Add(new MonthlyPerformanceDto(year, month, profit.Value));
            }
        }

        return result;
    }

    private static UpdateStrategyKpisDto MapLabelsToKpis(Dictionary<string, string> map)
    {
        decimal? Dec(string key) => map.TryGetValue(key, out var v) ? ParseDecimal(v) : null;
        int? Int(string key) => map.TryGetValue(key, out var v) ? ParseInt(v) : null;

        return new UpdateStrategyKpisDto(
            // Summary: top-left
            TotalProfit: Dec("TOTAL PROFIT"),
            ProfitInPips: Dec("PROFIT IN PIPS"),
            YearlyAvgProfit: Dec("YEARLY AVG PROFIT"),
            YearlyAvgReturn: Dec("YEARLY AVG % RETURN"),
            Cagr: Dec("CAGR"),
            // Summary: grid
            NumberOfTrades: Int("# OF TRADES"),
            SharpeRatio: Dec("SHARPE RATIO"),
            ProfitFactor: Dec("PROFIT FACTOR"),
            ReturnDrawdownRatio: Dec("RETURN / DD RATIO"),
            WinningPercentage: Dec("WINNING PERCENTAGE"),
            Drawdown: Dec("DRAWDOWN"),
            DrawdownPercent: Dec("% DRAWDOWN"),
            DailyAvgProfit: Dec("DAILY AVG PROFIT"),
            MonthlyAvgProfit: Dec("MONTHLY AVG PROFIT"),
            AverageTrade: Dec("AVERAGE TRADE"),
            AnnualReturnMaxDdRatio: Dec("ANNUAL % / MAX DD %"),
            RExpectancy: Dec("R EXPECTANCY"),
            RExpectancyScore: Dec("R EXPECTANCY SCORE"),
            StrQualityNumber: Dec("STR QUALITY NUMBER"),
            SqnScore: Dec("SQN SCORE"),
            // Stats: Strategy
            WinsLossesRatio: Dec("WINS / LOSSES RATIO"),
            PayoutRatio: Dec("PAYOUT RATIO (AVG WIN/LOSS)"),
            AverageBarsInTrade: Dec("AVERAGE # OF BARS IN TRADE"),
            Ahpr: Dec("AHPR"),
            ZScore: Dec("Z-SCORE"),
            ZProbability: Dec("Z-PROBABILITY"),
            Expectancy: Dec("EXPECTANCY"),
            Deviation: Dec("DEVIATION"),
            Exposure: Dec("EXPOSURE"),
            StagnationInDays: Int("STAGNATION IN DAYS"),
            StagnationPercent: Dec("STAGNATION IN %"),
            // Stats: Trades
            NumberOfWins: Int("# OF WINS"),
            NumberOfLosses: Int("# OF LOSSES"),
            NumberOfCancelled: Int("# OF CANCELLED/EXPIRED"),
            GrossProfit: Dec("GROSS PROFIT"),
            GrossLoss: Dec("GROSS LOSS"),
            AverageWin: Dec("AVERAGE WIN"),
            AverageLoss: Dec("AVERAGE LOSS"),
            LargestWin: Dec("LARGEST WIN"),
            LargestLoss: Dec("LARGEST LOSS"),
            MaxConsecutiveWins: Int("MAX CONSEC WINS"),
            MaxConsecutiveLosses: Int("MAX CONSEC LOSSES"),
            AverageConsecutiveWins: Dec("AVG CONSEC WINS"),
            AverageConsecutiveLosses: Dec("AVG CONSEC LOSS"),
            AverageBarsInWins: Dec("AVG # OF BARS IN WINS"),
            AverageBarsInLosses: Dec("AVG # OF BARS IN LOSSES")
        );
    }

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var cleaned = CleanNumericString(raw);
        if (cleaned.Length == 0)
            return null;
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static int? ParseInt(string? raw)
    {
        var dec = ParseDecimal(raw);
        return dec.HasValue ? (int)Math.Round(dec.Value) : null;
    }

    private static string CleanNumericString(string raw)
    {
        var s = raw.Trim();
        s = s.Replace("$", string.Empty, StringComparison.Ordinal);
        s = s.Replace("%", string.Empty, StringComparison.Ordinal);
        s = s.Replace("ticks", string.Empty, StringComparison.OrdinalIgnoreCase);
        s = s.Replace(" ", string.Empty, StringComparison.Ordinal);
        s = s.Replace(",", string.Empty, StringComparison.Ordinal);
        return s;
    }
}
