# Tasks: point-value-calibration-short-sl

Strict TDD: every behaviour task is RED → GREEN. Paths are relative to `app.trading.algoritmico.api/`.
Do NOT commit (user instruction). Never kill processes; the user's API may be running.

## Delivery forecast

- Decision needed before apply: No
- Chained PRs recommended: No
- 400-line budget risk: Low (~40 production lines in one file, ~200 test lines in one new file)

## Design decisions (settled from code)

| # | Decision | Evidence |
|---|----------|----------|
| D1 | No declared tick exists. Infer `tick = 10^-d`, where `d` = the maximum number of significant decimals (trailing zeros stripped) across the Open/Close prices of the SL trades passed to `Calibrate`. If `d = 0`, apply no exclusion. | `FtmoInstrumentSpec.cs:11-39` (no tick/digits column); repo grep for `TickSize\|Digits\|PricePrecision\|PipSize` = 0 hits; prices stored `decimal(18,5)` (`BacktestTradeConfiguration.cs:40-41`). Rows read back from SQL Server can carry scale 5, so stripping trailing zeros is mandatory. Without it the tick becomes 0.00001 and the filter does nothing in production while in-memory tests pass. |
| D2 | `< 3` retained → `InsufficientSamples`. `SampleCount`/`Min`/`Max` describe the RETAINED set. The excluded count is not persisted (deferred, no schema change). | Floor `SymbolPointValueCalibrator.cs:23`, `:73-83`; entity fields `SymbolCalibration.cs:20-23`. |
| D3 | Calibration runs ONLY at the end of an import, including an `Unchanged` (identical bytes) import, which writes no run/trade rows. There is no endpoint and no background job. Stored rows are NOT recomputed on deploy; they need an explicit, authorized re-import. | `BacktestImportService.cs:58-69`, `:168-175` (Unchanged returns the symbol), `:287-357`; `BacktestsController.cs:44-47` (GET only); grep `IHostedService\|BackgroundService` = 0 hits; route `StrategyBacktestsController.cs:56` `POST api/strategies/{id}/backtests/{kind}`. |
| D4 | Consumers: `FtmoSimulationInputs.cs:123-137` (Status + PointValue only), `CostDecompositionCalculator.cs:91-104` (Status, PointValue, SampleCount passthrough), `BacktestReadService.cs:72-76` → `backtests-list.component.html:97-99` (display). Min/Max are displayed only; no consumer computes with them. `TradeRiskNormalizer.cs:28` reuses only the `MinimumSlSamples` constant. | grep of `MinObserved\|MaxObserved` across api/src and web/src. |

Pre-existing tests that this change would alter: **none**. The five synthetic tests (`SymbolPointValueCalibratorTests.cs:117-210`) use whole-number prices (100/99). They are kept by the `d = 0` rule in D1. Without that rule, all five would break. The F1 fixture's shortest SL is 3.75 (≥ 2.00), so every `SampleCount = 90` assertion holds (`SymbolPointValueCalibratorTests.cs:95,223,252`, `BacktestImportServiceTests.cs:407,426`). `BacktestCalibrationConcurrencyTests.cs:151-152` rows have distance 11.67.

## Phase 1: RED tests

New file `tests/AppTradingAlgoritmico.UnitTests/Backtests/SymbolPointValueCalibratorShortSlTests.cs`. It is a new file so that `git diff` proves the existing test file is unedited. Use fractional prices and build `RealizedRisk` from an unrounded SL distance.

- [x] 1.1 Test `Calibrate_NqLikeShortSlRoundingOutliers_CalibratesAtTen`. Use ≥ 5 long SLs (≥ 20.0 points, tick 0.1, sample exactly 10) plus ≥ 2 short SLs (7–10 points) whose MAE uses a distance off by < 0.05, so they deviate > 0.5%. Assert Calibrated, `PointValue = 10`, `SampleCount` = long count, `Min = Max = 10`. RED: failed Inconsistent (spread 0.75%).
- [x] 1.2 Test `Calibrate_LongSlGenuineOnePercentSpread_StaysInconsistent`. All distances ≥ 200 ticks; samples 10 and 10.1. Assert Inconsistent, null PointValue, Min 10 / Max 10.1. Guard: passed on arrival (as predicted).
- [x] 1.3 Test `Calibrate_AllSamplesUnder200Ticks_InsufficientSamples`. Fractional prices, every distance < 200 ticks. Assert InsufficientSamples, `SampleCount = 0`, Min/Max/PointValue null. RED: failed, Status Calibrated instead of InsufficientSamples (SampleCount 4 != 0).
- [x] 1.4 Test `Calibrate_ExactlyTwoHundredTicks_IsRetained`. Distance exactly 20.0 at tick 0.1 is counted. RED on arrival too (Inconsistent, because the 19.9-point sample was not yet excluded).
- [x] 1.5 Test `Calibrate_PricesAtStorageScaleFive_SameResultAsScaleOne`. Feed the 1.1 population with prices like `21500.10000m`; the result equals 1.1. RED: failed Inconsistent.
- [x] 1.6 Test `Calibrate_GoldLikeTwoDecimalPrices_CalibratesAtHundred`. Use tick 0.01 with cents-rounded `RealizedRisk`, including a few SLs < 2.00 that would breach the gate. Assert 100 and the retained count. RED: failed Inconsistent.
- [x] 1.7 Test `Calibrate_DaxLikeLongSls_EverySampleRetained`. Tick 0.1, shortest SL 37 points. Assert 10 and `SampleCount` = all SL samples. Guard: passed on arrival.
- [x] 1.8 Test `Calibrate_NqLikeReversedOrder_IdenticalResult` (order independence of the tick). Guard: passed on arrival.
- [x] 1.9 Run `timeout 300 dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=bin-scratch/ --filter FullyQualifiedName~SymbolPointValueCalibratorShortSlTests`. Record which tests fail and why: 1.1/1.5/1.6 must fail on `Inconsistent`, and 1.3 on `SampleCount != 0`. 1.2/1.4/1.7/1.8 may pass on arrival; they are guards, so note that. Result: 5 failed (1.1, 1.3, 1.4, 1.5, 1.6), 3 passed (1.2, 1.7, 1.8).

