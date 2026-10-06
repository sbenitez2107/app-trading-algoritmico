using System.Globalization;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchEngine;
using static AppTradingAlgoritmico.Infrastructure.Services.FtmoGroupSearchRanking;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// The pure pieces of the calibration and the read-only guard, proven WITHOUT a real database (SQLite in memory), so
/// the gated run below can be trusted not to write and to measure what it claims.
///
/// Calibration record (2026-10-06, PASSED): SBDEMO2 pool, FX 1.05..1.20, maxPerInstrument=2; 130 strategies, 18 eligible,
/// 4029 enumerated, 1719 survivors, shortlist 150, full ground truth of 1719 groups (k4 stride 1). recall@10 and recall@25 =
/// 1.000 for k=2, 3 and 4; DEPTH d100 = K. Run took 4.5 h (~9.4 s per group, sequential). The old proxy failed with
/// recall@25 0.72 / 0.36 / 0.16 and was replaced by FtmoRaceSurrogate; a strided k4 truth gave a false failure (0.6 / 0.24).
/// </summary>
public class FtmoGroupSearchCalibrationGuardTests
{
    // ---- the interceptor ----

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("select [s].[Id] from [Strategies] as [s] where [s].[Id] in (@p0)")]
    [InlineData("  \r\n\tSELECT TOP(2) 1 FROM [T]")]
    [InlineData("-- a comment\nSELECT 1")]
    [InlineData("/* block */ SELECT [UpdatedAt], [CreatedAt] FROM [T]")]
    public void IsSelectOnly_AcceptsASelect_EvenWithCommentsAndColumnsNamedLikeVerbs(string sql)
        => ReadOnlyCommandInterceptor.IsSelectOnly(sql).Should().BeTrue();

    [Theory]
    [InlineData("INSERT INTO [T] ([A]) VALUES (1)")]
    [InlineData("UPDATE [T] SET [A] = 1")]
    [InlineData("DELETE FROM [T]")]
    [InlineData("MERGE [T] USING [U] ON 1 = 1 WHEN MATCHED THEN DELETE;")]
    [InlineData("WITH x AS (SELECT 1 AS a) DELETE FROM [T]")]
    [InlineData("SELECT 1; DROP TABLE [T]")]
    [InlineData("SELECT 1 FROM [T]; UPDATE [T] SET [A] = 1")]
    [InlineData("EXEC sp_who")]
    [InlineData("SET NOCOUNT ON; SELECT 1")]
    [InlineData("TRUNCATE TABLE [T]")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSelectOnly_RefusesAnythingThatIsNotASingleSelect(string sql)
        => ReadOnlyCommandInterceptor.IsSelectOnly(sql).Should().BeFalse();

    // RELIABILITY-001 — each of these passed the former denylist guard.
    [Theory]
    [InlineData("SELECT * INTO t FROM x")]
    [InlineData("SELECT [A] into [Copy] FROM [T]")]
    [InlineData("SELECT 1; SHUTDOWN")]
    [InlineData("SELECT 1; KILL 52")]
    [InlineData("SELECT 1; BACKUP DATABASE [D] TO DISK = 'x.bak'")]
    [InlineData("SELECT 1; DBCC CHECKDB")]
    [InlineData("SELECT 1; WAITFOR DELAY '00:10'")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1;;")]
    [InlineData("SELECT '--'; SHUTDOWN")]
    [InlineData("SELECT '--' ; DROP TABLE [T]")]
    [InlineData("SELECT '/*'; SHUTDOWN; SELECT '*/'")]
    [InlineData("SELECT 1 /* unterminated")]
    [InlineData("SELECT 'unterminated")]
    [InlineData("WITH x AS (SELECT 1 AS a) SELECT a INTO t FROM x")]
    [InlineData("SELECT 1 EXEC('SHUTDOWN')")]
    [InlineData("SELECT 1 EXECUTE sp_who")]
    [InlineData("SHUTDOWN -- SELECT")]
    public void IsSelectOnly_RefusesTheDenylistBypasses(string sql)
        => ReadOnlyCommandInterceptor.IsSelectOnly(sql).Should().BeFalse();

    [Theory]
    [InlineData("SELECT 1;")]
    [InlineData("SELECT 1 ;  \r\n")]
    [InlineData("SELECT 'a; INTO; EXEC' AS [x]")]
    [InlineData("SELECT 'it''s; into' FROM [T]")]
    [InlineData("SELECT [Into], \"Exec\", [a;b] FROM [T]")]
    [InlineData("SELECT 1 -- trailing; INTO\n")]
    [InlineData("/* a */ -- b\n WITH x AS (SELECT 1 AS a) SELECT a FROM x")]
    [InlineData("SELECT [s].[ExecutedAt], [s].[IntoValue] FROM [S] AS [s]")]
    public void IsSelectOnly_AcceptsSingleReads_WithSeparatorsAndKeywordsOnlyInsideLiteralsOrIdentifiers(string sql)
        => ReadOnlyCommandInterceptor.IsSelectOnly(sql).Should().BeTrue();

    private sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            {
                if (string.Equals(property.GetColumnType(), "nvarchar(max)", StringComparison.OrdinalIgnoreCase))
                    property.SetColumnType(null);
            }
        }
    }

    /// <summary>The schema is created through an UNGUARDED context on the same connection, then the guarded one is returned.</summary>
    private static async Task<(SqliteAppDbContext Guarded, SqliteConnection Connection)> GuardedContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        await using (var setup = new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Strategies.Add(new Strategy { Id = Guid.NewGuid(), Name = "seed", CreatedAt = DateTime.UtcNow, TradingAccountId = Guid.NewGuid() });
            await setup.SaveChangesAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(new ReadOnlyCommandInterceptor(), new NoSaveChangesInterceptor())
            .Options;
        return (new SqliteAppDbContext(options), connection);
    }

    [Fact]
    public async Task AGuardedContext_StillReads()
    {
        var (db, connection) = await GuardedContextAsync();
        await using var _ = connection;
        await using var __ = db;

        var names = await db.Strategies.Select(s => s.Name).ToListAsync();

        names.Should().Equal("seed");
    }

    [Fact]
    public async Task AnInsertThroughAGuardedContext_Throws_AndTheRowIsNotWritten()
    {
        var (db, connection) = await GuardedContextAsync();
        await using var _ = connection;
        await using var __ = db;

        var raw = async () => await db.Database.ExecuteSqlRawAsync("INSERT INTO Strategies (Id, Name) VALUES ('x', 'y')");
        await raw.Should().ThrowAsync<InvalidOperationException>().WithMessage("*read-only*");

        db.Strategies.Add(new Strategy { Id = Guid.NewGuid(), Name = "no", CreatedAt = DateTime.UtcNow, TradingAccountId = Guid.NewGuid() });
        var save = async () => await db.SaveChangesAsync();
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*read-only*");

        db.ChangeTracker.Clear();
        (await db.Strategies.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AnUpdateAndADeleteThroughAGuardedContext_Throw()
    {
        var (db, connection) = await GuardedContextAsync();
        await using var _ = connection;
        await using var __ = db;

        var update = async () => await db.Database.ExecuteSqlRawAsync("UPDATE Strategies SET Name = 'z'");
        var delete = async () => await db.Database.ExecuteSqlRawAsync("DELETE FROM Strategies");
        await update.Should().ThrowAsync<InvalidOperationException>();
        await delete.Should().ThrowAsync<InvalidOperationException>();
        (await db.Strategies.CountAsync()).Should().Be(1);
    }

    // ---- the vacuity guard, the FX band and the pool log ----

    [Theory]
    [InlineData(72, 72, 150, true)]   // the first real run: survivors fit in the shortlist
    [InlineData(150, 150, 150, true)] // boundary: survivors == ShortlistSize
    [InlineData(300, 300, 150, true)] // shortlist somehow equals the survivors
    [InlineData(151, 150, 150, false)]
    [InlineData(5000, 150, 150, false)]
    public void IsVacuous_WhenTheShortlistHoldsEverySurvivor(int survivors, int shortlist, int size, bool expected)
        => FtmoGroupSearchCalibration.IsVacuous(survivors, shortlist, size).Should().Be(expected);

    [Fact]
    public void VacuousMessage_SaysThatNothingIsDemonstrated_AndCarriesTheCounts()
        => FtmoGroupSearchCalibration.VacuousMessage(72, 72, 150).Should()
            .Contain("VACUOUS").And.Contain("nothing is demonstrated").And.Contain("survivors=72").And.Contain("ShortlistSize=150");

    [Fact]
    public void ResolveFxBand_DefaultsTo105And120_WhenUnsetOrBlank_AndReadsInvariantOverrides()
    {
        FtmoGroupSearchCalibration.ResolveFxBand(null, null).Should().Be((1.05m, 1.20m));
        FtmoGroupSearchCalibration.ResolveFxBand("", "  ").Should().Be((1.05m, 1.20m));
        FtmoGroupSearchCalibration.ResolveFxBand("0.9", "1.4").Should().Be((0.9m, 1.4m));
        FtmoGroupSearchCalibration.ResolveFxBand("1.10", null).Should().Be((1.10m, 1.20m));
    }

    [Fact]
    public void SymbolCounts_ListsEachSymbolWithItsCount_OrderedBySymbol()
        => FtmoGroupSearchCalibration.SymbolCounts(["XAUUSD", "NQ", "XAUUSD", null, "GER40"]).Should()
            .Be("(none)=1 GER40=1 NQ=1 XAUUSD=2");

    // ---- the ground-truth sample and the metric ----

    private static List<int[]> Combos(int pool, int kMin, int kMax) => [.. Enumerate(pool, kMin, kMax)];

    [Fact]
    public void GroundTruthSample_TakesEveryPairAndTriple_AndEveryTenthFourMemberCandidateInEnumerationOrder()
    {
        var survivors = Combos(10, 2, 4); // 45 + 120 + 210
        var sample = FtmoGroupSearchCalibration.GroundTruthSample(survivors, stride: 10);

        sample.Count(c => c.Length == 2).Should().Be(45);
        sample.Count(c => c.Length == 3).Should().Be(120);
        var fours = sample.Where(c => c.Length == 4).ToList();
        fours.Should().HaveCount(21, "indices 0, 10, ... 200 of the 210 four-member candidates");
        fours.Should().Equal(survivors.Where(c => c.Length == 4).Where((_, i) => i % 10 == 0));
        sample.Should().Equal(survivors.Where(c => c.Length < 4).Concat(fours), "enumeration order is kept");
    }

    [Fact]
    public void RecallAtK_IsTheShareOfTheTrueTopKThatTheShortlistHolds()
    {
        string[] truth = ["a", "b", "c", "d", "e"];

        FtmoGroupSearchCalibration.RecallAtK(truth, new HashSet<string> { "a", "c", "z" }, 2).Should().Be(0.5m, "top 2 = a, b; only a is shortlisted");
        FtmoGroupSearchCalibration.RecallAtK(truth, new HashSet<string> { "a", "b", "c" }, 3).Should().Be(1m);
        FtmoGroupSearchCalibration.RecallAtK(truth, new HashSet<string>(), 3).Should().Be(0m, "a recall of 0 is a result, not a missing value");
        FtmoGroupSearchCalibration.RecallAtK(truth, new HashSet<string> { "a", "b", "c", "d", "e" }, 10).Should().Be(1m, "K above the number of ranked candidates is clamped to it");
        FtmoGroupSearchCalibration.RecallAtK([], new HashSet<string> { "a" }, 10).Should().Be(0m);
    }

    [Fact]
    public void RecallAtK_CountsACandidateThatTiesWithTheKthEntry_AsInTheTopK()
    {
        // (key, score): b and c tie at the K = 2 boundary; only the id tie-break put b first.
        (string Key, int Score)[] truth = [("a", 1), ("b", 2), ("c", 2), ("d", 3)];
        static bool Ties((string, int Score) x, (string, int Score) y) => x.Score == y.Score;
        static string Key((string Key, int) t) => t.Key;

        FtmoGroupSearchCalibration.RecallAtK(truth, Key, Ties, new HashSet<string> { "a", "c" }, 2)
            .Should().Be(1m, "c ties with the 2nd entry, so it counts as in the top 2");
        FtmoGroupSearchCalibration.RecallAtK(truth, Key, Ties, new HashSet<string> { "a", "b", "c" }, 2)
            .Should().Be(1m, "hits are capped at K");
        FtmoGroupSearchCalibration.RecallAtK(truth, Key, Ties, new HashSet<string> { "a", "d" }, 2)
            .Should().Be(0.5m, "d does not tie with the boundary");
        FtmoGroupSearchCalibration.RecallAtK(truth, Key, static (_, _) => false, new HashSet<string> { "a", "c" }, 2)
            .Should().Be(0.5m, "without the tie rule the id order decides");
    }

    [Fact]
    public void DepthToHold_IsTheProxyRankAtWhichTheTrueTopKIsHeld()
    {
        string[] truth = ["a", "b", "c", "d"];
        string[] proxy = ["x", "b", "y", "a", "c", "d"];
        static string Key(string s) => s;
        static bool NoTies(string x, string y) => false;

        // top 2 = a, b: b at depth 2, a at depth 4.
        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, NoTies, 2, 0.5m).Should().Be(2, "ceil(0.5 * 2) = 1 hit: b");
        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, NoTies, 2, 0.9m).Should().Be(4, "ceil(0.9 * 2) = 2 hits");
        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, NoTies, 2, 1m).Should().Be(4);
        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, NoTies, 10, 1m).Should().Be(6, "K above the ranked count is clamped to it");
        FtmoGroupSearchCalibration.DepthToHold(proxy, Array.Empty<string>(), Key, NoTies, 10, 1m).Should().Be(0, "nothing to hold");
    }

    [Fact]
    public void DepthToHold_CountsACandidateThatTiesWithTheKthEntry_AsInTheTopK()
    {
        // b and c tie at the K = 2 boundary: holding a and c is already the whole top 2.
        (string Key, int Score)[] truth = [("a", 1), ("b", 2), ("c", 2), ("d", 3)];
        string[] proxy = ["a", "c", "b", "d"];
        static bool Ties((string, int Score) x, (string, int Score) y) => x.Score == y.Score;
        static string Key((string Key, int) t) => t.Key;

        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, Ties, 2, 1m).Should().Be(2, "c stands in for b");
        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, static (_, _) => false, 2, 1m)
            .Should().Be(3, "without the tie rule b is required");
    }

    [Fact]
    public void DepthToHold_IsNull_WhenATrueTopCandidateIsAbsentFromTheProxyOrder()
    {
        string[] truth = ["a", "b", "c"];
        string[] proxy = ["a", "c"];
        static string Key(string s) => s;
        static bool NoTies(string x, string y) => false;

        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, NoTies, 2, 0.5m).Should().Be(1, "a alone meets 1 of 2");
        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, NoTies, 2, 1m).Should().BeNull("b was removed by the proxy and is never held");
        FtmoGroupSearchCalibration.DepthToHold(proxy, truth, Key, NoTies, 3, 1m).Should().BeNull();
    }

    [Fact]
    public void TheProductionRankingTieRule_IgnoresOnlyTheIdTieBreak()
    {
        static FtmoGroupKindResultDto Evaluated(BacktestRunKind kind)
        {
            FtmoOrderStatisticsDto Days(int? m) => new(m is null ? 0 : 1, m, m, m, m, m);
            var summary = new FtmoMultiStartSummaryDto(
                100, [new(FtmoChainOutcome.Phase1Breached, false, 10, 0.1m)], 0, Days(null), Days(null), Days(100), Days(null), Days(null), Days(null));
            var run = new FtmoMultiStartRunDto(
                Guid.Empty, kind, BacktestSegment.Unknown, FtmoSimulationStatus.Evaluated, null, null, null, FtmoStartGrain.Monthly,
                new FtmoChallengeRulesDto(10m, 5m, 4, null), [], summary, [], false, null, null, 0, [], string.Empty);
            return new FtmoGroupKindResultDto(kind, FtmoSimulationStatus.Evaluated, null, [], null, [], run);
        }

        RankEntry Entry(int peak, decimal headroom, params Guid[] ids)
            => new(ids, peak, [Evaluated(BacktestRunKind.Deploy), Evaluated(BacktestRunKind.Evaluation)], headroom);
        static bool Ties(RankEntry a, RankEntry b) => Comparer.Compare(a, b with { MemberIds = a.MemberIds }) == 0;
        Guid g1 = Guid.Parse("00000000-0000-0000-0000-000000000001"), g2 = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            g3 = Guid.Parse("00000000-0000-0000-0000-000000000003");

        Comparer.Compare(Entry(1, 0.5m, g1, g2), Entry(1, 0.5m, g1, g3)).Should().NotBe(0, "the ranking is total through the ids");
        Ties(Entry(1, 0.5m, g1, g2), Entry(1, 0.5m, g1, g3)).Should().BeTrue("they differ only in the id tie-break");
        Ties(Entry(1, 0.5m, g1, g2), Entry(2, 0.5m, g1, g3)).Should().BeFalse("peak is a ranking key");
        Ties(Entry(1, 0.5m, g1, g2), Entry(1, 0.4m, g1, g3)).Should().BeFalse("headroom is a ranking key");
    }

    // ---- RELIABILITY-001: the SQL EF Core actually generates for the calibration's query shapes ----

    private sealed class RecordingInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task TheCalibrationsOwnEfQueries_GenerateOnlySingleReads_AndPassTheGuard()
    {
        var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        await using var _ = connection;
        var accountId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        await using (var setup = new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.TradingAccounts.Add(new TradingAccount { Id = accountId, Name = "SBDEMO2-x", CreatedAt = DateTime.UtcNow });
            setup.Strategies.Add(new Strategy { Id = strategyId, Name = "seed", CreatedAt = DateTime.UtcNow, TradingAccountId = accountId });
            await setup.SaveChangesAsync();
        }

        var recorder = new RecordingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(recorder, new ReadOnlyCommandInterceptor(), new NoSaveChangesInterceptor())
            .Options;
        await using var db = new SqliteAppDbContext(options);
        var ct = CancellationToken.None;

        var account = await db.TradingAccounts.Where(a => a.Name.StartsWith("SBDEMO2")).Select(a => a.Id).SingleAsync(ct);
        var ids = await db.Strategies.Where(s => s.TradingAccountId == account).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct);
        var names = await FtmoGroupMemberResolution.LoadNamesAsync(db, ids, ct);

        ids.Should().Equal(strategyId);
        names.Should().ContainKey(strategyId);
        recorder.Commands.Should().HaveCountGreaterThanOrEqualTo(3);
        recorder.Commands.Should().OnlyContain(sql => ReadOnlyCommandInterceptor.IsSelectOnly(sql));
    }
}

