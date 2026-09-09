# Proposal: Cost reconciliation and divergence (layer 0)

> ⚠️ **Read the emphasis, not the folder name.** The folder says *cost reconciliation*. Measurement
> (`.agents/knowledge/imox/MEASURED_Demo_vs_Backtest_Divergence.md`, 2026-09-09) moved the primary
> deliverable to **comparability detection**. The name is kept for traceability; the first thing this
> change ships is a check on whether the demo and backtest price series describe the same asset.

## Intent

**Problem.** Layer 0 of `openspec/SIMULATOR_ROADMAP.md` was scoped to split demo-vs-backtest
divergence into cost components. Measured on `WF_7_30_NQ_H_CW_H_O_H1_2.34.172`, 24 trades open at the
**exact same minute** in both series, yet entry prices differ by a mean of **+22.06 (Apr, n=5),
+22.55 (May, n=14), +40.90 (Jun, n=5)** — always positive, never near zero, and drifting. Root cause,
confirmed from the SQX Data Manager: data symbol `USATECHIDXUSD_M1_UTC02` (Dukascopy USA100) is bound
to instrument `NDX_DARWINEX`. **Prices come from one broker, point value and costs from another.**

**Why now.** Decomposing costs between two different assets is arithmetic dressed as signal. On
2026-06-11 the same signal produced demo TP +$147.90 and backtest SL −$195.75 purely because the
backtest entered 39 points lower. The comparability check is cheap, has already found root cause once,
and gates every other component.

**Success.** A per-strategy, report-only, ranked comparability readout whose caveat cannot be dropped
at a call site, plus a four-component decomposition that only runs on comparable pairs.

## Scope

### In Scope
- **Price-series offset check** — exact-minute pairing of demo vs backtest opens; per-month mean,
  range, sign consistency, drift, paired count. Runs first, gates the rest.
- **Swap** — `Σ demo(Swap)`, exact. The backtest models none, by academy decision (`01_SQX_Data.md:181-191`).
- **Embedded backtest cost** — implied point value `Profit / (|Close−Open| × Size)`; demo is exactly
  10.0000, backtest 9.81–12.5. Cross-checked against the configured 5.5 $/lot.
- **Trade-set difference** — reported as a set relationship (47 demo / 42 Deploy / 39 Evaluation),
  never folded into a residual.
- **Execution residual** — what remains, on the paired subset only.
- **`PlatformType? SourcePlatform` on `BacktestRun`** — the demo side already carries
  `TradingAccount.Platform`; the backtest side does not.
- One read endpoint + a minimal Angular readout.

### Out of Scope / Non-goals
- **Any threshold or pass/fail.** The KB reads large divergence as a modelling problem and publishes
  **no number**; `INDEX.md` §5 forbids inventing domain criteria. Output is ranked, report-only. A
  future threshold is elicited from the user and persisted as configuration, never a code constant.
- **Any claim that demo outperforms the backtest.** 24 paired signals cannot establish direction.
- Merging the two series into one; per-trade matching for composition; correcting the backtest.
- Bulk backtest import; the intrabar-path gap; DST handling; layers 1-6.

## Capabilities

### New Capabilities
- `demo-backtest-comparability`: exact-minute price-offset measurement and its non-droppable disclosure.
- `demo-backtest-cost-decomposition`: swap, embedded cost, trade-set difference, execution residual.

### Modified Capabilities
- `sqx-backtest-import`: a run records the platform that produced it.

## Approach — decisions taken

**D1 — The offset check is its own slice, shipped first.** Agreed with the orchestrator's inclination.
It is independently valuable (it names an actionable cause), it needs nothing from the other four
components, and it is the only piece that would still be right if the decomposition were abandoned.
*Rejected*: one change delivering all five — the exploration already revised sizing upward past the
400-line review budget, and a bug in component 4 would block shipping component 1.

**D2 — The offset is a comparability measure, not a score.** No single aggregated number is exposed.
The DTO carries `PairedTradeCount`, per-month `MeanOffsetPoints` / `MinOffsetPoints` /
`MaxOffsetPoints`, `SignConsistency`, and a **non-nullable `ComparabilityBasis`** computed property
mirroring `BreachBasis` (`Domain/Enums/BreachBasis.cs`) — that precedent made a caveat impossible to
drop at a call site, and the same trick applies here: the offset is measured on **paired opens only**
and states so structurally. Sign convention (demo minus backtest, in instrument points) is named on
the type. Ranking across the 123 strategies is by `|mean offset|`, presented as ordering, not verdict.
*Rejected*: a 0-100 "quality score" — it would be read as strategy performance and invites a threshold.

