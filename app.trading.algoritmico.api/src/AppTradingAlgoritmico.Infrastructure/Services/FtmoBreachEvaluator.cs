using AppTradingAlgoritmico.Domain.Enums;

namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// PR P2 — running-balance, three-state breach evaluator (design.md Decision 5, spec.md's Daily
/// Loss / Max Loss / Overlap requirements). <c>internal static</c>, pure: no I/O.
/// <para>
/// Evaluates every close in chronological order (<c>CloseSource</c>, then <c>RowIndex</c>) against
/// a MOVING daily floor (previous CE(S)T-midnight balance − <c>dailyPct</c>·Initial Capital; day 1
/// uses Initial Capital) and a STATIC max floor (Initial Capital·(1 − <c>maxPct</c>), computed once
/// and never adjusted). A breach is strictly below the floor (<c>&lt;</c>); exactly at the floor is
/// not a breach.
/// </para>
/// <para>
/// The FTMO trading day for a close is taken from <see cref="FtmoDayClock.Attribute"/> on its
/// <c>CloseSource</c>. When the candidate-day set has more than one member (ambiguous/invalid
/// source time), the EARLIEST candidate day is used for floor bookkeeping — the conservative
/// choice: it never grants the account extra floor room it might not actually have had. The
/// ambiguity itself still drives the <see cref="BreachContingencyCause"/> regardless of which day
/// was picked.
/// </para>
/// </summary>
internal static class FtmoBreachEvaluator
{
    /// <summary>The FTMO instant, source instant, balance, floor level and margin past it for one breaching close.</summary>
    internal readonly record struct BreachPoint(
        DateTime FtmoTime,
        DateTime SourceTime,
        decimal Balance,
        decimal Level,
        decimal MarginPastLevel);

    /// <summary>
    /// One limit's three-state finding. The constructor is private; use <see cref="Clean"/>,
    /// <see cref="Contingent"/> or <see cref="BreachedAt"/> so a <see cref="FtmoBreachVerdict.BreachContingent"/>
    /// can never be constructed with an empty <c>Causes</c> list.
    /// </summary>
    internal sealed record FtmoLimitFinding
    {
        private FtmoLimitFinding(
            FtmoBreachVerdict verdict,
            IReadOnlyList<BreachContingencyCause> causes,
            BreachPoint? firstBreach,
            BreachPoint? firstCleanBreach)
        {
            Verdict = verdict;
            Causes = causes;
            FirstBreach = firstBreach;
            FirstCleanBreach = firstCleanBreach;
        }

        public FtmoBreachVerdict Verdict { get; }

        public IReadOnlyList<BreachContingencyCause> Causes { get; }

        public BreachPoint? FirstBreach { get; }

        public BreachPoint? FirstCleanBreach { get; }

        /// <summary>Disclosure text. Never affirms survival — see spec.md's no-pass-wording requirement.</summary>
        public string DisclosureText => Verdict switch
        {
            FtmoBreachVerdict.Breached =>
                "A closed-trade breach with no identified contingency was detected in the replayed series.",
            FtmoBreachVerdict.BreachContingent =>
                "A closed-trade breach was detected, but it coincides with a condition that makes it uncertain: "
                + string.Join(", ", Causes) + ".",
            _ =>
                "No breach was found in the replayed closed-trade data. A closed-trade replay and "
                + "unmodelled swap both push the result toward looking less risky than reality allows; "
                + "this finding makes no claim about what a live equity path would have done.",
        };

        public static FtmoLimitFinding Clean() =>
            new(FtmoBreachVerdict.NoBreachObserved, [], null, null);

        public static FtmoLimitFinding BreachedAt(BreachPoint firstBreach, BreachPoint firstCleanBreach) =>
            new(FtmoBreachVerdict.Breached, [], firstBreach, firstCleanBreach);

        public static FtmoLimitFinding Contingent(
            IReadOnlyList<BreachContingencyCause> causes, BreachPoint firstBreach)
        {
            if (causes.Count == 0)
            {
                throw new ArgumentException(
                    "A BreachContingent finding must carry at least one cause.", nameof(causes));
            }

            return new FtmoLimitFinding(FtmoBreachVerdict.BreachContingent, causes, firstBreach, null);
        }
    }

    /// <summary>The two limits' findings for one evaluated segment.</summary>
    internal sealed record FtmoBreachEvaluation(FtmoLimitFinding Daily, FtmoLimitFinding Max);

