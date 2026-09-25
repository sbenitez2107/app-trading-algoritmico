using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// PR P1, Phase 2 — <see cref="FtmoDayClock.Attribute"/>. Pure domain unit; no I/O. Exercises the
/// candidate-set conversion from design.md Decision 1: ambiguous/invalid source timestamps yield two
/// candidate FTMO days, otherwise one; a DST-mismatch window is flagged only when the exact day
/// differs from the naive <c>(source - 1h).Date</c> day (spec.md Day-Boundary requirement).
/// </summary>
public class FtmoDayClockTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Fact]
    public void Attribute_00_30JerusalemOutsideDstWindow_MapsToPreviousBerlinDay()
    {
        var sourceLocal = new DateTime(2026, 1, 15, 0, 30, 0, DateTimeKind.Unspecified);

        var result = FtmoDayClock.Attribute(sourceLocal, Jerusalem, Berlin);

        result.CandidateDays.Should().ContainSingle()
            .Which.Should().Be(DateOnly.FromDateTime(new DateTime(2026, 1, 14)));
        result.Flags.Should().Be(FtmoDayClock.AttributionFlags.None);
    }

    [Fact]
    public void Attribute_2013_10_27_01_30Jerusalem_FlagsAmbiguousSourceTime()
    {
        // Measured (probe): the two ambiguous offsets (02:00, 03:00) convert to Berlin
        // 2013-10-27 01:30 and 2013-10-27 00:30 respectively — two instants, same calendar day.
        // The candidate set is built from BOTH offsets (never a silent standard-time guess); the
        // flag, not day-count, is what proves both were considered.
        var sourceLocal = new DateTime(2013, 10, 27, 1, 30, 0, DateTimeKind.Unspecified);

        var result = FtmoDayClock.Attribute(sourceLocal, Jerusalem, Berlin);

        result.Flags.Should().HaveFlag(FtmoDayClock.AttributionFlags.AmbiguousSourceTime);
        result.CandidateDays.Should().ContainSingle()
            .Which.Should().Be(DateOnly.FromDateTime(new DateTime(2013, 10, 27)));
    }

    [Fact]
    public void Attribute_2013_03_29_02_30Jerusalem_FlagsInvalidSourceTime()
    {
        // Measured (probe): both invalid-time candidates (local - BaseUtcOffset, local -
        // (BaseUtcOffset + 1h)) convert to Berlin 2013-03-29 01:30 and 00:30 — same calendar day,
        // two distinct instants. The flag proves both candidates were built, never a silent throw.
        var sourceLocal = new DateTime(2013, 3, 29, 2, 30, 0, DateTimeKind.Unspecified);

        var result = FtmoDayClock.Attribute(sourceLocal, Jerusalem, Berlin);

        result.Flags.Should().HaveFlag(FtmoDayClock.AttributionFlags.InvalidSourceTime);
        result.CandidateDays.Should().ContainSingle()
            .Which.Should().Be(DateOnly.FromDateTime(new DateTime(2013, 3, 29)));
    }

    [Fact]
    public void Attribute_MismatchWindowExactDayDiffersFromNaive_FlagsDstMismatchWindow()
    {
        // IL DST starts 2013-03-29 (Friday); EU DST starts 2013-03-31 (last Sunday of March).
        // Between those dates Jerusalem-Berlin is 2h, not the near-constant 1h. At 01:30 local,
        // measured (probe): exact Berlin day is 2013-03-29 but naive (source-1h) day is
        // 2013-03-30 — a genuine mismatch.
        var sourceLocal = new DateTime(2013, 3, 30, 1, 30, 0, DateTimeKind.Unspecified);

        var result = FtmoDayClock.Attribute(sourceLocal, Jerusalem, Berlin);

        var naiveDay = DateOnly.FromDateTime(sourceLocal.AddHours(-1));
        result.CandidateDays.Should().NotContain(naiveDay);
        result.Flags.Should().HaveFlag(FtmoDayClock.AttributionFlags.DstMismatchWindow);
    }

    [Fact]
    public void Attribute_MismatchWindowExactDayMatchesNaive_NoFlagFromMismatchAlone()
    {
        // Same 2h-offset window (IL on DST, EU not yet), but at noon the exact and naive days
        // still coincide because no midnight boundary is crossed by the extra hour of drift
        // (measured via probe: exactDay == naiveDay == 2013-03-30).
        var sourceLocal = new DateTime(2013, 3, 30, 12, 0, 0, DateTimeKind.Unspecified);

        var result = FtmoDayClock.Attribute(sourceLocal, Jerusalem, Berlin);

        var naiveDay = DateOnly.FromDateTime(sourceLocal.AddHours(-1));
        result.CandidateDays.Should().ContainSingle().Which.Should().Be(naiveDay);
        result.Flags.Should().NotHaveFlag(FtmoDayClock.AttributionFlags.DstMismatchWindow);
    }

    [Fact]
    public void Attribute_UnresolvableZoneId_ReturnsTimeZoneDataUnavailable()
    {
        var result = FtmoDayClock.AttributeFromIanaId(
            new DateTime(2026, 1, 15, 0, 30, 0, DateTimeKind.Unspecified), "Not/A_Real_Zone", Berlin);

        result.IsSuccess.Should().BeFalse();
        result.Refusal.Should().Be(FtmoDayClock.ZoneResolutionRefusal.TimeZoneDataUnavailable);
    }

    [Fact]
    public void Attribute_SummerJerusalemUtcPlus03_UsesTheZoneOffsetNotAFixedPlus02()
    {
        // 2026-07-15 00:30 Jerusalem is IDT (UTC+03:00) -> 2026-07-14 21:30 UTC -> Berlin CEST
        // (UTC+02:00) 2026-07-14 23:30. A fixed +02:00 source offset would instead yield
        // 22:30 UTC -> Berlin 2026-07-15 00:30, the NEXT FTMO day. Pinning the clock's own output
        // here fails if the clock ever regresses to a fixed offset.
        var summer = new DateTime(2026, 7, 15, 0, 30, 0, DateTimeKind.Unspecified);

        var result = FtmoDayClock.Attribute(summer, Jerusalem, Berlin);

        result.CandidateDays.Should().ContainSingle()
            .Which.Should().Be(new DateOnly(2026, 7, 14));
        result.FtmoLocal.Should().Be(new DateTime(2026, 7, 14, 23, 30, 0));
    }

    // The clock interprets its input as a wall clock in the SOURCE zone. A Utc/Local kind makes
    // TimeZoneInfo skip ambiguity detection and (for Local) read the value against the MACHINE's
    // zone, so the same data would land on different FTMO days per host. Only Unspecified is valid.
    // The ambiguous 2013-10-27 01:30 Jerusalem wall clock is used because it is exactly the value
    // whose ambiguity would be silently lost.
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Attribute_NonUnspecifiedKind_ThrowsArgumentException(DateTimeKind kind)
    {
        var sourceLocal = new DateTime(2013, 10, 27, 1, 30, 0, kind);

        var act = () => FtmoDayClock.Attribute(sourceLocal, Jerusalem, Berlin);

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("sourceLocal");
    }

    [Theory]
    [InlineData(DateTimeKind.Utc, "Asia/Jerusalem")]
    [InlineData(DateTimeKind.Local, "Asia/Jerusalem")]
    [InlineData(DateTimeKind.Utc, "Not/A_Real_Zone")]
    public void AttributeFromIanaId_NonUnspecifiedKind_ThrowsBeforeZoneResolution(DateTimeKind kind, string zoneId)
    {
        var sourceLocal = new DateTime(2013, 10, 27, 1, 30, 0, kind);

        var act = () => FtmoDayClock.AttributeFromIanaId(sourceLocal, zoneId, Berlin);

        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("sourceLocal");
    }
}