**D3 — The trade-set difference is a set relationship, not currency.** Report `PairedCount`,
`DemoOnlyCount`, `BacktestOnlyCount` and the P/L of each disjoint subset **separately**. The value
comparison and the execution residual are computed on the **paired subset only**.
*Rejected*: absorbing it into the residual — a residual mixing "traded better" with "traded more
often" informs nothing.

**D4 — Platform lives on `BacktestRun` as `PlatformType? SourcePlatform`, nullable, set explicitly
at import.** Verified: `TradingAccount.Platform` (`PlatformType`) already exists, so the demo half is
covered and no new enum is needed. ⚠️ **`PlatformType.MT4 = 0`**, so a non-nullable column would
silently assert MT4 for every legacy row. Nullable = "not recorded", never inferred. The 16-column SQX
export carries no engine field, so the value is a request parameter. *Rejected*: retrofitting after
the October 2026 SQX v144 / MT5 migration loads a mixed population.

**D5 — Whole-window aggregation for values; daily buckets are visualization only.**
`AnalyticsSeries.BuildDailyNetSeries` buckets on raw `.Date` with **no timezone conversion**, and with
43-47 trades over ~130 days most days are empty, so one day-boundary error moves a whole trade with
nothing to average it out. Exact-minute pairing is used **only** for the offset; it is never used to
merge series.

**Placement**: Application DTO mirroring the sealed/private-constructor/nested-factory pattern of
`BacktestNetSeries`; a stateless static calculator in `Infrastructure/Services` beside
`PortfolioAnalyticsCalculator`; one interface; one read endpoint.

## Affected Areas

| Layer / Area | Impact | Description |
|---|---|---|
| `Domain/Entities/BacktestRun.cs` | Modified (slice C) | `PlatformType? SourcePlatform` |
| `Domain/Enums/` | New (slice A) | `ComparabilityBasis` (mirrors `BreachBasis`) |
| `Application/DTOs/Divergence/` | New | Comparability DTO (A), decomposition DTO (B) |
| `Application/Interfaces/` | New | Read interface for the divergence report |
| `Infrastructure/Services/` | New | `DemoBacktestComparabilityCalculator` (A), decomposition (B) |
| `Infrastructure/Persistence/Configurations` + Migrations | Modified (slice C) | Additive nullable column |
| `WebAPI/Controllers` | Modified | One read endpoint per slice |
| Angular `features/strategies` (+ `core/services`) | Modified | Read-only readout, EN + ES keys |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| **n = 1 strategy.** Only 1 of 123 has both sides loaded | Certain | State it in the artifact and in the UI empty state; no threshold may be inferred from it; the readout must degrade to "insufficient paired trades" rather than a number |
| Offset read as a performance figure | High | D2 — no aggregated score, non-nullable basis, ranked not graded |
| Non-nullable platform column silently asserts MT4 (`MT4 = 0`) | High if unguarded | D4 — nullable; a test pins that a legacy row reads `null` |
| Second, independent modelling gap: `"1 minute data tick"` precision means intrabar SL/TP order is an **assumption** | Certain | **Recorded, not designed for.** Produces the same symptom and would survive fixing the price series. Documented in the DTO remarks so a clean offset is not read as "comparable" |
| Seasonal DST risk (Data Manager UTC+02 Jerusalem vs US rules) | Low here | **Ruled out as this finding's cause** — all 24 offsets are positive; a timezone error gives random signs. Recorded as a separate late-March / late-Oct risk |
| Daily bucketing corrupts the value comparison | Medium | D5 — whole-window aggregation |
| Scope creep into layer 1+ | Medium | Non-goals above |

## Migration and Rollback

- **Slices A and B: no migration and no rollback needed.** Both are pure read paths over existing
  tables; reverting is deleting new files and unregistering one endpoint.
- **Slice C** adds one **additive nullable** column to `BacktestRuns`. Rollback = `Down()` drops the
  column; no data loss, because no existing behaviour reads it. No backfill: `null` means "not
  recorded", which is the truth for every current row.

