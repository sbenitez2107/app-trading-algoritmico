using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 2a.3 (design D7) — the worker maps the runner's outcome to a job status. A fake runner and a
/// manual clock stand in for the engine and real time: no test sleeps.
/// </summary>
public class FtmoGroupSearchWorkerTests
{
    private static readonly FtmoGroupSearchRequest Request = new(MinMembers: 2, MaxMembers: 4);

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    private sealed class FakeRunner(Func<FtmoGroupSearchRequest, Action<FtmoGroupSearchProgressDto>, CancellationToken, Task<FtmoGroupSearchRunResult>> run)
        : IFtmoGroupSearchRunner
    {
        public int Calls { get; private set; }

        public Task<FtmoGroupSearchRunResult> RunAsync(FtmoGroupSearchRequest request, Action<FtmoGroupSearchProgressDto> report, CancellationToken ct)
        {
            Calls++;
            return run(request, report, ct);
        }
    }

    private static (FtmoGroupSearchJobRegistry Registry, FtmoGroupSearchWorker Worker, ManualClock Clock) Build(FakeRunner runner)
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var clock = new ManualClock();
        return (registry, new FtmoGroupSearchWorker(registry, runner, clock, NullLogger<FtmoGroupSearchWorker>.Instance), clock);
    }

    private static FtmoGroupSearchRowDto Row(int rank) => new(rank, [Guid.NewGuid()], ["A"], 1, false, true, 0.8m, [], []);

    private static FtmoGroupSearchRunResult WithRows(
        bool cancelled = false, FtmoGroupSearchStopReason stop = FtmoGroupSearchStopReason.None, int notComputed = 0)
        => FtmoGroupSearchJobRegistryTests.Result(stop, cancelled) with { Rows = [Row(1), Row(2)], NotComputed = notComputed };

    [Fact]
    public async Task ProcessAsync_RunnerCompletes_JobIsCompleted_WithTheRankedRows()
    {
        var (registry, worker, _) = Build(new FakeRunner((_, _, _) => Task.FromResult(WithRows())));
        var id = registry.TryStart(Request).JobId;

        await worker.ProcessAsync(id, CancellationToken.None);

        var job = registry.Get(id)!;
        job.Status.Should().Be(FtmoGroupSearchStatus.Completed);
        job.StopReason.Should().Be(FtmoGroupSearchStopReason.None);
        job.Rows.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(FtmoGroupSearchStopReason.MaxFullSimulations)]
    [InlineData(FtmoGroupSearchStopReason.WallClock)]
    public async Task ProcessAsync_BudgetStop_IsStoppedAtBudget_NamingTheLimit_AndTheNotComputedCount(FtmoGroupSearchStopReason limit)
    {
        var (registry, worker, _) = Build(new FakeRunner((_, _, _) => Task.FromResult(WithRows(stop: limit, notComputed: 40))));
        var id = registry.TryStart(Request).JobId;

        await worker.ProcessAsync(id, CancellationToken.None);

        var job = registry.Get(id)!;
        job.Status.Should().Be(FtmoGroupSearchStatus.StoppedAtBudget);
        job.StopReason.Should().Be(limit);
        job.NotComputed.Should().Be(40);
        job.Rows.Should().HaveCount(2, "the ranking covers what was evaluated");
    }

    [Fact]
    public async Task ProcessAsync_EngineCancelledFlag_MapsToCancelled_WithThePartialRows()
    {
        var (registry, worker, _) = Build(new FakeRunner((_, _, _) => Task.FromResult(WithRows(cancelled: true, notComputed: 90))));
        var id = registry.TryStart(Request).JobId;

        await worker.ProcessAsync(id, CancellationToken.None);

        var job = registry.Get(id)!;
        job.Status.Should().Be(FtmoGroupSearchStatus.Cancelled);
        job.Rows.Should().HaveCount(2);
        job.NotComputed.Should().Be(90);
    }

    [Fact]
    public async Task ProcessAsync_CancelThroughTheRegistry_ReachesTheRunnerAsAFiredToken_AndEndsCancelled()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner(async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return WithRows();
        });
        var (registry, worker, _) = Build(runner);
        var id = registry.TryStart(Request).JobId;

        var processing = worker.ProcessAsync(id, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        registry.Cancel(id);
        await processing.WaitAsync(TimeSpan.FromSeconds(10));

        registry.Get(id)!.Status.Should().Be(FtmoGroupSearchStatus.Cancelled);
    }

    [Fact]
    public async Task ProcessAsync_CancelBeforeTheWorkerClaims_EndsCancelled_WithoutRunningTheRunner()
    {
        var runner = new FakeRunner((_, _, _) => Task.FromResult(WithRows()));
        var (registry, worker, _) = Build(runner);
        var id = registry.TryStart(Request).JobId;
        registry.Cancel(id);

        await worker.ProcessAsync(id, CancellationToken.None);

        runner.Calls.Should().Be(0);
        registry.Get(id)!.Status.Should().Be(FtmoGroupSearchStatus.Cancelled);
    }

    [Fact]
    public async Task ProcessAsync_RunnerThrows_JobFails_WithoutLeakingTheException_AndTheWorkerKeepsServing()
    {
        var calls = 0;
        var runner = new FakeRunner((_, _, _) => ++calls == 1
            ? throw new InvalidOperationException("connection string secret")
            : Task.FromResult(WithRows()));
        var (registry, worker, _) = Build(runner);
        var first = registry.TryStart(Request).JobId;

        await worker.ProcessAsync(first, CancellationToken.None);

        var failed = registry.Get(first)!;
        failed.Status.Should().Be(FtmoGroupSearchStatus.Failed);
        failed.ErrorMessage.Should().NotBeNullOrWhiteSpace().And.NotContain("secret");

        var second = registry.TryStart(Request).JobId;
        await worker.ProcessAsync(second, CancellationToken.None);
        registry.Get(second)!.Status.Should().Be(FtmoGroupSearchStatus.Completed);
    }

    [Fact]
    public async Task ProcessAsync_StoppingTokenFires_CancelsTheRunningJob()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner(async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return WithRows();
        });
        var (registry, worker, _) = Build(runner);
        var id = registry.TryStart(Request).JobId;
        using var stopping = new CancellationTokenSource();

        var processing = worker.ProcessAsync(id, stopping.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        stopping.Cancel();
        await processing.WaitAsync(TimeSpan.FromSeconds(10));

        registry.Get(id)!.Status.Should().Be(FtmoGroupSearchStatus.Cancelled);
    }

    [Fact]
    public async Task ProcessAsync_StampsTheElapsedTime_FromTheInjectedClock_OnEveryProgressSnapshot()
    {
        var clock = new ManualClock();
        var registry = new FtmoGroupSearchJobRegistry();
        var runner = new FakeRunner((_, report, _) =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            report(FtmoGroupSearchJobRegistryTests.Progress(done: 4, elapsedMs: 0));
            return Task.FromResult(WithRows());
        });
        var worker = new FtmoGroupSearchWorker(registry, runner, clock, NullLogger<FtmoGroupSearchWorker>.Instance);
        var id = registry.TryStart(Request).JobId;

        await worker.ProcessAsync(id, CancellationToken.None);

        registry.Get(id)!.Progress.ElapsedMs.Should().Be(1500);
    }

    [Fact]
    public async Task ExecuteAsync_ServesAQueuedJob_ThenStopsCleanlyOnShutdown()
    {
        var (registry, worker, _) = Build(new FakeRunner((_, _, _) => Task.FromResult(WithRows())));

        await worker.StartAsync(CancellationToken.None);
        var id = registry.TryStart(Request).JobId;
        await registry.WhenFinishedAsync(id);
        await worker.StopAsync(CancellationToken.None);

        registry.Get(id)!.Status.Should().Be(FtmoGroupSearchStatus.Completed);
    }
}
