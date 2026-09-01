using AppTradingAlgoritmico.Application.Interfaces;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Services;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Backtests;

/// <summary>
/// Pure calibrator tests — no DB. Covers CAL-1 through CAL-5. Samples for the "real data"
/// tests come straight from the two committed fixtures via the (already-green) parser.
/// </summary>
public class SymbolPointValueCalibratorTests
{
    private const string F1Name = "ListOfTrades_XAUUSD_H1_IST.csv";
    private const string Symbol = "XAUUSD_M1_UTC02";

    /// <summary>
    /// F1 carries exactly 90 SL-closed XAUUSD trades. The prior revision pooled 185 across two
    /// fixtures, but the second one mixes two <c>Sample type</c> values and can no longer be
    /// imported as a run at all (see the single-sample-type guard), so 90 is the real number the
    /// grounded scenarios are built on.
    /// </summary>
    private const int F1SlClosedCount = 90;

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static async Task<List<BacktestTrade>> LoadFixtureTradesAsync(Guid runId, string fileName)
    {
        ISqxTradeListParser parser = new SqxTradeListParserService();

        await using var stream = File.OpenRead(FixturePath(fileName));
        var parsed = await parser.ParseAsync(stream, fileName, CancellationToken.None);
        return parsed.Trades.Select(t => ToEntity(runId, t.RowIndex, t)).ToList();
    }

    private static Task<List<BacktestTrade>> LoadF1Async() => LoadFixtureTradesAsync(Guid.NewGuid(), F1Name);

