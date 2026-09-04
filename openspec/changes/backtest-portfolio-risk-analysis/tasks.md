# Tasks: Backtest Portfolio Risk Analysis (slice 2b)

Two capabilities: `backtest-net-series-bridge` (4 req / 9 scen) and `backtest-portfolio-analytics`
(11 / 22), plus deltas `portfolio-monthly-var` (1 / 7) and `trade-risk-normalization` (1 / 1).

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,540 (PR1 ~330 · PR2 ~670 · PR3 ~540) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 → PR 3 |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Private cores: `CorrelationMatrixCore`, `SupportedPercentile`, `SeriesDensity`/`Measure`, `PercentilePolicy` threading. Live output bit-identical | PR 1 | `dotnet test --filter FullyQualifiedName~PortfolioAnalytics` | N/A — private calculator internals, no runtime surface | `PortfolioAnalyticsCalculator.cs` only |
| 2 | `backtest-net-series-bridge` in full, then the typed analytics adapters and both gates | PR 2 | `dotnet test --filter FullyQualifiedName~BacktestNetSeries\|BacktestVar\|BacktestCorrelation\|DensityGate` | N/A — no endpoint yet | New `DTOs/Backtests/*` + `Domain/Enums/*` |
| 3 | Run selection (five outcomes), the two `Unknown` refusals, endpoint, UI panel, i18n | PR 3 | `dotnet test --filter ~RunSelection\|~BacktestsController` + `pnpm vitest run group-risk` | `GET /api/backtests/portfolio-risk` against both fixtures | Controller + read service + web feature |

## Phase 0: Preconditions

- [ ] 0.1 **Substantive** — decide `SeriesDensity` provenance. `internal static SeriesDensity Measure(IReadOnlyList<decimal> denseDailyNets)` cannot produce `TradeCount` (a dense *daily* series cannot recover a trade count) nor `ExcludedUnscalableCount`, yet D4 lists `TradeCount` among "what is measured" and the File Changes row calls `SeriesDensityDto` a "read projection of the Infrastructure-side `SeriesDensity`". Either widen `Measure`'s inputs or declare those two fields bridge-sourced and composed at the read-service boundary. Gates 2.16 and 2.22 — the single-derivation test is otherwise unimplementable as written.
- [ ] 0.2 Clarify: `Min is null` yields "no evidence **for that run**" (analytics R6) while a missing match yields "no evidence for this **segment**" for the member (R4). A strategy with one trade-less run and one matching run must still succeed; neither file says the per-run state is non-fatal. Gates 3.3.
- [ ] 0.3 Housekeeping: the design's *Spec Dependencies* section is headed "requirements this design needs that no delta spec currently covers", but items 1, 5, 6, 7 and 8 are all now covered by the specs. Prune it before apply so a later reader does not re-spec satisfied items.

## Phase 1 (PR 1): Private cores — no new behaviour

- [x] 1.1 RED: pin current live `ComputeCorrelation`/`ComputeVaR`/monthly outputs as literal expected values (backfill, not RED-first — Note A).
- [x] 1.2 GREEN: private nested `AlignmentMode`; extract `CorrelationMatrixCore(labels, dayMaps, AlignmentMode)`; live `ComputeCorrelation` becomes an adapter passing `Union`. Signature unchanged.
- [x] 1.3 RED: `SupportedPercentile(sorted, p)` returns null iff `negativeCount < floor(p*(N-1)) + 1`; table cases including `N=3860, p=0.05 → 193` and `p=0.01 → 39`.
- [x] 1.4 GREEN: declare `internal readonly record struct SeriesDensity` and `Measure(...)` beside `SupportedPercentile`, with the input set per 0.1. `Percentile` body untouched.
- [x] 1.5 GREEN: private nested `PercentilePolicy { Unconditional, RequireSupport }`; thread it as a **required** parameter through `VarFromDaily(nets, policy)` and `ComputeMonthlyVar(nets, capital, policy)`. Both are private (`:440`, `:460`), so this is not a shipped-signature change.
- [x] 1.6 GREEN: live `ComputeVaR` becomes an adapter passing `Unconditional`.
- [x] 1.7 RED: `PercentilePolicy` regression — a live series that *would* fail the support test still returns its number (`Unconditional` never gates); every shipped daily and monthly VaR bit-identical.
- [x] 1.8 Verify the backend suite and 371/371 green and bit-identical (419/419 before PR1, **448/448** after — the 365/365 written here originally was a stale baseline). Do **not** assert "`ComputeMonthlyVar` untouched" — the spec forbids that claim; the assertion is live-output bit-identical.

