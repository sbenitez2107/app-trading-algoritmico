using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AppTradingAlgoritmico.Application.DTOs.Divergence;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Phase 3 — reflection contract tests plus the slice's own determinism/threshold tripwires,
/// mirroring the pattern established by <c>BacktestPortfolioRiskTripwireTests</c> (Backtests
/// slice 2b).
/// </summary>
public class ComparabilityContractTests
{
    [Fact]
    public void Dto_ComparabilityBasis_IsComputedNonNullableAndHasNoSetter()
    {
        var property = typeof(PriceOffsetComparabilityDto).GetProperty(nameof(PriceOffsetComparabilityDto.Basis));

        property.Should().NotBeNull();
        Nullable.GetUnderlyingType(property!.PropertyType).Should().BeNull(
            "a nullable Basis would let a caller construct a readout with no comparability disclosure at all");
        property.GetSetMethod(nonPublic: true).Should().BeNull(
            "Basis is computed from a constant — there is no setter to accidentally expose");

        typeof(PriceOffsetComparabilityDto).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().NotContain(
                p => p.Name == nameof(PriceOffsetComparabilityDto.Basis),
                "Basis must not be settable at construction either — only a computed property makes it non-droppable");
    }

    [Fact]
    public void Dto_ExposesNoAggregatedScoreOrGradeMember()
    {
        foreach (var type in new[] { typeof(PriceOffsetComparabilityDto), typeof(MonthlyPriceOffsetDto) })
        {
            foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                member.Name.Should().NotContain("Score", $"{type.Name}.{member.Name} would read as strategy performance and invite a threshold");
                member.Name.Should().NotContain("Grade", $"{type.Name}.{member.Name} would attach a pass/fail label");
                member.Name.Should().NotContain("Rank", $"{type.Name}.{member.Name} — ranking is a caller concern (design.md D8), never a field on the type");
                member.Name.Should().NotContain("Aggregate", $"{type.Name}.{member.Name} — the offset is reported per month, never as a single aggregated figure");
            }
        }
    }

    // =====================================================================
    // This slice's own file list, exercised as executable text.
    // =====================================================================

    private static readonly string[] SliceFiles =
    [
        "src/AppTradingAlgoritmico.Domain/Enums/ComparabilityBasis.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/DstTransitionRisk.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/ComparabilityReadoutStatus.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/NetPlBasis.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Divergence/PriceOffsetComparabilityDto.cs",
        "src/AppTradingAlgoritmico.Application/Interfaces/IDemoBacktestComparabilityReadService.cs",
        "src/AppTradingAlgoritmico.Infrastructure/Services/DemoBacktestComparabilityCalculator.cs",
        "src/AppTradingAlgoritmico.Infrastructure/Services/DemoBacktestComparabilityReadService.cs",
    ];

    private static string ApiRoot([CallerFilePath] string thisFile = "")
    {
        var dir = Directory.GetParent(thisFile);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AppTradingAlgoritmico.slnx")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the tripwire has to be able to read the slice's own source text");
        return dir!.FullName;
    }

    private static IEnumerable<(string File, string Text)> SliceSources()
    {
        var root = ApiRoot();
        foreach (var relative in SliceFiles)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue($"{relative} is part of this slice and must still exist");
            yield return (relative, StripComments(File.ReadAllText(path)));
        }
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    [Fact]
    public void Tripwire_NoSliceFileUsesARandomNumberGeneratorOrSeed()
    {
        foreach (var (file, text) in SliceSources())
        {
            Regex.IsMatch(text, @"\bRandom\b").Should().BeFalse(
                $"{file} must stay deterministic — identical inputs must produce byte-identical readouts");
            Regex.IsMatch(text, @"\bShuffle\b").Should().BeFalse($"{file} must not reorder anything at random");
            Regex.IsMatch(text, @"\b[Ss]eed\s*[:=(]").Should().BeFalse(
                $"{file} carries no seed: no hidden parameter may decide a figure");
        }
    }

    [Fact]
    public void Tripwire_NoSliceFileContainsANumericThreshold()
    {
        foreach (var (file, text) in SliceSources())
        {
            Regex.IsMatch(text, @"\b(Threshold|Cutoff|MinimumPaired|MinPaired|MinCount|MinHistory)\b", RegexOptions.IgnoreCase)
                .Should().BeFalse(
                    $"{file} must not invent a minimum paired-trade count or any other cutoff — "
                    + "spec 'Figures Publish At Any Paired Count Above Zero' forbids it explicitly");
        }
    }
}