## Phase 2: GREEN implementation

- [x] 2.1 In `src/AppTradingAlgoritmico.Infrastructure/Services/SymbolPointValueCalibrator.cs`, add `public const int MinimumSlDistanceTicks = 200;` and a private `InferTick` (max significant decimals over Open/Close, trailing zeros stripped; returns null when `d = 0`).
- [x] 2.2 Apply the exclusion in the sample loop (`:55-69`) before `samples.Add`. Leave the signature, `MaxSpreadFraction`, `MinimumSlSamples` and `SelectDistinctContentRuns` untouched.
- [x] 2.3 Update the class XML doc (`:7-14`) to state the exclusion, the tick inference and the `d = 0` rule.
- [x] 2.4 Re-run the 1.9 filter, then `--filter FullyQualifiedName~SymbolPointValueCalibrator`. All tests should be green. Result: 19/19 green (8 new + 11 existing).

## Phase 3: Falsification

Each mutation is temporary. Revert it and confirm with `git diff` that only the intended changes remain.

- [x] 3.1 Set `MinimumSlDistanceTicks = 0`. Tests 1.1, 1.3 and 1.6 MUST go RED. Result: RED on 6 tests (1.1, 1.3, 1.4, 1.5, 1.6, 1.8).
- [x] 3.2 Infer `d` from the raw decimal scale (no trailing-zero strip). Test 1.5 MUST go RED. Result: RED on 1.5 only.
- [x] 3.3 Change `<` to `<=`. Test 1.4 MUST go RED. Result: RED on 1.4 only.
- [x] 3.4 Drop the `d = 0` guard (the integer tick becomes 1). The existing `Calibrate_ThreeSamplesZeroSpread_Calibrates` MUST go RED, which proves the guard is load-bearing. Result: RED on 5 existing tests, including `Calibrate_ThreeSamplesZeroSpread_Calibrates`.

## Phase 4: Gates (from `app.trading.algoritmico.api/`, each under `timeout`)

- [x] 4.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror -p:BaseOutputPath=bin-scratch/ > build.log 2>&1`. Zero warnings. Result: 0 warnings, 0 errors.
- [x] 4.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log 2>&1`. No diffs. Result: exit 0, no diffs.
- [x] 4.3 `timeout 900 dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=bin-scratch/ > test.log 2>&1` (full suite, once). All green. Result: 1027 passed / 4 skipped / 0 failed (baseline 1019 + 8 new).
- [x] 4.4 `git diff --stat`. The existing file `SymbolPointValueCalibratorTests.cs` must be unchanged. Only the calibrator and the new test file may change. Result: existing test file has an empty git diff; only the calibrator changed, plus the new test file.

## Phase 5: Recalibration of stored rows (DB WRITE — authorization required)

- [x] 5.1 STOP and ask the user for explicit authorization. Name the target: local Docker container `mssql-db`, database `AppTA`, table `SymbolCalibrations` (rows for NQ, XAUUSD, DAX). Proceed only after a yes.
- [x] 5.2 The user restarts their API on the new build (the agent never kills processes). Through the API, record the baseline from `GET api/backtests/calibrations`.
- [x] 5.3 Re-import one already-imported file per symbol into its existing slot (`POST api/strategies/{id}/backtests/{kind}` or the import modal). Expect outcome `Unchanged`, which writes no run/trade rows and recalibrates.
- [x] 5.4 Verify: NQ Calibrated 10, n=1915; XAUUSD 100, n=776 (corrected: no gold stop is under 200 ticks); DAX 10 with an unchanged `SampleCount`. An NQ FTMO simulation no longer refuses `PointValueNotCalibrated`.

Recalibration evidence (2026-10-03, user-authorized; target mssql-db/AppTA/SymbolCalibrations): API started from the
committed build on :5000, both Deploy CSVs re-imported (SHA-256 identical to the stored ContentHash, outcome Unchanged,
no run/trade rows written), API stopped afterwards. Before: NQ Inconsistent/NULL/2034. After: NQ Calibrated 10/1915,
XAUUSD Calibrated 100/776 (recalibrated at 21:38:18 UTC), DAX untouched 10/485.