/// <summary>The persisted ground truth (FTMO_CALIBRATION_TRUTH_FILE): fingerprint, schema version and ranking-entry round trip.</summary>
public class FtmoGroupSearchCalibrationTruthTests
{
    private static readonly Guid G1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid G2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid G3 = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static TruthFingerprint Fingerprint(
        string account = "SBDEMO2", decimal risk = 50m, decimal capital = 10000m, decimal fxLow = 1.05m, decimal fxHigh = 1.20m,
        int maxPerInstrument = 2, Guid[]? eligible = null, int stride = 10, string[]? sample = null)
        => FtmoGroupSearchCalibration.BuildFingerprint(
            account, risk, capital, fxLow, fxHigh, maxPerInstrument, eligible ?? [G2, G1, G3], stride, sample ?? ["b", "a"]);

    [Fact]
    public void Fingerprint_IsIndependentOfTheOrderOfEligibleIdsAndSampleKeys()
        => FtmoGroupSearchCalibration.DiffFingerprints(
            Fingerprint(eligible: [G1, G2, G3], sample: ["a", "b"]), Fingerprint(eligible: [G3, G1, G2], sample: ["b", "a"]))
            .Should().BeEmpty();

    [Fact]
    public void DiffFingerprints_NamesEveryDifferingField()
    {
        var diff = FtmoGroupSearchCalibration.DiffFingerprints(
            Fingerprint(),
            Fingerprint("OTHER", 60m, 20000m, 1m, 1.3m, 3, [G1], 5, ["a", "z"]));

        diff.Should().Equal(
            "account", "risk", "capital", "fxLow", "fxHigh", "maxPerInstrument", "eligibleStrategyIds", "k4Stride", "sampleKeys");
    }

