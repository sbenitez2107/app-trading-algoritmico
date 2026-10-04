using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppTradingAlgoritmico.Application.DTOs.Backtests;
using AppTradingAlgoritmico.Domain.Entities;
using AppTradingAlgoritmico.Domain.Enums;
using AppTradingAlgoritmico.Infrastructure.Persistence;
using AppTradingAlgoritmico.Infrastructure.Services;
using AppTradingAlgoritmico.UnitTests.StrategyWorkflow;
using FluentAssertions;

namespace AppTradingAlgoritmico.UnitTests.Ftmo;

/// <summary>
/// ftmo-group-search 1a.1.5 — the group endpoint's serialized response for a fixed fixture is pinned by its
/// SHA-256. The expected hashes were captured from the service BEFORE the member-resolution extraction, so the
/// extraction must not change a single byte of the response: a success (USD + non-USD member, FX band) and a
/// member-level refusal (a symbol with no spec).
/// </summary>
public class FtmoGroupResponseSnapshotTests
{
    private const string Broker = "FTMO";
    private const string Gold = "XAUUSD_SNAP";
    private const string Euro = "EURGBP_SNAP";
    private const string NoSpec = "NOSPEC_SNAP";
    private static readonly DateTime SafeDay = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);

    private const string SuccessHash = "B805264AD3BBB66BACDD75449B5BEE523AEC69F0C0AF87F614D0262651737BCD";
    private const string RefusalHash = "D76BE4314D78CAB53C4ADE4B6D64966A91242D759259A78CA7CA2E9C6CE049DA";

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    private static BacktestTrade MakeTrade(int row, string symbol, decimal profit, DateTime open, DateTime close) => new()
    {
        RowIndex = row,
        Ticket = row + 1,
        Symbol = symbol,
        Type = "Long",
        OpenTime = open,
        OpenPrice = 100m,
        Size = 1.00m,
        CloseTime = close,
        ClosePrice = 101m,
        Profit = profit,
        Balance = 10_000m,
        SampleTypeRaw = "InSample",
        Segment = BacktestSegment.InSample,
        CloseType = profit < 0m ? "SL" : "TP",
        RealizedRisk = profit < 0m ? 100m : null,
    };

    private static async Task SeedMemberAsync(AppDbContext db, Guid id, string name, string symbol, int shift)
    {
        db.Strategies.Add(new Strategy { Id = id, Name = name, CreatedAt = DateTime.UtcNow, TradingAccountId = Guid.NewGuid() });
        foreach (var kind in new[] { BacktestRunKind.Deploy, BacktestRunKind.Evaluation })
        {
            var run = new BacktestRun
            {
                Id = Guid.NewGuid(),
                SourceFileName = "snap.csv",
                ContentHash = Guid.NewGuid().ToString("N"),
                StrategyId = id,
                Kind = kind,
                Symbol = symbol,
                CreatedAt = DateTime.UtcNow,
            };
            db.BacktestRuns.Add(run);
            var s = shift + (kind == BacktestRunKind.Evaluation ? 2 : 0);
            for (var i = 0; i < 60; i++)
            {
                var close = SafeDay.AddDays(i);
                var profit = i < 3 ? -1m : ((i + s) % 7 == 3 ? 400m : ((i + s) % 5 == 2 ? -150m : 20m));
                var trade = MakeTrade(i, symbol, profit, close.AddHours(-1), close);
                trade.BacktestRunId = run.Id;
                db.BacktestTrades.Add(trade);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task<AppDbContext> SeedWorldAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        db.BrokerRiskLimits.Add(new BrokerRiskLimits
        {
            Broker = Broker,
            FundingService = FundingService.Ftmo,
            Kind = GuardrailKind.LossLimits,
            FtmoProduct = FtmoProduct.TwoStep,
            DrawdownModel = DrawdownModel.Static,
            DailyLossLimitPct = 0.05m,
            MaxLossLimitPct = 0.10m,
            Verified = true,
        });
        foreach (var (symbol, currency, zone) in new[] { (Gold, "USD", "Asia/Jerusalem"), (Euro, "EUR", "Asia/Jerusalem") })
        {
            db.FtmoInstrumentSpecs.Add(new FtmoInstrumentSpec
            {
                SqxSymbol = symbol,
                FtmoSymbol = symbol,
                ContractSize = 100m,
                ProfitCurrency = currency,
                SizeDecimals = 2,
                Step = 0.01m,
                MinLot = 0.01m,
                MaxLots = 1000m,
                SourceTimeZoneId = zone,
                Provenance = "test",
                CapturedOn = new DateOnly(2026, 1, 1),
            });
            db.SymbolCalibrations.Add(new SymbolCalibration
            {
                Symbol = symbol,
                Status = CalibrationStatus.Calibrated,
                PointValue = 100m,
                SampleCount = 10,
                CalibratedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        }

        await db.SaveChangesAsync();
        await SeedMemberAsync(db, Id(1), "gold", Gold, shift: 1);
        await SeedMemberAsync(db, Id(2), "euro", Euro, shift: 2);
        await SeedMemberAsync(db, Id(3), "nospec", NoSpec, shift: 3);
        return db;
    }

    private static string Hash(FtmoGroupSimulationDto dto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));

    private static FtmoGroupSimulationParameters Params(params Guid[] ids) =>
        new(ids, Broker, 10_000m, 100m, 1.05m, 1.10m, 2, 0.01m, MinLot: 0.01m, MaxLots: 10m);

    [Fact]
    public async Task GroupResponse_ForAFixedMixedCurrencyGroup_IsByteIdenticalToThePreExtractionResponse()
    {
        await using var db = await SeedWorldAsync();

        var dto = await new FtmoGroupSimulationReadService(db).SimulateAsync(Params(Id(1), Id(2)), CancellationToken.None);

        dto.Kinds.Should().OnlyContain(k => k.Status == FtmoSimulationStatus.Evaluated);
        Hash(dto).Should().Be(SuccessHash);
    }

    [Fact]
    public async Task GroupResponse_ForAGroupWithARefusedMember_IsByteIdenticalToThePreExtractionResponse()
    {
        await using var db = await SeedWorldAsync();

        var dto = await new FtmoGroupSimulationReadService(db).SimulateAsync(Params(Id(1), Id(3)), CancellationToken.None);

        dto.Kinds.Should().OnlyContain(k => k.Status == FtmoSimulationStatus.Refused);
        Hash(dto).Should().Be(RefusalHash);
    }
}
