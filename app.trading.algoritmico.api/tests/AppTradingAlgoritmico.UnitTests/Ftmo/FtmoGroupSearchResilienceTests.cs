using System.Diagnostics;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.UnitTests.Ftmo.FtmoGroupSearchFixtures;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 2a review correction (RESILIENCE-001, -002, -005): a worker loop that exits never strands a job,
/// the proxy pass honours the job token, and every job outcome is logged.
/// </summary>
public class FtmoGroupSearchResilienceTests
{
    private static readonly FtmoGroupSearchRequest Request = new(MinMembers: 2, MaxMembers: 3);

    private sealed class FakeRunner(Func<CancellationToken, Task<FtmoGroupSearchRunResult>> run) : IFtmoGroupSearchRunner
    {
        public int Calls { get; private set; }

        public Task<FtmoGroupSearchRunResult> RunAsync(FtmoGroupSearchRequest request, Action<FtmoGroupSearchProgressDto> report, CancellationToken ct)
        {
            Calls++;
            return run(ct);
        }
    }

    internal sealed class CapturingLogger<T>(Func<string, bool>? throwWhen = null) : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            lock (Entries)
                Entries.Add((logLevel, message, exception));
            if (throwWhen?.Invoke(message) == true)
                throw new InvalidOperationException("logging sink is down");
        }
    }

    private static FtmoGroupSearchRowDto Row(int rank) => new(rank, [Guid.NewGuid()], ["A"], 1, false, true, 0.8m, [], []);

    private static FtmoGroupSearchRunResult Result(FtmoGroupSearchStopReason stop = FtmoGroupSearchStopReason.None, bool cancelled = false)
        => FtmoGroupSearchJobRegistryTests.Result(stop, cancelled, examined: 321) with { Rows = [Row(1), Row(2)] };

    private static (FtmoGroupSearchJobRegistry Registry, FtmoGroupSearchWorker Worker, CapturingLogger<FtmoGroupSearchWorker> Log) Build(
        IFtmoGroupSearchRunner runner, Func<string, bool>? throwWhen = null)
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var log = new CapturingLogger<FtmoGroupSearchWorker>(throwWhen);
        return (registry, new FtmoGroupSearchWorker(registry, runner, TimeProvider.System, log), log);
    }

    // ---- RESILIENCE-001: the worker loop exiting never strands a job ----

    [Fact]
    public async Task AQueuedJob_WhenStoppingIsRequested_EndsCancelled_AndANewStartIsAccepted()
    {
        var runner = new FakeRunner(_ => Task.FromResult(Result()));
        var (registry, worker, _) = Build(runner);
        var id = registry.TryStart(Request).JobId;
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();

        await worker.ServeAsync(stopping.Token);

        await registry.WhenFinishedAsync(id).WaitAsync(TimeSpan.FromSeconds(10));
        registry.Get(id)!.Status.Should().Be(FtmoGroupSearchStatus.Cancelled);
        runner.Calls.Should().Be(0);
        registry.TryStart(Request).Started.Should().BeTrue("a stranded job would keep refusing new starts (409)");
    }

    [Fact]
    public async Task AnUnexpectedWorkerLoopFault_FailsTheJob_AndRethrows()
    {
        // The job is claimed, then the start log itself throws: the exception escapes ProcessAsync before any Finish
        // and ends the loop with the job still Running.
        var (registry, worker, _) = Build(new FakeRunner(_ => Task.FromResult(Result())), throwWhen: m => m.Contains("started", StringComparison.Ordinal));
        var id = registry.TryStart(Request).JobId;

        var act = () => worker.ServeAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await registry.WhenFinishedAsync(id).WaitAsync(TimeSpan.FromSeconds(10));
        var job = registry.Get(id)!;
        job.Status.Should().Be(FtmoGroupSearchStatus.Failed);
        job.ErrorMessage.Should().Be(FtmoGroupSearchWorker.FailedMessage);
    }

    // ---- RESILIENCE-005: structured logs on every path ----

    private static async Task<List<(LogLevel Level, string Message, Exception? Exception)>> LogsOfAsync(IFtmoGroupSearchRunner runner, bool cancelFirst = false)
    {
        var (registry, worker, log) = Build(runner);
        var id = registry.TryStart(Request).JobId;
        if (cancelFirst)
            registry.Cancel(id);
        await worker.ProcessAsync(id, CancellationToken.None);
        return log.Entries;
    }

    [Fact]
    public async Task ACompletedJob_LogsItsStart_AndItsCompletion_WithStatusExaminedRowsAndDuration()
    {
        var logs = await LogsOfAsync(new FakeRunner(_ => Task.FromResult(Result())));

        logs.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("started", StringComparison.Ordinal));
        logs.Should().Contain(e => e.Level == LogLevel.Information
            && e.Message.Contains("Completed", StringComparison.Ordinal) && e.Message.Contains("321", StringComparison.Ordinal)
            && e.Message.Contains("2 rows", StringComparison.Ordinal) && e.Message.Contains("ms", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ABudgetStop_IsLoggedAsStoppedAtBudget_NamingTheLimit()
    {
        var logs = await LogsOfAsync(new FakeRunner(_ => Task.FromResult(Result(FtmoGroupSearchStopReason.WallClock))));

        logs.Should().Contain(e => e.Message.Contains("StoppedAtBudget", StringComparison.Ordinal) && e.Message.Contains("WallClock", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACancel_IsLogged()
    {
        var logs = await LogsOfAsync(new FakeRunner(_ => Task.FromResult(Result())), cancelFirst: true);

        logs.Should().Contain(e => e.Message.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ARefusal_IsLoggedWithItsMessage()
    {
        var logs = await LogsOfAsync(new FakeRunner(_ => throw new FtmoGroupSearchRefusedException("The pool has 30 eligible strategies")));

        logs.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("The pool has 30 eligible strategies", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailure_IsLoggedAsAnErrorWithTheException()
    {
        var logs = await LogsOfAsync(new FakeRunner(_ => throw new InvalidOperationException("boom")));

        logs.Should().Contain(e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    // ---- RESILIENCE-002: the proxy pass honours the job token ----

    private static readonly Strat A = Distinct(1, "A", [Trade(0, At(1, 5, 9), At(1, 5, 10), -50m), Trade(1, At(6, 5, 9), At(6, 5, 10), 10m)]);
    private static readonly Strat B = Distinct(2, "B", [Trade(0, At(1, 6, 9), At(1, 6, 10), 10m), Trade(1, At(6, 6, 9), At(6, 6, 10), -20m)]);

    [Fact]
    public void ProxyAll_WithACancelledToken_Throws_WithoutRunningThePass()
    {
        var cache = Cache(A, B);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => ProxyAll(cache, [Id(1), Id(2)], [[0, 1]], Params(), ct: cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void ProxyAll_CancelledMidPass_StopsPromptly()
    {
        var cache = Cache(A, B);
        int[] combo = [0, 1];
        var survivors = Enumerable.Repeat(combo, 5_000_000).ToArray();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var watch = Stopwatch.StartNew();

        var act = () => ProxyAll(cache, [Id(1), Id(2)], survivors, Params(), ct: cts.Token);

        act.Should().Throw<OperationCanceledException>();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the pass stops at the next iteration, not at its end");
    }

    [Fact]
    public void ProxyAll_WithALiveToken_GivesTheSameOutputAsWithout()
    {
        var cache = Cache(A, B);
        int[][] survivors = [[0, 1], [0, 1]];
        using var cts = new CancellationTokenSource();

        ProxyAll(cache, [Id(1), Id(2)], survivors, Params(), ct: cts.Token)
            .Should().Equal(ProxyAll(cache, [Id(1), Id(2)], survivors, Params(), maxDegreeOfParallelism: 1));
    }
}
