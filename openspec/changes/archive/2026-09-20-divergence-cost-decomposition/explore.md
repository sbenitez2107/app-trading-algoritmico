# Exploration — Divergence cost decomposition

> **Slice B of layer 0 in `openspec/SIMULATOR_ROADMAP.md`.** Gated behind the shipped
> `demo-backtest-comparability` capability (slice A).
>
> Engram topic: `sdd/divergence-cost-decomposition/explore`. Mirrored here by the orchestrator — the
> `sdd-explore` agent type has no file-write tool. Orchestrator verification notes are marked.

## Why gold, and why not by name

Measured over paired opens, offset as a share of price — the only comparison meaningful across
instruments quoted at 4,469 against 28,471:

| Instrument | Pairs | Mean price | Offset | Offset % | Positive |
|---|---|---|---|---|---|
| NQ | 160 | 28,470.6 | +25.436 | 0.0893% | 159/160 |
| DAX | 22 | 24,836.6 | +3.014 | 0.0121% | 14/22 |
| **XAUUSD** | 35 | 4,468.8 | **+0.101** | **0.0023%** | 26/35 |

Gold's offset is ≈39× smaller than NQ's and shows **no structure when split by month**, unlike DAX
which flips sign in June and NQ which spikes and reverts. For the indices a cost residual would be
dominated by the instrument mismatch rather than by execution.

**But the capability must not name an instrument.** Hardcoding `XAUUSD` would freeze a measurement
into code. It gates on slice A's result, so any instrument that qualifies is included — and the
indices qualify without a code change the day Darwinex data replaces the Dukascopy source.

## Why the slice is worth building — gold's cost profile

Over **661 demo trades** across 8 gold strategies:

| | |
|---|---|
| Gross profit | $6,101.08 |
| Commission | −$488.36 |
| **Swap** | **−$1,020.99** |
| Taxes | $0.00 |
| **Net** | **$4,591.73** |
| **Cost as % of gross** | **24.74%** |
| Trades paying swap | **334 of 661 (50.5%)** |

Against NQ, where cost was 4.2% of gross and swap touched 6 of 47 trades. **Gold is a different cost
regime**: swap alone is 16.7% of gross, half the trades pay it, and the backtest models none of it.
A gold backtest showing $6,101 gross corresponds to $4,592 real.

The embedded-cost method is confirmed on gold: `SymbolCalibrations` holds `XAUUSD_M1_UTC02` at
PointValue **100.0**, 776 samples, band 99.995–100.005. Implied point value `|Profit| / (|Δprice| ×
Size)` is **exactly 100.000** on demo and **100.170** on backtest — the same cost-inside-Profit
signature measured on NQ.

## A — The comparability gate: a structural dependency, never a computed threshold

This is the change's central design question, and the answer is constrained by what slice A
deliberately does **not** compute.

The distinction that separates gold from the indices — offset as a **percentage of price**, plus
monthly sign structure — **is not a field slice A produces**. Slice A's design decision D7 refused a
units conversion outright, naming the fields `…PriceUnits` precisely because no tick-size conversion
is performed. Any cutoff on that magnitude would be exactly the fabricated threshold that
`INDEX.md` §5 forbids and that slice A's own tripwire test exists to prevent.

**Recommendation: the gate is a caller decision expressed as a structural ordering constraint.**

Slice B's calculator takes slice A's `PriceOffsetComparabilityDto` as a **mandatory input
parameter** — never recomputed internally, never optional. That expresses *"you cannot decompose
cost without having already measured comparability for this exact strategy and run kind"*, which is
an ordering gate, not a magnitude gate. The decomposition DTO carries slice A's figures forward so a
human reads *"gold: 35 pairs, 0.0023% offset, no monthly structure — trust it"* against *"NQ: a
25-point offset, 159 of 160 positive, structural — do not."*

**No `IsComparableEnoughToDecompose` field may ever exist.** That is the shape the rule would take
if it were violated, so it is worth naming.

## B — What is reused and what is new

**Reusable from slice A**: the two `(OpenTime, OpenPrice)` projections and the pairing shape; the
per-subset net-P/L and `NetPlBasis` disclosure pattern; the `BacktestRunKind`-required convention
(D3); "null means undefined, never withheld" (D5); and the computed non-nullable disclosure property
mechanism that `ComparabilityBasis` established.

**New, absent from slice A**:
1. A tabulation of demo-only and backtest-only counts **for coverage labelling** — slice A's monthly
   breakdown covers *paired* trades only.
2. An isolated `Σ StrategyTrade.Swap`. Slice A only ever nets swap into `DemoNetPlBasis`.
3. Embedded cost via `SymbolCalibration.PointValue`, keyed by the **verbatim SQX symbol** on
   `BacktestRun`/`BacktestTrade` — confirmed against `BacktestImportService.UpsertCalibrationAsync`,
   with no broker or instrument mapping in between.
4. The residual arithmetic composing the three above.

