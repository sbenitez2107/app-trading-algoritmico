using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-group-search D7 — the background worker. It serves the registry's one-slot queue and maps the runner's
/// outcome to a job status: the engine's <c>Cancelled</c> flag to <see cref="FtmoGroupSearchStatus.Cancelled"/> (with the
/// partial rows), a budget stop to <see cref="FtmoGroupSearchStatus.StoppedAtBudget"/>, an exception to
/// <see cref="FtmoGroupSearchStatus.Failed"/> (the worker keeps serving). The runner receives a token linked to the
/// job's own cancel token AND to <c>stoppingToken</c>, so a shutdown cancels the job. Time comes from the injected
/// <see cref="TimeProvider"/>; the worker never sleeps. Leaving the page does not reach it: only an explicit cancel does.
/// </summary>
internal sealed class FtmoGroupSearchWorker(
    FtmoGroupSearchJobRegistry registry,
    IFtmoGroupSearchRunner runner,
    TimeProvider clock,
    ILogger<FtmoGroupSearchWorker> logger) : BackgroundService
{
    internal const string FailedMessage = "The search failed before it completed. Start a new search.";

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => ServeAsync(stoppingToken);

    /// <summary>
    /// The worker loop; <see cref="ExecuteAsync"/> is only this, so tests can drive the loop directly. Whatever ends the
    /// loop, a job still queued or running is made terminal so it never reports Running forever (and never blocks a new
    /// start): <see cref="FtmoGroupSearchStatus.Cancelled"/> on host stop, <see cref="FtmoGroupSearchStatus.Failed"/>
    /// on an unexpected fault, which is rethrown so the host still sees it.
    /// </summary>
    internal async Task ServeAsync(CancellationToken stoppingToken)
    {
        var faulted = false;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Guid id;
                try
                {
                    id = await registry.Reader.ReadAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                await ProcessAsync(id, stoppingToken);
            }
        }
        catch
        {
            faulted = true;
            throw;
        }
        finally
        {
            var abandoned = faulted
                ? registry.FinishCurrent(FtmoGroupSearchStatus.Failed, FailedMessage)
                : registry.FinishCurrent(FtmoGroupSearchStatus.Cancelled, null);
            if (abandoned is { } jobId)
                logger.LogWarning("FTMO group search job {JobId} ended {Status} because the worker stopped", jobId, faulted ? "Failed" : "Cancelled");
        }
    }

    internal async Task ProcessAsync(Guid jobId, CancellationToken stoppingToken)
    {
        if (registry.Claim(jobId) is not { } claim)
            return;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(claim.Token, stoppingToken);
        var started = clock.GetTimestamp();
        long ElapsedMs() => (long)clock.GetElapsedTime(started).TotalMilliseconds;

        logger.LogInformation("FTMO group search job {JobId} started", jobId);
        try
        {
            if (linked.IsCancellationRequested)
            {
                registry.Finish(jobId, FtmoGroupSearchStatus.Cancelled, null, null);
                logger.LogInformation("FTMO group search job {JobId} cancelled after {ElapsedMs} ms", jobId, ElapsedMs());
                return;
            }

            var run = await runner.RunAsync(
                claim.Request,
                p => registry.Publish(jobId, p with { ElapsedMs = ElapsedMs() }),
                linked.Token);

            registry.Publish(jobId, new FtmoGroupSearchProgressDto(
                FtmoGroupSearchStage.Ranking, run.FullySimulated, run.FullySimulated + run.NotComputed,
                ElapsedMs(), run.FullySimulated, run.FullySimulated + run.NotComputed, run.Funnel));
            var status = StatusOf(run);
            registry.Finish(jobId, status, run, null);
            logger.LogInformation(
                "FTMO group search job {JobId} ended {Status} (stop reason {StopReason}): examined {Examined}, {Rows} rows, {ElapsedMs} ms",
                jobId, status, run.StopReason, run.Funnel.Examined, run.Rows.Count, ElapsedMs());
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            registry.Finish(jobId, FtmoGroupSearchStatus.Cancelled, null, null);
            logger.LogInformation("FTMO group search job {JobId} cancelled after {ElapsedMs} ms", jobId, ElapsedMs());
        }
        catch (FtmoGroupSearchRefusedException ex)
        {
            registry.Finish(jobId, FtmoGroupSearchStatus.Failed, null, ex.Message);
            logger.LogWarning("FTMO group search job {JobId} refused: {Message}", jobId, ex.Message);
        }
#pragma warning disable CA1031 // The worker must keep serving whatever one job throws.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            registry.Finish(jobId, FtmoGroupSearchStatus.Failed, null, FailedMessage);
            logger.LogError(ex, "FTMO group search job {JobId} failed after {ElapsedMs} ms", jobId, ElapsedMs());
        }
    }

    private static FtmoGroupSearchStatus StatusOf(FtmoGroupSearchRunResult run)
        => run.Cancelled
            ? FtmoGroupSearchStatus.Cancelled
            : run.StopReason is FtmoGroupSearchStopReason.MaxFullSimulations or FtmoGroupSearchStopReason.WallClock
                ? FtmoGroupSearchStatus.StoppedAtBudget
                : FtmoGroupSearchStatus.Completed;
}
