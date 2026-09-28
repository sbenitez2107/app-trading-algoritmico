using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// ftmo-challenge-race — composes the unchanged <see cref="FtmoBreachEvaluator"/> with a target
/// scanner to report, per phase, whichever of "target reached" or "first breach" happens first
/// (design.md Decision 1: Compose). <c>internal static</c>, pure: no I/O.
/// <para>
/// A separate, truncating loop over the same trade series as the shipped breach replay (spec.md
/// "The Race Leaves The Shipped Breach Result Byte-Identical"): <see cref="FtmoBreachEvaluator.Evaluate"/>
/// is never edited and never truncated by this class.
/// </para>
/// </summary>
internal static class FtmoChallengeRace
{
    /// <summary>Disclosure (design.md "Disclosure"). Never affirms survival — spec.md's no-pass-wording requirement.</summary>
    internal const string Disclosure =
        "This race replays closed trades only and is not a prediction. A profit target reached first is "
        + "an optimistic reading in two ways: swap is not modelled, so the target can arrive earlier than "
        + "on a live account, and a closed-trade replay cannot see intraday equity dips, so breaches are "
        + "understated. A breach reached first is a strong result, because both biases push against it. "
        + "Neither by end of data means the replayed history ended before either event occurred.";

    internal readonly record struct PhaseResult(
        FtmoPhaseOutcome Outcome,
        DateTime? StartSourceOpen,
        DateTime? FirstTargetTouchSourceClose,
        DateOnly? MinTradingDaysMetFtmoDay,
        DateTime? OutcomeSourceClose,
        FtmoFirstBreachingLimit? BreachLimit,
        FtmoBreachPointClass? BreachPointClass,
        int? CalendarDaysElapsed,
        int? FtmoTradingDaysElapsed)
    {
        internal static readonly PhaseResult NotStarted =
            new(FtmoPhaseOutcome.NotStarted, null, null, null, null, null, null, null, null);
    }

    internal readonly record struct ChainResult(PhaseResult Phase1, PhaseResult Phase2);

    /// <summary>
    /// ftmo-multi-start PR1, task 1.1.4/1.6 (design.md Decision 1's optimisation clause) — a race-private
    /// <see cref="FtmoReplayCalendar.ElapsedDays"/> equivalent. The shipped helper re-attributes every
    /// trade's own-open bookkeeping day (a <see cref="TimeZoneInfo"/> conversion) on EVERY close-group
    /// cutoff check inside <see cref="RunPhase"/> — O(n) TimeZoneInfo work, n times, once per group, so
    /// O(n^2) overall. This memoises each trade's own-open day ONCE per <see cref="RunPhase"/> call, and
    /// answers the <b>monotonic</b> close-group scan (cutoffs only ever increase within that loop) with a
    /// two-pointer incremental distinct-day count, backed by a Fenwick tree over the trades' own
    /// bookkeeping days so an out-of-order (non-monotonic) query — <see cref="RunPhase"/>'s post-loop
    /// breach-point cutoff can land BEFORE the loop's last cutoff, since the loop keeps scanning after a
    /// breach to look for a later decided close — still gets an exact answer via a same-semantics direct
    /// O(n) fallback scan (used at most once per phase, so the phase stays O(n log n) overall, never
    /// O(n^2)). Proven equal to <see cref="FtmoReplayCalendar.ElapsedDays"/> at every close-group cutoff
    /// of both benchmark fixture profiles (including the non-monotonic breach cutoff), plus
    /// <see cref="RunChain"/> equality on every monthly start against the unoptimised implementation
    /// (FtmoChallengeRaceElapsedDaysCacheEquivalenceTests). <c>internal</c> so those tests exercise the
    /// production type directly, not a hand-copied reimplementation.
    /// </summary>
    internal sealed class CachedOpenDays
    {
        private readonly (DateTime OpenSource, DateTime CloseSource, bool Scalable, DateOnly Day)[] _rows;
        private readonly int[] _scalableByOpen;
        private readonly int[] _scalableByClose;
        private readonly DateOnly[] _sortedDistinctDays;
        private readonly int[] _dayRankOfRow;
        private readonly int[] _bit;
        private readonly bool[] _entered;
        private readonly bool[] _dayEntered;

        private int _openPtr;
        private int _closePtr;
        private DateTime? _lastEventSourceClose;

