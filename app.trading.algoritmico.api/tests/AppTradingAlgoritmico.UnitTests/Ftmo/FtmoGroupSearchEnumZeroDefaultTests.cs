using System.Reflection;
using System.Text.RegularExpressions;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 2a.1 (design D7, spec "The Job Lifecycle", "Disclosures") — every enum of the job contract has an
/// explicit non-completing zero member with its own test, the numeric values are pinned (they are wire values), and
/// no name or text uses the banned wording.
/// </summary>
public class FtmoGroupSearchEnumZeroDefaultTests
{
    private static readonly Regex Banned = new(@"surviv|\bpass(ed)?\b|\bsafe(ly|ty)?\b|would have", RegexOptions.IgnoreCase);

    [Fact]
    public void Status_DefaultValue_IsUnknown_AndNeverCompleted()
    {
        default(FtmoGroupSearchStatus).Should().Be(FtmoGroupSearchStatus.Unknown);
        default(FtmoGroupSearchStatus).Should().NotBe(FtmoGroupSearchStatus.Completed);
        ((int)FtmoGroupSearchStatus.Unknown).Should().Be(0);
    }

    [Fact]
    public void Status_Members_AreExactlyThese_WithPinnedNumbers()
    {
        Pairs<FtmoGroupSearchStatus>().Should().Equal(
            ("Unknown", 0), ("Running", 1), ("Completed", 2), ("StoppedAtBudget", 3), ("Cancelled", 4), ("Failed", 5));
    }

    [Fact]
    public void Stage_ZeroMember_IsUnknown_AndMembersArePinned()
    {
        default(FtmoGroupSearchStage).Should().Be(FtmoGroupSearchStage.Unknown);
        Pairs<FtmoGroupSearchStage>().Should().Equal(
            ("Unknown", 0), ("Loading", 1), ("Eligibility", 2), ("Proxy", 3), ("Simulating", 4), ("Ranking", 5));
    }

    [Fact]
    public void StopReason_ZeroMember_IsUnknown_NotNone_AndMembersArePinned()
    {
        default(FtmoGroupSearchStopReason).Should().Be(FtmoGroupSearchStopReason.Unknown);
        default(FtmoGroupSearchStopReason).Should().NotBe(FtmoGroupSearchStopReason.None);
        Pairs<FtmoGroupSearchStopReason>().Should().Equal(
            ("Unknown", 0), ("None", 1), ("MaxFullSimulations", 2), ("WallClock", 3));
    }

    [Fact]
    public void IneligibleReason_ZeroMember_IsUnknown_AndMembersArePinned()
    {
        default(FtmoGroupSearchIneligibleReason).Should().Be(FtmoGroupSearchIneligibleReason.Unknown);
        Pairs<FtmoGroupSearchIneligibleReason>().Should().Equal(
            ("Unknown", 0), ("MissingKind", 1), ("SymbolRefused", 2), ("ZoneUnresolved", 3),
            ("ProjectionRefused", 4), ("ProjectionRowless", 5), ("IdenticalDeployEval", 6));
    }

    [Fact]
    public void BannedWording_IsAbsentFromEnumNames_DtoPropertyNames_AndDisclosures()
    {
        var types = typeof(FtmoGroupSearchStatus).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(FtmoGroupSearchStatus).Namespace && t.Name.StartsWith("FtmoGroupSearch", StringComparison.Ordinal))
            .ToList();
        types.Should().Contain(t => t == typeof(FtmoGroupSearchJobDto));

        var names = types.SelectMany(t => t.IsEnum
            ? t.GetEnumNames().Append(t.Name)
            : t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Append(t.Name));
        names.Where(n => Banned.IsMatch(n)).Should().BeEmpty();

        foreach (var text in FtmoGroupSearchDisclosures.Build(12926, 150, 150, FtmoGroupSimulationLimits.Disclosures))
            Banned.IsMatch(text).Should().BeFalse($"'{text}' must not affirm an outcome");
    }

    [Fact]
    public void BannedWordingSweep_ActuallyCatchesTheBannedWords()
    {
        foreach (var word in new[] { "SurvivalShare", "passed", "Safe", "would have passed", "survived" })
            Banned.IsMatch(word).Should().BeTrue(word);
    }

    private static (string, int)[] Pairs<T>() where T : struct, Enum
        => [.. Enum.GetValues<T>().Select(v => (v.ToString(), Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture)))];
}
