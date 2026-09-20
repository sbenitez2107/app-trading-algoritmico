using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AppTradingAlgoritmico.Application.DTOs.Divergence;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Divergence;

/// <summary>
/// Phase 7 — reflection contract tests plus B1's own tripwires, mirroring
/// <c>ComparabilityContractTests</c>. This is B1's OWN <see cref="SliceFiles"/> array — separate
/// from slice A's hardcoded list, which does not see these files (design "Tripwire test" note).
/// </summary>
public class CostDecompositionContractTests
{
    [Fact]
    public void Dto_CoverageBasis_IsComputedNonNullableAndHasNoSetter()
    {
        var property = typeof(CoverageComponentDto).GetProperty(nameof(CoverageComponentDto.CoverageBasis));

        property.Should().NotBeNull();
        Nullable.GetUnderlyingType(property!.PropertyType).Should().BeNull(
            "a nullable CoverageBasis would let a caller construct a readout with no coverage disclosure at all");
        property.GetSetMethod(nonPublic: true).Should().BeNull(
            "CoverageBasis is computed from a constant — there is no setter to accidentally expose");

        typeof(CoverageComponentDto).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().NotContain(
                p => p.Name == nameof(CoverageComponentDto.CoverageBasis),
                "CoverageBasis must not be settable at construction either — only a computed property makes it non-droppable");
    }

    [Fact]
    public void Dto_ExposesNoThresholdScoreGradeOrRecommendationMember()
    {
        var forbiddenFragments = new[] { "IsComparable", "Score", "Grade", "Rank", "Pass", "Fail", "Threshold", "Acceptable" };

        foreach (var type in new[] { typeof(CostDecompositionDto), typeof(CoverageComponentDto), typeof(CoverageMonthDto) })
        {
            foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var fragment in forbiddenFragments)
                {
                    member.Name.Should().NotContain(fragment, $"{type.Name}.{member.Name} would smuggle in a filter, gate, or verdict");
                }
            }
        }
    }

    // =====================================================================
    // B1's own file list, exercised as executable text.
    // =====================================================================

    private static readonly string[] SliceFiles =
    [
        "src/AppTradingAlgoritmico.Domain/Enums/CoverageBasis.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/PeriodCoverage.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/CostDecompositionStatus.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Divergence/CostDecompositionDto.cs",
        "src/AppTradingAlgoritmico.Application/Interfaces/ICostDecompositionReadService.cs",
        "src/AppTradingAlgoritmico.Infrastructure/Services/CostObservation.cs",
        "src/AppTradingAlgoritmico.Infrastructure/Services/DemoBacktestCoverageCalculator.cs",
        "src/AppTradingAlgoritmico.Infrastructure/Services/CostDecompositionReadService.cs",
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
    public void Tripwire_NoSliceFileContainsANumericThresholdOrCutoff()
    {
        // Named identifier list, never a generic Min… regex — a generic pattern would
        // false-positive on B2's MinObserved/MaxObserved (design "Tripwire test" item 2).
        foreach (var (file, text) in SliceSources())
        {
            Regex.IsMatch(text, @"\b(Threshold|Cutoff|Acceptable|Tolerance|Band|MinTrades|MinPaired|MinMonths|MinSamples)\b")
                .Should().BeFalse($"{file} must not invent a threshold, cutoff, or acceptable band");
        }
    }

    [Fact]
    public void Tripwire_NoSliceFileContainsAnInstrumentLiteral()
    {
        foreach (var (file, text) in SliceSources())
        {
            Regex.IsMatch(text, "XAUUSD|GDAXI|USATECH|NDX|\"NQ\"|\"DAX\"").Should().BeFalse(
                $"{file} must not name a specific instrument (spec 'No Instrument Is Named In Code')");
        }
    }
}