        /// <summary>
        /// ftmo-multi-start PR1 apply follow-up (option A, PR1 gate rework) — <paramref name="attribution"/>
        /// is an optional PRECOMPUTED own-open bookkeeping day per <see cref="FtmoTradeProjector.ProjectedTrade.RowIndex"/>,
        /// from <see cref="AttributeOpenDays"/>. When supplied, the per-row <see cref="TimeZoneInfo"/>
        /// conversion is skipped entirely — the caller already paid for it once, for the whole chain (both
        /// phases) and, since open/close instants and the scalable flag never depend on FX (verified in
        /// <see cref="FtmoTradeProjector.Project"/> — <c>Net is null</c> depends only on <c>trade.Size</c>,
        /// never on the FX rate), both FX ends too. <c>null</c> falls back to the original per-call
        /// attribution, so every existing caller keeps its exact behaviour unedited.
        /// </summary>
        internal CachedOpenDays(
            IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
            TimeZoneInfo sourceZone,
            TimeZoneInfo berlinZone,
            IReadOnlyDictionary<int, DateOnly>? attribution = null)
        {
            var rows = new (DateTime, DateTime, bool, DateOnly)[trades.Count];
            for (var i = 0; i < trades.Count; i++)
            {
                var trade = trades[i];
                // Unscalable rows never actually opened on FTMO (FtmoReplayCalendar's own rule) — skip
                // the attribution entirely; their day is never read.
                var day = trade.Net is null
                    ? default
                    : attribution is not null
                        ? attribution[trade.RowIndex]
                        : FtmoDayClock.Attribute(trade.OpenSource, sourceZone, berlinZone).BookkeepingDay;
                rows[i] = (trade.OpenSource, trade.CloseSource, trade.Net is not null, day);
            }

            _rows = rows;

            var scalableIndices = new List<int>();
            for (var i = 0; i < rows.Length; i++)
                if (rows[i].Item3)
                    scalableIndices.Add(i);

            _scalableByOpen = scalableIndices.OrderBy(i => rows[i].Item1).ToArray();
            _scalableByClose = scalableIndices.OrderBy(i => rows[i].Item2).ToArray();

            _sortedDistinctDays = scalableIndices.Select(i => rows[i].Item4).Distinct().OrderBy(d => d).ToArray();

            _dayRankOfRow = new int[rows.Length];
            foreach (var i in scalableIndices)
                _dayRankOfRow[i] = Array.BinarySearch(_sortedDistinctDays, rows[i].Item4);

            _bit = new int[_sortedDistinctDays.Length + 1];
            _entered = new bool[rows.Length];
            _dayEntered = new bool[_sortedDistinctDays.Length];
        }

        /// <summary>Same cutoff rule as <see cref="FtmoReplayCalendar.ElapsedDays"/>: distinct cached days in range.</summary>
        internal int CountOpenDays(DateOnly anchorDay, DateOnly breachDay, DateTime eventSourceClose)
        {
            if (_lastEventSourceClose is not null && eventSourceClose < _lastEventSourceClose.Value)
                return CountOpenDaysDirect(anchorDay, breachDay, eventSourceClose);

            _lastEventSourceClose = eventSourceClose;

            while (_openPtr < _scalableByOpen.Length && _rows[_scalableByOpen[_openPtr]].OpenSource < eventSourceClose)
            {
                Enter(_scalableByOpen[_openPtr]);
                _openPtr++;
            }

            while (_closePtr < _scalableByClose.Length && _rows[_scalableByClose[_closePtr]].CloseSource <= eventSourceClose)
            {
                Enter(_scalableByClose[_closePtr]);
                _closePtr++;
            }

            return CountLessOrEqual(breachDay) - CountLessOrEqual(anchorDay.AddDays(-1));
        }

        /// <summary>
        /// Non-monotonic fallback, semantically identical to <see cref="FtmoReplayCalendar.ElapsedDays"/>'s
        /// own O(n) scan: used only when a cutoff arrives smaller than one already answered by the
        /// incremental (monotonic) path above, so the two-pointer state cannot be trusted for it.
        /// </summary>
        private int CountOpenDaysDirect(DateOnly anchorDay, DateOnly breachDay, DateTime eventSourceClose)
        {
            var openDays = new HashSet<DateOnly>();
            foreach (var row in _rows)
            {
                if (!row.Scalable)
                    continue;

                if (!(row.OpenSource < eventSourceClose || row.CloseSource <= eventSourceClose))
                    continue;

                if (row.Day >= anchorDay && row.Day <= breachDay)
                    openDays.Add(row.Day);
            }

            return openDays.Count;
        }