    private static BacktestTrade ToEntity(Guid runId, int rowIndex, Application.DTOs.Backtests.ParsedBacktestTradeDto t) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = runId,
        RowIndex = rowIndex,
        Ticket = t.Ticket,
        Symbol = t.Symbol,
        Type = t.Type,
        OpenTime = t.OpenTime,
        OpenPrice = t.OpenPrice,
        Size = t.Size,
        CloseTime = t.CloseTime,
        ClosePrice = t.ClosePrice,
        Profit = t.Profit,
        Balance = t.Balance,
        SampleTypeRaw = t.SampleTypeRaw,
        Segment = t.Segment,
        SegmentIndex = t.SegmentIndex,
        CloseType = t.CloseType,
        RealizedRisk = t.RealizedRisk,
        StopLoss = t.StopLoss,
        Comment = t.Comment,
        CreatedAt = DateTime.UtcNow,
    };

    private static BacktestTrade SlTrade(decimal openPrice, decimal closePrice, decimal size, decimal mae, decimal profit = 999m) => new()
    {
        Id = Guid.NewGuid(),
        BacktestRunId = Guid.NewGuid(),
        RowIndex = 0,
        Ticket = 1,
        Symbol = Symbol,
        Type = "Buy",
        OpenTime = DateTime.UtcNow,
        OpenPrice = openPrice,
        Size = size,
        CloseTime = DateTime.UtcNow,
        ClosePrice = closePrice,
        Profit = profit,
        Balance = 0,
        SampleTypeRaw = "IST",
        Segment = BacktestSegment.InSampleTest,
        CloseType = "SL",
        RealizedRisk = mae,
        CreatedAt = DateTime.UtcNow,
    };

    // ---- 3.1 / 3.2 ----

    [Fact]
    public async Task Calibrate_RealSlSamplesFromF1_YieldsExactPointValue()
    {
        var trades = await LoadF1Async();

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);

        result.SampleCount.Should().Be(F1SlClosedCount);
        result.Status.Should().Be(CalibrationStatus.Calibrated);
        result.PointValue.Should().Be(100.000m);
        result.MinObserved.Should().Be(100.000m);
        result.MaxObserved.Should().Be(100.000m);
        result.CalibratedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task Calibrate_ProfitMutatedToGarbage_PointValueUnchanged()
    {
        var trades = await LoadF1Async();
        foreach (var t in trades)
            t.Profit = 999_999m; // garbage — would land in the 100.47-102.15 Profit-derived band if used

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);

        result.PointValue.Should().Be(100.000m);
    }

    // ---- 3.3: floor is 3 (C1) ----

    [Fact]
    public void Calibrate_TwoSamples_InsufficientSamplesWithNullPointValue()
    {
        var trades = new[]
        {
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
        };

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);

        result.Status.Should().Be(CalibrationStatus.InsufficientSamples);
        result.PointValue.Should().BeNull();
        result.SampleCount.Should().Be(2);
    }

    [Fact]
    public void Calibrate_ThreeSamplesZeroSpread_Calibrates()
    {
        var trades = new[]
        {
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
        };

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);

        result.Status.Should().Be(CalibrationStatus.Calibrated);
        result.PointValue.Should().Be(100m);
        result.SampleCount.Should().Be(3);
    }

    // ---- 3.4: spread gate ----

    [Fact]
    public void Calibrate_SpreadOverHalfPercent_InconsistentWithMinMaxPersisted()
    {
        // pointValue = mae / (|close-open| * size); denominators fixed at 1 so pointValue == mae.
        var trades = new[]
        {
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100.00m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100.00m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 101.00m), // 1% spread vs median 100
        };

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);

        result.Status.Should().Be(CalibrationStatus.Inconsistent);
        result.PointValue.Should().BeNull();
        result.MinObserved.Should().Be(100.00m);
        result.MaxObserved.Should().Be(101.00m);
    }

    // ---- 3.5: guards ----

    [Fact]
    public void Calibrate_DegenerateAndZeroGuardedSamples_SkippedNotDivided()
    {
        var trades = new[]
        {
            SlTrade(openPrice: 100m, closePrice: 100m, size: 1m, mae: 50m),  // ClosePrice == OpenPrice
            SlTrade(openPrice: 100m, closePrice: 99m, size: 0m, mae: 50m),   // Size == 0
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 0m),    // Mae == 0
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
        };

        Action act = () => SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);

        act.Should().NotThrow();
        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);
        result.SampleCount.Should().Be(3);
        result.Status.Should().Be(CalibrationStatus.Calibrated);
    }

    [Fact]
    public void Calibrate_IgnoresNonSlClosedTrades()
    {
        var trades = new List<BacktestTrade>
        {
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
            SlTrade(openPrice: 100m, closePrice: 99m, size: 1m, mae: 100m),
        };
        var ptClosed = SlTrade(openPrice: 100m, closePrice: 200m, size: 1m, mae: 9999m);
        ptClosed.CloseType = "PT";
        trades.Add(ptClosed);

        var result = SymbolPointValueCalibrator.Calibrate(Symbol, trades, DateTime.UtcNow);

        result.SampleCount.Should().Be(3, "the PT-closed trade must never enter the sample count");
    }

    // ---- 3.6: order independence ----

    [Fact]
    public async Task Calibrate_SameTradesInReverseOrder_YieldsIdenticalResult()
    {
        var forward = await LoadF1Async();
        var reversed = Enumerable.Reverse(forward).ToList();

        var resultAB = SymbolPointValueCalibrator.Calibrate(Symbol, forward, DateTime.UtcNow);
        var resultBA = SymbolPointValueCalibrator.Calibrate(Symbol, reversed, DateTime.UtcNow);

        resultAB.SampleCount.Should().Be(F1SlClosedCount);
        resultAB.SampleCount.Should().Be(resultBA.SampleCount);
        resultAB.PointValue.Should().Be(resultBA.PointValue);
        resultAB.MinObserved.Should().Be(resultBA.MinObserved);
        resultAB.MaxObserved.Should().Be(resultBA.MaxObserved);
    }

    // ---- CAL-6: run selection deduplicates by content hash ----

    [Fact]
    public async Task SelectDistinctContentRuns_SameFileImportedForTwoStrategies_CountsItOnce()
    {
        // FK attribution reintroduced exactly the double-counting the previous revision\'s join
        // table prevented: one SQX strategy deployed on two accounts is two Strategy rows, and the
        // same exported file legitimately backs a run under each. Pooled naively that reports twice
        // the sample it actually has — and SampleCount is the exact value the InsufficientSamples
        // floor evaluates.
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        var trades = await LoadFixtureTradesAsync(runA, F1Name);
        trades.AddRange(await LoadFixtureTradesAsync(runB, F1Name));

        var selected = SymbolPointValueCalibrator.SelectDistinctContentRuns(
            [(runA, "identical-bytes"), (runB, "identical-bytes")]);

        selected.Should().HaveCount(1, "two runs, one file, one contribution");
        var result = SymbolPointValueCalibrator.Calibrate(
            Symbol, trades.Where(t => selected.Contains(t.BacktestRunId)), DateTime.UtcNow);

        result.SampleCount.Should().Be(F1SlClosedCount, "not 180 — the file is counted once");
        result.PointValue.Should().Be(100.000m);
        result.MinObserved.Should().Be(100.000m);
        result.MaxObserved.Should().Be(100.000m);
    }

    [Fact]
    public async Task SelectDistinctContentRuns_TwoGenuinelyDifferentFiles_BothContribute()
    {
        // De-duplication is by CONTENT, not by symbol and not by strategy. Two different exports
        // for the same symbol are two independent samples and both belong in the population.
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        var trades = await LoadFixtureTradesAsync(runA, F1Name);
        trades.AddRange(await LoadFixtureTradesAsync(runB, F1Name));

        var selected = SymbolPointValueCalibrator.SelectDistinctContentRuns(
            [(runA, "hash-a"), (runB, "hash-b")]);

        selected.Should().HaveCount(2);
        var result = SymbolPointValueCalibrator.Calibrate(
            Symbol, trades.Where(t => selected.Contains(t.BacktestRunId)), DateTime.UtcNow);

        result.SampleCount.Should().Be(F1SlClosedCount * 2, "different content hashes are different evidence");
    }

    [Fact]
    public void SelectDistinctContentRuns_ThreeRunsSharingOneHash_PicksTheSameOneEveryTime()
    {
        // The kept run must not depend on query order, or the same database would calibrate
        // differently between two identical requests.
        var a = new Guid("11111111-1111-1111-1111-111111111111");
        var b = new Guid("22222222-2222-2222-2222-222222222222");
        var c = new Guid("33333333-3333-3333-3333-333333333333");

        var forward = SymbolPointValueCalibrator.SelectDistinctContentRuns([(a, "h"), (b, "h"), (c, "h")]);
        var backward = SymbolPointValueCalibrator.SelectDistinctContentRuns([(c, "h"), (b, "h"), (a, "h")]);

        forward.Should().BeEquivalentTo([a]);
        backward.Should().BeEquivalentTo([a]);
    }
}
