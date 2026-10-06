using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1d.1 (design D8, hard rule 5) — grep-checkable claims about the search's own source text, in the
/// style of <c>BacktestPortfolioRiskTripwireTests</c>. The source root comes from <see cref="CallerFilePathAttribute"/>,
/// never from the binaries' folder (this repository is built with a relocated <c>BaseOutputPath</c>).
/// <para>
/// (1) The pure search files are deterministic and never touch the evaluator or the database. (2) The one-group
/// capability and its read path never reach the search: the search COMPOSES <c>ComputeGroup</c>, not the other way
/// round. (3) The shipped engine files and the 18 files of the portfolio-risk slice are byte-identical to their
/// pinned hashes (line endings normalised), so a change to one of them fails here, loudly.
/// </para>
/// </summary>
public class FtmoGroupSearchTripwireTests
{
    private const string Services = "src/AppTradingAlgoritmico.Infrastructure/Services/";

    /// <summary>Pure: no I/O, no clock, no ids minted, no randomness. A missing entry fails loudly (rename tripwire).</summary>
    private static readonly string[] PureSearchFiles =
    [
        Services + "FtmoGroupSearchEngine.cs",
        Services + "FtmoGroupSearchRanking.cs",
        Services + "FtmoGroupSearchLimits.cs",
        Services + "FtmoLimitHeadroom.cs",
        Services + "FtmoProjectionCache.cs",
        Services + "FtmoDailyLossProfile.cs",
        Services + "FtmoPeakConcurrency.cs",
        Services + "FtmoRaceSurrogate.cs",
    ];

    /// <summary>The search's registry and worker (slice 2a) keep a clock and mint ids by design; every other check still applies to them.</summary>
    private static bool IsClockOrIdOwner(string relative) =>
        relative.Contains("JobRegistry", StringComparison.Ordinal) || relative.Contains("Worker", StringComparison.Ordinal);

    /// <summary>The one-group capability and the read path that serves it.</summary>
    private static readonly string[] OneGroupFiles =
    [
        Services + "FtmoGroupComputation.cs",
        Services + "FtmoGroupDiagnostics.cs",
        Services + "FtmoGroupMerger.cs",
        Services + "FtmoGroupSimulationReadService.cs",
        Services + "FtmoMultiStartReadService.cs",
        "src/AppTradingAlgoritmico.WebAPI/Controllers/FtmoSimulationsController.cs",
    ];

