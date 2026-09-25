using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// PR P1, task 1.1/1.2 — characterization pins for the BCL <see cref="TimeZoneInfo"/> behaviour
/// design.md Decision 1 relies on. This suite observes the BCL directly; it does not exercise any
/// production code (<see cref="AppTradingAlgoritmico.Infrastructure.Services"/> has no
/// <c>FtmoDayClock</c> yet). It re-pins what was measured on Windows .NET 9 via pwsh 2026-09-24
/// (Engram #2769) and additionally pins the Linux container's tzdata-backed behaviour per task 1.2.
/// A failing pin here is the NodaTime decision point (design.md Decision 1), not a reason to weaken
/// the assertion.
/// </summary>
public class FtmoTimeZoneCharacterizationTests
{
    private static readonly TimeZoneInfo Jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Fact]
    public void FindSystemTimeZoneById_JerusalemAndBerlin_BothResolve()
    {
        Jerusalem.Should().NotBeNull();
        Berlin.Should().NotBeNull();
    }

    [Theory]
    [InlineData(2026, 1, 15)] // winter
    [InlineData(2026, 7, 15)] // summer
    public void ConvertTimeFromUtc_0030Jerusalem_MapsToPreviousBerlinDay2330(int year, int month, int day)
    {
        var jerusalemLocal = new DateTime(year, month, day, 0, 30, 0, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(jerusalemLocal, Jerusalem);
        var berlinLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, Berlin);

        var expectedPreviousDay = new DateTime(year, month, day, 0, 0, 0).AddDays(-1);
        berlinLocal.Date.Should().Be(expectedPreviousDay.Date);
        berlinLocal.TimeOfDay.Should().Be(new TimeSpan(23, 30, 0));
    }

    [Fact]
    public void IsAmbiguousTime_20131027_0130Jerusalem_IsTrue_AndConvertTimeToUtcAssumesStandardTime()
    {
        var fallBack = new DateTime(2013, 10, 27, 1, 30, 0, DateTimeKind.Unspecified);

        Jerusalem.IsAmbiguousTime(fallBack).Should().BeTrue();

        var utc = TimeZoneInfo.ConvertTimeToUtc(fallBack, Jerusalem);
        var standardOffsetUtc = fallBack - Jerusalem.BaseUtcOffset;
        utc.Should().Be(standardOffsetUtc);
    }

    [Fact]
    public void IsInvalidTime_20130329_0230Jerusalem_IsTrue_AndConvertTimeToUtcThrows()
    {
        var springForward = new DateTime(2013, 3, 29, 2, 30, 0, DateTimeKind.Unspecified);

        Jerusalem.IsInvalidTime(springForward).Should().BeTrue();

        var act = () => TimeZoneInfo.ConvertTimeToUtc(springForward, Jerusalem);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsAmbiguousTime_20120923_0130Jerusalem_IsTrue_UnderPre2013IsraeliRule()
    {
        var preRuleFallBack = new DateTime(2012, 9, 23, 1, 30, 0, DateTimeKind.Unspecified);

        Jerusalem.IsAmbiguousTime(preRuleFallBack).Should().BeTrue();
    }
}