        private void Enter(int rowIndex)
        {
            if (_entered[rowIndex])
                return;

            _entered[rowIndex] = true;

            // Distinct-day semantics (HashSet<DateOnly>.Add in the direct fallback): only the FIRST row
            // to reach a given day counts it — a second row sharing the same day must not double it.
            var rank = _dayRankOfRow[rowIndex];
            if (_dayEntered[rank])
                return;

            _dayEntered[rank] = true;
            BitAdd(rank);
        }

        private void BitAdd(int rank)
        {
            for (var i = rank + 1; i < _bit.Length; i += i & -i)
                _bit[i]++;
        }

        private int BitSum(int rank)
        {
            var sum = 0;
            for (var i = rank + 1; i > 0; i -= i & -i)
                sum += _bit[i];
            return sum;
        }

        /// <summary>Count of distinct ENTERED days &lt;= <paramref name="day"/> (0 if none qualify).</summary>
        private int CountLessOrEqual(DateOnly day)
        {
            var lo = 0;
            var hi = _sortedDistinctDays.Length - 1;
            var found = -1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (_sortedDistinctDays[mid] <= day)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found < 0 ? 0 : BitSum(found);
        }
    }

    /// <summary>
    /// ftmo-multi-start PR1 apply follow-up (option A) — each trade's own-open bookkeeping day, computed
    /// ONCE per chain (not per phase, and not per FX end). Open/close instants and the scalable flag
    /// (<c>Net is null</c>) depend only on <see cref="FtmoTradeProjector.ProjectedTrade.OpenSource"/> and
    /// the source trade's declared size (see <see cref="FtmoTradeProjector.Project"/>) — never on the FX
    /// rate — so this attribution is valid for BOTH FX ends' projected series when keyed by
    /// <see cref="FtmoTradeProjector.ProjectedTrade.RowIndex"/>, and for a phase-2 subset of the same
    /// series (a phase-2 subset's rows are the same rows, same <c>RowIndex</c>, as the full series passed
    /// in here). Skips <c>Unscalable</c> rows (their day is never read, matching <see cref="CachedOpenDays"/>'s
    /// own skip).
    /// </summary>
    internal static IReadOnlyDictionary<int, DateOnly> AttributeOpenDays(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades, TimeZoneInfo sourceZone, TimeZoneInfo berlinZone)
    {
        var attribution = new Dictionary<int, DateOnly>(trades.Count);
        foreach (var trade in trades)
        {
            if (trade.Net is null)
                continue;

            attribution[trade.RowIndex] = FtmoDayClock.Attribute(trade.OpenSource, sourceZone, berlinZone).BookkeepingDay;
        }

        return attribution;
    }

    private static (int CalendarDaysElapsed, int FtmoTradingDaysElapsed) ElapsedDaysCached(
        FtmoReplayCalendar.ReplayAnchor anchor, DateOnly breachDay, DateTime eventSourceClose, CachedOpenDays cache)
    {
        var calendarDays = breachDay.DayNumber - anchor.FtmoDay.DayNumber;
        var tradingDays = cache.CountOpenDays(anchor.FtmoDay, breachDay, eventSourceClose);
        return (calendarDays, tradingDays);
    }

    /// <summary>
    /// ftmo-multi-start PR1, task 1.2 (design.md Decision 2) — the earliest-breach selection across
    /// the daily and max limits, extracted VERBATIM from <see cref="RunPhase"/>'s breach-limit
    /// selection block. <c>null</c> when neither limit ever breaches. On a tie between the two limits
    /// at the SAME close (<see cref="FtmoBreachEvaluator.BreachPoint.RowIndex"/> equal), daily wins and
    /// is tagged <see cref="FtmoFirstBreachingLimit.BothSameClose"/>; otherwise the earlier of the two
    /// by <c>(SourceTime, RowIndex)</c> wins. Called by <see cref="RunPhase"/> and the funded phase
    /// (Phase 1.3).
    /// </summary>
    internal static (FtmoBreachEvaluator.BreachPoint BreachPoint, FtmoFirstBreachingLimit BreachLimit)? FirstBreach(
        FtmoBreachEvaluator.FtmoBreachEvaluation evaluation)
    {
        if (evaluation.Daily.FirstBreach is null && evaluation.Max.FirstBreach is null)
            return null;

        var daily = evaluation.Daily.FirstBreach;
        var max = evaluation.Max.FirstBreach;

        if (daily is null)
            return (max!.Value, FtmoFirstBreachingLimit.Max);

        if (max is null)
            return (daily.Value, FtmoFirstBreachingLimit.Daily);

        if (daily.Value.RowIndex == max.Value.RowIndex)
            return (daily.Value, FtmoFirstBreachingLimit.BothSameClose);

        var dailyIsEarlier = (daily.Value.SourceTime, daily.Value.RowIndex)
            .CompareTo((max.Value.SourceTime, max.Value.RowIndex)) < 0;
        return dailyIsEarlier
            ? (daily.Value, FtmoFirstBreachingLimit.Daily)
            : (max.Value, FtmoFirstBreachingLimit.Max);
    }