    private static string ApiRoot([CallerFilePath] string thisFile = "")
    {
        var dir = Directory.GetParent(thisFile);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AppTradingAlgoritmico.slnx")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the tripwire has to be able to read the search's own source text");
        return dir!.FullName;
    }

    private static string PathOf(string relative) => Path.Combine(ApiRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Read(string relative)
    {
        var path = PathOf(relative);
        File.Exists(path).Should().BeTrue($"{relative} is part of this tripwire and must still exist");
        return File.ReadAllText(path);
    }

    /// <summary>Every <c>FtmoGroupSearch*.cs</c> under Services, so a file added by a later slice is covered without editing this test.</summary>
    private static IEnumerable<string> AllSearchFiles()
        => Directory.GetFiles(PathOf(Services), "FtmoGroupSearch*.cs").Select(f => Services + Path.GetFileName(f)).Order(StringComparer.Ordinal);

    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    private static string Code(string relative) => StripComments(Read(relative));

    // ---- (1) the search files are deterministic and read-only ----

    [Fact]
    public void ThePureSearchFilesAllExist_AndTheGlobCoversThem()
    {
        foreach (var file in PureSearchFiles)
            File.Exists(PathOf(file)).Should().BeTrue($"{file} must still exist");

        AllSearchFiles().Should().Contain(PureSearchFiles.Where(f => Path.GetFileName(f).StartsWith("FtmoGroupSearch", StringComparison.Ordinal)));
    }

    [Fact]
    public void NoSearchFile_UsesARandomGenerator_AShuffle_OrASeed()
    {
        foreach (var file in PureSearchFiles.Concat(AllSearchFiles()).Distinct())
        {
            var text = Code(file);
            Regex.IsMatch(text, @"\bRandom\b").Should().BeFalse($"{file}: same input must give identical output");
            Regex.IsMatch(text, @"\bShuffle\b").Should().BeFalse($"{file} must not reorder anything at random");
            Regex.IsMatch(text, @"\b[Ss]eed\s*[:=(]").Should().BeFalse($"{file} carries no seed");
        }
    }

    [Fact]
    public void NoPureSearchFile_MintsAnIdOrReadsTheClock()
    {
        foreach (var file in PureSearchFiles.Concat(AllSearchFiles().Where(f => !IsClockOrIdOwner(f))).Distinct())
        {
            var text = Code(file);
            Regex.IsMatch(text, @"\bGuid\.NewGuid\b").Should().BeFalse($"{file} must be a pure function of its input");
            Regex.IsMatch(text, @"\bDateTime(Offset)?\.(Now|UtcNow)\b").Should().BeFalse($"{file} must not read the clock");
        }
    }

    [Fact]
    public void NoSearchFile_WritesToTheDatabase_OrCallsTheBreachEvaluatorDirectly()
    {
        var files = PureSearchFiles.Concat(AllSearchFiles()).Append(Services + "FtmoGroupMemberResolution.cs").Distinct();
        foreach (var file in files)
        {
            var text = Code(file);
            Regex.IsMatch(text, @"\bSaveChanges(Async)?\b").Should().BeFalse($"{file} only reads");
            Regex.IsMatch(text, @"\bFtmoBreachEvaluator\b").Should().BeFalse($"{file} replicates the evaluator's bookkeeping, it never calls the evaluator");
        }
    }

    [Fact]
    public void TheEngine_ComposesComputeGroup_AndNeverTheReplayItself()
    {
        var engine = Code(Services + "FtmoGroupSearchEngine.cs");

        engine.Should().MatchRegex(@"\bComputeGroup\(", "the full simulation of a candidate is the shipped group computation");
        engine.Should().NotMatchRegex(@"\bComputeRun\(", "the search reaches the replay only through ComputeGroup");
    }

    // ---- (2) the one-group capability never reaches the search ----

    [Fact]
    public void TheOneGroupFiles_HoldNoCombinationLoop_NoRankingCall_AndNoReferenceToTheSearch()
    {
        string[] forbidden =
        [
            @"\bFtmoGroupSearch\w*", @"\bFtmoLimitHeadroom\b", @"\bFtmoProjectionCache\b", @"\bFtmoDailyLossProfile\b", @"\bFtmoPeakConcurrency\b",
            @"\b[Cc]ombinations?\b", @"\b[Pp]ermutations?\b", @"\bShortlist\w*", @"\bPowerSet\b",
        ];

        foreach (var file in OneGroupFiles)
        {
            var text = Code(file);
            foreach (var pattern in forbidden)
                Regex.IsMatch(text, pattern).Should().BeFalse($"{file} must stay a one-group capability (matched {pattern})");
        }
    }

    // ---- (3) the shipped engine and the portfolio-risk slice are unchanged ----

    /// <summary>SHA-256 of the file text, UTF-8 without BOM, CRLF normalised to LF.</summary>
    private static string Hash(string relative)
    {
        var text = Read(relative).Replace("\r\n", "\n");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static readonly (string File, string Sha256)[] PinnedFiles =
    [
        // Shipped engine files (hard rule 2).
        (Services + "FtmoBreachEvaluator.cs", "2f65618a64392c859d88b5215280892b6822767e345c45be854a7802adc3a063"),
        (Services + "FtmoChallengeRace.cs", "39eef3da9865df9ac9043131befe6f267391f31e96c8eb3c708d3f65b253cee7"),
        (Services + "FtmoFundedPhase.cs", "939c1de324d1a031c74628015a0a84b3622e7369cd253e9392c2abb0cc4227a2"),
        (Services + "FtmoStartEnumerator.cs", "1e6388aa04e175ea9c0ed7ad6b799b2ff101684b7fc1fd01375c3a2944c84497"),
        (Services + "FtmoOpenPositionSweep.cs", "b2e783ae7e0012f9079849d3a9f948a60acff2f367308caf104331086d8bc704"),
        (Services + "FtmoReplayCalendar.cs", "06e4ab2afad9565e8df7269d3b566391731141109ccbe25c4f967faaf74c3e0f"),
        (Services + "FtmoTradeProjector.cs", "41b7cfc84daa80e5418a7212920b128d87125852fa31c5162837ddb3de476114"),
        (Services + "FtmoGroupMerger.cs", "1215e23d7ae5e7f30a43661cbf77e0897c658e6cd8a6fbfa84e6c0d27818af40"),
        (Services + "FtmoGroupComputation.cs", "11ac942175b55e3c53de3cc0dbb4ada7c41fb371595b7170e02665b056ebd51d"),
        (Services + "FtmoGroupDiagnostics.cs", "c4d2bbf1080b274813024f73eab36c356f9236d5e763a39256c18edb29b682f5"),
        (Services + "FtmoMultiStartReadService.cs", "ba9535b28342d853ab4fd66936a897e57c5a3c0f94cf7e74f4fd71a342b4a265"),
        // The 18 files of the portfolio-risk slice (BacktestPortfolioRiskTripwireTests.SliceFiles).
        ("src/AppTradingAlgoritmico.Domain/Enums/BacktestNetSeriesStatus.cs", "4a825a6ed6ebdb9df2efa847362a3181210f3f9d4d873ba091d509049151d9e6"),
        ("src/AppTradingAlgoritmico.Domain/Enums/VarWithholdReason.cs", "978a92d00f0d98f39744015872ae31f78e3aa436da10e2ed043e1f55819375dd"),
        ("src/AppTradingAlgoritmico.Domain/Enums/BacktestRunSegmentState.cs", "833412c5f4246ca3393238ee8754d430800e05fec94e16ca18dd5ee34c992618"),
        ("src/AppTradingAlgoritmico.Domain/Enums/GroupRiskMemberStatus.cs", "88f38f7e86e94f48ee628cba032e047fb8c3fa42c6cabeb5eae537941f1a60dc"),
        ("src/AppTradingAlgoritmico.Domain/Enums/GroupRiskAnalysisStatus.cs", "e07a10ec22a0f4dd5d77e6007c8d07f532db4ae87a92b11041370fa9dcdad176"),
        ("src/AppTradingAlgoritmico.Domain/Backtests/BacktestRunSegmentRow.cs", "b3fbbe8e2ff23bbefb730feff8a09771a46fbd48575d94b22c51e2dc410aebd2"),
        ("src/AppTradingAlgoritmico.Domain/Backtests/RunSegmentSelection.cs", "7ee4e601de523c4eef9160632385d0c7133af29514dab33d6e011a700600c238"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestNetSeries.cs", "12fb9e05d612b8e1b009fcdb07ea5178cc2d11af2bbbd76f472ec8592d36800f"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestNetSeriesResult.cs", "1be483fa14c1ac9aada146ffdbd9842b5585e9c3aaab0483106456b4fd052d69"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/SeriesDensityDto.cs", "ffc6b7e36c89a63486278a4bd440273d49c499079c8da45d7b730b3a77acd2fe"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestPortfolioRiskDto.cs", "69dcff9bcf09ded652f709ca90206a04848517a2cdbb08b21dd9aca0240c8d30"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestServiceRiskDto.cs", "d1bf0509cb5d5fee265969b49194b01399e2bb5c55bf831542857a87b60871f4"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/BacktestCorrelationDto.cs", "10057fa5fe103355d3e1216c776dc3d300d83e0b46d148be74294aa8e715750e"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/GroupRiskAnalysisRequest.cs", "cb7c97c92a43ceb618d8dda4ea6b615b60b294be2de53135ea7c761ef7be69c4"),
        ("src/AppTradingAlgoritmico.Application/DTOs/Backtests/GroupRiskAnalysisDto.cs", "148b74394d4aee8773404e5eced59f6c8831e38c6fafa14c05ec53f7125a538b"),
        ("src/AppTradingAlgoritmico.Infrastructure/Services/PortfolioAnalyticsCalculator.cs", "70e6eea814fa921ab3c19496faaca8f6219fa45f935a87dc39fea2006728c1b4"),
        ("src/AppTradingAlgoritmico.Infrastructure/Services/BacktestReadService.cs", "eb76ace25d6fc13843aebba013733c210152e836330ab78bba83d89fb1586ab8"),
        ("src/AppTradingAlgoritmico.WebAPI/Controllers/BacktestsController.cs", "f0ebe1f498cd6c8f647733a30e559c86616ddfae25eb24b50055ce768e72d7d0"),
    ];

    [Fact]
    public void TheShippedEngineFiles_AndTheEighteenSliceFiles_AreByteIdenticalToTheirPins()
    {
        var mismatches = PinnedFiles
            .Select(p => (p.File, Expected: p.Sha256, Actual: Hash(p.File)))
            .Where(x => x.Expected != x.Actual)
            .Select(x => $"{x.File} expected {x.Expected} actual {x.Actual}")
            .ToList();

        mismatches.Should().BeEmpty("an engine file was edited, or its pin is stale");
    }
}