## Testing (strict TDD)

Every slice red-first, `dotnet test` from `app.trading.algoritmico.api`. New tests pin: exact-minute
pairing, per-month aggregation, sign-consistency, drift, the "insufficient paired trades" refusal,
`ComparabilityBasis` non-nullability (reflection test, matching `BacktestNetSeries`' precedent), the
paired-subset restriction, the implied-point-value derivation against the measured 10.0000 / 9.81–12.5
figures, and `null` platform on legacy rows. A fixture is derived from the measured case so the
committed numbers are the ones the database produced.

**Must not regress: backend 607/607 with 0 warnings, frontend 393/393.**

Expected **byte-identical** (unchanged files) across all slices:
`BacktestPortfolioRiskTripwireTests`, `PortfolioAnalyticsCalculatorLiveOutputRegressionTests`,
`PortfolioAnalyticsCalculatorTests`, `PortfolioAnalyticsPrivateCoreTests`,
`BacktestNetSeriesBridgeTests`, `SqxTradeListParserTests`, `TradeResizerTests`,
`TradeRiskNormalizerEstimateTests`, `TradeRiskNormalizerNormalizeTests`, `LotGridTests`,
`SymbolPointValueCalibratorTests`, `OosWindowResolverTests`, `BacktestGroupRiskAnalysisTests`.
Slice C is the only one that touches import: `BacktestImportServiceTests`, `BacktestSchemaTests` and
`StrategyBacktestsControllerTests` **gain** cases; existing cases stay unchanged.

## Sizing and slice recommendation

| Slice | Content | Est. changed lines | Ship |
|---|---|---|---|
| **A** | Price-series offset check + `ComparabilityBasis` + endpoint + readout | ~350-400 | **First, alone** |
| **B** | Swap, embedded cost, trade-set difference, execution residual (gated on A) | ~400-500 | Second |
| **C** | `SourcePlatform` on `BacktestRun` + import parameter | ~120-180 | Independent; before Oct 2026 |

**Recommendation: chained PRs, A → B, with C independent.** A single PR exceeds the 400-line review
budget; the exploration already revised sizing upward for exactly this reason.

## Open questions needing a human

1. **Does the user intend to rebind the SQX data symbol to a Darwinex-sourced series?** If yes, the
   offset becomes a transitional diagnostic; if no, it is a permanent bias disclosure. It changes how
   prominent the readout should be, not what it computes.
2. **Should slice A ship with a UI at all, or backend-only first?** A backend-only A is ~200 lines.
3. **Where should the offset readout live** — strategy detail, or a cross-strategy ranked list? The
   ranked list is the form that pays across 123 strategies, but only 1 has data today.
4. **Slice C: is the platform per run, or per strategy?** Proposed per run. If the user always
   rebuilds both slots on the same platform, per strategy is smaller — but per run survives a mixed
   rebuild.

## Assumptions flagged

- **A1** The 24-trade offset generalizes to other strategies bound to the same data symbol. Unverified
  — n = 1.
- **A2** Exact-minute pairing on `OpenTime` suffices; no timezone conversion is applied to either
  side before pairing. This is what produced the measured result, and applying a conversion now would
  invalidate it.
- **A3** The paired subset is the correct denominator for the execution residual. Follows from D3, not
  from measurement.
- **A4** `Domain` may reference `PlatformType` from `BacktestRun` without a layering violation —
  `TradingAccount` already does.
- **A5** The user will supply the platform at import time. If not, every new run is `null` too, and
  slice C buys only the capability, not the data.

## Success Criteria

- [ ] The offset readout reproduces the measured figures for `WF_7_30_NQ_H_CW_H_O_H1_2.34.172`:
      24 paired trades, +22.06 / +22.55 / +40.90 by month.
- [ ] No threshold, pass/fail, grade or aggregated score appears anywhere in the output.
- [ ] The comparability caveat cannot be dropped at a call site (compile-enforced, pinned by a test).
- [ ] The decomposition refuses to run — or discloses — when the offset is material.
- [ ] Trade-set difference is reported as counts and separate subset P/L, never inside a residual.
- [ ] Backend 607/607 → ≥607 with 0 warnings; frontend 393/393 → ≥393.
