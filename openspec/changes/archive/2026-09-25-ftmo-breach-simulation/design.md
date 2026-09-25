# Design: FTMO 2-Step breach simulation (roadmap layer 1)

> Inputs: `proposal.md`, `explore.md`, Engram #2746/#2747/#2749/#2766, and the settled answers
> (overlap measured absent, source zone `Asia/Jerusalem` with DST, user-supplied FTMO contract
> specs, IS/OOS reported separately). Where this design contradicts the proposal, see §Corrections.

## Technical Approach

Three pure `internal static` units in `Infrastructure/Services` (Slice B's calculator pattern):
`FtmoDayClock` (source zone to FTMO day), `FtmoTradeProjector` (money-per-point resize onto the
FTMO grid) and `FtmoBreachEvaluator` (running balance, three-state findings). A read service loads
one strategy's held runs, the FTMO `BrokerRiskLimits` row, a new persisted `FtmoInstrumentSpec` row
and the `SymbolCalibration`, then returns one result per run. The service never merges runs, so
IS and OOS stay separate. `BacktestNetSeries.Bridge` and `TradeResizer.Resize` are deliberately
NOT reused on the FTMO side (Decision 2).

## Architecture Decisions

### Decision 1: Timezone conversion (BCL `TimeZoneInfo`, IANA IDs, candidate-set conversion)

| Option | Tradeoff | Decision |
|---|---|---|
| BCL `TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem" / "Europe/Berlin")` | No dependency. Since .NET 6 it accepts IANA IDs on Windows (via ICU) and on Linux (tzdata). Historical rules come from the OS: Windows registry vs Linux tzdata | **Chosen**, with pinning tests |
| NodaTime + embedded tzdb | OS-independent history. Adds a dependency, which is a `review-risk` category | Rejected for now. Fallback if the pins fail on Windows |
| Fixed `+02:00` | Wrong for half the year (#2749) | Rejected |

Resolution: call `FindSystemTimeZoneById(ianaId)` first. On `TimeZoneNotFoundException`, try
`TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out winId)` and resolve `winId`. If both fail,
the result is a `TimeZoneDataUnavailable` refusal, never a fallback offset. The runtime image
`mcr.microsoft.com/dotnet/aspnet:10.0` is Debian-based and is assumed to ship tzdata. A test pins
that assumption.

**Conversion never calls `ConvertTimeToUtc` on raw source times.** Documented behaviour of
`TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)`:
- It throws `ArgumentException` when `sourceTimeZone.IsInvalidTime(dateTime)`.
- It silently assumes **standard time** when the time is ambiguous.
- It throws when `Kind == Local` or `Kind == Utc` and the zone does not match. EF returns
  `Unspecified`.

A silent guess and a throw are both unacceptable here. So `FtmoDayClock.Attribute(sourceLocal)`
builds a **candidate set of UTC instants**:
- Ambiguous (`IsAmbiguousTime`): two candidates, one per offset from `GetAmbiguousTimeOffsets`.
- Invalid (`IsInvalidTime`): two candidates, `local − BaseUtcOffset` and `local − (BaseUtcOffset + 1h)`.
  An invalid timestamp means the data contradicts the declared zone.
- Otherwise: one candidate, `local − GetUtcOffset(local)`.

Each candidate goes through `ConvertTimeFromUtc(utc, berlin)`, which is unambiguous from UTC, and
yields an FTMO day. **Mismatch-window sensitivity**: when `jerusalem.GetUtcOffset(utc) −
berlin.GetUtcOffset(utc) ≠ 1h`, the clock also computes the *naive* day `(sourceLocal − 1h).Date`.
If the naive day differs from the exact day, the close is flagged `DstMismatchWindow`.

Output: `DayAttribution(IReadOnlySet<DateOnly> CandidateDays, DateTime FtmoLocal, AttributionFlags Flags)`.

**Verification status.** EXECUTED on Windows .NET 9 via pwsh, 2026-09-24 (Engram #2769), not merely
reasoned from documentation: `FindSystemTimeZoneById` resolves both `Asia/Jerusalem` and
`Europe/Berlin`; 00:30 Jerusalem maps to 23:30 of the previous Berlin day in both winter and summer
(the near-constant 1h offset is confirmed, not assumed); `IsAmbiguousTime` is true at the 2013-10-27
fall-back and `ConvertTimeToUtc` silently assumes standard time there; `ConvertTimeToUtc` THROWS
`ArgumentException` on the invalid 2013-03-29 02:30 spring-forward time; Windows also knows the
pre-2013 Israeli rule (2012-09-23 ambiguous). The Linux container's tzdata-backed behaviour remains
UNVERIFIED. Task 1 in P1 is therefore a *characterization* RED suite that re-pins the already-measured
Windows behaviour and additionally pins the Linux container:
- `ConvertTimeToUtc` throws on an invalid time.
- `ConvertTimeToUtc` picks standard time on an ambiguous time.
- Israel's transition instants: 2012-09-23 (the pre-2013 rule), 2013-03-29 (Friday) and
  2013-10-27, next to the EU's 2013-03-31 and 2013-10-27.

A failing pin on Linux (or a Windows regression) triggers the NodaTime fallback. It is not a reason
to weaken the test.

### Decision 2: Money-per-point rescaling. The existing resizer assumes equal point values

The existing code (`BacktestReadService.cs:232-246`) calls `TryNormalize` and `TradeResizer.Resize`
with ONE `grid`. `Bridge` then computes `Profit·q'/q`. That is only correct when a target lot and a
source lot move the same money per point. On FTMO that holds for XAUUSD (100 vs 100). It is false
for DAX (10 vs 1). **Finding**: passing the FTMO grid to `TradeResizer` floors *source* lots onto
FTMO's step, so both the lot count and the P/L come out wrong.

Rule, with M = `C_ftmo · FX` (USD per point per FTMO lot; FX = 1 when the profit currency is USD),
q the source size, and Â the run's estimate from `TryNormalize` on the **declared source grid**:

    u    = q · target · P_src / (Â · M)                      one quotient, TradeResizer's D3
    q'   = clamp(floor(u / step_ftmo) · step_ftmo, min_ftmo, max_ftmo)   <- lot-grid rounding enters here only
    net' = Profit · (q' · M) / (q · P_src)                   Profit/(q·P_src) = points moved

When `P_src == M`, the rule reduces exactly to `TradeResizer` + `Bridge`. That identity is a RED test.

**DAX worked example.** q = 0.06, Â = $200, target = $50, P_src = 10, C = 1, FX = 1.08.
- **Money-per-point rule:** u = 0.06·50·10/(200·1.08) = 0.1389, so q' = **0.13** GER40.cash lots
  and net' = Profit·0.234. Achieved risk is $46.80, which is ≤ $50.
- **Lot-count path (wrong):** q' = floor(1.5)·0.01 = 0.01 lots, and it reports net = Profit/6,
  i.e. $33 of risk. At FTMO, 0.01 GER40.cash actually risks about $3.60. The P/L is overstated
  about 9.3x and the lot is 13x too small.

### Decision 3: Currency. Declared FX band, not refusal, not a single rate

Because sizing targets USD risk, FX nearly **cancels** (net' ≈ Profit·target/Â). It survives only
through the floor and the min/max clamps. So a single rate over 2013–2026 does not distort the
P/L. It only changes the rounding.

Choice: for a non-USD profit currency, the caller declares `fxLow`/`fxHigh` (USD per unit, a
degenerate band is allowed). The projector and evaluator run at both ends. If the verdicts differ,
the result is `BreachContingent` with cause `FxRoundingSensitive`. A missing band gives
`FxRateNotDeclared`. The band is echoed on the result.

Rejected alternatives:
- **Refuse DAX:** this discards a strategy the arithmetic can evaluate.
- **Single rate:** this hides the rounding sensitivity.

### Decision 4: Where inputs live

| Input | Home | Why |
|---|---|---|
| Contract size, currency, min/step/max, FTMO symbol, source zone | New table `FtmoInstrumentSpecs`, keyed by SQX symbol | A fact about the instrument, reused on every call, with provenance |
| FX band, initial capital, target risk, source grid, broker row name | Request | A modelling choice per question |
| 5% / 10% | `BrokerRiskLimits` | Proposal D3 |

Columns: `SqxSymbol` (unique), `FtmoSymbol`, `ContractSize`, `ProfitCurrency` (ISO), `SizeDecimals`,
`Step`, `MinLot`, `MaxLots`, `SourceTimeZoneId`, `Provenance` (required), `CapturedOn`.

Migration `AddFtmoInstrumentSpecs` seeds all four rows: `XAUUSD_M1_UTC02`→`XAUUSD`,
`DEUIDXEUR_M1_UTC02`→`GER40.cash`, `USATECHIDXUSD_M1_UTC02`→`US100.cash`, and
`BTCUSD_M1_UTC02`→`BTCUSD` (contract size 1, min 0.01, step 0.01, max 5.00, USD) — the BTC SQX
symbol is established at `MEASURED_Concurrent_BTC_Margin_Exhaustion.md:67`, user-confirmed
2026-09-24 (Engram #2769). Its SQL carries a provenance comment ("user-supplied from the FTMO MT
platform 2026-09-24, Engram #2766/#2769"). The SQL is an `internal const` pinned by a test
(precedent: `AddSourcePlatformToBacktestRun`). **Applying the migration is a separate,
user-authorised step**, independent of this confirmation.

### Decision 5: Three-state finding

Every closing trade updates the balance in order (`CloseTime`, then `RowIndex`).

**Daily limit.** The reference balance is `B_midnight(D)`; on day 1 it is the initial capital. The
limit is breached when `balance < B_midnight(D) − daily%·initial`.

**Max limit.** The limit is breached when `balance < initial·(1 − max%)`.

A breaching close is *clean* when it has no causes. Causes:

| Cause | Condition | Limit |
|---|---|---|
| `ConcurrentOpenPosition` | Another trade has `OpenTime < close < CloseTime`, read from `BacktestTrade` | both |
| `AmbiguousSourceTime` / `InvalidSourceTime` / `DstMismatchWindow` | A flagged close whose candidate days intersect {D−1, D} | daily only (the static floor is day-free) |
| `UnscalableTradeExcluded` | Max: an `Unscalable` row closed at any time before the breach. Daily: one closed before the breach on the same FTMO day (its reference predates the excluded close, or their candidate days intersect) | both, scoped per limit |
| `FxRoundingSensitive` | The verdict differs across the FX band | both |

Verdicts:
- `Breached`: at least one clean breaching close. If the first breach was not real, the account
  lived to the clean one.
- `BreachContingent`: breaches exist, but none is clean. `Causes` is non-empty; the factory
  enforces this.
- `NoBreachObserved`: no breaching close.

Each finding reports `FirstBreach` and `FirstCleanBreach`: the FTMO time, the source time, the
balance, the level and the margin past it.

### Decision 6: Refusals are a status, not a verdict

`FtmoSimulationStatus { Evaluated, Refused }` plus a `FtmoSimulationRefusal` reason. A refused run
carries NO findings (the `TryNormalize` null-profile precedent). Reasons:

| Reason | Trigger |
|---|---|
| `ProductNotTwoStep` | The product is null or OneStep |
| `LimitsNotConfigured` | No FTMO `BrokerRiskLimits` row, or the row's `DailyLossLimitPct`/`MaxLossLimitPct` is null or outside `(0, 1]` |
| `DrawdownModelNotStatic` | The row's drawdown model is not Static |
| `InstrumentSpecMissing` | No `FtmoInstrumentSpec` for the run's symbol |
| `PointValueNotCalibrated` | Calibration status is not `Calibrated` **or** `PointValue` is null. `Calibrated` is enum value 0, the CLR default, so the null check is load-bearing. This covers NQ. |
| `FxRateNotDeclared` / `InvalidFxBand` | Non-USD currency without a valid band |
| `RiskNotEstimable` | The normalizer refused the run |
| `RunSegmentsDisagree` | The run's trades carry more than one segment |
| `TimeZoneDataUnavailable` | The zones could not be resolved |
| `InvalidRequest` | Bad capital, target or source grid |

### Decision 7: `DailyBreached` deprecation

`BreachBasis` is a computed property of `Kind` (`PortfolioAnalyticsDto.cs:223`), not set in
`PortfolioService`. Changes:
- Add `BreachBasis.VarQuantileComparison = 1`.
- The switch returns it for `LossLimits`. `StagedLossLimits` keeps `ClosedTradeLowerBound`;
  `VarTarget` stays null.
- `PortfolioService.cs:445-459` is untouched apart from its comment. The value is pinned by the
  existing tests plus a new −8% day / VaR 2% test.
- Add an XML `DEPRECATED — VaR95 comparison` note on `DailyBreached`/`DailyHeadroomPct`.
- **Not `[Obsolete]`**: the backend builds warnings-as-errors, so CS0618 would break every
  construction site.
- The web enum is numeric, and `breachBasisLabel` returns `''` for unknown values, so without a
  frontend change the disclosure would silently vanish. P4 therefore adds the TS member and a
  label ("Comparación VaR95, no una pérdida diaria real"), plus a CHANGELOG entry.

### Decision 8: Delivery (floors; slices overran ~2x)

| PR | Content | Floor | Realistic |
|---|---|---|---|
| P1 | `FtmoDayClock` + characterization pins | 300 | 550 |
| P2 | Projector + evaluator + enums | 450 | 900 |
| P3 | Entity, config, migration + seed, schema tests | 300 | 550 |
| P4 | Read service, DTOs, endpoint, D2 relabel, web label | 450 | 850 |

400-line budget risk: **High** for P2 and P4. Four chained PRs are recommended, instead of the
proposal's two.

## Data Flow

    Client -> StrategyBacktestsController GET ftmo-breach
      -> FtmoBreachSimulationReadService
           load runs+trades, BrokerRiskLimits(broker), FtmoInstrumentSpec(symbol), SymbolCalibration
           guards -> Refused(reason)                                   [no findings]
           TradeRiskNormalizer.TryNormalize(trades, sourceGrid)
           for fx in {fxLow, fxHigh}:
              FtmoTradeProjector.Project(profile, trades, spec, P_src, fx, target)
              FtmoBreachEvaluator.Evaluate(projected, FtmoDayClock, limits, initial)
           merge the two evaluations -> verdict + causes + resize counts per fx
      <- FtmoBreachSimulationDto { Runs[] (one per run: Kind, Segment, Status, findings) }

## File Changes

| File | Action |
|---|---|
| `Infrastructure/Services/FtmoDayClock.cs`, `FtmoTradeProjector.cs`, `FtmoBreachEvaluator.cs` | Create (internal) |
| `Domain/Enums/FtmoBreachVerdict.cs`, `BreachContingencyCause.cs`, `FtmoSimulationRefusal.cs`, `FtmoSimulationStatus.cs` | Create |
| `Domain/Entities/FtmoInstrumentSpec.cs`, `Persistence/Configurations/…`, migration | Create |
| `Application/DTOs/Backtests/FtmoBreachSimulationDto.cs`, `Interfaces/IFtmoBreachSimulationReadService.cs` | Create |
| `Infrastructure/Services/FtmoBreachSimulationReadService.cs`, `WebAPI/Controllers/StrategyBacktestsController.cs` | Create / Modify |
| `Domain/Enums/BreachBasis.cs`, `DTOs/Portfolios/PortfolioAnalyticsDto.cs`, `PortfolioService.cs` (comment) | Modify |
| `web/.../portfolio.service.ts`, `portfolio-detail.component.ts` (+spec), `CHANGELOG.md` | Modify |

## Interfaces / Contracts

```csharp
internal sealed record ProjectedTrade(int RowIndex, DateTime OpenSource, DateTime CloseSource,
    decimal? Net /* null = Unscalable */, ResizeOutcome Outcome, decimal FtmoLots);
internal static FtmoBreachEvaluation Evaluate(IReadOnlyList<ProjectedTrade> trades,
    FtmoDayClock clock, decimal initialCapital, decimal dailyPct, decimal maxPct);
public sealed record FtmoLimitFinding(FtmoBreachVerdict Verdict,
    IReadOnlyList<BreachContingencyCause> Causes, BreachPoint? FirstBreach, BreachPoint? FirstCleanBreach);
```

Every run result carries `NotModelled = [Swap, FtmoCommission, IntradayEquity]`, the
`EmbeddedSourceCommissionRescaled` disclosure and the fixed no-pass sentence.

## Testing Strategy

| Layer | RED tests |
|---|---|
| Clock | Pins: invalid time throws; ambiguous time assumes standard. 00:30 source maps to the previous FTMO day. Israel 2013-03-29 window flagged `DstMismatchWindow`. Ambiguous and invalid times yield two candidates. An unresolvable ID refuses. |
| Projector | P_src == M reproduces `TradeResizer` + `Bridge`. DAX example gives 0.13 lots and ×0.234. BTC capped at 5.00. Min pin counted. |
| Evaluator | −8% day with VaR 2% gives `Breached`. The reference moves with the midnight balance; the floor is static. Overlap gives `BreachContingent`. A later clean breach gives `Breached`. Unscalable-before-breach gives `BreachContingent`. |
| Service | Each refusal reason. Two runs give two results. FX band disagreement gives `FxRoundingSensitive`. |
| Relabel | `DailyBreached` value unchanged; `BreachBasis` becomes `VarQuantileComparison`; web label renders. |

## Threat Matrix

N/A: no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary.

## Migration / Rollout

One additive migration (`AddFtmoInstrumentSpecs` + seed). Applying it needs the user's explicit
authorisation and the target connection named. `Down` drops the table. P1 and P2 ship no I/O.

## Corrections to upstream artifacts

1. The proposal's scope item 4 and D4 ("data UTC offset, caller-declared") are superseded. The
   zone is a persisted IANA ID per SQX symbol.
2. The proposal's D6 ("commission absent") is wrong. SQX `Profit` embeds the configured commission
   (KB `MEASURED_Demo_vs_Backtest_Divergence.md` §6: $5.5/lot on NQ). It is rescaled with the P/L,
   so the results must disclose it as embedded.
3. The settled rule "closes in mismatch windows are contingent" is too broad. With exact IANA
   conversion those closes are attributed correctly. Only closes whose day **changes** against the
   naive offset are flagged.
4. The concurrent spec's scenario "FTMO LossLimits breach labelled `ClosedTradeLowerBound`"
   contradicts its own relabel scenario. On `LossLimits`, `DailyBreached` is the only breach
   readout.
5. The proposal says UI is out of scope, but D2 is invisible without the one web label change.
6. KB `01_SQX_Data.md:158` records NQ as calibrated at 10 (183 samples). RESOLVED: the database is
   authoritative and shows `Inconsistent` with 2034 samples — the KB entry is stale, superseded by
   later imports widening the spread. `USATECHIDXUSD_M1_UTC02` stays `PointValueNotCalibrated` /
   refused (Decision 6), never the KB's stale value.

## Open Questions

- [x] RESOLVED 2026-09-24 (Engram #2769): the user confirmed the SQX→FTMO symbol mapping for all
  four instruments, including the BTC SQX symbol (`BTCUSD_M1_UTC02`). Applying the seed migration
  still requires separate, explicit user authorisation and the target connection named.
