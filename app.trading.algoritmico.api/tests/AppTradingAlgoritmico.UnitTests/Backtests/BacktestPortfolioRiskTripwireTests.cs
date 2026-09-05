using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Phase 4 — the two boundary tripwires against slice 3, plus the D8 absence-by-construction
/// guarantee. They are grep-checkable claims about this slice's own source text, so they are
/// asserted as such rather than paraphrased in prose nobody re-runs.
/// <para>
/// The source root is resolved from <see cref="CallerFilePathAttribute"/> rather than
/// <c>AppContext.BaseDirectory</c> on purpose: this repository is sometimes built with a relocated
/// <c>BaseOutputPath</c> (locked DLLs), which would put the binaries outside the tree and make a
/// directory walk from the output folder fail for a reason that has nothing to do with the
/// tripwire.
/// </para>
/// </summary>
public class BacktestPortfolioRiskTripwireTests
{
    /// <summary>
    /// Every file this slice (2b) creates or modifies. It doubles as a RENAME tripwire: a missing
    /// entry fails loudly instead of silently reducing the surface being greped.
    /// </summary>
    private static readonly string[] SliceFiles =
    [
        "src/AppTradingAlgoritmico.Domain/Enums/BacktestNetSeriesStatus.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/VarWithholdReason.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/BacktestRunSegmentState.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/GroupRiskMemberStatus.cs",
        "src/AppTradingAlgoritmico.Domain/Enums/GroupRiskAnalysisStatus.cs",
        "src/AppTradingAlgoritmico.Domain/Backtests/BacktestRunSegmentRow.cs",
        "src/AppTradingAlgoritmico.Domain/Backtests/RunSegmentSelection.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestNetSeries.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestNetSeriesResult.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/SeriesDensityDto.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestPortfolioRiskDto.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestServiceRiskDto.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestCorrelationDto.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/GroupRiskAnalysisRequest.cs",
        "src/AppTradingAlgoritmico.Application/DTOs/Backtests/GroupRiskAnalysisDto.cs",
        "src/AppTradingAlgoritmico.Infrastructure/Services/PortfolioAnalyticsCalculator.cs",
        "src/AppTradingAlgoritmico.Infrastructure/Services/BacktestReadService.cs",
        "src/AppTradingAlgoritmico.WebAPI/Controllers/BacktestsController.cs",
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

    /// <summary>
    /// The EXECUTABLE text of a file — comments removed.
    /// <para>
    /// The tripwires are claims about what this slice DOES, not about what its documentation is
    /// allowed to mention. Without this, <c>RunSegmentSelection</c> could not cite the
    /// <c>OosWindow.Resolver</c> precedent it deliberately follows, and the reasoning would have to
    /// be deleted to keep the assertion green — which trades a real explanation for a literal one.
    /// </para>
    /// </summary>
    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    // =====================================================================
    // Tripwire 1 — the slice is WHOLLY DETERMINISTIC. If it ever needs a
    // random number or a seed, it has crossed into slice 3.
    // =====================================================================

    [Fact]
    public void Tripwire_NoSliceFileUsesARandomNumberGenerator()
    {
        foreach (var (file, text) in SliceSources())
        {
            Regex.IsMatch(text, @"\bRandom\b").Should().BeFalse(
                $"{file} must stay deterministic — a generator here would mean the slice has "
                + "become a search, which is slice 3's job");
            Regex.IsMatch(text, @"\bShuffle\b").Should().BeFalse($"{file} must not reorder anything at random");
        }
    }

    [Fact]
    public void Tripwire_NoSliceFileTakesOrSetsASeed()
    {
        // Matches `seed:`, `seed =`, `seed(` and their capitalised forms — an IDENTIFIER, so the
        // word "seeded" in a doc comment does not trip it.
        foreach (var (file, text) in SliceSources())
        {
            Regex.IsMatch(text, @"\b[Ss]eed\s*[:=(]").Should().BeFalse(
                $"{file} carries no seed: an identical input must produce an identical figure with "
                + "no hidden parameter deciding it");
        }
    }

    // =====================================================================
    // Tripwire 2 — the slice evaluates EXACTLY ONE caller-named group and
    // never iterates over or ranks candidate groups.
    // =====================================================================

    [Fact]
    public void Tripwire_TheReadSurfaceEvaluatesExactlyOneGroup()
    {
        var method = typeof(IBacktestReadService).GetMethod(nameof(IBacktestReadService.GetGroupRiskAnalysisAsync));
        method.Should().NotBeNull();

        method!.ReturnType.Should().Be(
            typeof(Task<GroupRiskAnalysisDto>),
            "ONE analysis, never a collection of them — returning many would be ranking candidates");

        var requestParameters = method.GetParameters()
            .Where(p => p.ParameterType == typeof(GroupRiskAnalysisRequest))
            .ToList();
        requestParameters.Should().ContainSingle("one request describes one group");
    }

    [Fact]
    public void Tripwire_NoAnalysisTypeExposesACollectionOfAnalyses()
    {
        foreach (var type in new[] { typeof(GroupRiskAnalysisDto), typeof(GroupRiskAnalysisRequest) })
        {
            foreach (var property in type.GetProperties())
            {
                var carriesAnalyses =
                    property.PropertyType == typeof(GroupRiskAnalysisDto[])
                    || (property.PropertyType.IsGenericType
                        && property.PropertyType.GetGenericArguments().Contains(typeof(GroupRiskAnalysisDto)));

                carriesAnalyses.Should().BeFalse(
                    $"{type.Name}.{property.Name} must not hold alternative groupings — the slice "
                    + "computes figures for the one group it was given and never compares it against others");
            }
        }
    }

    // =====================================================================
    // 4.3 — D8's absence-by-construction: no date filtering in this slice.
    // =====================================================================

    [Fact]
    public void Tripwire_NoSliceFilePerformsACloseTimeBoundaryComparisonOrConsultsOosWindow()
    {
        foreach (var (file, text) in SliceSources())
        {
            text.Should().NotContain(
                "CloseTime >=",
                $"{file} does no date filtering at all — the segment is a property OF the run, and "
                + "selection happens at run granularity");
            text.Should().NotContain(
                "OosWindow",
                $"{file} must not consult OosWindow: which trades of an Evaluation run are unseen "
                + "by the optimiser is a different and later question from which sample a run is");
        }
    }
}