    [Fact]
    public void DiffFingerprints_NamesOnlyTheDifferingField()
        => FtmoGroupSearchCalibration.DiffFingerprints(Fingerprint(), Fingerprint(risk: 51m)).Should().Equal("risk");

    [Fact]
    public void DiffFingerprints_TreatsEqualDecimalsOfDifferentScaleAsEqual()
        => FtmoGroupSearchCalibration.DiffFingerprints(Fingerprint(risk: 50m), Fingerprint(risk: 50.00m)).Should().BeEmpty();

    private static FtmoGroupKindResultDto Kind(BacktestRunKind kind, int? median, int breached, bool evaluated = true)
    {
        FtmoOrderStatisticsDto Days(int? m) => new(m is null ? 0 : 1, m, m, m, m, m);
        if (!evaluated)
            return new FtmoGroupKindResultDto(kind, FtmoSimulationStatus.Refused, FtmoGroupRefusal.MemberNotFound, [], null, [], null);

        var summary = new FtmoMultiStartSummaryDto(
            100, [new(FtmoChainOutcome.Phase1Breached, false, breached, 0.1m), new(FtmoChainOutcome.FundedNoBreachAtEndOfData, true, 100 - breached, 0.123456789m)],
            0, Days(null), Days(null), Days(median), Days(null), Days(null), Days(null));
        var run = new FtmoMultiStartRunDto(
            Guid.Empty, kind, BacktestSegment.Unknown, FtmoSimulationStatus.Evaluated, null, null, null, FtmoStartGrain.Monthly,
            new FtmoChallengeRulesDto(10m, 5m, 4, null), [], summary, [], false, null, null, 0, [], string.Empty);
        return new FtmoGroupKindResultDto(kind, FtmoSimulationStatus.Evaluated, null, [], null, [], run);
    }

