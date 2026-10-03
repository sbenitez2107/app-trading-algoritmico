# Delta for symbol-point-value-calibration

> Change: `point-value-calibration-short-sl`. One ADDED requirement and three MODIFIED requirements.
> MODIFIED blocks carry the full requirement text, including the preserved scenarios.

## ADDED Requirements

### Requirement: Short-Distance SL Samples Are Excluded

Calibration MUST exclude every SL-closed sample whose price distance
`|ClosePrice - OpenPrice|` is strictly less than `200 × tick`. A sample at
exactly `200 × tick` MUST be retained.

`tick` MUST be inferred as `10^-d`, where `d` is the maximum number of
SIGNIFICANT decimal places (trailing zeros ignored) observed across `OpenPrice`
and `ClosePrice` of the SL-closed trades handed to calibration. The inference
MUST NOT depend on the stored decimal scale. `21500.10000` and `21500.1` both
contribute one decimal place.

When `d = 0` (no fractional price observed), the grid cannot be told apart from
a coincidence, and calibration MUST apply NO exclusion. This keeps the behaviour
in place before this requirement.

Rationale: SQX exports prices rounded to the grid while `MAE` uses the
unrounded SL level, so the implied distance error stays under half a tick. At
200 ticks that moves a sample by at most ±0.25%, which can never push a correct
symbol past the 0.5% spread gate. The exclusion is order-independent because
`d` is a maximum over the whole population.

#### Scenario: Short-SL rounding outliers no longer block calibration

- GIVEN an NQ-like symbol (1-decimal prices, tick 0.1) whose long-SL samples (≥ 20.0 points) are all exactly 10, plus short-SL samples (< 20.0 points) whose grid rounding puts them more than 0.5% from 10
- WHEN calibration runs
- THEN `Status = Calibrated`, `PointValue = 10`, and `SampleCount` equals the number of long-SL samples only

#### Scenario: A genuinely inconsistent symbol stays inconsistent

- GIVEN a symbol whose SL distances are all ≥ 200 ticks and whose samples differ by 1%
- WHEN calibration runs
- THEN `Status = Inconsistent`, `PointValue` is NULL, and `MinObserved`/`MaxObserved` are the retained extremes

#### Scenario: Every sample under 200 ticks

- GIVEN a symbol with fractional prices where every SL distance is below 200 ticks
- WHEN calibration runs
- THEN `Status = InsufficientSamples`, `PointValue` is NULL, `SampleCount = 0`, and `MinObserved`/`MaxObserved` are NULL

#### Scenario: Exactly 200 ticks is retained

- GIVEN a sample whose distance is exactly `200 × tick`
- WHEN calibration runs
- THEN that sample counts toward `SampleCount`

#### Scenario: Stored decimal scale does not change the tick

- GIVEN the NQ-like population with every price carried at scale 5 (as read from `decimal(18,5)`)
- WHEN calibration runs
- THEN the result is identical to the scale-1 population

#### Scenario: Integer-only prices apply no exclusion

- GIVEN SL-closed trades whose prices are all whole numbers
- WHEN calibration runs
- THEN no sample is excluded by distance
- PINNED BY the existing synthetic tests in `SymbolPointValueCalibratorTests` (prices 100/99), which MUST stay unedited and green

#### Scenario: Gold-like and DAX-like symbols are unchanged in value

- GIVEN a gold-like symbol (2-decimal prices, tick 0.01, point value 100) and a DAX-like symbol (1-decimal, shortest SL ≥ 37 points)
- WHEN calibration runs
- THEN gold calibrates at 100 and DAX calibrates at 10, with every DAX sample retained

## MODIFIED Requirements

### Requirement: Point Value Derived From MAE, Never From Profit

For each symbol, `PointValue` MUST be computed only from trades where
`CloseType == SL` that survive the short-distance exclusion (see "Short-Distance
SL Samples Are Excluded"), as `|MAE| / (|OpenPrice - ClosePrice| * Size)`.
`Profit` MUST NOT be used as the derivation source.

#### Scenario: XAUUSD calibrates exactly

- GIVEN `Strategy S1`'s Deploy run from `ListOfTrades_XAUUSD_H1_IST.csv` (90 SL-closed XAUUSD trades, shortest SL distance 3.75 ≥ 200 × 0.01)
- WHEN calibration runs
- THEN `PointValue = 100.000`, `MinObserved = MaxObserved = 100.000`, `SampleCount = 90`

### Requirement: Auditable Evidence Persisted

Every calibration MUST persist `SampleCount`, `MinObserved`, `MaxObserved`,
and `CalibratedAt` alongside `PointValue`, even when observed values vary
across trades. `SampleCount`, `MinObserved` and `MaxObserved` MUST describe the
RETAINED population, after the short-distance exclusion, so they describe the
same set the median and the spread gate evaluated.

**Deferred, not implemented:** persisting or surfacing the number of samples
excluded for short distance. No column carries it, so the persisted row shows
only the retained count.

#### Scenario: Spread is visible, not hidden

- GIVEN a symbol whose SL trades yield varying point values
- WHEN calibration runs
- THEN `MinObserved != MaxObserved` is persisted and surfaced, not averaged away silently

#### Scenario: Evidence describes the retained set

- GIVEN a symbol where some SL samples are excluded for short distance
- WHEN calibration runs
- THEN `SampleCount`, `MinObserved` and `MaxObserved` are computed only over the retained samples

### Requirement: Minimum Sample Size Gate

A symbol's RETAINED SL-closed sample count (after the short-distance exclusion)
below the configured minimum (default: 3) MUST still produce a persisted
`SymbolCalibration` row, but with `PointValue` NULL and
`Status = InsufficientSamples`. Import of trades for that symbol still
succeeds; only the point-value assessment is withheld. The persisted row
carries the retained `SampleCount`, which is where the shortfall can be audited.
The floor is intentionally low (3, not a larger statistical minimum) because
`PointValue` is a contract constant with zero measured variance across the 90
SL closes of the committed fixture. The `Inconsistent` status (spread > 0.5%
over the retained samples) is the guard against a genuinely bad sample, not the
count.

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

#### Scenario: The floor counts retained samples, not raw SL closes

- GIVEN a symbol with many SL closes of which fewer than 3 are at or above 200 ticks
- WHEN calibration runs
- THEN `Status = InsufficientSamples` and `SampleCount` is the retained count
