# Design: Trade Risk Normalization (slice 2a)

## Technical Approach

Two pure static calculators in `Infrastructure/Services`, following `SymbolPointValueCalibrator`
exactly. `TradeRiskNormalizer` estimates the run's own risked amount `Â` from its SL closes and
labels every trade's risk with its provenance; `TradeResizer` rescales sizes onto a lot grid.
Nothing is persisted, no existing calculator or entity changes, and the output is a type the
shipped weight pipeline cannot bind to. The proposal's mechanism holds; the measured corrections
(obs 2368) replace four of its assumptions and are encoded as decisions below.

## Architecture Decisions

### D1 — `Â` is the best-supported stabbing point plus a consistency fraction

**Choice.** Each SL trade contributes `[rᵢ, rᵢ·(qᵢ+step)/qᵢ]` (floor inversion of `uᵢ ∈ [qᵢ, qᵢ+step)`).
Candidates are the interval lower endpoints; `Â` is the candidate contained by the most intervals,
tie-broken by the smallest value. `ConsistencyFraction = covered / slSampleCount`.
**Rejected.** Strict intersection of all intervals — it holds on a fine grid and breaks on a
coarse one, so it cannot be the general rule. Measured under floor: on
`ListOfTrades_XAUUSD_H1_IST.csv` (2-decimal) the intersection is **non-empty**,
`Â ∈ [199.98, 200.16)`, all 90 SL trades. On `_OOST.csv` (1-decimal) it is empty — and all 7
offenders sit at the minimum lot (7/7), where a clamp-up destroys the inversion. The robust form
degrades where the strict form returns nothing at all. Also rejected: mean/median of endpoints,
which no interval need contain.
**Rationale.** A stabbing point always exists at some lower endpoint, so the search is exact and
finite. Deterministic tie-break for the same reason `SelectDistinctContentRuns` takes the min GUID:
one database must not estimate two ways. Reproduces the measurement: **Â = 199.98, 90/90 (100%)**
on the 2-decimal fixture and **Â = 200.00, 88/95 (93%)** on the 1-decimal one.
The 2-decimal estimate is 199.98 and **not** 200.00, which is a property of the rule, not a defect:
candidates are band *lower endpoints*, lower endpoints are realized risks, and realized risk under
floor never reaches the target — so no band offers 200.00 as a candidate. The feasible band
`[199.98, 200.16)` **brackets** the configured amount without the estimator ever returning it.
Returning 200.00 on this fixture would require seeding from the config, which D2 forbids. (The
1-decimal run does return 200.00 because a clamped trade realizes exactly that.)
Sample floor reuses `MinimumSlSamples = 3` — the consistency gate is the real guard (calibrator
precedent, C1); a higher floor strands thin runs and 1-of-3 disagreement already fails 85%.

### D2 — `Â` never comes from the configured $200; realized risk never comes from `Profit`

**Choice.** Source is `BacktestTrade.RealizedRisk` (`|MAE|`, SL-only, never defaulted).
**Rejected.** `Profit` — carries spread and commission (~$0.94 on ticket 1851: MAE −173.76 vs
Profit −174.70) and breaks the floor intersection at trade 33, where `MAE` holds through all 90;
only 69% of `Profit` intervals contain 200 against 100% of `MAE` ones. Also rejected: seeding `Â`
from the $200 config. The measurement *brackets* the config on the 2-decimal run — band
`[199.98, 200.16)`, point estimate 199.98 — which is the strongest available argument for measuring
rather than assuming, not against it: the same
estimator reports 93% on the 1-decimal run, where 7 clamped trades realize $229–$405 against that
same $200. A seeded constant would have reported the intent and hidden the outcome.

### D3 — Rounding is FLOOR, in both directions of the pipeline

