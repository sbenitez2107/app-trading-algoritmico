using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Domain.Backtests;

/// <summary>
/// A run's out-of-sample boundary. <b>Cannot be constructed anywhere outside this file</b> — the
/// constructor is <c>private</c> and the only caller is the nested <see cref="Resolver"/>, which is
/// inside this type's program text and therefore the only code the compiler permits to reach it.
/// Holding an <see cref="OosWindow"/> is proof that the run it came from is an
/// <see cref="BacktestRunKind.Evaluation"/> run whose strategy has a walk-forward export.
/// <para>
/// WHY THE NESTING IS INVERTED. The obvious shape — a top-level <c>OosWindowResolver</c> with a
/// nested window type — does not compile: in C# a nested type may reach its enclosing type's
/// private members, but NOT the other way round, so the resolver could only have been given an
/// <c>internal</c> factory. <c>internal</c> is assembly-wide, which would have made "only the
/// resolver can produce a boundary" a claim about intent rather than a fact about the type system.
/// Inverting the nesting buys the real guarantee, at the cost of the call reading
/// <c>OosWindow.Resolver.TryGetOosWindow(...)</c>.
/// </para>
/// <para>
/// It is a CLASS, not a struct, and that is load-bearing. A struct always has a <c>default</c>
/// instance no matter how private its constructor, and <c>default(OosWindow).FromInclusive</c>
/// would be <c>DateTime.MinValue</c> — a boundary that admits every trade ever imported, which is
/// precisely the false out-of-sample claim this type exists to prevent. As a class the absence of a
/// window is <c>null</c>, which callers cannot mistake for a permissive one.
/// </para>
/// <para>
/// SCOPE OF THE GUARANTEE, stated plainly. The compiler enforces CONSTRUCTION. It does not stop
/// anyone from reading <see cref="StrategyWalkForwardExport.OosFromDate"/> and writing their own
/// date comparison; that is prevented only by keeping every such comparison in this one file
/// (<see cref="Includes"/> for materialised trades, <see cref="Resolver.StrategiesWithOosEvidence"/>
/// for the grid's single-query aggregate) so that a repository grep for <c>CloseTime &gt;=</c>
/// finds nothing else. That half is a convention checked by grep, and it is described as such
/// rather than dressed up as structural. See design.md D8.
/// </para>
/// </summary>
public sealed class OosWindow
{
    private OosWindow(DateTime fromInclusive) => FromInclusive = fromInclusive;

    /// <summary>The owning walk-forward export's <c>OosFromDate</c>. Inclusive.</summary>
    public DateTime FromInclusive { get; }

    /// <summary>THE comparison. Every per-trade out-of-sample decision in the system goes through here.</summary>
    public bool Includes(BacktestTrade trade) => trade.CloseTime >= FromInclusive;

    /// <summary>The out-of-sample subset of a materialised trade list.</summary>
    public IEnumerable<BacktestTrade> Filter(IEnumerable<BacktestTrade> trades) => trades.Where(Includes);

    /// <summary>
    /// The only way to obtain an <see cref="OosWindow"/> (design.md D8).
    /// <para>
    /// An enum alone stops nothing: naming a run "Deploy" does not prevent anyone filtering its
    /// trades by a date and calling the result out-of-sample. What stops it is that the date is not
    /// available to them. A Deploy run yields NO window — not an empty one, not a zero-trade
    /// result — so "this run has no out-of-sample evidence" is a state the caller must handle
    /// rather than a number that quietly reads as evidence of nothing.
    /// </para>
    /// </summary>
    public static class Resolver
    {
        /// <summary>
        /// Obtains the out-of-sample boundary for <paramref name="run"/>, or returns false when
        /// there is none: the run is not an <see cref="BacktestRunKind.Evaluation"/> run, or the
        /// strategy has no walk-forward export yet. The second case is not an error — a run
        /// imported before its export stays valid and simply becomes evaluable later, with no
        /// re-import, because the boundary is owned by the export and never copied onto the run.
        /// </summary>
        public static bool TryGetOosWindow(BacktestRun run, StrategyWalkForwardExport? export, out OosWindow? window)
        {
            window = null;

            if (run.Kind != BacktestRunKind.Evaluation || export is null)
                return false;

            window = new OosWindow(export.OosFromDate);
            return true;
        }

        /// <summary>
        /// The grid's readiness aggregate (design.md D12), expressed here so that the boundary
        /// comparison exists in exactly one file. Returns one <see cref="BacktestReadinessRow"/>
        /// per requested strategy.
        /// <para>
        /// Composed of <see cref="IQueryable{T}"/> so the whole thing translates to ONE server-side
        /// query for a whole page of strategies rather than one lookup per row — the grid loads all
        /// of an account's strategies in a single call, so a per-row shape would be over a hundred
        /// extra round-trips on every page load. Both facts are gathered in the SAME projection for
        /// the same reason: two queries would be two chances for the page to disagree with itself.
        /// </para>
        /// <para>
        /// It takes the query sources as arguments rather than a <c>DbContext</c> so this type stays
        /// in Domain with no EF dependency.
        /// </para>
        /// </summary>
        public static IQueryable<BacktestReadinessRow> ReadinessRows(
            IQueryable<Strategy> strategies,
            IQueryable<BacktestRun> runs,
            IQueryable<BacktestTrade> trades,
            IQueryable<StrategyWalkForwardExport> exports,
            IReadOnlyCollection<Guid> strategyIds)
            => strategies
                .Where(s => strategyIds.Contains(s.Id))
                .Select(s => new BacktestReadinessRow(
                    s.Id,
                    runs.Any(r => r.StrategyId == s.Id),
                    exports.Any(e => e.StrategyId == s.Id
                        && runs.Any(r =>
                            r.StrategyId == s.Id
                            && r.Kind == BacktestRunKind.Evaluation
                            && trades.Any(t => t.BacktestRunId == r.Id && t.CloseTime >= e.OosFromDate)))));
    }
}