    private static RankEntry Entry(int peak, decimal headroom, int? median, int breached, bool evaluated, params Guid[] ids)
        => new(ids, peak, [Kind(BacktestRunKind.Deploy, median, breached, evaluated), Kind(BacktestRunKind.Evaluation, median, breached, evaluated)], headroom);

    private static List<RankEntry> SampleEntries() =>
    [
        Entry(3, 0.500m, 12, 10, true, G1, G2),
        Entry(3, 0.500m, 12, 10, true, G1, G3),   // ties with the first except for the id tie-break
        Entry(1, 0.25m, null, 40, true, G2, G3),  // null median
        Entry(2, 0.1000m, 7, 5, false, G1, G2, G3), // refused kinds, trailing-zero decimal
    ];

    [Fact]
    public void TruthRoundTrip_PreservesTheRankingOrderAndTiesExactly_IncludingNullsAndDecimals()
    {
        var entries = SampleEntries();
        var fp = Fingerprint();

        var json = FtmoGroupSearchCalibration.SerializeTruth(fp, entries);
        var loaded = FtmoGroupSearchCalibration.DeserializeTruth(json, fp);

        loaded.Should().HaveCount(entries.Count);
        var before = Rank(entries).ToList();
        var after = Rank(loaded).ToList();
        after.Select(r => FtmoGroupSearchCalibration.Key(r.Entry.MemberIds))
            .Should().Equal(before.Select(r => FtmoGroupSearchCalibration.Key(r.Entry.MemberIds)));
        after.Select(r => r.WithinCeiling).Should().Equal(before.Select(r => r.WithinCeiling));
        for (var i = 1; i < after.Count; i++)
        {
            Comparer.Compare(after[i - 1].Entry, after[i].Entry).Should().Be(Comparer.Compare(before[i - 1].Entry, before[i].Entry));
            Comparer.Compare(after[i - 1].Entry, after[i].Entry with { MemberIds = after[i - 1].Entry.MemberIds })
                .Should().Be(Comparer.Compare(before[i - 1].Entry, before[i].Entry with { MemberIds = before[i - 1].Entry.MemberIds }), "ties are preserved");
        }

        loaded.Select(e => e.Headroom).Should().Equal(entries.Select(e => e.Headroom));
        loaded[3].Headroom.ToString(CultureInfo.InvariantCulture).Should().Be("0.1000", "the decimal scale survives");
        loaded[2].Kinds[0].Run!.Summary!.DaysToBothTargets.Median.Should().BeNull();
        loaded[3].Kinds[0].Status.Should().Be(FtmoSimulationStatus.Refused);
        loaded[3].Kinds[0].Refusal.Should().Be(FtmoGroupRefusal.MemberNotFound);
        loaded[0].Kinds[0].Run!.Summary!.Outcomes[1].Share.Should().Be(0.123456789m);
        loaded.Select(e => Key(e.MemberIds)).Should().Equal(entries.Select(e => Key(e.MemberIds)));
        loaded.Select(e => e.Peak).Should().Equal(entries.Select(e => e.Peak));
    }

