# Verify Report - backtest-portfolio-risk-analysis - PR2 (Phase 2A + 2B, commit 4c211e8)

**Scope**: PR2 of three (bridge + analytics adapters only). PR1 (Phase 1, private cores) shipped at
`55bc2a2` and passed independent verification at `2d90f4f` (summary carried forward below). PR3
(Phase 3: run selection, endpoint, UI) and Phase 4 (tripwires) **do not exist yet** - correctly
absent, not evaluated as gaps. Every scenario naming a segment refusal, run-selection outcome, or
the simulated-closes UI qualifier legitimately awaits PR3 (see "Scenarios awaiting PR3" below).

## PR1 carried-forward verdict (unchanged, not re-litigated)

PASS. 448/448 backend (baseline 419/419), `-warnaserror` clean, three injected defects reproduced
and reverted verbatim. Full detail in git history of this file / commit `2d90f4f`.

## Completeness - Phase 2A + 2B tasks (24/24 checked in tasks.md)

All 9 Phase 2A boxes and all 15 Phase 2B boxes are ticked. Phase 0 (0.1-0.3), Phase 3, Phase 4
remain unticked, matching the tasks doc's own scope note - not this PR's rows.

## Measured, independently (not taken from the apply report)

| Check | Command | Result |
|---|---|---|
| Backend suite | dotnet test AppTradingAlgoritmico.slnx -c Release | 489/489 passed (16s clean run). Matches reported baseline 448/448 -> 489/489 (+41: 12 bridge + 29 adapter). |
| Build | dotnet build AppTradingAlgoritmico.slnx -warnaserror | 0 Warning(s), 0 Error(s) |
| Format | dotnet format ./AppTradingAlgoritmico.slnx --verify-no-changes --no-restore | Clean (exit 0, no output) |
| Tree state | git status --porcelain | Empty before and after my own two injected-defect runs |
| Changed lines (backend only) | git diff --stat 2d90f4f 4c211e8 -- app.trading.algoritmico.api | 1,708 insertions + 1 deletion = 1,709, exactly matching the claimed figure. Production = 774 across 9 files; tests = 935 across 3 files. |
| Web files touched | git diff --stat 2d90f4f 4c211e8 -- app.trading.algoritmico.web | 3 files, 3 insertions/3 deletions - package.json (0.24.1 to 0.25.0), environment.ts, environment.development.ts (same version-string bump). See WARNING 1 below. |

## Re-proven injected defects (re-run myself, not accepted from the report)

Both defects were introduced with sed, run, observed, then reverted with git checkout --,
followed by a full green re-run and an empty git status --porcelain.

**1. AlignmentMode.Intersection on the live correlation adapter (line 422)**