    /// <summary>
    /// Runs one phase's scanner (target, close groups, flat-book check) plus the unchanged evaluator
    /// (breach) over <paramref name="phaseTrades"/>, and combines them per design.md Decision 3
    /// (breach wins on a tie).
    /// </summary>
    internal static PhaseResult RunPhase(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> phaseTrades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct,
        decimal targetPct,
        FtmoBreachEvaluator.FtmoBreachEvaluation? precomputedEvaluation = null,
        IReadOnlyDictionary<int, DateOnly>? openDayAttribution = null)
    {
        ArgumentNullException.ThrowIfNull(phaseTrades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        if (phaseTrades.Count == 0)
            return PhaseResult.NotStarted with { Outcome = FtmoPhaseOutcome.NeitherByEndOfData };

        var anchor = FtmoReplayCalendar.Build(phaseTrades, sourceZone, berlinZone);
        var startOpen = anchor.SourceOpen;
        var openDaysCache = new CachedOpenDays(phaseTrades, sourceZone, berlinZone, openDayAttribution);

        var ordered = phaseTrades
            .OrderBy(t => t.CloseSource)
            .ThenBy(t => t.RowIndex)
            .ToList();

        var targetLevel = capital * (1m + targetPct);

        DateTime? firstTouch = null;
        DateOnly? minDaysMetDay = null;
        DateTime? minDaysMetClose = null;
        DateTime? decidedClose = null;
        DateOnly? decidedDay = null;
        int? decidedCalendarDays = null;
        int? decidedTradingDays = null;

        var runningBalance = capital;
        var index = 0;
        while (index < ordered.Count)
        {
            var groupClose = ordered[index].CloseSource;
            while (index < ordered.Count && ordered[index].CloseSource == groupClose)
            {
                if (ordered[index].Net is { } net)
                    runningBalance += net;
                index++;
            }

            if (firstTouch is null && runningBalance >= targetLevel)
                firstTouch = groupClose;

            var groupDay = FtmoDayClock.Attribute(groupClose, sourceZone, berlinZone).BookkeepingDay;
            var (calendarDays, tradingDays) = ElapsedDaysCached(anchor, groupDay, groupClose, openDaysCache);

            if (minDaysMetDay is null && tradingDays >= FtmoChallengeRules.MinTradingDaysPerPhase)
            {
                minDaysMetDay = groupDay;
                minDaysMetClose = groupClose;
            }

            if (decidedClose is null
                && runningBalance >= targetLevel
                && tradingDays >= FtmoChallengeRules.MinTradingDaysPerPhase
                && !FtmoOpenPositionSweep.OpenAt(phaseTrades, groupClose))
            {
                decidedClose = groupClose;
                decidedDay = groupDay;
                decidedCalendarDays = calendarDays;
                decidedTradingDays = tradingDays;
                break;
            }
        }

        // ftmo-multi-start PR1 apply follow-up (option A): the caller (RunChain's phase-1 call, the
        // benchmark, or FtmoBreachSimulationReadService) may already hold this EXACT phase's evaluation —
        // reuse it instead of re-running the O(n log n) evaluator on the identical series. The contract
        // is enforced by the caller, never validated here (this method has no way to tell "this
        // evaluation" from "a look-alike one for a different series" without re-evaluating, which would
        // defeat the optimisation) — see FtmoChallengeRaceSharedEvaluationTests for the equivalence proof
        // and its falsification.
        var evaluation = precomputedEvaluation
            ?? FtmoBreachEvaluator.Evaluate(phaseTrades, sourceZone, berlinZone, capital, dailyPct, maxPct);

        var firstBreach = FirstBreach(evaluation);
        var breachPoint = firstBreach?.BreachPoint;
        var breachLimit = firstBreach?.BreachLimit;

        // Breach wins on a tie (design.md Decision 3): breach at or before the decided close.
        if (breachPoint is not null && (decidedClose is null || breachPoint.Value.SourceTime <= decidedClose.Value))
        {
            var (calendarDays, tradingDays) = ElapsedDaysCached(
                anchor, breachPoint.Value.FtmoDay, breachPoint.Value.SourceTime, openDaysCache);

            // Bug A fix (ftmo-multi-start PR0, spec.md "A breached phase whose balance later touches
            // the target reports no post-breach touch"): a live FTMO account never reaches a touch or
            // day-minimum event that happens after the account already breached, so both readouts are
            // reported only when they occur at or before the breach close.
            var touchAtOrBeforeBreach =
                firstTouch is not null && firstTouch.Value <= breachPoint.Value.SourceTime ? firstTouch : null;
            var minDaysMetDayAtOrBeforeBreach =
                minDaysMetClose is not null && minDaysMetClose.Value <= breachPoint.Value.SourceTime
                    ? minDaysMetDay
                    : null;

            return new PhaseResult(
                FtmoPhaseOutcome.BreachedFirst, startOpen, touchAtOrBeforeBreach, minDaysMetDayAtOrBeforeBreach,
                breachPoint.Value.SourceTime, breachLimit,
                breachPoint.Value.Causes.Count == 0 ? FtmoBreachPointClass.Clean : FtmoBreachPointClass.Contingent,
                calendarDays, tradingDays);
        }

        if (decidedClose is not null)
        {
            return new PhaseResult(
                FtmoPhaseOutcome.TargetReachedFirst, startOpen, firstTouch, minDaysMetDay,
                decidedClose, null, null, decidedCalendarDays, decidedTradingDays);
        }

        var last = ordered[^1];
        var lastDay = FtmoDayClock.Attribute(last.CloseSource, sourceZone, berlinZone).BookkeepingDay;
        var (lastCalendarDays, lastTradingDays) = ElapsedDaysCached(anchor, lastDay, last.CloseSource, openDaysCache);

        return new PhaseResult(
            FtmoPhaseOutcome.NeitherByEndOfData, startOpen, firstTouch, minDaysMetDay,
            null, null, null, lastCalendarDays, lastTradingDays);
    }

    /// <summary>
    /// Runs the two-phase chain: phase 1 over the whole series, phase 2 (design.md Decision 1/2) over
    /// the post-handover subset (<c>Open &gt;= T AND Close &gt; T</c>) with capital reset to Initial
    /// Capital, only when phase 1 reaches its target. A row that opens before T and closes after T (an
    /// <c>Unscalable</c> row spanning T) stays out of phase 2 — it belongs to phase 1's already-decided
    /// group, not to a fresh account starting at T (ftmo-multi-start PR0, bug B).
    /// </summary>
    /// <param name="precomputedPhase1Evaluation">
    /// ftmo-multi-start PR1 apply follow-up (option A) — an optional, ALREADY-COMPUTED
    /// <see cref="FtmoBreachEvaluator.Evaluate"/> result for phase 1, i.e. for the EXACT series passed as
    /// <paramref name="trades"/>, with the same <paramref name="capital"/>/<paramref name="dailyPct"/>/
    /// <paramref name="maxPct"/>. The caller (the benchmark, or the future PR4 multi-start service, which
    /// will call this the same way per design.md Decision 1) owns this contract; passing an evaluation of
    /// a DIFFERENT series silently produces a wrong (but not exception-throwing) phase-1 result — see
    /// FtmoChallengeRaceSharedEvaluationTests's falsification test. Phase 2 always evaluates fresh: its
    /// series is a different subset, so there is nothing to share for it here.
    /// </param>
    /// <param name="precomputedOpenDayAttribution">
    /// An optional, already-computed <see cref="AttributeOpenDays"/> result for <paramref name="trades"/>
    /// (or a superset keyed by the same <c>RowIndex</c>s, e.g. shared across both FX ends). <c>null</c>
    /// computes it once here, still only once per chain rather than once per phase.
    /// </param>
    internal static ChainResult RunChain(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct,
        FtmoBreachEvaluator.FtmoBreachEvaluation? precomputedPhase1Evaluation = null,
        IReadOnlyDictionary<int, DateOnly>? precomputedOpenDayAttribution = null)
    {
        var attribution = precomputedOpenDayAttribution ?? AttributeOpenDays(trades, sourceZone, berlinZone);

        var phase1 = RunPhase(
            trades, sourceZone, berlinZone, capital, dailyPct, maxPct, FtmoChallengeRules.Phase1TargetPct,
            precomputedPhase1Evaluation, attribution);

        if (phase1.Outcome != FtmoPhaseOutcome.TargetReachedFirst)
            return new ChainResult(phase1, PhaseResult.NotStarted);

        var phase2Trades = trades
            .Where(t =>
                t.OpenSource >= phase1.OutcomeSourceClose!.Value
                && t.CloseSource > phase1.OutcomeSourceClose!.Value)
            .OrderBy(t => t.CloseSource)
            .ThenBy(t => t.RowIndex)
            .ToList();

        if (phase2Trades.Count == 0)
        {
            return new ChainResult(
                phase1, PhaseResult.NotStarted with { Outcome = FtmoPhaseOutcome.NeitherByEndOfData });
        }

        var phase2 = RunPhase(
            phase2Trades, sourceZone, berlinZone, capital, dailyPct, maxPct, FtmoChallengeRules.Phase2TargetPct,
            precomputedEvaluation: null, attribution);

        return new ChainResult(phase1, phase2);
    }

    /// <summary>
    /// Whole-chain rank, least to most favourable to reaching the target (design.md Decision 6):
    /// <c>P1 Breached &lt; P1 Neither &lt; (P1 Target, P2 Breached) &lt; (P1 Target, P2 Neither) &lt;
    /// (P1 Target, P2 Target)</c>.
    /// </summary>
    private static int Rank(ChainResult chain) => chain.Phase1.Outcome switch
    {
        FtmoPhaseOutcome.BreachedFirst => 0,
        FtmoPhaseOutcome.NeitherByEndOfData => 1,
        FtmoPhaseOutcome.TargetReachedFirst => chain.Phase2.Outcome switch
        {
            FtmoPhaseOutcome.BreachedFirst => 2,
            FtmoPhaseOutcome.TargetReachedFirst => 4,
            _ => 3,
        },
        _ => 1,
    };

    /// <summary>
    /// Merges the two FX-band ends' whole chains (design.md Decision 6): reports the lower-ranked
    /// (less favourable) chain; on a rank tie, the less favourable timing; on an identical row,
    /// <see cref="FtmoFxBandEnd.BothEnds"/> with the <c>fxLow</c> values.
    /// </summary>
    internal static (ChainResult Chain, FtmoFxBandEnd End, bool RoundingSensitive) MergeEnds(
        ChainResult low, ChainResult high)
    {
        // "Outcome pairs differ" (spec.md's FX-band requirement) is read together with scenarios
        // 2.8.4/2.8.5: an identical ROW (same outcome AND same deciding close for every phase) is the
        // only case that needs no tag — a timing-only disagreement at the same rank still counts as
        // the two ends disagreeing on the outcome pair's own evidentiary weight.
        var identicalRow =
            low.Phase1.Outcome == high.Phase1.Outcome
            && low.Phase1.OutcomeSourceClose == high.Phase1.OutcomeSourceClose
            && low.Phase2.Outcome == high.Phase2.Outcome
            && low.Phase2.OutcomeSourceClose == high.Phase2.OutcomeSourceClose;
        var roundingSensitive = !identicalRow;

        var rankLow = Rank(low);
        var rankHigh = Rank(high);

        if (rankLow != rankHigh)
            return rankLow < rankHigh
                ? (low, FtmoFxBandEnd.FxLow, roundingSensitive)
                : (high, FtmoFxBandEnd.FxHigh, roundingSensitive);

        // Same rank: identical row -> BothEnds.
        if (identicalRow)
            return (low, FtmoFxBandEnd.BothEnds, roundingSensitive);

        // Ranks 0 (P1 Breached), 2 (P2 Breached) and 4 (P2 Target) have an explicit, spec-named timing
        // tie-break (earlier breach / later target). Ranks 1 and 3 (a tie between two undecided
        // chains — both P1 Neither, or both P1 Target/P2 Neither at different phase-1 closes) have no
        // such rule (orchestrator decision, point 3): report the fxLow end, tagged sensitive, since the
        // rows are already known not to be identical at this point.
        if (rankLow is 0 or 2 or 4)
        {
            var preferEarlier = rankLow != 4;
            var (lowClose, highClose) = rankLow == 0
                ? (low.Phase1.OutcomeSourceClose, high.Phase1.OutcomeSourceClose)
                : (low.Phase2.OutcomeSourceClose, high.Phase2.OutcomeSourceClose);

            if (lowClose == highClose)
                return (low, FtmoFxBandEnd.BothEnds, roundingSensitive);

            var lowIsPreferred = preferEarlier ? lowClose < highClose : lowClose > highClose;
            return lowIsPreferred
                ? (low, FtmoFxBandEnd.FxLow, roundingSensitive)
                : (high, FtmoFxBandEnd.FxHigh, roundingSensitive);
        }

        return (low, FtmoFxBandEnd.FxLow, roundingSensitive);
    }

    /// <summary>
    /// Top-level entry: race-only refusal (<see cref="FtmoChallengeRaceRefusal.ProfitTargetMismatch"/>),
    /// then the FX-band whole-chain race, mapped to <see cref="FtmoChallengeRaceDto"/>. The caller is
    /// responsible for the whole-run refusal path (hard rule 7) — this method assumes a TwoStep,
    /// Evaluated run.
    /// </summary>
    /// <param name="precomputedLowEvaluation">
    /// ftmo-multi-start PR1 apply follow-up (option A) — an optional, already-computed
    /// <see cref="FtmoBreachEvaluator.Evaluate"/> result for <paramref name="lowProjected"/>'s phase 1
    /// (e.g. <see cref="FtmoBreachSimulationReadService"/> already computes this evaluation for its own
    /// merged-finding readout — reused here instead of re-running the evaluator).
    /// </param>
    /// <param name="precomputedHighEvaluation">The same, for <paramref name="highProjected"/>'s phase 1.</param>
    internal static FtmoChallengeRaceDto Evaluate(
        decimal? profitTargetPct,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> lowProjected,
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> highProjected,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal capital,
        decimal dailyPct,
        decimal maxPct,
        FtmoBreachEvaluator.FtmoBreachEvaluation? precomputedLowEvaluation = null,
        FtmoBreachEvaluator.FtmoBreachEvaluation? precomputedHighEvaluation = null)
    {
        var rules = new FtmoChallengeRulesDto(
            FtmoChallengeRules.Phase1TargetPct, FtmoChallengeRules.Phase2TargetPct,
            FtmoChallengeRules.MinTradingDaysPerPhase, TimeLimitDays: null);

        if (profitTargetPct is not null && profitTargetPct.Value != FtmoChallengeRules.Phase1TargetPct)
        {
            return new FtmoChallengeRaceDto(
                FtmoChallengeRaceRefusal.ProfitTargetMismatch, profitTargetPct, rules, null, null, false, Disclosure);
        }

        // Open/close instants and the scalable flag never depend on FX (verified in
        // FtmoTradeProjector.Project), so one attribution pass over lowProjected, keyed by RowIndex,
        // answers both FX ends' phase-1 (and, via RunChain's own subset-by-RowIndex reuse, phase-2) calls.
        var attribution = AttributeOpenDays(lowProjected, sourceZone, berlinZone);

        var low = RunChain(
            lowProjected, sourceZone, berlinZone, capital, dailyPct, maxPct, precomputedLowEvaluation, attribution);
        var high = RunChain(
            highProjected, sourceZone, berlinZone, capital, dailyPct, maxPct, precomputedHighEvaluation, attribution);
        var (chain, end, roundingSensitive) = MergeEnds(low, high);

        var phase1Dto = ToPhaseDto(chain.Phase1, end);
        var phase2Dto = ToPhaseDto(chain.Phase2, chain.Phase2.Outcome == FtmoPhaseOutcome.NotStarted ? null : end);

        return new FtmoChallengeRaceDto(
            null, profitTargetPct, rules, phase1Dto, phase2Dto, roundingSensitive, Disclosure);
    }

    private static FtmoChallengePhaseDto ToPhaseDto(PhaseResult phase, FtmoFxBandEnd? end) => new(
        phase.Outcome, phase.StartSourceOpen, phase.FirstTargetTouchSourceClose, phase.MinTradingDaysMetFtmoDay,
        phase.OutcomeSourceClose, phase.BreachLimit, phase.BreachPointClass,
        phase.CalendarDaysElapsed, phase.FtmoTradingDaysElapsed, end);
}