    private static string Key(IReadOnlyList<Guid> ids) => string.Join(";", ids);

    [Fact]
    public void DeserializeTruth_FailsClearly_NamingTheDifferingFields_OnAStaleFingerprint()
    {
        var json = FtmoGroupSearchCalibration.SerializeTruth(Fingerprint(), SampleEntries());

        var load = () => FtmoGroupSearchCalibration.DeserializeTruth(json, Fingerprint(risk: 70m, stride: 5));

        load.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("stale").And.Contain("risk").And.Contain("k4Stride");
    }

    [Fact]
    public void DeserializeTruth_FailsClearly_OnAnUnknownSchemaVersion()
    {
        var json = FtmoGroupSearchCalibration.SerializeTruth(Fingerprint(), SampleEntries())
            .Replace($"\"schemaVersion\":{FtmoGroupSearchCalibration.TruthSchemaVersion}", "\"schemaVersion\":999");

        var load = () => FtmoGroupSearchCalibration.DeserializeTruth(json, Fingerprint());

        load.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("schema").And.Contain("999");
    }

    [Fact]
    public void DeserializeTruth_FailsClearly_OnGarbage()
    {
        var load = () => FtmoGroupSearchCalibration.DeserializeTruth("not json", Fingerprint());

        load.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("truth file");
    }

