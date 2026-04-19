using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AppTradingAlgoritmico.Application.Interfaces;

namespace AppTradingAlgoritmico.Infrastructure.Services;

public class SqxParserService : ISqxParserService
{
    public async Task<string?> ExtractPseudocodeAsync(Stream sqxStream, CancellationToken ct = default)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await sqxStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            using var innerArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            var settingsEntry = innerArchive.GetEntry("settings.xml");
            if (settingsEntry is null)
                return null;

            using var settingsStream = settingsEntry.Open();
            var doc = XDocument.Load(settingsStream);

            return ExtractPseudocodeFromXml(doc);
        }
        catch
        {
            return null;
        }
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
            if (key is not null && !string.IsNullOrWhiteSpace(value) && key != "Chart" && key != "Direction" && key != "Symbol" && key != "Size")
            {
                paramParts.Add($"{key}={value}");
            }
        }

        return paramParts.Count > 0 ? $"({string.Join(", ", paramParts)})" : "";
    }
}
