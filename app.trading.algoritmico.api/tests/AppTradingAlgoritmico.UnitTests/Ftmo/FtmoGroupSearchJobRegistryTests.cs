using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>ftmo-group-search 2a.2 (design D7) — the single in-memory job: atomic start, immutable monotonic snapshots, retention.</summary>
public class FtmoGroupSearchJobRegistryTests
{
    private static readonly FtmoGroupSearchRequest Request = new(MinMembers: 2, MaxMembers: 4);
    private static readonly FtmoGroupSearchFunnelDto NoFunnel = new(0, 0, 0, 0, 0, 0, 0);

    internal static FtmoGroupSearchProgressDto Progress(
        FtmoGroupSearchStage stage = FtmoGroupSearchStage.Simulating, int done = 0, long elapsedMs = 0, FtmoGroupSearchFunnelDto? funnel = null)
        => new(stage, done, 150, elapsedMs, done, 150, funnel ?? NoFunnel);

    internal static FtmoGroupSearchRunResult Result(
        FtmoGroupSearchStopReason stop = FtmoGroupSearchStopReason.None, bool cancelled = false, int examined = 12926)
        => new(cancelled, stop, 0, 150, new FtmoGroupSearchFunnelDto(examined, 0, 0, 0, 0, 0, 150), [], []);

    [Fact]
    public void TryStart_WhenIdle_StartsARunningJob_ReadableById_AndAsCurrent()
    {
        var registry = new FtmoGroupSearchJobRegistry();

        var start = registry.TryStart(Request);

        start.Started.Should().BeTrue();
        registry.Get(start.JobId)!.Status.Should().Be(FtmoGroupSearchStatus.Running);
        registry.GetCurrent()!.JobId.Should().Be(start.JobId);
    }

    private static readonly FtmoGroupSearchRequest Full = new(
        TradingAccountId: Guid.NewGuid(), MinMembers: 2, MaxMembers: 4, MaxPerInstrument: 2,
        IncludeIdenticalDeployEval: true, OnePercentRule: true, EliminationCeiling: 0.07m, Broker: "FTMO",
        InitialCapital: 10000m, TargetRiskPerTrade: 100m, FxLow: 0.9m, FxHigh: 1.1m, SizeDecimals: 2, Step: 0.01m,
        MinLot: 0.01m, MaxLots: 100m, MaxFullSimulations: 50, MaxWallClockSeconds: 120);

    [Fact]
    public void Job_CarriesTheSubmittedRequest_OnGetGetCurrentAndAfterProgressAndFinish()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var id = registry.TryStart(Full).JobId;

        registry.Get(id)!.Request.Should().Be(Full);
        registry.GetCurrent()!.Request.Should().Be(Full);

        registry.Publish(id, Progress(done: 3));
        registry.GetCurrent()!.Request.Should().Be(Full);