### PR 1 apply record

| Item | Result |
|---|---|
| Backend suite | **448/448** (baseline was **419/419**, not the 365/365 this checklist and the design's testing strategy still say — stale figure, carried forward from an earlier slice) |
| Frontend suite | **371/371**, untouched — PR1 changes no web file |
| `dotnet build -warnaserror` | 0 warnings, 0 errors solution-wide |
| `dotnet format --verify-no-changes` | clean |
| Changed lines | **228 insertions / 21 deletions** in `PortfolioAnalyticsCalculator.cs` + **482** new authored test lines (est. was ~330 for production+tests combined) |
| Files | `PortfolioAnalyticsCalculator.cs` (modified) · `PortfolioAnalyticsCalculatorLiveOutputRegressionTests.cs`, `PortfolioAnalyticsPrivateCoreTests.cs` (new) |

**1.1 could not be RED-first** (Note A). It backfills shipped behaviour, so all 7 of its assertions
passed before any production change. Trustworthiness came from three recorded injected-defect runs,
each reverted:

1. `AlignmentMode.Intersection` on the live `ComputeCorrelation` adapter ⇒
   `ComputeCorrelation_UnionAlignment_PinsCoefficientAndAverage` FAILED:
   *"Expected correlation.Matrix[0] to be equal to {1M, 0.8182M}, but {1M, 1M} differs at index 1."*
2. `PercentilePolicy.RequireSupport` on all three live call sites ⇒ 3 failures, all
   `System.InvalidOperationException : Nullable object must have a value` at
   `PortfolioAnalyticsCalculator.cs:318`. The deliberate `.Value` (rather than `?? 0m`) turns a
   mis-wired policy into a loud crash instead of a silently published `0`.
3. `RequireSupport` on the **monthly call site only** ⇒ 2 failures, isolating the monthly half:
   *"Expected service.MonthlyVar95 to be -300M because Unconditional never gates: 1 negative window
   < the 4 the relation needs, but found &lt;null&gt;"* — and the pre-existing shipped test
   `ComputeVaR_MonthlyVar_ZeroFilledDaysDoNotDistortSums` caught it independently.

**Deviations from the checklist text**, both deliberate:

- `CorrelationMatrixCore(dayMaps, mode)` takes **no `labels`** parameter. Labels shape the DTO, not
  the math; keeping them in the adapter lets PR2's `decimal?`-celled DTO reuse the same core without
  a second signature change. The core returns `(Matrix, ObservationDays, AverageCorrelation)`.
- 1.3 and 1.4 are pinned **by reflection** over the non-public cores. Both are non-public by design
  (D4/D7) and their first public consumers are PR2's adapters, so PR1 has no public surface through
  which to assert the predicate table. The reflection binding doubles as a rename tripwire.
- 1.4 gained tests it was not paired with (it is a GREEN-only row). New production code with zero
  coverage in the PR that introduces it was judged worse than five extra assertions.
- The synthetic boundary rows for the support relation (`floor(p(N-1))` exactly, and one more) are
  asserted here at the **predicate** level for both `p = 0.05` and `p = 0.01`. That does not
  discharge 2.14, which is the same boundary at the **gate/adapter** level.

## Phase 2A (PR 2): `backtest-net-series-bridge`

- [x] 2.1 Create `Domain/Enums/{BacktestNetSeriesStatus,VarWithholdReason}.cs`. No `SegmentSelection`/`SegmentSource` (D8).
- [x] 2.2 RED: `net_i = source[i].Profit * (ResizedSize / OriginalSize)`; at `target = Â` nets reproduce `Profit` exactly (IST `Â = 199.98`).
- [x] 2.3 RED: a resized `RowIndex` with no source match **throws** `ArgumentException` naming it.
- [x] 2.4 RED: a **duplicated** source `RowIndex` throws naming it (the concatenated-runs wiring error).
- [x] 2.5 RED: **defensive guard, hand-built** — a `ResizedTradeSeries` constructed directly in the test with a non-contiguous strict-subset `RowIndex` set pairs correctly and does not throw on the differing count. **Must not be fixture-driven**: `TradeResizer.Resize` adds a row per trade unconditionally, so every real series has equal counts and a fixture version would be green under the rejected positional zip. Carry the "defensive, hand-constructed, no production producer" label in the test name and comment — its absence is as untestable as the guard itself.
- [x] 2.6 RED: `Weight` `1.5`, `0.5`, `0` each refused as a **status** naming member + weight, `Series is null`, **no throw**; `1` converts with every net unscaled. One test with both assertions covers the bridge's two weight scenarios and the `trade-risk-normalization` pointer scenario (Note D) — do not write three.
- [x] 2.7 RED: `Unscalable` rows contribute no net to `Nets` (not a `0`); `Nets.Count == resized.Trades.Count - ExcludedUnscalableCount`, asserted on the same series instance.
- [x] 2.8 GREEN: `Application/DTOs/Backtests/BacktestNetSeries.cs` — sealed **class**, private ctor, nested `public static class Bridge` with `Build`/`TryBuild(out BacktestNetSeries?)`, required `decimal memberWeight`, `BacktestSegment segment`, `ExcludedUnscalableCount`, **no `Density` member**; plus `BacktestNetSeriesResult.cs` and `DatedNet`.
- [x] 2.9 Reflection test: no public ctor, no scaling member, no `Density`; `PortfolioMemberInput(Trades: resizedSeries)` still does not compile.

## Phase 2B (PR 2): `backtest-portfolio-analytics` — adapters and gates

- [x] 2.10 Create `Application/DTOs/Backtests/SeriesDensityDto.cs`: `TradeCount`, `DenseDayCount`, `NegativeDayCount`, `NonZeroDayCount`, `NegativeWindowCount`, `ExcludedUnscalableCount`.
- [x] 2.11 RED: IST dense series 3,860 elements, 164 negative days (4.25%), 318 non-zero (8.24%); daily VaR95 withheld (164 < 193) while **VaR99 reports `sorted[38]`** (164 ≥ 39) — one run, two verdicts, the gate evaluated per confidence level. OOST 3,804 / 172 / 320, same VaR95 verdict.
- [x] 2.12 RED: monthly VaR95 reports **−400.19** on IST (1,148 negative windows of M=3,831, needs ≥192) and **−378.62** on OOST (1,203 of 3,775, needs ≥189); both clear `MinHistoryDays = 90`. Assert the figures, not just the path.
- [x] 2.13 RED: the wrong predicate — a non-zero-day gate at 5% would REPORT both fixtures (8.24%, 8.41%) while the true daily VaR95 is `0.00`.
- [x] 2.14 RED: **synthetic boundary, both gates** — a constructed population with exactly `floor(p(M-1))` negative observations withholds and one more reports; mirror case for the daily gate's *reporting* branch (Note B).
- [x] 2.15 RED: **injected defect** — zero out all but 191 of IST's negative window sums; the monthly figure flips to withheld with the count reported.
- [x] 2.16 RED: **one derivation** — the payload's `NegativeDayCount`/`NonZeroDayCount`/`DenseDayCount`/`NegativeWindowCount` are the same values the gates consumed; assert on the same `SeriesDensity`/`ComputeMonthlyVar` result, never on recomputed numbers. Scope excludes `TradeCount`/`ExcludedUnscalableCount` per 0.1.
- [x] 2.17 RED: no trim — the backtest adapter passes `windowDays: 0`; `ObservationDays == 3,860` on IST, not 250, and the gate needs 193 not 13.
- [x] 2.18 RED: intersection alignment — disjoint trading days yield a withheld (`null`) cell not `0`; `CoActiveDays`/`CoActiveShare` reported with **no** co-absence caveat; `CoActiveDays < 2` or a constant series withholds; all-withheld ⇒ `AverageCorrelation is null` + `WithheldCellCount`. Live path keeps `Union`, bit-identical.
- [x] 2.19 GREEN: `ComputeCorrelation(IReadOnlyList<BacktestNetSeries>)` with `AlignmentMode.Intersection`; `ComputeVaR(IReadOnlyList<BacktestNetSeries>)` passing `windowDays: 0` and `PercentilePolicy.RequireSupport`.
- [x] 2.20 RED: no public member accepts an untyped `(label, broker, dated nets)` tuple — reflection over the calculator's public surface; only the two typed entry points are public.
- [x] 2.21 GREEN: `BacktestPortfolioRiskDto.cs`, `BacktestCorrelationDto.cs`, `BacktestServiceRiskDto.cs`; reuse `VarTargetReadoutDto` unchanged. No shipped DTO touched.
- [x] 2.22 GREEN: project `SeriesDensity` → `SeriesDensityDto` at the read-service boundary, composing the bridge-sourced counts per 0.1, per series and for the merged group series.
- [x] 2.23 RED: every withheld figure serialises as JSON `null`, never `0`.
- [x] 2.24 RED: **no band position** (D4c) — `MonthlyVar95Percent` comes only from the shipped `monthlyVar95 / initialCapital` basis with the denominator label; the slice never derives a band position from the currency figure.

### PR 2 apply record

| Item | Result |
|---|---|
| Backend suite | **489/489** (baseline **448/448**; +41 new: 12 bridge, 29 adapter) |
| Frontend suite | **371/371**, not re-run — PR2 changes no web *code*. Corrected by verification: the diff does touch 3 web files, but only the `0.24.1`→`0.25.0` version bump, so no code path could alter the outcome. Skipping the suite was right; "zero web files" was not. |
| `dotnet build -warnaserror` | 0 warnings, 0 errors solution-wide |
| `dotnet format --verify-no-changes` | clean (required one formatting pass over the new adapter block) |
| Changed lines | **1,709** = production **774** (278 added to `PortfolioAnalyticsCalculator.cs` + 496 in eight new files) + tests **935**. Estimate was ~670; the overrun is tests and per-field provenance doc comments, the same shape as PR1 (est. 330, actual 731) |
| Files | 8 created (`Domain/Enums/{BacktestNetSeriesStatus,VarWithholdReason}`, `Application/DTOs/Backtests/{BacktestNetSeries,BacktestNetSeriesResult,SeriesDensityDto,BacktestPortfolioRiskDto,BacktestServiceRiskDto,BacktestCorrelationDto}`) · `PortfolioAnalyticsCalculator.cs` modified · 2 test files created · 1 PR1 test file touched by one disambiguating line |

**Every 2A/2B row was RED first.** The first build after the bridge tests failed with
`CS0246: BacktestNetSeries could not be found`; the analytics tests were likewise written and
compiled against absent types before any adapter existed. Two rows are backfills of shipped
behaviour and were proven by injected defect instead:

1. **Live union alignment** (2.18's last clause). Flipping the live `ComputeCorrelation` adapter to
   `AlignmentMode.Intersection` failed PR2's new `ComputeCorrelation_LivePath_StillAlignsOnTheUnion...`
   (*"to be 0.5000M ... but found 0M"*) **and** PR1's `ComputeCorrelation_UnionAlignment_PinsCoefficientAndAverage`
   (*"{1M, 0.8182M}, but {1M, 1M} differs at index 1"*). Reverted; 489/489 green.
2. **The density gate itself.** Passing `PercentilePolicy.Unconditional` on the backtest daily door
   failed **8** tests (recorded as 7 here originally — the OOST row of a `Theory` was undercounted;
   corrected by verification), and the JSON assertion printed the exact failure this slice exists to prevent:
   `"dailyVar95":0` in the serialised payload. Reverted.

**MEASURED CORRECTION — the published VaR99 is `199.4423`, not `199.46`.** Both `design.md` (E1)
and `portfolio-monthly-var/spec.md` state it as "`sorted[38]` is `-199.46`, negated".
`sorted[38] == -199.46` is correct and is pinned by its own test. The **published** figure is not:
`Percentile` reads `rank = p * (N-1) = 0.01 * 3859 = 38.59`, which is fractional, so it
INTERPOLATES between `sorted[38] = -199.46` and `sorted[39] = -199.43`, giving `199.44229999999999988`.
The scenario's actual claim — VaR99 reports while VaR95 is withheld on the same run — is unaffected;
only the literal is wrong. **Every other measured anchor held exactly**: IST 3,860 / 164 / 318,
OOST 3,804 / 172 / 320, monthly IST 1,148 of M=3,831 -> `400.19`, OOST 1,203 of M=3,775 -> `378.62`,
and IST trimmed to 250 has exactly 5 negative days.

**Deviations from the design, all deliberate and all additive:**

- `BacktestNetSeries` carries **`TradeCount`**, which the design's *Interfaces* sketch omits.
  `SeriesDensityDto.TradeCount` is specified as bridge-sourced (0.1) and nothing else knows it.
- `BacktestPortfolioRiskDto` carries **`ObservationDays`**, which the sketch omits; task 2.17
  asserts it. It equals `Density.DenseDayCount` precisely because nothing is trimmed.
- **`SeriesDensityDto` is composed in the calculator adapter, not "at the read-service boundary"**
  as D4/0.1 states. The risk DTO *contains* the density and the adapter *builds* the risk DTO, so
  the read service never sees a `SeriesDensity` at all. The design's stated location is not
  reachable; its stated *reason* (one place holding both origins) is satisfied here.
- `GroupSegment` **throws** on a mixed-segment group. PR3 owns the user-facing refusal naming the
  members; this is the D1-class backstop so the adapter can never silently label a figure with one
  member's segment.
- `ByService` is ordered by service name, not by `Var95` descending as the shipped path does — a
  nullable figure cannot rank, and determinism is a requirement of this capability.
- `AverageCorrelation` averages the already-rounded `decimal` cells; the live core averages the
  raw `double`s. Different payload, no shipped number moved.
- One line of `PortfolioAnalyticsCalculatorLiveOutputRegressionTests.cs` changed: `ComputeCorrelation([])`
  became `ComputeCorrelation(Array.Empty<PortfolioMemberInput>())`. **Adding the second typed
  overload makes an EMPTY collection expression ambiguous (CS0121)** — a source-compatibility
  consequence worth recording. No pinned figure was touched.

**2.15 could not be implemented literally.** "Zero out all but 191 of IST's negative window sums"
is not reachable from outside the calculator: window sums are private and the adapter takes trades.
The implemented equivalent is IST's own window count (M = 3,831) with its negative mass built to
exactly 191 and 192 — one either side of the threshold — via a tail-loss construction, asserting
the verdict flips while the count stays disclosed.

**PR1's verification suggestion is discharged**: `ComputeVaR_MonthlyGateBoundary_Synthetic` is a
monthly-LABELLED boundary pair (M = 91, 4 withholds / 5 reports), removing the shared-code-path
inferential step.

## Phase 3 (PR 3): Run selection, endpoint, UI

- [ ] 3.1 RED: a request whose segment field is omitted (null) is refused; no figure produced.
- [ ] 3.2 RED: an explicit request for `BacktestSegment.Unknown` is refused.
- [ ] 3.3 RED: run-segment derivation — a run with no trades (`Min` is null) yields no segment and no evidence for that run, never coerced to `Unknown` (fatality per 0.2); a run whose trades disagree (`Min != Max`) is **refused**, naming the run.
- [ ] 3.4 RED: a run whose trades are genuinely `Unknown` is never selected for any requested segment.
- [ ] 3.5 RED: a member with no run carrying the requested segment yields the explicit *no evidence for this segment* state — no series, not an empty one.
- [ ] 3.6 RED: **the anti-shortcut row** — a strategy whose `Deploy` run is `InSampleTest` and whose `Evaluation` run is `OutOfSample`, with `InSampleTest` requested, selects the `Deploy` run. `Kind` never infers or overrides `Segment`.
- [ ] 3.7 RED: **both** runs carrying the requested segment ⇒ member refused naming the strategy and both `Kind`s; an optional `BacktestRunKind` disambiguates.
- [ ] 3.8 RED: a group whose members' selected runs disagree on `Segment` is refused, naming the disagreeing members and their segments; no partial figure.
- [ ] 3.9 GREEN: one server-side projection over `runs.Where(r => strategyIds.Contains(r.StrategyId))` selecting `Id`, `StrategyId`, `Kind`, `Min`/`Max` of `(int?)t.Segment` — one query for the whole group, the `ReadinessRows` precedent. No date comparison.
- [ ] 3.10 GREEN: `Application/DTOs/Backtests/GroupRiskAnalysisRequest.cs` — `strategyIds[]`, `targetRiskPerTrade`, grid fields, **`BacktestSegment?`**, optional `BacktestRunKind`, funding service.
- [ ] 3.11 GREEN: `IBacktestReadService.GetGroupRiskAnalysisAsync(...)` + `BacktestReadService` impl: select run → `TryNormalize` → `Resize` → `Bridge` → analytics. First production caller of slice 2a (Note C).
- [ ] 3.12 GREEN: `BacktestsController` read endpoint `GET api/backtests/portfolio-risk`; `NonUnitWeight` → **422** naming the member; not-specified, `Unknown`, disagreeing-run, ambiguous-run, heterogeneous-group and no-evidence each map to their own distinct status.
- [ ] 3.13 RED+GREEN: Vitest — withheld VaR renders its state label and never `0`; density counts, `ExcludedUnscalableCount`, segment label, denominator label, approximation disclaimer and "simulated closes" always present; each refusal state renders its own message. Then the panel in `web/features/sqx/...` + `assets/i18n/{en,es}.json`.

## Phase 4: Tripwires and closeout

- [ ] 4.1 Determinism test: repeated calls on unchanged inputs return byte-identical payloads.
- [ ] 4.2 Grep test: no `Random`/seed in the slice (tripwire 1); no iteration over candidate groups (tripwire 2).
- [ ] 4.3 Grep test: **no** `CloseTime >=` and **no** `OosWindow` reference anywhere in the slice — absence by construction (D8).
- [ ] 4.4 `dotnet format`, `pnpm format`, full backend + web suites once.

## Notes

- **A — 1.1 cannot be RED-first.** It backfills shipped behaviour. Follow slice 2a's precedent: temporarily inject a defect (flip `AlignmentMode` on the live adapter, or pass `RequireSupport` on the live path) and confirm 1.1 and 1.7 fail, then revert. Record it in the PR.
- **B — neither fixture exercises either gate's second branch.** Monthly reports on both; daily withholds on both. 2.14 and 2.15 are the only tests that speak to the unexercised branches.
- **C — slice 2a has no production caller today.** `TradeResizer.Resize` and `TradeRiskNormalizer.TryNormalize` are referenced only by tests. 3.11 builds the wiring from nothing.
- **D — three scenarios, one behaviour.** "Weight `1.5` is refused" is asserted by the bridge's non-unit-weight scenario, the bridge's throw-vs-status scenario, and `trade-risk-normalization`'s pointer scenario. That is deliberate cross-pinning, not drift: write ONE test asserting both the refusal content and the non-throw mechanism, and reference it from all three.
- **Requirement coverage.** PR1: analytics R1's bit-identical half + monthly-var's real-account-unchanged scenario. PR2A: all four bridge requirements + the `trade-risk-normalization` pointer. PR2B: analytics R1–R2, R8–R9, monthly-var's gate scenarios. PR3: analytics R3–R7, R11. Phase 4: analytics R10.
- **Threat matrix**: N/A per design — no routing, shell, subprocess or VCS boundary.
