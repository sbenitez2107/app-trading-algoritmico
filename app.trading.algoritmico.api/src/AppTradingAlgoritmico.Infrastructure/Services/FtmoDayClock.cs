namespace AppTradingAlgoritmico.Infrastructure.Services;

/// <summary>
/// PR P1 — maps a source-zone timestamp to its FTMO trading day. `internal static`, pure: no I/O,
/// no <c>DateTime.Now</c>, no randomness (design.md Decision 1).
/// <para>
/// Deliberately NEVER calls <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/> on a
/// raw source-local timestamp: that method silently assumes standard time on an ambiguous input and
/// throws <see cref="ArgumentException"/> on an invalid one — both unacceptable here. Instead it
/// builds the candidate set of UTC instants directly (ambiguous → both
/// <see cref="TimeZoneInfo.GetAmbiguousTimeOffsets"/>; invalid → <c>local - BaseUtcOffset</c> and
/// <c>local - (BaseUtcOffset + 1h)</c>; otherwise the single <see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/>
/// offset) and converts each candidate through <see cref="TimeZoneInfo.ConvertTimeFromUtc"/>, which is
/// unambiguous from UTC.
/// </para>
/// </summary>
internal static class FtmoDayClock
{
    [Flags]
    public enum AttributionFlags
    {
        None = 0,
        AmbiguousSourceTime = 1 << 0,
        InvalidSourceTime = 1 << 1,
        DstMismatchWindow = 1 << 2,
    }

    public enum ZoneResolutionRefusal
    {
        TimeZoneDataUnavailable,
    }

    public readonly record struct DayAttribution(
        IReadOnlySet<DateOnly> CandidateDays,
        DateTime FtmoLocal,
        AttributionFlags Flags);

    public readonly record struct ZoneResolutionResult(
        bool IsSuccess,
        DayAttribution? Attribution,
        ZoneResolutionRefusal? Refusal)
    {
        public static ZoneResolutionResult Success(DayAttribution attribution)
            => new(true, attribution, null);

        public static ZoneResolutionResult Failure(ZoneResolutionRefusal refusal)
            => new(false, null, refusal);
    }

    /// <summary>
    /// Resolves <paramref name="sourceZoneId"/> per design.md Decision 1's fallback chain:
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById(string)"/> first, then
    /// <see cref="TimeZoneInfo.TryConvertIanaIdToWindowsId"/>. If both fail, returns
    /// <see cref="ZoneResolutionRefusal.TimeZoneDataUnavailable"/> — never a fallback offset, never a throw.
    /// </summary>
    public static ZoneResolutionResult AttributeFromIanaId(DateTime sourceLocal, string sourceZoneId, TimeZoneInfo berlin)
    {
        EnsureWallClock(sourceLocal);
        ArgumentNullException.ThrowIfNull(sourceZoneId);
        ArgumentNullException.ThrowIfNull(berlin);

        var sourceZone = ResolveZone(sourceZoneId);
        if (sourceZone is null)
            return ZoneResolutionResult.Failure(ZoneResolutionRefusal.TimeZoneDataUnavailable);

        return ZoneResolutionResult.Success(Attribute(sourceLocal, sourceZone, berlin));
    }

    /// <summary>
    /// PR P4 — exposes the same fallback chain <see cref="AttributeFromIanaId"/> uses internally, so
    /// the read service can resolve BOTH the source zone and <c>Europe/Berlin</c> up front and refuse
    /// with <see cref="ZoneResolutionRefusal.TimeZoneDataUnavailable"/> before touching any trade,
    /// rather than discovering the failure mid-replay.
    /// </summary>
    internal static bool TryResolveZone(string ianaId, out TimeZoneInfo? zone)
    {
        zone = ResolveZone(ianaId);
        return zone is not null;
    }

