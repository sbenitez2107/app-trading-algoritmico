using System.Threading.Channels;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D7 — the in-memory job registry (singleton). At most ONE job: <see cref="TryStart"/> is atomic
/// under a lock, and a start while a job runs is refused with the running id (409 for the controller). Progress is an
/// immutable snapshot published with <see cref="Volatile"/>, so a poll never takes the lock and never sees a half-written
/// state. The last job, terminal included, is retained until the next start; nothing is persisted.
/// </summary>
internal sealed class FtmoGroupSearchJobRegistry : IFtmoGroupSearchJobs
{
    private static readonly FtmoGroupSearchFunnelDto EmptyFunnel = new(0, 0, 0, 0, 0, 0, 0);

    private readonly object _gate = new();
    private readonly Channel<Guid> _queue = Channel.CreateBounded<Guid>(1);
    private Job? _current;

    private sealed class Job(Guid id, FtmoGroupSearchRequest request)
    {
        private FtmoGroupSearchJobDto _snapshot = null!;

        internal Guid Id { get; } = id;

        internal FtmoGroupSearchRequest Request { get; } = request;

        internal CancellationTokenSource Cts { get; } = new();

        internal TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool Terminal { get; set; }

        internal FtmoGroupSearchJobDto Snapshot
        {
            get => Volatile.Read(ref _snapshot);
            set => Volatile.Write(ref _snapshot, value);
        }
    }

    /// <summary>The worker's queue: one slot, because only one job exists at a time.</summary>
    internal ChannelReader<Guid> Reader => _queue.Reader;

    public FtmoGroupSearchStartResult TryStart(FtmoGroupSearchRequest request)
    {
        lock (_gate)
        {
            if (_current is { Terminal: false })
                return new FtmoGroupSearchStartResult(false, _current.Id);

            var id = Guid.NewGuid();
            var job = new Job(id, request)
            {
                Snapshot = Build(id, request, FtmoGroupSearchStatus.Running, Progress(FtmoGroupSearchStage.Loading, EmptyFunnel), null, null),
            };
            _queue.Writer.TryWrite(job.Id);
            _current = job;
            return new FtmoGroupSearchStartResult(true, job.Id);
        }
    }

    public FtmoGroupSearchJobDto? Get(Guid jobId) => Volatile.Read(ref _current) is { } job && job.Id == jobId ? job.Snapshot : null;

    public FtmoGroupSearchJobDto? GetCurrent() => Volatile.Read(ref _current)?.Snapshot;

    public bool Cancel(Guid jobId)
    {
        lock (_gate)
        {
            if (_current is not { } job || job.Id != jobId)
                return false;

            if (!job.Terminal)
                job.Cts.Cancel();

            return true;
        }
    }

    /// <summary>The worker takes the job: its request and the job's own cancel token. Null when the id is not the current job.</summary>
    internal (FtmoGroupSearchRequest Request, CancellationToken Token)? Claim(Guid jobId)
    {
        lock (_gate)
            return _current is { Terminal: false } job && job.Id == jobId ? (job.Request, job.Cts.Token) : null;
    }

    /// <summary>Publishes a snapshot. Monotonic: done count and elapsed time never decrease; ignored once terminal.</summary>
    internal void Publish(Guid jobId, FtmoGroupSearchProgressDto progress)
    {
        lock (_gate)
        {
            if (_current is not { Terminal: false } job || job.Id != jobId)
                return;

            var last = job.Snapshot.Progress;
            var next = progress with
            {
                FullSimulationsDone = Math.Max(last.FullSimulationsDone, progress.FullSimulationsDone),
                ElapsedMs = Math.Max(last.ElapsedMs, progress.ElapsedMs),
            };
            job.Snapshot = Build(job.Id, job.Request, FtmoGroupSearchStatus.Running, next, null, null);
        }
    }

    /// <summary>Ends the job in a terminal status and keeps it as the retained last job.</summary>
    internal void Finish(Guid jobId, FtmoGroupSearchStatus status, FtmoGroupSearchRunResult? run, string? error)
    {
        lock (_gate)
        {
            if (_current is not { Terminal: false } job || job.Id != jobId)
                return;

            var progress = job.Snapshot.Progress;
            if (run is not null)
                progress = progress with { FullSimulationsDone = run.FullySimulated, Funnel = run.Funnel };

            job.Snapshot = Build(job.Id, job.Request, status, progress, run, error);
            job.Terminal = true;
            job.Cts.Dispose();
            job.Done.TrySetResult();
        }
    }

    /// <summary>
    /// Ends the current job, whatever its id, if it is not terminal yet: for the worker when its loop exits. Returns the
    /// id it ended, or null when there was nothing to end.
    /// </summary>
    internal Guid? FinishCurrent(FtmoGroupSearchStatus status, string? error)
    {
        lock (_gate)
        {
            if (_current is not { Terminal: false } job)
                return null;

            Finish(job.Id, status, null, error);
            return job.Id;
        }
    }

    /// <summary>Completes when the job reaches a terminal status; for the worker's tests and shutdown.</summary>
    internal Task WhenFinishedAsync(Guid jobId)
    {
        lock (_gate)
            return _current is { } job && job.Id == jobId ? job.Done.Task : Task.CompletedTask;
    }

    private static FtmoGroupSearchProgressDto Progress(FtmoGroupSearchStage stage, FtmoGroupSearchFunnelDto funnel)
        => new(stage, 0, 0, 0, 0, 0, funnel);

    private static FtmoGroupSearchJobDto Build(
        Guid id, FtmoGroupSearchRequest request, FtmoGroupSearchStatus status, FtmoGroupSearchProgressDto progress,
        FtmoGroupSearchRunResult? run, string? error)
        => new(
            id, status, progress,
            run?.StopReason ?? FtmoGroupSearchStopReason.None,
            run?.NotComputed ?? 0,
            run?.Ineligible ?? [],
            run?.Rows ?? [],
            FtmoGroupSearchDisclosures.Build(
                progress.Funnel.Examined, progress.Funnel.Shortlisted, progress.FullSimulationsDone, FtmoGroupSimulationLimits.Disclosures),
            error,
            request);
}