    [Fact]
    public void WriteTruthAtomically_WritesTheFile_AndLeavesNoTempFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ftmo-truth-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "truth.json");

            FtmoGroupSearchCalibration.WriteTruthAtomically(path, "{\"a\":1}");
            FtmoGroupSearchCalibration.WriteTruthAtomically(path, "{\"a\":2}");

            File.ReadAllText(path).Should().Be("{\"a\":2}");
            Directory.GetFiles(dir).Select(Path.GetFileName).Should().Equal(["truth.json"], "only the final file remains");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}

/// <summary>
/// ftmo-group-search 1d.3 / D9 — proxy calibration against the user's REAL pool. <c>[CalibrationFact]</c>: skipped unless
/// <c>FTMO_CALIBRATION_CONNECTION</c> is set, and running it needs EXPLICIT user authorization that names the target
/// (project database rule). The context is no-tracking and a command interceptor throws on anything that is not a
/// single SELECT (proven by <see cref="FtmoGroupSearchCalibrationGuardTests"/>), so the run cannot write.
/// <para>
/// Ground truth: the full simulation of every k=2 and k=3 candidate and every 10th k=4 candidate in enumeration order
/// (deterministic, no RNG; the k=4 slice is a biased sample, see <c>FtmoGroupSearchCalibration.GroundTruthSample</c>), ranked per size by the production ranking. Metric: <c>recall@K = |topK(full) ∩ shortlist| / K</c>
/// for K in {10, 25}, per size. Trust threshold: recall@10 and recall@25 &gt;= 0.9 per size with at least 10 sampled candidates (ties at the K boundary on the full ranking key count as in the top K), and at least one size must qualify; otherwise raise
/// <see cref="FtmoGroupSearchLimits.ShortlistSize"/> or revise the proxy score, and re-record.
/// </para>
/// <para>Optional settings (environment): <c>FTMO_CALIBRATION_ACCOUNT</c> (trading-account name prefix, default
/// <c>SBDEMO2</c>), <c>FTMO_CALIBRATION_RISK</c> (default 50), <c>FTMO_CALIBRATION_CAPITAL</c> (default 10000),
/// <c>FTMO_CALIBRATION_K4_STRIDE</c> (default 10), <c>FTMO_CALIBRATION_TRUTH_FILE</c> (a path: persists the full-simulation
/// ground truth when absent, reuses it when present after a fingerprint check; unset = always simulate), <c>FTMO_CALIBRATION_FX_LOW</c> / <c>FTMO_CALIBRATION_FX_HIGH</c> (the
/// FX band that makes non-USD strategies such as DAX/EUR eligible; default 1.05 and 1.20 when unset). The run logs the
/// eligible symbols with a per-symbol count, and FAILS (never passes vacuously) when the shortlist holds every survivor
/// (<c>shortlist == survivors</c> or <c>survivors &lt;= ShortlistSize</c>): recall is then 1.0 by construction.</para>
/// <para>RESULT: NOT RUN (recorded by slice 1d.3.3 once the user authorizes the run).</para>
/// </summary>
public class FtmoGroupSearchCalibrationTests(ITestOutputHelper output)
{
    private static readonly int[] Ks = [10, 25];

    private static string Setting(string name, string fallback)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

    private static string FormatDepth(IReadOnlyList<RankedEntry> truth, IReadOnlyList<string> sizeOrder, int K, decimal fraction)
        => FtmoGroupSearchCalibration.DepthToHold(
            sizeOrder, truth, r => FtmoGroupSearchCalibration.Key(r.Entry.MemberIds),
            (a, b) => Comparer.Compare(a.Entry, b.Entry with { MemberIds = a.Entry.MemberIds }) == 0, K, fraction)?.ToString(CultureInfo.InvariantCulture)
            ?? "unreachable";