Filtered run: ComputeCorrelation_LivePath_StillAlignsOnTheUnionWhereTheBacktestPathWithholds and
ComputeCorrelation_UnionAlignment_PinsCoefficientAndAverage (PR1's test) both failed, verbatim:

Expected PortfolioAnalyticsCalculator.ComputeCorrelation(live).Matrix[0][1] to be 0.5000M because
the live path aligns on the UNION and a non-trading day contributes 0, but found 0M
(difference of -0.5000).

Expected correlation.Matrix[0] to be equal to {1M, 0.8182M}, but {1M, 1M} differs at index 1.

Matches the apply report's claimed output verbatim, both files. Reverted; confirmed by git diff
showing only that one line changed, then restored.

**2. PercentilePolicy.Unconditional on the backtest daily door (line 533)**

Full-suite run: 8 failures, not the 7 the apply record states (Failed: 8, Passed: 481, Total:
489). Distinct failing tests: both data rows of ComputeVaR_ANonZeroDayShareGateWouldPublishAFigureMeasuredToBeExactlyZero
(IST and OOST), ComputeVaR_DailyGateBoundary_Synthetic(negativeDays: 5),
ComputeVaR_NoMembers_WithholdsEveryFigureWithNoSeriesRatherThanPublishingZeros,
ComputeVaR_OostPopulation_WithholdsDailyVar95,
ComputeVaR_IstFixture_WithholdsDailyVar95WhileReportingVar99OnTheSameRun, and
BacktestPortfolioRiskDto_EveryWithheldFigure_SerialisesAsJsonNullNeverZero. The last one prints
the literal failure mode this slice exists to prevent, confirmed verbatim (dailyVar95 appears as
the numeral 0 in the serialised JSON payload, not as null).

Reverted; confirmed by git diff showing only that one line changed, then restored; final full
suite 489/489, git status --porcelain empty.

See WARNING 2 below for the count discrepancy (8 vs the stated 7) - the JSON literal itself is
correct, only the tally is off by one.

## D9's split - verified in source, not accepted from the report

Read BacktestNetSeries.cs directly.

- Fact (1) ResizedTradeSeries not assignable to PortfolioMemberInput.Trades - INTACT, reasserted by
  PortfolioMemberInput_StillCannotBindAnAlreadySizedSeries (reflection over the sole constructor's
  Trades parameter type).
- Fact (2) ResizedTrade has no cost fields - INTACT, unchanged from slice 2a.
- Fact (3), refusal - CONFIRMED STRUCTURAL: BacktestNetSeries is sealed, its only constructor is
  private, and the only path to an instance is the nested Bridge.Build/TryBuild, which take a
  required (non-optional, non-default) decimal memberWeight parameter and check memberWeight != 1
  before any net is computed. Reflection test BacktestNetSeries_HasNoPublicConstructorNoScalingMemberAndNoDensity
  asserts zero public constructors, no Scale/Weight/Multiply-named member, and no Density member.
- Immunity-degrades-to-convention claim CONFIRMED: Nets is IReadOnlyList of DatedNet where
  DatedNet.Net is a bare decimal. Multiplying a weight into series.Nets[i].Net compiles - there is
  no type-level barrier to a caller re-scaling a BacktestNetSeries after the fact. I looked for a
  bypass myself and found none of the four named risks: no second public constructor, no public
  setter (all properties are get-only), no with expression (this is a class, not a record, so C#
  does not synthesize one), and no JsonConstructor or deserialization attribute - the type is only
  ever produced by the calculator's outputs, never bound from request JSON. The two stated
  mitigations (sealed-type-only adapters; the reflection test) are real and are the only defense -
  not a type-system guarantee, exactly as claimed. Not a CRITICAL finding: the claim was that
  immunity is convention, not structural, and that is exactly what I found - no unclaimed stronger
  guarantee, no unclaimed bypass either.

## The pairing test - hand-built, not fixture-driven

Read Build_HandBuiltNonContiguousSubset_PairsByLookupAndDoesNotRefuseTheDifferingCount directly.
Confirmed: the source list is HandBuiltSource() plus two manually appended Trade(...) calls
(rowIndex 3, rowIndex 4), and the resized series is built as a with-expression overriding Trades to
two rows (index 0 and index 3) - a non-contiguous, strict two-element subset of a five-element
source, assembled literally in the test body. There is no fixture file or RawTradeListFixture.Load
call anywhere in this test. The label (DEFENSIVE GUARD, HAND-CONSTRUCTED) travels with the test in
an XML doc comment stating exactly why a fixture-driven version would be meaningless (every real
ResizedTradeSeries has Trades.Count equal to source.Count, so a fixture would pass unchanged under
the rejected positional zip). Confirmed as claimed.

## Unscalable rows - excluded and counted, never zeroed

Build_UnscalableRows_ContributeNoNetAtAllAndAreCounted asserts the Nets collection does not
contain a zero net, alongside ExcludedUnscalableCount equal to 1 and the reconciliation
TradeCount minus ExcludedUnscalableCount equal to Nets.Count. Source (Bridge.Build) skips the
Unscalable row with a bare continue, never adding a DatedNet with a zero net. Confirmed as
claimed, not a breakeven-trade conflation.

## Live correlation path - bit-identical, Union preserved

PortfolioAnalyticsCalculator.cs line 422 (live ComputeCorrelation over PortfolioMemberInput) passes
AlignmentMode.Union; the backtest adapter at line 604 passes AlignmentMode.Intersection. Confirmed
by source read and by the injected-defect re-proof above, which broke exactly the live test when
Union was flipped. No shared code path silently changed both.

## No shipped DTO touched

BacktestPortfolioRiskDto, BacktestCorrelationDto, BacktestServiceRiskDto, SeriesDensityDto,
BacktestNetSeriesResult are all new files under Application/DTOs/Backtests/. Confirmed
PortfolioRiskDto's Var95/Var99 (the shipped real-account DTO) are untouched non-nullable decimal -
read directly, not inferred from the doc comment claiming it.

## The VaR99 correction - resolved in the artifacts, not merely flagged

The apply record states the artifact defect (spec/design said 199.46, published figure is
199.4423) was left unedited in the spec deliberately, flagged but not silently resolved. Reading
the current specs/portfolio-monthly-var/spec.md and design.md shows this is no longer true: the
spec's VaR99-reports-while-VaR95-is-withheld scenario now states the published figure as 199.4423
directly, with an inline correction note explaining the rank equals 38.59 interpolation and
explicitly retracting the earlier sorted-index-38-negated wording. design.md's current text
contains no 199.46 reference at all. No artifact still asserts 199.46 as a published value.
sorted[38] equal to -199.46 remains correctly pinned by its own, separate test
(IstFixture_TheVar99ReadIndexHoldsTheMeasuredNegativeValue) and is not confused with the published
figure anywhere I read. This appears to have been corrected in a spec revision after the apply
report was written; it is good news, not a residual gap.

## Single-derivation scope - confirmed as four-gating-counts-only

ComputeVaR_ReportedGatingCounts_AreTheCountsThatProducedTheVerdicts asserts only the four gating
counts (DenseDayCount, NegativeDayCount, NonZeroDayCount, NegativeWindowCount) against re-derived
support predicates. TradeCount and ExcludedUnscalableCount get their own, separate reconciliation
test (ComputeVaR_TradeLevelCounts_ReconcileAgainstTheSeriesTheyDescribe), exactly as
SeriesDensityDto's doc comment specifies. Confirmed: no test extends the single-derivation
assertion to the two bridge-sourced counts.

## No band position derived

ComputeVaR_MonthlyVar95Percent_IsOnlyTheShippedCapitalBasisAndNeverABandPosition asserts
MonthlyVar95Percent equals MonthlyVar95 divided by InitialCapital and reflects over
BacktestPortfolioRiskDto's properties to assert none contains Band or TargetVar in its name.
VarTarget is populated as null by the calculator (the read service's job, not built yet - correctly
deferred to PR3). Confirmed.

## Spec compliance matrix (PR2 scope only)

| Requirement (capability) | Scenarios | Backing test(s) | Verdict |
|---|---|---|---|
| Bridge pairs by RowIndex lookup (bridge) | 2/2 | Build_AtTheRunsOwnEstimate..., Build_HandBuiltNonContiguousSubset... | PASS |
| Pairing failure throws, not a status (bridge) | 3/3 | Build_ResizedRowWithNoSourceMatch..., Build_DuplicatedSourceRowIndex..., weight-refusal Theory | PASS |
| Non-unit weight refused (bridge) | 3/3 | Build_NonUnitWeight_... (Theory: 1.5/0.5/0), Build_UnitWeight_Converts... | PASS |
| Unscalable rows excluded, counted (bridge) | 1/1 | Build_UnscalableRows_ContributeNoNetAtAllAndAreCounted | PASS |
| Typed adapters, bit-identical live path (analytics R1) | 3/3 | ComputeCorrelation_LivePath_StillAlignsOnTheUnion..., Calculator_ExposesNoPublicOverloadOverAnUntypedDatedNetTuple, PR1's live regression suite | PASS |
| No 250-day trim (analytics R2) | 1/1 | ComputeVaR_BacktestAdapter_PassesNoWindowTrimAtAll | PASS |
| Density metrics accompany every figure (analytics R8) | 1/1 | ComputeVaR_IstFixture_WithholdsDailyVar95... (density assertions) | PASS |
| Intersection alignment plus co-activity (analytics R9) | 3/3 | ComputeCorrelation_DisjointTradingDays_..., ComputeCorrelation_ValidIntersection_..., ComputeCorrelation_LivePath_StillAlignsOnTheUnion... | PASS |
| Density gate scenarios (portfolio-monthly-var delta) | 6/6 of 7 (7th is PR1's real-account-unchanged scenario) | ComputeVaR_IstFixture_WithholdsDailyVar95..., ComputeVaR_OostPopulation_WithholdsDailyVar95, ComputeVaR_ANonZeroDayShareGate..., ComputeVaR_IstFixture_ReportsTheMonthlyVar95Figure, ComputeVaR_OostPopulation_ReportsTheMonthlyVar95Figure | PASS |
| Weight-refusal pointer (trade-risk-normalization delta) | 1/1 | Same Theory as bridge's non-unit-weight scenario (Note D cross-pin) | PASS |

PR2 scenario total: every requirement in PR2's scope (bridge's 4/9, analytics R1/R2/R8/R9, and 6 of
monthly-var's 7 gate scenarios, plus the trade-risk-normalization pointer) has a passing,
independently re-run, runtime-executed covering test. No UNTESTED, no FAILING.

## Scenarios legitimately awaiting PR3 (not evaluated as gaps)

- Segment-as-metadata requirement (analytics R3) - both scenarios.
- Required segment or no-evidence state (analytics R4) - both scenarios.
- Unknown not selectable (analytics R5) - both scenarios.
- Bounded two-row run selection, including the anti-shortcut and both-runs-match refusals, and the
  trade-less-run-is-non-fatal clause (analytics R6) - all 5 scenarios.
- Group segment disagreement refusal (analytics R7) - 1 scenario (the calculator's GroupSegment
  throw is the backstop; the user-facing named refusal is PR3's read-service or controller job).
- Simulated-closes qualifier in the UI (analytics R11) - 1 scenario.
- Determinism and no-ranking tripwires (analytics R10) - Phase 4, not PR2 or PR3.

## Deviations from design - judged

| Deviation | Judgment |
|---|---|
| SeriesDensityDto composed in the calculator adapter, not at the read-service boundary (D4/0.1) | SOUND. Verified in source: BacktestDenseSeries (private, in the calculator) builds the SeriesDensityDto and both ComputeVaR/ComputeCorrelation embed it directly in their returned DTOs; the read service (PR3, not yet written) would only ever see the already-composed risk or correlation DTO, never a bare SeriesDensity. The design's stated location is genuinely unreachable given this structure. The stated reason - one place holding both the day-level (Measure) and trade-level (bridge) counts - is satisfied by the adapter instead. Accept as a legitimate, well-reasoned relocation. |
| ByService ordered by name, not Var95 descending | SOUND. A nullable decimal cannot support a total order without an arbitrary null-placement rule, and determinism is an explicit requirement (analytics R10, this slice). Ordinal name ordering is deterministic and does not depend on any withheld figure. |
| Task 2.15 reinterpreted (window count built to exactly 191/192 rather than zeroing out sums) | SOUND. Verified ComputeMonthlyVar's window sums are computed in a private helper never exposed to a caller; the adapter's public surface takes trades, not sums. The implemented TailNegativeSeries helper is a faithful construction that hits the identical boundary (negativeWindows at 191 vs 192, M equal to 3,831) the task asked for, verified by the test asserting the verdict flips exactly there. |
| BacktestNetSeries.TradeCount and BacktestPortfolioRiskDto.ObservationDays added beyond the design's interface sketch | SOUND, additive. Both are read directly by tests and by SeriesDensityDto's own stated provenance (0.1). No existing consumer is affected since these are new types. |
| GroupSegment throws on a mixed-segment group | SOUND backstop. Verified: GroupSegment throws an ArgumentException if the members' distinct Segment values number more than one. This is unreachable from PR2's own adapter callers (single-segment fixtures only) and exists purely so a future wiring bug in PR3's group construction cannot silently label a heterogeneous group with one member's segment. PR3 still owns the named, non-throwing user-facing refusal (analytics R7). |
| ComputeCorrelation with an empty collection literal required disambiguation to Array.Empty of PortfolioMemberInput due to a compiler ambiguity error | ACCEPTABLE, worth recording as a SUGGESTION, not a defect. Verified the compiler forces this: adding the second typed overload over BacktestNetSeries array makes an empty collection expression ambiguous between the two IReadOnlyList overloads. This is a real source-compatibility cost of the two-typed-doors design, but it is a compile-time-only, one-call-site cost with zero runtime behavior change (confirmed: no pinned figure in that test moved). Grep confirms no other call site in the codebase passes an empty collection literal to ComputeCorrelation today, so the blast radius is exactly the one line changed. |

## WARNING-level findings

WARNING 1 - "PR2 changes zero web files" is imprecise. The tasks.md PR2 apply record and the task
instructions both state PR2 changes zero web files, used to justify not re-running the frontend
suite. The diff between commits 2d90f4f and 4c211e8 restricted to the web project shows 3 files
changed (package.json, environment.ts, environment.development.ts), all the same version-string
bump from 0.24.1 to 0.25.0, 3 insertions and 3 deletions total. This is almost certainly an
automated version-bump unrelated to PR2's feature work (no logic file touched), so not re-running
pnpm test remains the right call - but the literal claim is false as written. Recommend the next
edit to tasks.md or apply-progress say "no functional web file changed" rather than "zero web
files."

WARNING 2 - injected-defect number 2's failure count is 8, not 7. The apply record and this task's
own instructions state 7 failures for PercentilePolicy.Unconditional on the backtest daily door. My
own re-run measured Failed: 8, Passed: 481, Total: 489. The extra failure is the second data row
(OOST) of the Theory test ComputeVaR_ANonZeroDayShareGateWouldPublishAFigureMeasuredToBeExactlyZero
- both its IstFileName and OostFileName rows fail under the injected defect, and the apply record
appears to have counted only one. The load-bearing claim (the JSON literally prints the figure as
the numeral zero rather than null) is still verified verbatim, and the defect was still fully
reverted with a clean 489/489 afterward - this is a tally error in the record, not a gap in test
coverage or a wrong revert.

## SUGGESTION-level findings

- None beyond the ComputeCorrelation empty-collection disambiguation note captured in the
  deviations table above.

## Issues summary

- CRITICAL: 0
- WARNING: 2 (both documentation and tally precision issues in the apply record; neither affects
  shipped behavior, both independently re-verified against source and test output)
- SUGGESTION: 1 (the compiler-forced disambiguation cost, judged acceptable)

## Overall verdict for PR2: PASS WITH WARNINGS

Both warnings are about the accuracy of the apply record's prose (an off-by-one test tally; an
imprecise "zero web files" claim covering a non-functional version bump), not about the shipped
code, the specs, or test coverage. Every requirement and scenario in PR2's scope (bridge's 4/9,
analytics R1/R2/R8/R9, and 6 of monthly-var's 7 gate scenarios) has a passing, independently
re-run, runtime-executed covering test. Both injected defects reproduced their claimed failure
signatures (one verbatim including the exact assertion text; the other verbatim on its most
important single assertion, off by one on the aggregate count) and were fully reverted, restoring
489/489 and a clean tree. The D9 structural/convention split, the hand-built defensive pairing
test, the unscalable-row accounting, the live-path Union preservation, the no-shipped-DTO-touched
claim, the single-derivation scope, and the no-band-position claim were all independently confirmed
in source rather than accepted from the artifacts. The one previously-flagged artifact defect (the
199.46 vs 199.4423 VaR99 figure) has since been corrected in both design.md and
portfolio-monthly-var/spec.md - no artifact asserts the wrong figure today. PR3 (run selection,
endpoint, UI) and Phase 4 (tripwires) remain correctly unimplemented and are not evaluated as gaps.

Recommendation: proceed to PR3. Before archiving the whole change, correct the two WARNING-level
prose inaccuracies in tasks.md's PR2 apply record (the failure count and the "zero web files"
wording) so the permanent record matches what was actually measured.
