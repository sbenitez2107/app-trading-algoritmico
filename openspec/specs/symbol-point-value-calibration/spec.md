# Symbol Point-Value Calibration Specification

> **Revision 3 — documentation reconciliation.** Corrections and additions below bring this
> spec in line with what shipped after the correction work units. NO code changed in that pass;
> every requirement added or amended names the test that already pins it.

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
still succeeds; only the point-value assessment is withheld. The persisted row
carries the actual `SampleCount`, which is where the shortfall is auditable.
The floor is intentionally low (3, not a larger statistical minimum) because
`PointValue` is a contract constant with zero measured variance across the 90
SL closes of the committed fixture — the `Inconsistent` status (spread > 0.5%,
see the recompute requirement below) is the guard against a genuinely bad
sample, not the count.

(Revision 3 correction — clause NARROWED, deliberately. The requirement
previously also demanded that "the batch result MUST flag the symbol as
'insufficient sample' with its actual count". That clause is DROPPED as a MUST
for two reasons. First, it names a batch, and batching was deleted in revision 2
— import is one file per slot, so the sentence cannot be made true as written.
Second, nothing implements it: the import result carries a `Reason` only, and
that field is populated exclusively when calibration FAILS, never to report a
thin sample. It is dropped rather than kept-and-marked because archiving merges
this text into the main capability specs, where a MUST reads as a guarantee the
system provides; an unimplemented MUST there is precisely the drift this
revision exists to remove. The intent is preserved as the deferred item below,
which claims nothing.)

**Deferred, not implemented:** surfacing "insufficient sample (n/3)" in the
import RESPONSE, so the operator learns of a withheld point value without
opening the calibrations panel. The persisted row is already correct and already
carries the count; only the response-side reporting is missing.

#### Scenario: Thin symbol does not calibrate

- GIVEN a symbol with 2 SL-closed trades
- WHEN calibration runs
- THEN a `SymbolCalibration` row IS written with `PointValue` NULL, `Status = InsufficientSamples`, and `SampleCount = 2`
- PINNED BY `SymbolPointValueCalibratorTests.Calibrate_TwoSamples_InsufficientSamplesWithNullPointValue`

#### Scenario: The floor is a floor, not a margin

- GIVEN a symbol with exactly 3 SL-closed trades and zero observed spread
- WHEN calibration runs
- THEN it calibrates — 3 is accepted, not rejected
- PINNED BY `SymbolPointValueCalibratorTests.Calibrate_ThreeSamplesZeroSpread_Calibrates`

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
- PARTIALLY PINNED BY `SymbolPointValueCalibratorTests.SelectDistinctContentRuns_TwoGenuinelyDifferentFiles_BothContribute` (the union half) and `BacktestCalibrationConcurrencyTests.ImportTradeListAsync_LosingTheCalibrationInsertRace_StillCalibratesAndDoesNotFailTheImport` (the replace/UPDATE half)
- COVERAGE GAP — the two halves are pinned SEPARATELY and never composed: no test imports a second genuinely different file for the same symbol THROUGH the import path and asserts the prior value was replaced by the union's value. The composition is believed correct by construction (selection is a pure function over persisted runs, and the upsert's UPDATE branch is exercised) but it is not proven end to end. Recorded rather than implied.

### Requirement: A Concurrent Calibration Upsert Converges Instead Of Failing The Import

Calibration writes one row per symbol behind a UNIQUE constraint, so two
imports finishing at once can race the same insert. The loser MUST NOT surface
as a request failure: the upsert MUST retry ONCE against a FRESH persistence
context — fresh because the entity from a refused insert is still pending and
re-saving would re-issue the very statement that was refused — so that the
second attempt reads the winner's committed row and takes the UPDATE branch.
Both writers therefore CONVERGE on one calibrated row. A SECOND conflict MUST
NOT be retried: that is no longer a race, and looping would hide a real fault.

Calibration runs AFTER the run and its trades are committed, so its failure MUST
NOT rewrite the import's true outcome. A permanent calibration fault MUST leave
the outcome as `Imported`/`Replaced` and carry the failure in the result's
`Reason`, naming the symbol and warning that the stored point value may be
stale. Reporting `Rejected` would be a false negative about committed rows;
letting it escape would be a false failure for a request whose data landed.
Because a reason on a SUCCESSFUL outcome is a warning rather than a rejection,
the import UI MUST render it as a distinct, non-failure notice — a warning the
user never sees is the same silent skip in another costume.

#### Scenario: Losing the insert race still calibrates and still imports

- GIVEN a competing writer inserts the symbol's calibration row between this import's read and its write
- WHEN the import completes
- THEN the unique-constraint conflict is absorbed, the retry updates the winner's row, and the import reports its true successful outcome
- PINNED BY `BacktestCalibrationConcurrencyTests.ImportTradeListAsync_LosingTheCalibrationInsertRace_StillCalibratesAndDoesNotFailTheImport`

#### Scenario: A permanent calibration fault reports, never throws

- GIVEN calibration fails for a reason a retry cannot resolve
- WHEN the import completes
- THEN the outcome remains the one the committed rows earned, `Reason` names the symbol and the failure, and no exception escapes
- PINNED BY `BacktestCalibrationConcurrencyTests.ImportTradeListAsync_WhenCalibrationFailsOutright_StillReportsTheImportThatCommitted`

#### Scenario: The warning is visible without claiming the slot failed

- GIVEN a slot imported successfully but carries a calibration warning in `Reason`
- WHEN the import modal renders that slot's result
- THEN the warning is displayed as a warning, and the slot is NOT presented as failed
- PINNED BY `import-strategy-backtests-modal.component.spec.ts > submit_ImportedWithAWarningReason_ShowsTheWarningWithoutClaimingTheSlotFailed`

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
