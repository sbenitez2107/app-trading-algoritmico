using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Phase 3 — the group risk read path end to end: request validation, run selection over persisted
/// rows, the first production wiring of slice 2a's <c>TryNormalize</c>/<c>Resize</c>, and the
/// analytics adapters PR2 shipped.
/// <para>
/// The two <c>Unknown</c> refusals are DIFFERENT RULES with different reasoning and are asserted
/// separately: a request field that is null cannot express "required input" on a non-nullable enum
/// whose default is <c>Unknown</c> (a binding fact), while a request FOR <c>Unknown</c> asks for a
/// figure whose label asserts something the parser explicitly declined to classify (an evidence
/// fact). Collapsing them into one test would lose the second reason.
/// </para>
/// </summary>
public class BacktestGroupRiskAnalysisTests
{
    private const decimal Capital = 10_000m;

    /// <summary>The IST export's own Â. At this target the resizer is the identity, so every net is the source Profit verbatim.</summary>
    private const decimal IstEstimate = 199.98m;

    private DbContextOptions<AppDbContext> _options = default!;

    private AppDbContext CreateDb()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(_options);
    }

    private static BacktestReadService CreateSut(AppDbContext db) => new(db);

    private static GroupRiskAnalysisRequest Request(
        Guid[] strategyIds,
        BacktestSegment? segment = BacktestSegment.InSampleTest,
        BacktestRunKind? runKind = null,
        decimal target = IstEstimate,
        string? fundingService = null)
        => new(
            StrategyIds: strategyIds,
            InitialCapital: Capital,
            TargetRiskPerTrade: target,
            Segment: segment,
            RunKind: runKind,
            FundingService: fundingService);

    private static async Task<Guid> SeedStrategyAsync(AppDbContext db, string name)
    {
        var strategy = new Strategy { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return strategy.Id;
    }

    /// <summary>Seeds one run slot for a strategy and fills it from a committed fixture.</summary>
    private static async Task<Guid> SeedRunAsync(
        AppDbContext db, Guid strategyId, BacktestRunKind kind, string fixtureFile)
    {
        var runId = await SeedEmptyRunAsync(db, strategyId, kind);
        db.BacktestTrades.AddRange(RawTradeListFixture.Load(fixtureFile, runId));
        await db.SaveChangesAsync();
        return runId;
    }

    private static async Task<Guid> SeedEmptyRunAsync(AppDbContext db, Guid strategyId, BacktestRunKind kind)
    {
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            SourceFileName = $"{kind}.csv",
            ContentHash = Guid.NewGuid().ToString("N"),
            StrategyId = strategyId,
            Kind = kind,
            Symbol = "XAUUSD_M1_UTC02",
            CreatedAt = DateTime.UtcNow,
        };
        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    /// <summary>Overwrites every trade's segment — the only way to reach states the parser forbids.</summary>
    private static async Task RelabelAsync(AppDbContext db, Guid runId, BacktestSegment segment)
    {
        var trades = await db.BacktestTrades.Where(t => t.BacktestRunId == runId).ToListAsync();
        foreach (var trade in trades) trade.Segment = segment;
        await db.SaveChangesAsync();
    }

    // =====================================================================
    // 3.1 / 3.2 — the two request-side refusals. Two rules, one outcome.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_WithNoSegmentSpecified_IsRefusedAndProducesNoFigure()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "IST strategy");
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId], segment: null), CancellationToken.None);

        result.Status.Should().Be(
            GroupRiskAnalysisStatus.SegmentNotSpecified,
            "without an explicit segment every figure would be silently in-sample; the field is "
            + "BacktestSegment? precisely so an omitted JSON property is distinguishable from Unknown");
        result.Risk.Should().BeNull();
        result.Correlation.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupRiskAnalysis_ForTheUnknownSegment_IsRefusedForADifferentReasonThanOmission()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "IST strategy");
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId], segment: BacktestSegment.Unknown), CancellationToken.None);

        result.Status.Should().Be(
            GroupRiskAnalysisStatus.UnknownSegmentNotSelectable,
            "publishing a figure labelled 'computed over the Unknown sample' is the same act as "
            + "publishing a 0.00 VaR — the label asserts something the data does not support");
        result.Status.Should().NotBe(
            GroupRiskAnalysisStatus.SegmentNotSpecified,
            "the caller who forgot and the caller who asked for Unknown must stay distinguishable");
        result.Risk.Should().BeNull();
    }

    // =====================================================================
    // 3.5 — the member-level no-evidence state, over persisted rows.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_WhenNoRunCarriesTheRequestedSegment_IsTheNoEvidenceState()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "IST strategy");
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId], segment: BacktestSegment.OutOfSample), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.NoEvidenceForSegment);
        result.Members.Should().ContainSingle()
            .Which.Status.Should().Be(GroupRiskMemberStatus.NoEvidenceForSegment);
        result.Risk.Should().BeNull("no evidence is a STATE, never an empty series that aggregates to 0");
    }

    [Fact]
    public async Task GetGroupRiskAnalysis_WithATradelessSecondRun_StillResolvesFromTheRunThatMatches()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "half-populated strategy");
        await SeedEmptyRunAsync(db, strategyId, BacktestRunKind.Deploy);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Evaluation, RawTradeListFixture.IstFileName);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId]), CancellationToken.None);

        result.Status.Should().Be(
            GroupRiskAnalysisStatus.Completed,
            "one slot imported and the other not yet is the normal mid-import state of the "
            + "two-row constraint, and it is NON-FATAL to the member");
        result.Members.Should().ContainSingle()
            .Which.RunKind.Should().Be(BacktestRunKind.Evaluation);
    }

    // =====================================================================
    // 3.3 — a run whose trades disagree, reached only by editing the store.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_WithARunWhoseTradesDisagree_RefusesNamingTheRun()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "hand-edited strategy");
        var runId = await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);

        var oneTrade = await db.BacktestTrades.FirstAsync(t => t.BacktestRunId == runId);
        oneTrade.Segment = BacktestSegment.OutOfSample;
        await db.SaveChangesAsync();

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId]), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.RunSegmentsDisagree);
        result.Members.Should().ContainSingle().Which.Detail.Should().Contain(
            runId.ToString(), "the refusal has to NAME the run whose rows were edited");
        result.Risk.Should().BeNull();
    }

    // =====================================================================
    // 3.4 — a genuinely Unknown run is never selected. RUN-side rule.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_WithARunLabelledUnknown_NeverSelectsItForAMeaningfulSegment()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "unclassified strategy");
        var runId = await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);
        await RelabelAsync(db, runId, BacktestSegment.Unknown);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId], segment: BacktestSegment.InSampleTest), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.NoEvidenceForSegment);
        result.Risk.Should().BeNull();
    }

    // =====================================================================
    // 3.6 — the anti-shortcut row, over persisted rows this time.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_DeployRunIsInSampleTest_SelectsItWithoutConsultingKind()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "AlgoWizard full-period export");
        var deployRunId = await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);
        var evaluationRunId = await SeedRunAsync(db, strategyId, BacktestRunKind.Evaluation, RawTradeListFixture.IstFileName);
        await RelabelAsync(db, evaluationRunId, BacktestSegment.OutOfSample);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId], segment: BacktestSegment.InSampleTest), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.Completed);
        var member = result.Members.Should().ContainSingle().Subject;
        member.RunId.Should().Be(
            deployRunId,
            "a Deploy run's trades CAN be InSampleTest — that is the committed IST fixture. "
            + "A Kind-based shortcut would have picked the Evaluation run here");
        member.RunKind.Should().Be(BacktestRunKind.Deploy);
        result.Segment.Should().Be(BacktestSegment.InSampleTest);

        // And the mirror: requesting OutOfSample picks the Evaluation run, from the same store.
        var mirror = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId], segment: BacktestSegment.OutOfSample), CancellationToken.None);
        mirror.Members.Should().ContainSingle().Which.RunId.Should().Be(evaluationRunId);
    }

    // =====================================================================
    // 3.7 — both runs match: refused, and the disambiguator resolves it.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_WhenBothRunsCarryTheSegment_RefusesNamingTheStrategyAndBothKinds()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "Ambiguous strategy");
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);
        await SeedRunAsync(db, strategyId, BacktestRunKind.Evaluation, RawTradeListFixture.IstFileName);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId]), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.AmbiguousRunSelection);
        var member = result.Members.Should().ContainSingle().Subject;
        member.Label.Should().Be("Ambiguous strategy");
        member.Detail.Should().Contain("Deploy").And.Contain("Evaluation");
        result.Risk.Should().BeNull("picking either would make the published figure depend on an arbitrary choice");
    }

    [Fact]
    public async Task GetGroupRiskAnalysis_WhenBothRunsCarryTheSegmentAndARunKindIsGiven_Resolves()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "Ambiguous strategy");
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);
        var evaluationRunId = await SeedRunAsync(db, strategyId, BacktestRunKind.Evaluation, RawTradeListFixture.IstFileName);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId], runKind: BacktestRunKind.Evaluation), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.Completed);
        result.Members.Should().ContainSingle().Which.RunId.Should().Be(evaluationRunId);
    }

    // =====================================================================
    // The figures themselves — the read path must reproduce PR2's anchors.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_OverTheIstFixture_WithholdsDailyVar95AndPublishesTheMonthlyFigure()
    {
        await using var db = CreateDb();
        var strategyId = await SeedStrategyAsync(db, "IST strategy");
        await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([strategyId]), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.Completed);
        var risk = result.Risk!;

        risk.Segment.Should().Be(BacktestSegment.InSampleTest);
        risk.WindowDays.Should().Be(0, "no trim — the gate is evaluated over the whole stated sample");
        risk.ObservationDays.Should().Be(3860);
        risk.Density.TradeCount.Should().Be(329);
        risk.Density.NegativeDayCount.Should().Be(164);
        risk.Density.NonZeroDayCount.Should().Be(318);

        risk.DailyVar95.Should().BeNull("164 negative days is short of the 193 the read index needs");
        risk.DailyVar95Withheld.Should().Be(VarWithholdReason.InsufficientNegativeObservations);
        risk.DailyVar99.Should().Be(199.44229999999999988m, "interpolated between sorted[38] and sorted[39]");
        risk.MonthlyVar95.Should().Be(400.19m, "published as a positive loss magnitude");

        result.Correlation!.Alignment.Should().Be("Intersection");
        result.Correlation.Segment.Should().Be(BacktestSegment.InSampleTest);
    }

    [Fact]
    public async Task GetGroupRiskAnalysis_OverAThreeMemberGroup_LabelsEveryMemberInRequestOrder()
    {
        await using var db = CreateDb();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var strategyId = await SeedStrategyAsync(db, $"S{i}");
            await SeedRunAsync(db, strategyId, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);
            ids.Add(strategyId);
        }

        var result = await CreateSut(db).GetGroupRiskAnalysisAsync(
            Request([.. ids]), CancellationToken.None);

        result.Status.Should().Be(GroupRiskAnalysisStatus.Completed);
        result.Members.Should().HaveCount(3);
        result.Correlation!.Labels.Should().Equal("S0", "S1", "S2");

        // Three identical members: every off-diagonal cell is a perfect correlation, and none is
        // withheld — the co-active intersection is the whole series.
        result.Correlation.WithheldCellCount.Should().Be(0);
    }

    // =====================================================================
    // 3.8 — the heterogeneous-group refusal.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_WhenMembersSelectedRunsDisagreeOnSegment_RefusesNamingThem()
    {
        // Reached through the read service's own refusal helper rather than through a request,
        // because exact-match selection cannot produce it: every selected run carries the ONE
        // requested segment. This asserts the user-facing refusal content the design owes, and the
        // gap is recorded in the PR notes.
        var refusal = BacktestReadService.DescribeSegmentDisagreement(
        [
            ("Alpha", BacktestSegment.InSampleTest),
            ("Beta", BacktestSegment.OutOfSample),
        ]);

        refusal.Should().NotBeNull();
        refusal.Should().Contain("Alpha").And.Contain("InSampleTest");
        refusal.Should().Contain("Beta").And.Contain("OutOfSample");
    }

    [Fact]
    public void DescribeSegmentDisagreement_WhenEveryMemberAgrees_IsNull()
        => BacktestReadService.DescribeSegmentDisagreement(
        [
            ("Alpha", BacktestSegment.InSampleTest),
            ("Beta", BacktestSegment.InSampleTest),
        ]).Should().BeNull();

    // =====================================================================
    // 4.1 — determinism (analytics R10). Same inputs, byte-identical output.
    // =====================================================================

    [Fact]
    public async Task GetGroupRiskAnalysis_CalledTwiceOnUnchangedInputs_ReturnsByteIdenticalPayloads()
    {
        await using var db = CreateDb();
        var first = await SeedStrategyAsync(db, "Alpha");
        await SeedRunAsync(db, first, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);
        var second = await SeedStrategyAsync(db, "Beta");
        await SeedRunAsync(db, second, BacktestRunKind.Deploy, RawTradeListFixture.IstFileName);

        var request = Request([first, second]);
        var a = await CreateSut(db).GetGroupRiskAnalysisAsync(request, CancellationToken.None);
        var b = await CreateSut(db).GetGroupRiskAnalysisAsync(request, CancellationToken.None);

        a.Status.Should().Be(GroupRiskAnalysisStatus.Completed);
        System.Text.Json.JsonSerializer.Serialize(a)
            .Should().Be(System.Text.Json.JsonSerializer.Serialize(b),
                "no RNG, no seed, and every ordering is explicit — the whole slice is deterministic");
    }
}
