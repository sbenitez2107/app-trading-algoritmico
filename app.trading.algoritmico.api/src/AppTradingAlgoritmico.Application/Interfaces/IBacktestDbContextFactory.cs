namespace AppTradingAlgoritmico.Application.Interfaces;

/// <summary>
/// Creates a NEW, independently-tracked <see cref="IBacktestDbContext"/> for one attempt of a
/// retryable unit of work. The caller owns the returned context and MUST dispose it.
/// <para>
/// This exists because <c>CreateExecutionStrategy().ExecuteAsync(...)</c> makes a transient
/// failure retryable but does NOT make the retried delegate idempotent. Re-entering that delegate
/// with the request-scoped context carries the failed attempt's change tracker into the retry:
/// entries left <c>Added</c> by a throwing <c>SaveChangesAsync</c> are re-inserted alongside a
/// second graph (duplicate-key violation, not transient, so it propagates), and entries already
/// accepted by a <c>SaveChangesAsync</c> that preceded a failing <c>CommitAsync</c> have
/// originals == current, so re-assigning the same value emits no UPDATE column at all.
/// </para>
/// <para>
/// Neither <c>RollbackAsync</c> nor disposing the transaction touches the change tracker, so a
/// fresh context per attempt — EF Core's own connection-resiliency guidance — is the fix.
/// See <c>BacktestImportRetrySafetyTests</c> for the property both defects violated.
/// </para>
/// </summary>
public interface IBacktestDbContextFactory
{
    /// <summary>Creates a context with an empty change tracker. The caller disposes it.</summary>
    IBacktestDbContext Create();
}