    private static TimeZoneInfo? ResolveZone(string ianaId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch (TimeZoneNotFoundException)
                {
                    return null;
                }
            }

            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the candidate-set conversion and computes the DST-mismatch flag per design.md
    /// Decision 1 and spec.md's Day-Boundary requirement.
    /// </summary>
    public static DayAttribution Attribute(DateTime sourceLocal, TimeZoneInfo sourceZone, TimeZoneInfo berlin)
    {
        EnsureWallClock(sourceLocal);
        ArgumentNullException.ThrowIfNull(sourceZone);
        ArgumentNullException.ThrowIfNull(berlin);

        var flags = AttributionFlags.None;
        List<DateTime> candidateUtcInstants;

        if (sourceZone.IsInvalidTime(sourceLocal))
        {
            flags |= AttributionFlags.InvalidSourceTime;
            candidateUtcInstants =
            [
                DateTime.SpecifyKind(sourceLocal - sourceZone.BaseUtcOffset, DateTimeKind.Utc),
                DateTime.SpecifyKind(sourceLocal - (sourceZone.BaseUtcOffset + TimeSpan.FromHours(1)), DateTimeKind.Utc),
            ];
        }
        else if (sourceZone.IsAmbiguousTime(sourceLocal))
        {
            flags |= AttributionFlags.AmbiguousSourceTime;
            var offsets = sourceZone.GetAmbiguousTimeOffsets(sourceLocal);
            candidateUtcInstants = offsets
                .Select(offset => DateTime.SpecifyKind(sourceLocal - offset, DateTimeKind.Utc))
                .ToList();
        }
        else
        {
            var offset = sourceZone.GetUtcOffset(sourceLocal);
            candidateUtcInstants = [DateTime.SpecifyKind(sourceLocal - offset, DateTimeKind.Utc)];
        }

        var candidateDays = new HashSet<DateOnly>();
        var ftmoLocal = default(DateTime);
        foreach (var utc in candidateUtcInstants)
        {
            var berlinLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, berlin);
            ftmoLocal = berlinLocal;
            candidateDays.Add(DateOnly.FromDateTime(berlinLocal));
        }

        // Mismatch-window sensitivity (design.md Decision 1): only when the near-constant 1h
        // offset breaks down AND the exact day disagrees with the naive (source - 1h) day.
        if (candidateUtcInstants.Count == 1)
        {
            var utc = candidateUtcInstants[0];
            var jerusalemOffset = sourceZone.GetUtcOffset(utc);
            var berlinOffset = berlin.GetUtcOffset(utc);
            if (jerusalemOffset - berlinOffset != TimeSpan.FromHours(1))
            {
                var naiveDay = DateOnly.FromDateTime(sourceLocal.AddHours(-1));
                if (!candidateDays.Contains(naiveDay))
                    flags |= AttributionFlags.DstMismatchWindow;
            }
        }

        return new DayAttribution(candidateDays, ftmoLocal, flags);
    }

    /// <summary>
    /// The input is a wall clock in the SOURCE zone, so only <see cref="DateTimeKind.Unspecified"/> is
    /// valid. With <see cref="DateTimeKind.Utc"/> or <see cref="DateTimeKind.Local"/>,
    /// <see cref="TimeZoneInfo.IsAmbiguousTime(DateTime)"/> skips ambiguity detection, and Local is read
    /// against the host's zone, so the same data would land on a different FTMO day on each host.
    /// <para>
    /// This throws rather than returning a typed outcome because a wrong kind is a caller bug, not a
    /// data condition: every production source (EF Core <c>datetime2</c> columns with no value
    /// converter, <c>TryParseExact</c> with <see cref="System.Globalization.DateTimeStyles.None"/>)
    /// yields Unspecified. The same reasoning applies to the <see cref="ArgumentNullException"/> guards
    /// here. The value is never normalized with <see cref="DateTime.SpecifyKind"/>, because that would
    /// hide the bug this guard exists to catch.
    /// </para>
    /// </summary>
    private static void EnsureWallClock(DateTime sourceLocal)
    {
        if (sourceLocal.Kind != DateTimeKind.Unspecified)
            throw new ArgumentException(
                $"Expected a source-zone wall clock with DateTimeKind.Unspecified, got {sourceLocal.Kind}.",
                nameof(sourceLocal));
    }
}
