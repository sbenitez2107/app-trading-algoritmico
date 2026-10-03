# Proposal: Exclude Short-Distance SL Samples From Point-Value Calibration

## Intent

`USATECHIDXUSD_M1_UTC02` (NQ) is `Inconsistent` (spread 0.658% > 0.5%) although its true point value is exactly 10 (1988/2034 samples are 10.000000). SQX exports prices rounded to the price grid while MAE uses the unrounded SL level; on 7–10 point SLs that sub-half-tick error exceeds the gate. NQ has no point value, so the FTMO simulation refuses it (`FtmoSimulationInputs.cs:126-133`).

## Scope

### In Scope
- Drop SL samples whose price distance is under 200 ticks; tick inferred from observed price decimals (`SymbolPointValueCalibrator.cs:51-69`).
- `SampleCount`/`MinObserved`/`MaxObserved` describe the retained set.
- RED-first tests plus falsification mutations.
- Authorized re-import that recalibrates the stored rows in local `mssql-db` / `AppTA`.

### Out of Scope
- Persisting or showing the excluded-sample count (deferred).
- A declared tick column (`FtmoInstrumentSpec.cs:11-39` has none).
- A recalibrate endpoint or background job.
- Changing `MaxSpreadFraction`, the floor of 3, or the run dedup.

## Capabilities

### New Capabilities
None

### Modified Capabilities
- `symbol-point-value-calibration`: adds the short-distance exclusion. Evidence and the sample floor now describe the retained population.

## Approach

Inside `Calibrate`, compute `tick = 10^-d`, where `d` is the maximum number of significant decimals (trailing zeros stripped) across the SL trades' Open/Close prices. Skip a sample when `|Close − Open| < 200 × tick`. When `d = 0`, apply no exclusion, so behaviour stays as today. At 200 ticks, half-tick rounding moves a sample by ≤ ±0.25% and cannot breach 0.5%. Rejected: a 1% tolerance or percentile trimming (they would hide real errors) and interval intersection (more complex).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Infrastructure/Services/SymbolPointValueCalibrator.cs` | Modified | Tick inference + exclusion |
| `tests/.../Backtests/SymbolPointValueCalibratorShortSlTests.cs` | New | RED scenarios |
| `SymbolCalibrations` rows (data) | Modified | NQ → Calibrated 10; gold n 776 → 266 |

No Domain/Application/WebAPI/Angular code changes. Display-only consumers: `backtests-list.component.html:97-99`, cost decomposition `SampleCount`.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| DB `decimal(18,5)` scale makes the inferred tick 0.00001, so the filter does nothing | Med | Strip trailing zeros; scale-5 test + mutation |
| Bound assumes distance error < half tick | Low | Evidence: errors −0.0393..+0.0402 on a 0.1 tick |
| Integer-grid symbol gets no filter | Low | Same as today: fails toward `Inconsistent` |

## Rollback Plan

Revert the calibrator commit, then re-import one file per symbol (with authorization) to restore the old rows. No schema change.

## Dependencies

- User authorization before any DB-writing step.

## Success Criteria

- [ ] NQ: Calibrated, 10, n=1915, spread 0.264%.
- [ ] Gold: 100, n=266. DAX: unchanged at 10.
- [ ] All pre-existing calibrator tests green and unedited.

## Proposal question round

Assumed, needs review:
1. The excluded count is not shown yet, even though NQ will show n=1915 out of 2034.
2. Gold's displayed sample count dropping from 776 to 266 is acceptable.
3. Integer-only symbols keep today's unfiltered behaviour.
4. Recalibration happens by re-importing existing files, not through a new endpoint.

Approved: the user approved the plan and all four assumptions above on 2026-10-03
(excluded-sample count deferred with no schema change; gold 776 -> 266 samples accepted;
integer-only symbols stay unfiltered; recalibration by re-importing existing files).
