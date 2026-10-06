using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 2a.3 — a refusal (pool too large, no limits row) is the user's to read, so the worker surfaces
/// its message; any other exception stays the fixed, non-leaking failure message.
/// </summary>
public class FtmoGroupSearchWorkerRefusalTests
{
    private sealed class ThrowingRunner(Exception ex) : IFtmoGroupSearchRunner
    {
        public Task<FtmoGroupSearchRunResult> RunAsync(FtmoGroupSearchRequest request, Action<FtmoGroupSearchProgressDto> report, CancellationToken ct)
            => throw ex;
    }

    private static async Task<FtmoGroupSearchJobDto> RunJobAsync(Exception ex)
    {
        var registry = new FtmoGroupSearchJobRegistry();
        var worker = new FtmoGroupSearchWorker(registry, new ThrowingRunner(ex), TimeProvider.System, NullLogger<FtmoGroupSearchWorker>.Instance);
        var start = registry.TryStart(new FtmoGroupSearchRequest(MinMembers: 2, MaxMembers: 3));
        await worker.ProcessAsync(start.JobId, CancellationToken.None);
        return registry.Get(start.JobId)!;
    }

    [Fact]
    public async Task ARefusal_FailsTheJob_WithItsOwnMessage()
    {
        var job = await RunJobAsync(new FtmoGroupSearchRefusedException("The pool has 30 eligible strategies; the maximum is 24."));

        job.Status.Should().Be(FtmoGroupSearchStatus.Failed);
        job.ErrorMessage.Should().Be("The pool has 30 eligible strategies; the maximum is 24.");
    }

    [Fact]
    public async Task AnyOtherException_StillFailsWithTheFixedMessage()
    {
        var job = await RunJobAsync(new InvalidOperationException("secret connection string"));

        job.Status.Should().Be(FtmoGroupSearchStatus.Failed);
        job.ErrorMessage.Should().Be(FtmoGroupSearchWorker.FailedMessage);
    }
}