**Choice.** `q = ⌊raw/step⌋·step`, in the estimator's inversion *and* in the resizer.
**Rejected.** Round-half (the proposal's assumption).
**Rationale.** Measured: 2-decimal realized risk tops at **199.98 and never exceeds 200**;
round-half would put ~half the trades above target. A round-half resizer emits positions
systematically larger than the backtest simulated.

### D4 — Rejection is a state the caller must handle, and it carries its evidence

**Choice.** Two shapes, deliberately. `Estimate(...)` **always** returns `RunRiskEstimate` with
`Status ∈ {Estimated, InsufficientSamples, Inconsistent}` and `RiskPerTrade` null unless
`Estimated` — the calibrator's shape, so the measured fraction survives the rejection.
`TryNormalize(..., out RunRiskProfile? profile) → bool` gates consumption — a rejected run yields
**no per-trade output at all**, not a list of `Unavailable` rows.
**Rejected.** Throwing (an exception-shaped surprise); returning normalized trades with a warning
flag (a number that reads as evidence of nothing — `OosWindow`'s stated reason for "no window, not
an empty one"); bool+out alone (loses the fraction the user must see).
**Rationale.** Threshold **0.85**, settled. The nullable `out` makes an ignored `bool` a CS8602
warning under the project's nullable settings.

### D5 — Four risk bases, each with a mechanism that produces it

| Basis | Producer | Interval |
|---|---|---|
| `Measured` | `CloseType == "SL"`, `RealizedRisk` present | `[r, r]` (a point) |
| `Imputed` | non-SL, `MinLot < q < MaxLots` | `(Â·q/(q+step), Â]` |
| `Unbounded` | non-SL at a grid edge: `q == MaxLots` → `(null, Â]`; `q == MinLot` → `[Â·q/(q+step), null)` | one endpoint null |
| `Unavailable` | `Size ≤ 0`, or an SL row with null/zero `RealizedRisk` | both null |

**Rationale.** A trailing stop changes the **exit, not the sizing** — every trade was sized from the
initial stop, so imputing `Â` for the ~73% of non-SL exits *recovers* the amount SQX used rather
than guessing. The `SL` label is clean: against a single reference of 75% of the **SL** median
(196.43 → 147.32), 0 of 90 `SL` trades fall below it and 74 of 96 `TrailingStop` do. 28 of the 96
`TrailingStop` are profitable exits, 25 of them among those 74 — the profitable ones are not a
subset of the below-threshold ones and the counts must not be read as nested. The shared reference is the point — `TrailingStop`'s own median is 98.12, half the
`SL` one, and comparing each group to itself would hide exactly the separation being demonstrated. `Unbounded` is not decorative — at `q ==
MinLot` a legitimate floor and a clamp-up are indistinguishable, so risk is unbounded **above**
(measured: the 1-decimal run's clamped trades realize $229.40–$404.60 against a $200 target);
at `q == MaxLots` it is unbounded **below**. Precedence:
`Measured > Unavailable > Unbounded > Imputed`.

### D6 — An interval has no scalar accessor

**Choice.** `TradeRiskInterval(decimal? Low, decimal? High)` with **no** `Value`, `Midpoint`,
`Mean`, or implicit `decimal` conversion. The guard pins this **positively**, not by blacklist:
`Low` and `High` are the whole of the type's decimal-valued surface, and no method — instance **or
static** — may return `decimal` or `decimal?`. A name blacklist alone catches only what someone
thought to name obviously; `static decimal Collapse(interval)` and `decimal? Average` both passed
the first version of it. `NormalizedTrade` carries `RLow`/`RHigh`, never a scalar `R`.
**Rejected.** A point estimate plus a `Basis` tag (the proposal's prose mitigation) — a bare number
next to an enum gets read as the number.
**Consumer contract.** MAY: render both endpoints; derive `R` bounds (`Profit > 0` →
`[P/High, P/Low]`; `Profit < 0` → endpoints **swap** — pin this); count by basis; test overlap.
MAY NOT: collapse to a point; average intervals; **sum endpoints across trades** (every trade shares
the same `Â`, so the errors are dependent and interval arithmetic overstates); rank on an
`Imputed`/`Unbounded` R without carrying its basis; divide by a null endpoint.

### D7 — The resizer scales the normalizer's interval; it never recomputes risk

**Choice.** `q' = clamp(⌊qᵢ·target/(Â·step)⌋·step)`, evaluated as ONE quotient; achieved risk is the trade's own
interval scaled by `q'/qᵢ`. The unrounded size is taken as its lower endpoint `uᵢ := qᵢ`.
**Rejected.** Band midpoint for `uᵢ` — it breaks the round-trip (`⌊qᵢ·1.005⌋` can exceed `qᵢ`).
Also rejected: `qᵢ·(target/Â)`, the two-rounding form. It rounds the quotient to 28 digits and only
then multiplies, flooring a step LOW when the exact lot count is integral and the quotient does not
terminate — `Â=199.98, target=66.66, size=3.00` gives 0.99 against an exact 1.00, which is exact
and reproducible. Two independent size×target sweeps put the disagreement rate at a few percent and
found the two-rounding form lower in **every** disagreement; the rate depends on which targets are
swept, so the direction is the claim, not a percentage. One-sided downward drift is precisely what
`achieved ≤ target` cannot catch. Found by review, not by the suite.
Recomputing achieved risk from `Â` uniformly — discards the exact measured value on SL trades.
**Rationale.** At `target = Â` the sizes reproduce **exactly**, because `qᵢ` is already on the grid.
The resizer therefore invents no precision the normalizer did not have.

### D8 — An unreachable target is emitted, labelled and counted — never silently clamped

| Outcome | Condition | Consequence |
|---|---|---|
| `OnTarget` | `MinLot ≤ q' ≤ MaxLots` | achieved ≤ target, within one step |
| `RaisedToMinimum` | `q' < MinLot` | **over-risked** — achieved interval exceeds target. Reachable only when `target < Â`; at `target = Â` the scale is 1 and nothing is raised |
| `CappedAtMaximum` | `q' > MaxLots` | under-risked |
| `Unscalable` | `Size ≤ 0` | size returned unchanged, achieved risk unknown. **Not** `RaisedToMinimum`: that outcome asserts the row is over-risked, which is unknowable when there is nothing to scale from |

`ResizedTradeSeries` carries `UnscalableCount` alongside the three clamp counts, so the four are
exhaustive over the rows. A non-positive `target` is refused outright (`ArgumentOutOfRangeException`)
— it floors negative, trips the minimum-lot branch for every row, and would otherwise return a
complete, plausible-looking series that is entirely wrong.
`MinLot == Step` (0.01 on a 2-decimal grid). **Rejected**: `0.1` from `06_Money Management.md` —
that is *Size if no MM*, the fallback when money management is **off**, not a floor; the 1-decimal
export's 33.8% pinned at 0.1 (= its own step, 114/337) is the evidence that the floor is the
step — against 0.3% (1/329) on the 2-decimal grid. Broker
minimums per symbol: deferred, no data source exists. `LotGrid(SizeDecimals, Step, MinLot, MaxLots)`
is a `Domain/Backtests` value object with a validating constructor and an `ImoxRetester` preset
(2, 0.01, 0.01, 10) — a record, not constants, so the 1-decimal grid is testable without shipping
support for it. The series is **not refused** on clamping (clamping is legitimate and unavoidable);
the counts and `MaxAchievedRisk` are the numbers a consumer checks. No clamp-fraction threshold is
defended — nothing measured supports one.

### D9 — `ResizedTradeSeries` is unreachable by the weight multiplier

`PortfolioStrategy.Weight` **coexists** untouched in this cut. Three facts, in descending strength:

1. **Structural.** `ResizedTradeSeries` is a sealed record, not `IReadOnlyList<StrategyTrade>`;
   `PortfolioMemberInput.Trades` is hard-typed, so passing one is a compile error. No conversion,
   no implicit operator, no `ToStrategyTrades()` — and this slice adds none.
2. **Structural.** Its element `ResizedTrade` has no `Commission`/`Swap`/`Taxes` and no
   `BaseEntity`, so `AnalyticsSeries.NetOf(StrategyTrade)` cannot bind — `w * NetOf(t)` does not compile.
3. **Convention, and named as such.** A future consumer that *does* accept the series MUST refuse
   `Weight != 1`. That is a spec obligation on slice 2b, not a type fact — stated the way
   `OosWindow` states the limit of its own guarantee rather than dressing it up as structural.

**Rejected.** `OosWindow`'s inverted-nesting trick (private ctor + nested factory) to make the
series unconstructable by hand: impossible across assemblies (the DTO is in Application, the
resizer in Infrastructure per convention), and the threat it blocks — hand-construction with a wrong
`TargetRiskPerTrade` — is strictly smaller than the double-scaling threat already closed by (1).
Also rejected permanently: synthesizing `StrategyTrade` (entity misuse; puts backtest rows one
`SaveChanges` from live data).

### D10 — Placement

Calculators in `Infrastructure/Services` (`SymbolPointValueCalibrator` precedent: public static,
stateless, plain entity/DTO args, no DbContext, directly unit-tested). Neither needs
`AnalyticsSeries`; the median is a private helper, exactly as the calibrator duplicates its own.
The `internal` + zero-`InternalsVisibleTo` constraint therefore does not bind — but Infrastructure
placement satisfies it anyway, so a later slice can reach those primitives without a move.
**Rejected.** `Domain/Backtests` (the `OosWindow.Resolver` precedent for a pure calculator in
Domain) — it would buy the D4 guarantee structurally at the cost of splitting the calculator
convention and permanently barring `AnalyticsSeries`.

### D11 — Grid adequacy is reported separately from model fit

**Choice.** `RunRiskEstimate` carries `MinLotPinnedFraction` (share of the run's trades whose `Size`
sits at `LotGrid.MinLot`) alongside `ConsistencyFraction`. Both are reported; only consistency
gates.
**Rejected.** Folding pinning into the consistency gate, or raising the gate until the 1-decimal
fixture fails it.
**Rationale.** They answer different questions and the measurements separate cleanly. *Consistency*
asks whether the floor sizing model applies at all: 100% (2-decimal) vs 93% (1-decimal) — both
plainly pass, so this number cannot discriminate the grids. *Pinning* asks whether the grid can
express the target: 0.3% vs **33.8%**, two orders of magnitude. Collapsing them into one gate would
reject a coarse grid for the wrong reason and report a fit failure that did not occur. No threshold
is defended on pinning either — it is a number a consumer reads, like `MaxAchievedRisk` in D8.

## Data Flow

    BacktestTrade[] (one run) ──→ TradeRiskNormalizer.Estimate ──→ RunRiskEstimate
                                          │                        (Status + fraction + evidence)
                                          │  Status != Estimated ──→ REJECTED, no trades emitted
                                          ▼
                                  TryNormalize ──→ RunRiskProfile { Estimate, NormalizedTrade[] }
                                          │                          (Basis + TradeRiskInterval each)
                                          ▼
                             TradeResizer.Resize(profile, target, grid)
                                          ▼
                                  ResizedTradeSeries  ──✗──→ PortfolioMemberInput  (compile error, D9)
                                          │
                                          └──→ RunRiskProfileDto ──→ GET api/backtests/runs/{id}/risk-profile ──→ UI row

## File Changes

| File | Action | Description |
|---|---|---|
| `Domain/Backtests/LotGrid.cs` | Create | Validated grid value object + `ImoxRetester` preset |
| `Domain/Enums/RiskBasis.cs`, `RunRiskEstimateStatus.cs`, `ResizeOutcome.cs` | Create | Three enums (`CalibrationStatus` precedent) |
| `Application/DTOs/Backtests/TradeRiskInterval.cs` | Create | `(decimal? Low, decimal? High)`, no scalar accessor |
| `Application/DTOs/Backtests/RunRiskEstimate.cs` | Create | `Status` + `RiskPerTrade` + `ConsistencyFraction` + `MinLotPinnedFraction` + `SlSampleCount` (5 members, per the contract block). No `ConsistentCount` — it is `ConsistencyFraction × SlSampleCount` |
| `Application/DTOs/Backtests/NormalizedTrade.cs`, `RunRiskProfile.cs` | Create | Per-trade basis/interval/R-bounds; profile bundle |
| `Application/DTOs/Backtests/ResizedTrade.cs`, `ResizedTradeSeries.cs` | Create | Already-sized output + outcome counts |
| `Application/DTOs/Backtests/RunRiskProfileDto.cs` | Create | Read projection |
| `Infrastructure/Services/TradeRiskNormalizer.cs` | Create | D1, D2, D4, D5 |
| `Infrastructure/Services/TradeResizer.cs` | Create | D3, D7, D8 |
| `Application/Interfaces/IBacktestReadService.cs` + `Infrastructure/Services/BacktestReadService.cs` | Modify | `GetRunRiskProfileAsync(runId, ct)` |
| `WebAPI/Controllers/BacktestsController.cs` | Modify | `[HttpGet("runs/{id:guid}/risk-profile")]` |
| `web/features/sqx/backtests/backtests-list/*` + `assets/i18n/{en,es}.json` | Modify | Expandable risk-profile row |
| Migrations · `StrategyTrade` · `PortfolioStrategy.Weight` · portfolios · analytics calculators | **Untouched** | No schema change; nothing persisted |

## Interfaces / Contracts

```csharp
// Both fractions always populated, including when Status != Estimated:
// the evidence must survive the rejection (D4).
public sealed record RunRiskEstimate(
    RunRiskEstimateStatus Status,
    decimal? RiskPerTrade,            // null unless Estimated
    decimal ConsistencyFraction,      // gates (D1)
    decimal MinLotPinnedFraction,     // reported, never gates (D11)
    int SlSampleCount);

public static class TradeRiskNormalizer
{
    public const decimal MinimumConsistencyFraction = 0.85m; // settled, uncalibrated (see D11)

    public static RunRiskEstimate Estimate(IEnumerable<BacktestTrade> trades, LotGrid grid);

    // false => Status != Estimated. No per-trade output exists for a rejected run.
    public static bool TryNormalize(
        IReadOnlyList<BacktestTrade> trades, LotGrid grid, out RunRiskProfile? profile);
}

public static class TradeResizer
{
    // Total: holding a RunRiskProfile already proves the estimate passed.
    public static ResizedTradeSeries Resize(RunRiskProfile profile, decimal targetRiskPerTrade, LotGrid grid);
}
```

## Testing Strategy

Strict TDD — every row is a RED test first.

| Layer | What | Approach |
|---|---|---|
| Unit — estimator | `Â = 199.98`, consistent 90/90 (`ConsistencyFraction = 1.0`) on `ListOfTrades_XAUUSD_H1_IST.csv`, **plus** a separate assertion that the band `[199.98, 200.16)` brackets the configured $200 | Fixture-driven; the measurement *is* the assertion. The second assertion is what the anchor was really claiming |
| Unit — estimator | Strict intersection holds on the 2-decimal fixture and is empty on the 1-decimal one; the robust form returns 199.98 and 200.00 respectively | Pins D1's reason, not just its result |
| Unit — estimator | `Profit` as source breaks at trade 33 where `RealizedRisk` holds through 90 | Pins D2 |
| Unit — estimator | 1-decimal fixture → `Estimated` at 93% (clears 85%), `MinLotPinnedFraction = 33.8%` | Pins D11: the gate passes, the pinning metric is what flags the grid |
| Unit — estimator | All 7 inconsistent 1-decimal trades sit at `MinLot`; none of the 66 non-pinned trades do | Pins the D5 `Unbounded` mechanism as measured, not posited |
| Unit — estimator | 2 SL closes → `InsufficientSamples`; 0 SL closes → never falls back to $200 | Calibrator-shaped |
| Unit — normalizer | One trade per basis; `TryNormalize` false ⇒ `profile is null` | Pins D4/D5 |
| Unit — normalizer | R bounds swap when `Profit < 0` | Pins the D6 gotcha |
| Unit — resizer | **Round-trip**: `target = Â` reproduces all 329 sizes exactly | Pins D7 |
| Unit — **estimator** | Floor vs round-half reconstruction: floor reproduces the original sizes and tops at 199.98 with 0 trades over $200; round-half reproduces 55/90, tops at 217.20, 35 over | Pins D3. This belongs at the estimator, **not** the resizer: at `target = Â` the scale is exactly 1 and every size is already on the grid, so the two rules agree there and discriminate nothing |
| Unit — resizer | `RaisedToMinimum` overshoots and is counted; `CappedAtMaximum` undershoots | Pins D8 |
| Compile-time | `PortfolioMemberInput(Trades: series)` does not compile | Documented in the spec; asserted by absence of any conversion member |
| Integration | `GET runs/{id}/risk-profile` returns the profile; a rejected run returns its fraction | Existing controller test pattern |
| Frontend | Expandable row shows measured/imputed/unbounded counts and never a bare risk for a non-measured trade | Vitest on `backtests-list.component.spec.ts` |
| Regression | Portfolio and live-trade suites unchanged and green | 365/365 + 371/371 |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or
process-integration boundary. Pure arithmetic over already-persisted rows plus one read endpoint.

## Migration / Rollout

No migration. Nothing persisted, no consumer to repair. Revert the commits. Two chained PRs
(PR1 calculators + fixture validation; PR2 read surface + UI) — the 400-line budget risk is High
for a single PR.

## What This Slice Cannot Claim

- **Not that non-SL risk is measured.** It is imputed from `Â` and bounded by the trade's own `Size`.
  If SQX ever sized off something other than the initial stop, the imputation inherits that error
  silently — nothing here detects it.
- **Not that `Â` will equal the configured risk.** It does not on the 2-decimal fixture (199.98,
  band `[199.98, 200.16)`) and coincidentally does on the 1-decimal one (200.00), where a clamped
  trade realizes exactly that. Under floor the estimate can only ever sit at or below the target,
  never on it by construction. The slice reports what the data supports and never seeds from the
  config — and the 1-decimal run realizes $229–$405 on its 7 clamped trades against that same $200.
- **Not that the 85% gate is calibrated.** It has **zero measured failures** — 100% and 93%, both
  pass. It is a guard against a run that is broken in some way not yet observed, not a discriminator
  between the grids in hand; D11's pinning fraction does that job. A judgment, and one with less
  evidence behind it than the first revision of this document claimed.
- **Not that a resized series is broker-executable.** Slippage, spread, swap, commission and real
  broker step/minimum are unmodelled; P/L rescaling is linear in volume, exact only for
  volume-proportional costs.
- **Not that R is stable.** A new run or a recalibration moves `Â` and every imputed interval with it.
- **Not that portfolio analytics use R.** Nothing consumes `ResizedTradeSeries`; `Weight` still
  governs every shipped number.
- **Not that clamped trades are rare or acceptable.** Counts are reported; no threshold is defended.
- **Not anything about 1-decimal exports, correlation, breach probability, Darwinex Zero VaR, the
  selector, or the OOS boundary** (slice 2b).
- **Not that point value is irrelevant.** It cancels in `Â` and in imputation, but any future
  conversion of a resized size back to a price-based stop needs it again.

## Open Questions

- [ ] Resize target default — `Â` (assumed) or an operator-supplied per-simulation value?
- [ ] Is a per-series clamp fraction ever a rejection, or always advisory? No measurement supports a
      threshold today.
- [ ] Does slice 2b generalise `PortfolioMemberInput.Trades` to a projection, or map at the boundary?
      Deferred deliberately — the shape is dictated by what scoring needs.