**Recommendation**: a **new** `CostDecompositionDto` that composes slice A's DTO as an input, rather
than extending the sealed `PriceOffsetComparabilityDto`. Slice A's own spec names
`demo-backtest-cost-decomposition` as a separate future slice in its Non-Goals.

## C — The coverage component is a presumption, and must say so

The roadmap's criterion — *"a period with no backtest trades and available demo trades is a coverage
gap, not a divergence"* — is correctly **one-directional**: the confirmed cause is source-data gaps
in the Dukascopy feed, which delete **backtest** trades and never demo trades.

What it cannot do is distinguish *"the data was absent"* from *"the strategy did not signal"* with
certainty. That is precisely the ambiguity slice B exists to resolve, and it cannot resolve it
conclusively, because the only independent corroboration — SQX's Data Manager gap percentage — has
**no ingestion path anywhere in the codebase**. It is GUI-only.

> ✅ **Orchestrator verification**: confirmed. A repo-wide search for data-quality ingestion returns
> only unrelated uses of the word "gap" — zero-filled days in `AnalyticsSeries` and a VaR comment.
> There is no reader for the Data Manager's quality figures.

So the coverage label is a **presumption from absence, not a proof**, and must carry its own
disclosure property mirroring `ComparabilityBasis`. It must never be presented as confirmed for an
instrument that has not been externally checked.

**Granularity**: reuse slice A's **month grain** rather than inventing a day-level aggregate, and
expose the underlying open timestamps so a later capability can drill deeper without a second
aggregate type existing now.

## D — Embedded cost: four calibration states, and no staleness concept

`CalibrationStatus` has three members — `Calibrated = 0`, `InsufficientSamples`, `Inconsistent` — and
`PointValue` is **null unless `Calibrated`**.

A **fourth state** exists and is distinct: **no row at all**, for a symbol never imported.
`UpsertCalibrationAsync` writes a row only once an import for that symbol has run, so "row absent"
is not the same as "row present with a null PointValue". Both must be reported distinctly, and
**neither may fall back to an assumed point value**.

> ✅ **Orchestrator verification**: the codebase already records this distinction itself.
> `SymbolCalibration.cs:10` reads *"a missing row cannot express 'tried, not enough' vs 'never
> tried'."* Documented intent, not an inference.
>
> Also checked, because `Calibrated = 0` is the CLR default and this codebase has twice been bitten
> by an optimistic enum zero (`PlatformType.MT4 = 0`, `FundingService.Other = 0`): `Status` is
> assigned explicitly from `result.Status` on **both** the insert and update paths in
> `BacktestImportService`. No path creates a row without setting it. Not a hazard here.

**"Stale calibration" is `NOT FOUND` as a domain concept.** `CalibratedAt` exists but nothing
compares it to anything — no TTL, no recency rule, no re-certification trigger. Per `INDEX.md` §5,
do not invent a staleness cutoff; **report `CalibratedAt` verbatim beside the figure** and let the
reader judge.

> ✅ **Orchestrator verification**: confirmed. All six references to `CalibratedAt` are writes,
> storage configuration, or projections. Nothing compares it.

Gold is already `Calibrated`, so the primary case has no blocker.

## E — What the residual may claim

Even on gold with matching prices, the residual still carries two independent causes recorded in
slice A's Non-Goals: the **M1-versus-tick intrabar ambiguity**, and unpaired-trade effects.

The roadmap's component table computes the residual over the whole disjoint partition. **That is
wrong.** Folding demo-only and backtest-only P/L into one residual mixes *"the same signal produced a
different result"* with *"this trade never had a counterpart at all"*.

**Recommendation: compute the primary residual over the paired subset only**, reporting the unpaired
subsets separately — exactly as slice A already does. Never combined into one figure.

## F — Placement and sizing

New `internal static` calculators sibling to `DemoBacktestComparabilityCalculator.cs` and
`SymbolPointValueCalibrator.cs` in `Infrastructure/Services/`; new DTOs in
`Application/DTOs/Divergence/`.

**Do not fold into `PortfolioAnalyticsCalculator`** — slice A deliberately placed its calculator
beside it rather than inside it (D6).

**Sizing: ~600-750 production plus test lines**, `400-line budget risk: High`. The estimate accounts
for the last four slices overrunning their forecasts by roughly 2×. Recommend chained PRs by
component in the roadmap's own order: **coverage → swap and embedded cost → residual**.

## Risks

- **Threshold-invention pressure.** A later phase may read "gate on the comparability result" as
  licence to add a numeric cutoff. There is no such number in any measured or vendor source, and
  slice A's tripwire test exists to catch exactly this.
- **The coverage label is unverifiable** without the Data Manager figures, which have no ingestion
  path. It must never read as confirmed for an instrument nobody checked externally.
- **No staleness concept exists.** Do not invent one.
- **Sizing will likely exceed a naive per-component split** once shared plumbing — DI, controller
  wiring, disclosure and tripwire tests — is duplicated across four components.

## Carry into the proposal

1. Exact field and property names for the coverage disclosure.
2. Record **"residual over the full partition"** explicitly as a rejected alternative, with the
   reason, so the roadmap's own wording does not reintroduce it later.