    [CalibrationFact]
    public async Task TheProxyShortlist_HoldsTheTrueTopCandidates_OnTheRealPool()
    {
        var connection = Environment.GetEnvironmentVariable(CalibrationFactAttribute.ConnectionVariable)!;
        var accountPrefix = Setting("FTMO_CALIBRATION_ACCOUNT", "SBDEMO2");
        var risk = decimal.Parse(Setting("FTMO_CALIBRATION_RISK", "50"), CultureInfo.InvariantCulture);
        var capital = decimal.Parse(Setting("FTMO_CALIBRATION_CAPITAL", "10000"), CultureInfo.InvariantCulture);
        var stride = int.Parse(Setting("FTMO_CALIBRATION_K4_STRIDE", "10"), CultureInfo.InvariantCulture);
        var (fxLow, fxHigh) = FtmoGroupSearchCalibration.ResolveFxBand(
            Environment.GetEnvironmentVariable("FTMO_CALIBRATION_FX_LOW"), Environment.GetEnvironmentVariable("FTMO_CALIBRATION_FX_HIGH"));
        var ct = CancellationToken.None;

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(new ReadOnlyCommandInterceptor(), new NoSaveChangesInterceptor())
            .Options;
        await using var db = new AppDbContext(dbOptions);

        // ---- load (reads only), then no database access ----
        var accountId = await db.TradingAccounts.Where(a => a.Name.StartsWith(accountPrefix)).Select(a => a.Id).SingleAsync(ct);
        var ids = await db.Strategies.Where(s => s.TradingAccountId == accountId).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct);
        var parameters = new FtmoGroupSimulationParameters(ids, "FTMO", capital, risk, null, null, 2, 0.01m, 0.01m, 10m);
        var names = await FtmoGroupMemberResolution.LoadNamesAsync(db, ids, ct);
        var limits = await FtmoSimulationInputs.ResolveLimitsAsync(db, "FTMO", ct);
        limits.Refusal.Should().BeNull("the FTMO limits row must be configured");
        var resolved = await FtmoGroupMemberResolution.LoadMembersAsync(db, ids, ids, names, null, null, ct);
        var trades = await FtmoGroupMemberResolution.LoadTradesAsync(db, resolved.Members, ct);
        FtmoSimulationInputs.TryResolveBerlin(out var berlin).Should().BeTrue();
        var groupParams = FtmoGroupMemberResolution.BuildGroupParams(resolved.Members, resolved.Resolve, limits, berlin!, capital, fxLow, fxHigh);
        var cache = FtmoProjectionCache.Build(resolved.Members, trades, parameters.TryBuildSourceGrid()!, risk, resolved.Resolve, groupParams);

        // ---- the funnel as production runs it ----
        var search = new SearchOptions(2, FtmoGroupSimulationLimits.MaxMembers);
        var plan = Plan(cache, resolved.Resolve, search, groupParams, risk);
        var survivors = Prune(cache, plan.Eligibility.Eligible, resolved.Resolve, search, capital, risk).Survivors;
        output.WriteLine($"POOL strategies={ids.Count} eligible={plan.Eligibility.Eligible.Count} excluded={plan.Eligibility.Exclusions.Count} "
            + $"enumerated={plan.Funnel.Enumerated} survivors={survivors.Count} shortlist={plan.Shortlist.Count}");
        var eligibleSymbols = plan.Eligibility.Eligible
            .Select(id => resolved.Members.Single(m => m.StrategyId == id))
            .Select(m => m.Runs.Values.Select(r => r.Symbol).FirstOrDefault(s => s is not null))
            .ToList();
        output.WriteLine($"FX band={fxLow}..{fxHigh} maxPerInstrument={search.MaxPerInstrument}");
        output.WriteLine($"ELIGIBLE symbols (per-symbol count): {FtmoGroupSearchCalibration.SymbolCounts(eligibleSymbols)}");
        plan.Eligibility.Eligible.Count.Should().BeLessThanOrEqualTo(FtmoGroupSearchLimits.MaxPoolSize);

        // A shortlist that holds every survivor makes recall 1.0 by construction: fail instead of passing vacuously.
        if (FtmoGroupSearchCalibration.IsVacuous(survivors.Count, plan.Shortlist.Count, search.ShortlistSize))
        {
            var message = FtmoGroupSearchCalibration.VacuousMessage(survivors.Count, plan.Shortlist.Count, search.ShortlistSize);
            output.WriteLine(message);
            Assert.Fail(message);
        }

