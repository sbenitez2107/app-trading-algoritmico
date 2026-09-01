# Symbol Point-Value Calibration Specification

## Purpose

Derive and persist one auditable point value per symbol from SL-closed
backtest trades, using `MAE` (exact) instead of `Profit` (spread/commission
contaminated), gated by a minimum sample size. Consumed by later slices for
risk normalization; this capability only persists the number and its evidence.

## Amendment (this revision)

Strategy-scoped FK attribution (see `strategy-model`) reintroduces exactly
the double-counting a join table used to prevent: the same file, imported for
two strategies (e.g. one SQX strategy deployed on both FTMO-Demo2 and
SBDEMO2), now legitimately produces two `BacktestRun` rows sharing one
`ContentHash`. Calibration MUST count SL-closed trades from only ONE run per
distinct `ContentHash` (see the new requirement below), or `SampleCount` —
the exact value the `InsufficientSamples` floor evaluates — reports double
the true sample.

Also note: `ListOfTrades_XAUUSD_H1_OOST.csv` mixes two `Sample type` values
and can no longer be imported as a Deploy/Evaluation run (see
`sqx-backtest-import`'s single-sample-type guard). It is excluded from the
scenarios below; `ListOfTrades_XAUUSD_H1_IST.csv` (90 SL-closed trades) is
the grounded example instead.

## Requirements

### Requirement: Point Value Derived From MAE, Never From Profit

For each symbol, `PointValue` MUST be computed only from trades where
`CloseType == SL`, as `|MAE| / (|OpenPrice - ClosePrice| * Size)`. `Profit`
MUST NOT be used as the derivation source.

#### Scenario: XAUUSD calibrates exactly

- GIVEN `Strategy S1`'s Deploy run from `ListOfTrades_XAUUSD_H1_IST.csv` (90 SL-closed XAUUSD trades)
- WHEN calibration runs
- THEN `PointValue = 100.000`, `MinObserved = MaxObserved = 100.000`, `SampleCount = 90`

### Requirement: Auditable Evidence Persisted

Every calibration MUST persist `SampleCount`, `MinObserved`, `MaxObserved`,
and `CalibratedAt` alongside `PointValue`, even when observed values vary
across trades.

#### Scenario: Spread is visible, not hidden

- GIVEN a symbol whose SL trades yield varying point values
- WHEN calibration runs
- THEN `MinObserved != MaxObserved` is persisted and surfaced, not averaged away silently

### Requirement: Minimum Sample Size Gate

A symbol's SL-closed trade count below the configured minimum (default: 3)
MUST still produce a persisted `SymbolCalibration` row, but with `PointValue`
NULL and `Status = InsufficientSamples`. Import of trades for that symbol
still succeeds; only the point-value assessment is withheld, and the batch
result MUST flag the symbol as "insufficient sample" with its actual count.
The floor is intentionally low (3, not a larger statistical minimum) because
`PointValue` is a contract constant with zero measured variance across 185 SL
closes — the `Inconsistent` status (spread > 0.5%, see the recompute
requirement below) is the guard against a genuinely bad sample, not the count.

#### Scenario: Thin symbol does not calibrate

- GIVEN a symbol with 2 SL-closed trades
- WHEN calibration runs
- THEN a `SymbolCalibration` row IS written with `PointValue` NULL and `Status = InsufficientSamples`, and the result reports "insufficient sample (2/3)"

### Requirement: Calibration Recomputes Over All Known Trades For A Symbol

When a new run adds SL-closed trades for a symbol that already has
calibration data, the system MUST recompute `PointValue` and its evidence
over the UNION of all SL-closed trades for that symbol across every imported
run (deduplicated by `ContentHash`, see below), replacing the prior
calibration.

#### Scenario: A second run adds new SL trades for the same symbol

- GIVEN XAUUSD calibration already exists from `Strategy S1`'s Deploy run (90 SL-closed trades from `ListOfTrades_XAUUSD_H1_IST.csv`)
- WHEN `Strategy S2`'s Deploy run — a genuinely different XAUUSD file (different `ContentHash`) — is also imported
- THEN calibration recomputes over the union of both runs' SL-closed trades, replacing the `S1`-only value

### Requirement: Calibration Deduplicates Identical Runs By Content Hash

When computing the SL-closed trade population for a symbol, the system MUST
include trades from only ONE `BacktestRun` per distinct `ContentHash`, even
when multiple runs (across different strategies) share that hash.
`SampleCount` MUST reflect the deduplicated population, not the raw row count
summed across all runs.

#### Scenario: Same file imported for two strategies does not double-count

- GIVEN `ListOfTrades_XAUUSD_H1_IST.csv` already backs `Strategy S1`'s Deploy run, contributing 90 SL-closed XAUUSD trades to calibration
- WHEN the identical bytes are also imported as `Strategy S2`'s Deploy run (same `ContentHash`, different `StrategyId`)
- THEN XAUUSD calibration still reports `SampleCount = 90`, not `180`, and `PointValue`/`MinObserved`/`MaxObserved` are computed from the single deduplicated set

#### Scenario: Genuinely different files for the same symbol both count

- GIVEN `Strategy S1`'s Deploy run and `Strategy S3`'s Deploy run are both XAUUSD but from two files with different `ContentHash` values
- WHEN calibration runs
- THEN both runs' SL-closed trades contribute — deduplication is by content hash, not by symbol or strategy

### Requirement: Rejected Rows Never Enter Calibration

A degenerate row rejected at import (`ClosePrice == OpenPrice`) MUST be
excluded from the SL-closed trade population used for calibration.

#### Scenario: Degenerate row excluded

- GIVEN a rejected degenerate SL-flagged row
- WHEN calibration runs for that symbol
- THEN the rejected row's `MAE` is not part of the sample count or the `PointValue` computation
