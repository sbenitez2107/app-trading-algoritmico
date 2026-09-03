# Tasks: Trade Risk Normalization (slice 2a) — PR1 only

PR1 = calculators, types, fixture-validated unit tests. The design's `GET runs/{id}/risk-profile`
integration row and the Vitest expandable-row row are **PR2 and deliberately omitted** — no spec
exists for them yet. Threat Matrix is `N/A` in design, so no threat RED tasks.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 700–950 (≈450 production, ≈450 tests) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1a → PR1b → PR1c (all inside the design's PR1) |
| Delivery strategy | ask-on-risk (default; none supplied) |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| 1 | Grid + enums + `Estimate` (Phases 1–2) | PR1a | `dotnet test app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests --filter "FullyQualifiedName~LotGridTests|FullyQualifiedName~TradeRiskNormalizerEstimateTests"` | N/A — pure static calculator, no I/O; the committed fixtures are the harness | New files only, no consumer |
| 2 | `TryNormalize` + per-trade types (Phase 3) | PR1b | `... --filter FullyQualifiedName~TradeRiskNormalizerNormalizeTests` | N/A — same | New files only; PR1a stands alone |
| 3 | `TradeResizer` + series (Phase 4) | PR1c | `... --filter FullyQualifiedName~TradeResizerTests` | N/A — same | New files only; PR1b stands alone |

## Phase 1: Foundation

- [x] 1.1 Create `Domain/Enums/RiskBasis.cs`: `Measured|Imputed|Unbounded|Unavailable`.
- [x] 1.2 Create `Domain/Enums/RunRiskEstimateStatus.cs`: `Estimated|InsufficientSamples|Inconsistent`. Design names it two ways; the contract name wins.
- [x] 1.3 Create `Domain/Enums/ResizeOutcome.cs`: `OnTarget|RaisedToMinimum|CappedAtMaximum`.
- [x] 1.4 RED `LotGridTests`: reject `Step<=0`, `MinLot<Step`, `MaxLots<MinLot`, decimals mismatching step; `ImoxRetester` == (2, 0.01, 0.01, 10).
- [x] 1.5 GREEN `Domain/Backtests/LotGrid.cs` — sealed record, validating ctor, `ImoxRetester` preset (D8).
- [x] 1.6 Create `Application/DTOs/Backtests/TradeRiskInterval.cs` `(decimal? Low, decimal? High)` — no `Value`/`Midpoint`/`Mean`/implicit conversion (D6).
- [x] 1.7 Create `Application/DTOs/Backtests/RunRiskEstimate.cs`: `Status`, `RiskPerTrade`, `ConsistencyFraction`, `MinLotPinnedFraction`, `SlSampleCount` (D4).
- [x] 1.8 Add a raw-CSV fixture loader to the test project — the shipped parser rejects `_OOST.csv` wholesale (multiple `Sample type` values), so parser-based loading cannot serve the 1-decimal cases.

## Phase 2: Estimator (RED → GREEN)

- [x] 2.1 RED IST fixture: `Â = 199.98` (not 200.00 — candidates are band lower endpoints and none is 200.00), `SlSampleCount = 90`, consistent 90/90 (100%), `Status = Estimated`, band `[199.98, 200.16)` brackets the configured $200.
- [x] 2.2 GREEN `Infrastructure/Services/TradeRiskNormalizer.Estimate` — floor inversion `[rᵢ, rᵢ(qᵢ+step)/qᵢ]`, most-covered lower endpoint, smallest-value tie-break (D1, D3).
- [x] 2.3 RED strict intersection: IST non-empty at `[199.98, 200.16)`, OOST empty; the robust form returns 199.98 on IST and 200.00 on OOST.
- [x] 2.4 RED source: `RealizedRisk` holds through all 90; `Profit` breaks at trade 33 (69% vs 100% of intervals contain 200); ticket 1851 → 173.76, not 174.70 (D2).
- [x] 2.5 RED OOST fixture: `Estimated`, `Â = 200.00`, 88/95 (93%) — clears the 85% gate.
- [x] 2.6 RED+GREEN `MinLotPinnedFraction`: 114/337 (33.8%) OOST, 1/329 (0.3%) IST; always reported, never gates (D11).
- [x] 2.7 RED OOST offenders: all 7/7 inconsistent trades sit at `MinLot` 0.10 realizing $229.40–$404.60; 29 SL trades are pinned but only 7 are inconsistent.
- [x] 2.8 RED+GREEN refusals: 2 SL closes → `InsufficientSamples`; a sub-85% population → `Inconsistent` with `RiskPerTrade` null and the fraction kept; 0 SL closes never yields 200 (`MinimumSlSamples = 3`).

## Phase 3: Normalizer (RED → GREEN)

- [x] 3.1 RED `TryNormalize` false ⇒ `profile is null` — a rejected run emits no per-trade rows at all (D4).
- [x] 3.2 RED one trade per basis; a `MinLot` pin opens the interval's **HIGH** side and `MaxLots` the LOW side; precedence `Measured > Unavailable > Unbounded > Imputed` (D5).
- [x] 3.3 RED IST labels: none of the 96 `TrailingStop` trades is `Measured` (74 below 75% of the shared **SL** median 196.43 → 147.32; 28 of the 96 profitable, 25 of them among those 74); all 90 `SL` trades are, and 0 fall below that threshold.
- [x] 3.4 RED R bounds: `Profit > 0` → `[P/High, P/Low]`; `Profit < 0` **swaps** the endpoints; a null endpoint gives a null bound, never a division (D6).
- [x] 3.5 GREEN implement `TryNormalize` plus `NormalizedTrade.cs` and `RunRiskProfile.cs`.

## Phase 4: Resizer (RED → GREEN)

- [x] 4.1 RED round-trip: `target = Â = 199.98` on `ImoxRetester` reproduces all 329 IST sizes exactly and no achieved risk exceeds 199.98 (D7).
- [x] 4.2 RED floor vs round-half both computed at the **estimator's** reconstruction, not the resizer (at `target = Â` the scale is 1 and both rules agree, discriminating nothing): only floor reproduces the sizes and the ≤200 cap; the supported rule is named in the assertion message (D3).
- [x] 4.3 RED clamps: `RaisedToMinimum` overshoots the target, `CappedAtMaximum` undershoots, both counted, `MaxAchievedRisk` reported, the series never refused (D8).
- [x] 4.4 GREEN `Infrastructure/Services/TradeResizer.Resize` plus `ResizedTrade.cs`, `ResizedTradeSeries.cs` — `scale = target/Â`, `q' = clamp(⌊qᵢ·scale/step⌋·step)`, achieved = the trade's own interval × `q'/qᵢ`, carrying `TargetRiskPerTrade` + `LotGrid`.
- [x] 4.5 RED+GREEN reflection guard: `ResizedTradeSeries` exposes no `StrategyTrade` conversion — no implicit/explicit operator, no `ToStrategyTrades` (D9).

## Phase 5: Verification

- [x] 5.1 Full backend `dotnet test` green; portfolio and live-trade suites unchanged.
- [x] 5.2 XML docs on both calculators naming the decision each rule implements (`SymbolPointValueCalibrator` precedent).