        // ---- ground truth: full simulation of the sample ----
        var sample = FtmoGroupSearchCalibration.GroundTruthSample(survivors, stride);
        var sampleProxies = sample.Select(combo => ComputeProxy(cache, [.. combo.Select(i => plan.Eligibility.Eligible[i])], groupParams)).ToList();
        var candidates = sample.Select((combo, n) =>
            new ShortlistedCandidate([.. combo.Select(i => plan.Eligibility.Eligible[i])], sampleProxies[n].DailyUsed, sampleProxies[n].Peak)).ToList();
        output.WriteLine($"GROUND TRUTH sample={candidates.Count} (k=2:{sample.Count(c => c.Length == 2)} k=3:{sample.Count(c => c.Length == 3)} k=4:{sample.Count(c => c.Length == 4)})");

        // The ground truth is expensive (about an hour) and independent of the proxy: FTMO_CALIBRATION_TRUTH_FILE persists it.
        var truthFile = Environment.GetEnvironmentVariable("FTMO_CALIBRATION_TRUTH_FILE") is { Length: > 0 } tf ? tf : null;
        var fingerprint = FtmoGroupSearchCalibration.BuildFingerprint(
            accountPrefix, risk, capital, fxLow, fxHigh, search.MaxPerInstrument, plan.Eligibility.Eligible, stride, candidates.Select(c => FtmoGroupSearchCalibration.Key(c.MemberIds)));
        IReadOnlyList<RankEntry> entries;
        if (truthFile is not null && File.Exists(truthFile))
        {
            entries = FtmoGroupSearchCalibration.DeserializeTruth(File.ReadAllText(truthFile), fingerprint);
            output.WriteLine($"TRUTH source=file path={truthFile} entries={entries.Count}");
        }
        else
        {
            var outcome = Simulate(
                cache, candidates, groupParams, new SimulationBudget(int.MaxValue),
                p => { if (p.Done % 50 == 0) output.WriteLine($"  simulated {p.Done}/{p.Total}"); }, ct);
            outcome.Cancelled.Should().BeFalse();
            entries = [.. outcome.Results.Select(r => BuildEntry(cache, r, groupParams))];
            if (truthFile is not null)
                FtmoGroupSearchCalibration.WriteTruthAtomically(truthFile, FtmoGroupSearchCalibration.SerializeTruth(fingerprint, entries));
            output.WriteLine($"TRUTH source=simulated path={truthFile ?? "(none)"} entries={entries.Count}");
        }

        // ---- recall per size ----
        var shortlisted = plan.Shortlist.Select(c => FtmoGroupSearchCalibration.Key(c.MemberIds)).ToHashSet();
        output.WriteLine("SAMPLE COUNTS " + string.Join(" ", sample.GroupBy(c => c.Length).OrderBy(g => g.Key).Select(g => $"k={g.Key}:{g.Count()}")));
        static bool Ties(RankedEntry a, RankedEntry b) => Comparer.Compare(a.Entry, b.Entry with { MemberIds = a.Entry.MemberIds }) == 0;
        var asserted = new List<int>();
        var failures = new List<string>();

        // The production proxy order, reused (not copied): the engine's own Shortlist with an unbounded size returns every
        // non-removed sampled candidate in global proxy order (daily-used, then peak, then the combo comparer).
        var proxyOrder = Shortlist(sample, sampleProxies, search with { ShortlistSize = int.MaxValue });
        var quota = search.ShortlistSize / (search.MaxMembers - search.MinMembers + 1);
        foreach (var k in sample.Select(c => c.Length).Distinct().Order())
        {
            var truth = Rank(entries.Where(e => e.MemberIds.Count == k)).ToList();
            var recalls = Ks.Select(K => (K, Recall: FtmoGroupSearchCalibration.RecallAtK(
                truth, r => FtmoGroupSearchCalibration.Key(r.Entry.MemberIds), Ties, shortlisted, K))).ToList();
            output.WriteLine($"RECALL k={k} sampled={truth.Count} " + string.Join(" ", recalls.Select(r => $"recall@{r.K}={r.Recall:F3}")));

            var sizeOrder = proxyOrder.Where(i => sample[i].Length == k)
                .Select(i => FtmoGroupSearchCalibration.Key(candidates[i].MemberIds)).ToList();
            var depths = Ks.Select(K => $"top{K}: d90={FormatDepth(truth, sizeOrder, K, 0.9m)} d100={FormatDepth(truth, sizeOrder, K, 1m)}");
            var survivorCount = survivors.Count(c => c.Length == k);
            var sampledCount = sample.Count(c => c.Length == k);
            var strided = k > 3 && stride > 1 ? $" (strided 1-in-{stride})" : string.Empty;
            output.WriteLine($"DEPTH k={k} survivors={survivorCount} sampled={sampledCount}{strided} quota={quota} ranked={sizeOrder.Count} " + string.Join(" ", depths));

            if (truth.Count < Ks[0])
                continue;

            asserted.Add(k);
            failures.AddRange(recalls.Where(r => r.Recall < 0.9m).Select(r => $"k={k} recall@{r.K}={r.Recall:F3}"));
        }

        asserted.Should().NotBeEmpty("a calibration in which no size had at least 10 sampled candidates asserts nothing and proves nothing");
        failures.Should().BeEmpty("the proxy shortlist must hold at least 90% of the true top K for every asserted size; failing (k, K): "
            + string.Join(", ", failures));
    }
}
