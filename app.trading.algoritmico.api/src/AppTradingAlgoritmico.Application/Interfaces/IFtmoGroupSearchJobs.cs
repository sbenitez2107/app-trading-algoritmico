using AppTradingAlgoritmico.Application.DTOs.Backtests;

namespace AppTradingAlgoritmico.Application.Interfaces;

/// <param name="Started">False when a job is already running: nothing was started and <paramref name="JobId"/> is the running job's id (409).</param>
public sealed record FtmoGroupSearchStartResult(bool Started, Guid JobId);

/// <summary>ftmo-group-search D7 — the single in-memory job: start, poll, cancel. Jobs are lost on API restart.</summary>
public interface IFtmoGroupSearchJobs
{
    FtmoGroupSearchStartResult TryStart(FtmoGroupSearchRequest request);

    /// <summary>Null for an unknown (or lost) id.</summary>
    FtmoGroupSearchJobDto? Get(Guid jobId);

    /// <summary>The running job, or the last terminal one; null when none has been started since the API began.</summary>
    FtmoGroupSearchJobDto? GetCurrent();

    /// <summary>Requests cancellation. False for an unknown id; a no-op (true) on a terminal job.</summary>
    bool Cancel(Guid jobId);
}

/// <summary>
/// Runs one search to its end. Reports progress through <paramref name="report"/> (the worker stamps the elapsed
/// time) and honours <paramref name="ct"/>: on a cancel it returns the partial result flagged <c>Cancelled</c>.
/// </summary>
public interface IFtmoGroupSearchRunner
{
    Task<FtmoGroupSearchRunResult> RunAsync(
        FtmoGroupSearchRequest request, Action<FtmoGroupSearchProgressDto> report, CancellationToken ct);
}
