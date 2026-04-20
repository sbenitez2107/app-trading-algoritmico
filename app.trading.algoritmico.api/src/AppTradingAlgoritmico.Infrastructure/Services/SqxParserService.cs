using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AppTradingAlgoritmico.Application.DTOs.Strategies;
using AppTradingAlgoritmico.Application.Interfaces;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public class SqxParserService : ISqxParserService
{
    // Param keys that represent platform/context configuration, not tuning parameters.
    // Mirrors the exclusion list in ExtractParams used by the pseudocode extractor.
    private static readonly HashSet<string> ExcludedParamKeys =
        new(StringComparer.OrdinalIgnoreCase) { "Chart", "Direction", "Symbol", "Size" };

    // Categories we recognise as indicators inside <signals> (entry conditions).
    //   - "indicator"    : classic indicator Item (SMA, LinearRegression, RSI, etc.) — name from @key
    //   - "simpleRules"  : bundled indicator+comparison (StdDevRising, ADXRising, ...) — name from @mI
    //                      (the "mI" attribute holds the underlying indicator module)
    private static readonly HashSet<string> SignalIndicatorCategories =
        new(StringComparer.OrdinalIgnoreCase) { "indicator", "simpleRules" };

    // Categories we recognise as indicators inside entry-order price formulas (under <Then>).
    //   - "indicator"    : e.g. HighestInRange, LowestInRange used for a stop/limit entry price
    //   - "priceValue"   : SessionHigh/SessionLow/etc — price-anchored values computed from bars
    private static readonly HashSet<string> PriceFormulaCategories =
        new(StringComparer.OrdinalIgnoreCase) { "indicator", "priceValue" };

    public async Task<ParsedSqxMetadataDto?> ExtractStrategyMetadataAsync(
        Stream sqxStream, CancellationToken ct = default)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await sqxStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            using var innerArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            // strategy_Portfolio.xml has the clean strategy definition
            // (<StrategyFile><Strategy>...<Rules>...<signals>...). settings.xml is the bulky
            // results/WalkForward container with <ResultsGroup> as root — it does NOT expose
            // the Strategy element as a direct descendant of root, so parsing fails there.
            var entry = innerArchive.GetEntry("strategy_Portfolio.xml")
                        ?? innerArchive.GetEntry("settings.xml");
            if (entry is null)
                return null;

            using var settingsStream = entry.Open();
            var doc = XDocument.Load(settingsStream);

            var pseudocode = ExtractPseudocodeFromXml(doc);
            var (entryIndicators, priceIndicators, indicatorParameters) = ExtractIndicators(doc);

            return new ParsedSqxMetadataDto(pseudocode, entryIndicators, priceIndicators, indicatorParameters);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> ExtractPseudocodeAsync(Stream sqxStream, CancellationToken ct = default)
    {
        var metadata = await ExtractStrategyMetadataAsync(sqxStream, ct);
        return metadata?.Pseudocode;
    }

    public Task<string?> ParseSqbConfigAsync(Stream sqbStream, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(sqbStream, ZipArchiveMode.Read);
        var configEntry = archive.GetEntry("config.xml");
        if (configEntry is null)
            return Task.FromResult<string?>(null);

        using var reader = new StreamReader(configEntry.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();
        return Task.FromResult<string?>(xml);
    }

    /// <summary>
    /// Extracts Entry Indicators, Price Indicators, and Indicator Parameters from the
    /// strategy XML document. Handles both SQX patterns:
    ///
    ///   - Classic: indicators as separate Items with categoryType="indicator" inside <![CDATA[<signals>]]>,
    ///     wrapped by operator Items (IsRising, IsLower, ...).
    ///   - SimpleRules: bundled indicator+comparison Items with categoryType="simpleRules"
    ///     (e.g. StdDevRising, ADXRising). The underlying indicator name lives in the
    ///     "mI" (module) attribute; the Item's own Params are the indicator's tuning params.
    ///
    /// Entry Indicators  = unique indicator names found inside <![CDATA[<signals>]]>, alphabetical, CSV.
    /// Price Indicators  = unique indicator names found inside entry-order price formulas
    ///                     (Items inside <![CDATA[<Then>…<Param key="#Price#">]]> → Formula → Item).
    /// Indicator Params  = "IndicatorName(k1=v1, k2=v2); OtherName(kA=vA)" for the union of both sets,
    ///                     params from the first occurrence (same name ⇒ same tuning across signals in practice).
    /// Returns (null, null, null) when neither set has any indicators.
    /// </summary>
    private static (string? entryIndicators, string? priceIndicators, string? indicatorParameters)
        ExtractIndicators(XDocument doc)
    {
        var entryMap = CollectIndicatorsFromSignals(doc);
        var priceMap = CollectIndicatorsFromPriceFormulas(doc);

        if (entryMap.Count == 0 && priceMap.Count == 0)
            return (null, null, null);

        // Union map for parameters. Entry takes precedence on collision (same indicator
        // used in both signals and price formula — rare but possible).
        var unionMap = new Dictionary<string, List<(string key, string value)>>(StringComparer.Ordinal);
        foreach (var kv in entryMap) unionMap[kv.Key] = kv.Value;
        foreach (var kv in priceMap)
            if (!unionMap.ContainsKey(kv.Key)) unionMap[kv.Key] = kv.Value;

        return (FormatNames(entryMap), FormatNames(priceMap), FormatParams(unionMap));
    }

    private static Dictionary<string, List<(string key, string value)>> CollectIndicatorsFromSignals(XDocument doc)
    {
        var map = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);

        foreach (var signals in doc.Descendants("signals"))
        {
            foreach (var item in signals.Descendants("Item"))
            {
                var categoryType = item.Attribute("categoryType")?.Value;
                if (string.IsNullOrEmpty(categoryType) || !SignalIndicatorCategories.Contains(categoryType))
                    continue;

                // Name source depends on category:
                //   - "indicator"   → @key  (LinearRegression, LowestInRange, SMA, RSI, ...)
                //   - "simpleRules" → @mI   (StdDev, ADX — the underlying indicator module)
                var name = string.Equals(categoryType, "simpleRules", StringComparison.OrdinalIgnoreCase)
                    ? item.Attribute("mI")?.Value
                    : item.Attribute("key")?.Value;

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!map.ContainsKey(name))
                    map[name] = CollectItemParams(item);
            }
        }

        return map;
    }

    private static Dictionary<string, List<(string key, string value)>> CollectIndicatorsFromPriceFormulas(XDocument doc)
    {
        var map = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);

        // Walk every <Then> block and find Params whose key is "#Price#" — these are the
        // entry order price parameters (EnterAtStop/EnterAtLimit/etc.). Anything indicator-ish
        // nested inside their Formula is a "Price Indicator".
        foreach (var thenBlock in doc.Descendants("Then"))
        {
            foreach (var priceParam in thenBlock.Descendants("Param")
                         .Where(p => p.Attribute("key")?.Value == "#Price#"))
            {
                foreach (var item in priceParam.Descendants("Item"))
                {
                    var categoryType = item.Attribute("categoryType")?.Value;
                    if (string.IsNullOrEmpty(categoryType) || !PriceFormulaCategories.Contains(categoryType))
                        continue;

                    var name = item.Attribute("key")?.Value;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (!map.ContainsKey(name))
                        map[name] = CollectItemParams(item);
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Collects tuning parameters from an indicator Item's direct children (leaf Param elements).
    /// Platform/context params (#Chart#, #Direction#, #Symbol#, #Size#) are excluded.
    /// </summary>
    private static List<(string key, string value)> CollectItemParams(XElement item) =>
        item.Elements("Param")
            .Select(p => (
                key: p.Attribute("key")?.Value?.Trim('#') ?? string.Empty,
                value: p.Value))
            .Where(p => !string.IsNullOrWhiteSpace(p.key)
                        && !string.IsNullOrWhiteSpace(p.value)
                        && !ExcludedParamKeys.Contains(p.key))
            .ToList();

    private static string? FormatNames(Dictionary<string, List<(string key, string value)>> map) =>
        map.Count == 0 ? null
            : string.Join(", ", map.Keys.OrderBy(k => k, StringComparer.Ordinal));

    private static string? FormatParams(Dictionary<string, List<(string key, string value)>> map)
    {
        if (map.Count == 0) return null;
        var parts = map
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv =>
            {
                if (kv.Value.Count == 0) return kv.Key;
                var joined = string.Join(", ", kv.Value.Select(p => $"{p.key}={p.value}"));
                return $"{kv.Key}({joined})";
            });
        return string.Join("; ", parts);
    }

    private static string ExtractPseudocodeFromXml(XDocument doc)
    {
        var sb = new StringBuilder();
        var strategy = doc.Root?.Element("Strategy");
        if (strategy is null)
            return "Unable to parse strategy";

        var engine = strategy.Attribute("engine")?.Value ?? "Unknown";
        sb.AppendLine($"Engine: {engine}");

        var mm = strategy.Element("MoneyManagement");
        if (mm is not null)
        {
            var mmType = mm.Attribute("type")?.Value ?? "Unknown";
            sb.AppendLine($"Money Management: {mmType}");
        }

        var globalSlPt = strategy.Element("GlobalSLPT");
        if (globalSlPt is not null)
        {
            var slValues = globalSlPt.Descendants("globalSL").FirstOrDefault();
            var ptValues = globalSlPt.Descendants("globalPT").FirstOrDefault();
            if (slValues is not null)
            {
                var slValue = slValues.Descendants("value").FirstOrDefault()?.Value;
                if (slValue is not null && slValue != "0")
                    sb.AppendLine($"Global SL: {slValue}");
            }
            if (ptValues is not null)
            {
                var ptValue = ptValues.Descendants("value").FirstOrDefault()?.Value;
                if (ptValue is not null && ptValue != "0")
                    sb.AppendLine($"Global TP: {ptValue}");
            }
        }

        var rules = strategy.Element("Rules");
        if (rules is null)
            return sb.ToString();

        foreach (var evt in rules.Elements("Events").Elements("Event"))
        {
            foreach (var rule in evt.Elements("Rule"))
            {
                var ruleName = rule.Attribute("name")?.Value ?? "Unnamed Rule";
                var ruleType = rule.Attribute("type")?.Value ?? "";
                sb.AppendLine();
                sb.AppendLine($"[{ruleName}] ({ruleType})");

                foreach (var signal in rule.Descendants("signal"))
                {
                    var item = signal.Element("Item");
                    if (item is null) continue;

                    var display = item.Attribute("display")?.Value;
                    var itemName = item.Attribute("name")?.Value;
                    if (display is null && itemName is null) continue;

                    var paramValues = ExtractParams(item);
                    var displayText = display ?? itemName ?? "Unknown";
                    sb.AppendLine($"  Signal: {displayText} {paramValues}");
                }

                ExtractIfThen(rule, sb, "  ");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void ExtractIfThen(XElement parent, StringBuilder sb, string indent)
    {
        var ifBlock = parent.Element("If");
        var thenBlock = parent.Element("Then");

        if (ifBlock is not null)
        {
            foreach (var item in ifBlock.Elements("Item"))
            {
                var display = item.Attribute("display")?.Value ?? item.Attribute("name")?.Value ?? "Unknown";
                var paramValues = ExtractParams(item);
                sb.AppendLine($"{indent}IF: {display} {paramValues}");
            }
        }

        if (thenBlock is not null)
        {
            foreach (var item in thenBlock.Elements("Item"))
            {
                var display = item.Attribute("display")?.Value ?? item.Attribute("name")?.Value ?? "Unknown";
                var paramValues = ExtractParams(item);
                sb.AppendLine($"{indent}THEN: {display} {paramValues}");
            }
        }
    }

    private static string ExtractParams(XElement item)
    {
        var paramParts = new List<string>();

        foreach (var param in item.Elements("Param"))
        {
            var key = param.Attribute("key")?.Value?.Trim('#');
            var value = param.Value;
            if (key is not null && !string.IsNullOrWhiteSpace(value) && !ExcludedParamKeys.Contains(key))
            {
                paramParts.Add($"{key}={value}");
            }
        }

        return paramParts.Count > 0 ? $"({string.Join(", ", paramParts)})" : "";
    }
}