    /// <summary>
    /// Runs the replay. <paramref name="trades"/> need not be pre-sorted; this method sorts by
    /// <c>(CloseSource, RowIndex)</c> before evaluating.
    /// </summary>
    internal static FtmoBreachEvaluation Evaluate(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades,
        TimeZoneInfo sourceZone,
        TimeZoneInfo berlinZone,
        decimal initialCapital,
        decimal dailyPct,
        decimal maxPct)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlinZone);

        var ordered = trades
            .OrderBy(t => t.CloseSource)
            .ThenBy(t => t.RowIndex)
            .ToList();

        var maxFloor = initialCapital * (1m - maxPct);

        var balance = initialCapital;
        DateOnly? currentDay = null;
        var dailyFloor = initialCapital - dailyPct * initialCapital;

        BreachPoint? dailyFirstBreach = null;
        BreachPoint? dailyFirstCleanBreach = null;
        IReadOnlyList<BreachContingencyCause> dailyCleanCandidateCauses = [];

        BreachPoint? maxFirstBreach = null;
        BreachPoint? maxFirstCleanBreach = null;
        IReadOnlyList<BreachContingencyCause> maxCleanCandidateCauses = [];

        // An Unscalable row contributes no P/L, so every later tracked balance is off by its
        // missing net. The STATIC floor never moves, so that offset matters at every later close:
        // the max-limit latch is permanent. The DAILY reference is `balance` itself at the day's
        // first close (see the day-change branch), so once a reference is taken AFTER the excluded
        // close, balance and reference carry the same offset and `balance < reference` is
        // unchanged. Only a daily breach whose reference predates the excluded close is uncertain:
        // tracked by `unscalableSinceDailyReference`, and — because the excluded close's own FTMO
        // day may be ambiguous — by intersecting its candidate days with the breaching close's.
        var unscalableClosedBefore = false;
        var unscalableSinceDailyReference = false;
        var unscalableCandidateDays = new HashSet<DateOnly>();

        foreach (var trade in ordered)
        {
            if (trade.Net is null)
            {
                // Unscalable: no balance contribution, but it can still downgrade a LATER breach.
                unscalableClosedBefore = true;
                unscalableSinceDailyReference = true;
                unscalableCandidateDays.UnionWith(
                    FtmoDayClock.Attribute(trade.CloseSource, sourceZone, berlinZone).CandidateDays);
                continue;
            }

            var attribution = FtmoDayClock.Attribute(trade.CloseSource, sourceZone, berlinZone);
            var day = attribution.CandidateDays.Min();

            if (currentDay is null || day != currentDay)
            {
                // New FTMO day begins: the reference balance is the balance carried from the end of
                // the previous day (or Initial Capital on day 1, which `balance` already equals
                // before any close has happened).
                dailyFloor = balance - dailyPct * initialCapital;
                currentDay = day;
                unscalableSinceDailyReference = false;
            }

            balance += trade.Net.Value;

            var causes = new List<BreachContingencyCause>();

            if (HasConcurrentOpenPosition(ordered, trade))
                causes.Add(BreachContingencyCause.ConcurrentOpenPosition);

            // Shared causes above apply to both limits; the Unscalable cause is scoped per limit.
            var maxCauses = new List<BreachContingencyCause>(causes);
            if (unscalableClosedBefore)
                maxCauses.Add(BreachContingencyCause.UnscalableTradeExcluded);

            var dailyBreached = balance < dailyFloor;
            var maxBreached = balance < maxFloor;

            if (dailyBreached)
            {
                var dailyCauses = new List<BreachContingencyCause>(causes);
                if (unscalableSinceDailyReference || unscalableCandidateDays.Overlaps(attribution.CandidateDays))
                    dailyCauses.Add(BreachContingencyCause.UnscalableTradeExcluded);
                if (attribution.Flags.HasFlag(FtmoDayClock.AttributionFlags.AmbiguousSourceTime))
                    dailyCauses.Add(BreachContingencyCause.AmbiguousSourceTime);
                if (attribution.Flags.HasFlag(FtmoDayClock.AttributionFlags.InvalidSourceTime))
                    dailyCauses.Add(BreachContingencyCause.InvalidSourceTime);
                if (attribution.Flags.HasFlag(FtmoDayClock.AttributionFlags.DstMismatchWindow))
                    dailyCauses.Add(BreachContingencyCause.DstMismatchWindow);

                var point = new BreachPoint(attribution.FtmoLocal, trade.CloseSource, balance, dailyFloor, dailyFloor - balance);
                dailyFirstBreach ??= point;

                if (dailyCauses.Count == 0 && dailyFirstCleanBreach is null)
                {
                    dailyFirstCleanBreach = point;
                }
                else if (dailyFirstCleanBreach is null && dailyCleanCandidateCauses.Count == 0)
                {
                    dailyCleanCandidateCauses = dailyCauses;
                }
            }

            if (maxBreached)
            {
                var point = new BreachPoint(attribution.FtmoLocal, trade.CloseSource, balance, maxFloor, maxFloor - balance);
                maxFirstBreach ??= point;

                if (maxCauses.Count == 0 && maxFirstCleanBreach is null)
                {
                    maxFirstCleanBreach = point;
                }
                else if (maxFirstCleanBreach is null && maxCleanCandidateCauses.Count == 0)
                {
                    maxCleanCandidateCauses = maxCauses;
                }
            }
        }

        var daily = BuildFinding(dailyFirstBreach, dailyFirstCleanBreach, dailyCleanCandidateCauses);
        var max = BuildFinding(maxFirstBreach, maxFirstCleanBreach, maxCleanCandidateCauses);

        return new FtmoBreachEvaluation(daily, max);
    }

    private static FtmoLimitFinding BuildFinding(
        BreachPoint? firstBreach, BreachPoint? firstCleanBreach, IReadOnlyList<BreachContingencyCause> firstBreachCauses)
    {
        if (firstCleanBreach is not null)
            return FtmoLimitFinding.BreachedAt(firstBreach!.Value, firstCleanBreach.Value);

        if (firstBreach is not null)
            return FtmoLimitFinding.Contingent(firstBreachCauses, firstBreach.Value);

        return FtmoLimitFinding.Clean();
    }

    /// <summary>
    /// Another trade's <c>OpenSource</c> precedes and <c>CloseSource</c> follows this close — reads
    /// <c>OpenTime</c>, which a bare net series drops (spec.md's Overlap requirement).
    /// </summary>
    private static bool HasConcurrentOpenPosition(
        IReadOnlyList<FtmoTradeProjector.ProjectedTrade> all, FtmoTradeProjector.ProjectedTrade close)
    {
        foreach (var other in all)
        {
            if (other.RowIndex == close.RowIndex)
                continue;

            if (other.OpenSource < close.CloseSource && other.CloseSource > close.CloseSource)
                return true;
        }

        return false;
    }
}