        registry.Finish(id, FtmoGroupSearchStatus.Completed, Result(), null);
        registry.GetCurrent()!.Request.TargetRiskPerTrade.Should().Be(100m);
        registry.GetCurrent()!.Request.InitialCapital.Should().Be(10000m);
        registry.GetCurrent()!.Request.EliminationCeiling.Should().Be(0.07m);
    }

    [Fact]
    public void TryStart_WhileRunning_IsRefused_CarryingTheRunningId_AndNoSecondJobExists()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var first = registry.TryStart(Request);

        var second = registry.TryStart(Request);

        second.Started.Should().BeFalse();
        second.JobId.Should().Be(first.JobId);
        registry.GetCurrent()!.JobId.Should().Be(first.JobId);
    }

    [Fact]
    public void TryStart_UnderConcurrentCallers_ExactlyOneWins()
    {
        for (var round = 0; round < 200; round++)
        {
            var registry = new FtmoGroupSearchJobRegistry();
            using var gate = new ManualResetEventSlim(false);
            var tasks = Enumerable.Range(0, 16).Select(_ => Task.Factory.StartNew(
                () =>
                {
                    gate.Wait();
                    return registry.TryStart(Request);
                },
                TaskCreationOptions.LongRunning)).ToArray();

            gate.Set();
            var results = tasks.Select(t => t.GetAwaiter().GetResult()).ToList();

            results.Count(r => r.Started).Should().Be(1, $"round {round}");
            results.Select(r => r.JobId).Distinct().Should().HaveCount(1, "every refusal carries the winner's id");
        }
    }

    [Theory]
    [InlineData(FtmoGroupSearchStatus.Completed)]
    [InlineData(FtmoGroupSearchStatus.StoppedAtBudget)]
    [InlineData(FtmoGroupSearchStatus.Cancelled)]
    [InlineData(FtmoGroupSearchStatus.Failed)]
    public void TryStart_AfterATerminalJob_IsAccepted_AndReplacesTheRetainedJob(FtmoGroupSearchStatus terminal)
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var first = registry.TryStart(Request);
        registry.Finish(first.JobId, terminal, Result(), null);

        var second = registry.TryStart(Request);

        second.Started.Should().BeTrue();
        second.JobId.Should().NotBe(first.JobId);
        registry.Get(first.JobId).Should().BeNull("only the last job is retained");
    }

    [Fact]
    public void Finish_RetainsTheLastTerminalJob_WithItsResult_UntilTheNextStart()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var start = registry.TryStart(Request);

        registry.Finish(start.JobId, FtmoGroupSearchStatus.Completed, Result(), null);

        var job = registry.GetCurrent()!;
        job.Status.Should().Be(FtmoGroupSearchStatus.Completed);
        job.StopReason.Should().Be(FtmoGroupSearchStopReason.None);
        registry.Get(start.JobId).Should().BeSameAs(job);
    }

    [Fact]
    public void Publish_IsMonotonic_NeverMovesBackwards_AndSnapshotsAreImmutable()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var id = registry.TryStart(Request).JobId;

        registry.Publish(id, Progress(done: 5, elapsedMs: 900));
        var before = registry.Get(id)!;
        registry.Publish(id, Progress(done: 3, elapsedMs: 400));
        var after = registry.Get(id)!;

        before.Progress.FullSimulationsDone.Should().Be(5);
        after.Progress.FullSimulationsDone.Should().Be(5);
        after.Progress.ElapsedMs.Should().Be(900);
        before.Progress.Should().NotBeSameAs(after.Progress);

        registry.Publish(id, Progress(done: 7, elapsedMs: 1200));
        before.Progress.FullSimulationsDone.Should().Be(5, "a snapshot already handed out never changes");
        registry.Get(id)!.Progress.FullSimulationsDone.Should().Be(7);
    }

    [Fact]
    public void Publish_OnATerminalOrStaleJob_IsIgnored()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var id = registry.TryStart(Request).JobId;
        registry.Finish(id, FtmoGroupSearchStatus.Completed, Result(), null);

        registry.Publish(id, Progress(done: 99, elapsedMs: 9));
        registry.Publish(Guid.NewGuid(), Progress(done: 99));

        registry.Get(id)!.Progress.ElapsedMs.Should().Be(0, "a late publish must not touch the retained terminal job");
        registry.Get(id)!.Progress.FullSimulationsDone.Should().Be(150, "the final result's count stands");
    }

    [Fact]
    public void Cancel_OnARunningJob_FiresTheToken_AndKeepsRunningUntilTheWorkerFinishes()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var id = registry.TryStart(Request).JobId;
        var token = registry.Claim(id)!.Value.Token;

        registry.Cancel(id).Should().BeTrue();

        token.IsCancellationRequested.Should().BeTrue();
        registry.Get(id)!.Status.Should().Be(FtmoGroupSearchStatus.Running);
    }

    [Fact]
    public void Cancel_IsIdempotentOnATerminalJob_AndFalseForAnUnknownId()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var id = registry.TryStart(Request).JobId;
        registry.Finish(id, FtmoGroupSearchStatus.Completed, Result(), null);

        registry.Cancel(id).Should().BeTrue();
        registry.Cancel(id).Should().BeTrue();
        registry.Get(id)!.Status.Should().Be(FtmoGroupSearchStatus.Completed);
        registry.Cancel(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Get_UnknownId_AndGetCurrentBeforeAnyStart_AreNull()
    {
        var registry = new FtmoGroupSearchJobRegistry();

        registry.Get(Guid.NewGuid()).Should().BeNull();
        registry.GetCurrent().Should().BeNull();
    }

    [Fact]
    public async Task TryStart_QueuesTheJobId_ForTheWorker()
    {
        var registry = new FtmoGroupSearchJobRegistry();

        var id = registry.TryStart(Request).JobId;

        (await registry.Reader.ReadAsync()).Should().Be(id);
    }

    [Fact]
    public void Disclosures_ArePresentWhileRunning_AndAfterABudgetStop_WithTheExaminedCount()
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var id = registry.TryStart(Request).JobId;

        var running = registry.Get(id)!.Disclosures;
        running.Should().Contain(d => d.Contains("groups examined") && d.Contains("selection bias"));
        running.Should().Contain(d => d.Contains("closed-trade") && d.Contains("not a certification"));
        running.Should().Contain(d => d.Contains("lost when the API restarts"));
        running.Should().Contain(FtmoGroupSimulationLimits.Disclosures);

        registry.Publish(id, Progress(funnel: new FtmoGroupSearchFunnelDto(12926, 1, 2, 3, 4, 5, 150)));
        registry.Get(id)!.Disclosures.Should().Contain(d => d.StartsWith("12926 groups examined", StringComparison.Ordinal));

        registry.Finish(id, FtmoGroupSearchStatus.StoppedAtBudget, Result(FtmoGroupSearchStopReason.WallClock), null);
        var stopped = registry.Get(id)!;
        stopped.Disclosures.Should().Contain(d => d.StartsWith("12926 groups examined", StringComparison.Ordinal) && d.Contains("150 fully simulated"));
        stopped.Disclosures.Should().Contain(d => d.Contains("lost when the API restarts"));
    }
}
