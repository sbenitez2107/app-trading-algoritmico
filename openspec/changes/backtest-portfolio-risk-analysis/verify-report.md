# Verify Report — backtest-portfolio-risk-analysis — FULL CHANGE (PR1 + PR2 + PR3)

Scope: this report supersedes the PR2-scoped report and covers the whole three-PR chain, now
complete. Chain: `0e02731` planning -> `55bc2a2` PR1 (v0.24.0) -> `2d90f4f` PR1 verify PASS ->
`4c211e8` PR2 (v0.25.0) -> `e66264a` PR2 verify PASS WITH WARNINGS -> `e516635` PR3 (v0.26.0, HEAD).

Overall verdict: PASS. CRITICAL: 0. WARNING: 0. SUGGESTION: 1.

The two PR2 WARNINGs (the "zero web files" prose inaccuracy and the 7-vs-8 injected-failure count)
are documentation-only, already corrected in tasks.md own PR2 apply record, and are not repeated
here as open items - carried-forward context only.

## Independently measured at e516635 (HEAD)

| Check | Command | Result |
|---|---|---|
| Backend suite | dotnet test AppTradingAlgoritmico.slnx -c Release | 543/543, 0 failed, 22s |
| Backend build | dotnet build -warnaserror -p:BaseOutputPath=scratch/ | 0 Warning(s), 0 Error(s) |
| Backend format | dotnet format --verify-no-changes | clean (no output, exit clean) |
| Frontend suite (clean run) | pnpm test | 380/380, 31/31 files, 0 failed |
| Git tree | git status --porcelain | clean, matches HEAD |
| Live-path regression | dotnet test --filter FullyQualifiedName~LiveOutputRegression | 7/7 pass, live correlation/VaR still bit-identical after PR3 |

## Injection re-proof (claim 1 - the whole reason the slice exists)

Replaced, in group-risk-panel.component.html, the withheld-branch template:

    @if (risk.dailyVar95 !== null) { {{ risk.dailyVar95 | number: '1.2-2' }} }
    @else { withheld reason span }

with:

    {{ (risk.dailyVar95 ?? 0) | number: '1.2-2' }}

Ran pnpm test (full suite - the "--" filter syntax fails schema validation on this Angular CLI
version: pnpm test -- group-risk errors "Data path must NOT have additional properties"). Result:

    FAIL  group-risk-panel.component.spec.ts > withheldDailyVar95_RendersItsStateLabelAndNeverAZero
    AssertionError: expected ' 0.00 ' to contain 'SQX.BACKTESTS.GROUP_RISK.WITHHELD_INS...'
    Test Files  1 failed | 30 passed (31)
         Tests  1 failed | 379 passed (380)

Exactly the failure claimed. Reverted from a scratchpad backup; git status --porcelain empty
afterward - reversion proven. Confirmed independently in source: the template has no "?? 0"
anywhere else, dailyVar95 is typed number-or-null in backtest.service.ts (three DTO copies, lines
~197/206/224), and the spec assertion is expect(cell.textContent).not.toMatch(/\d/) - a withheld
cell may carry no digit at all, so 0, 0.00, or a padded dash all fail it.

## Claim-by-claim verification

1. Withheld VaR cannot render as 0. CONFIRMED above, by injection and by source inspection.
2. D9's guarantee (structural refusal / conventional immunity). Read BacktestNetSeries.cs in full:
   private constructor, sealed class, every property is a get-only auto-property (no init, so no
   object-initializer bypass), the only factory is the nested public static class Bridge. No second
   constructor, no public setter, no "with" expression is possible (it is a class, not a record), no
   JsonConstructor or deserialization attribute exists anywhere on the type. No bypass found.
   "w * series.Nets[i].Net" does compile (Nets is a list of bare decimals), confirming immunity is
   convention, not structural, exactly as D3 states.
3. Run selection, D8a's five outcomes. Read RunSegmentSelection.Select and
   BacktestRunSegmentRow.State: disagreeing runs are checked and refused FIRST (before any segment
   match); trade-less (NoTrades) runs are excluded from candidates without refusing the member;
   Unknown-segment runs are dropped from candidates unconditionally; exactly one match resolves;
   both matches refuse (AmbiguousRunSelection) unless runKind narrows to one.
   BacktestRunSelectionTests.cs line 95 (Select_WithOneTradelessRunAndOneMatchingRun_...) and lines
   115/131 (Select_DeployRunIsInSampleTestAndEvaluationRunIsOutOfSample_Picks...ForInSampleTest /
   ...ForOutOfSample) are present and pass, pinning both the non-fatal trade-less case and the
   anti-shortcut Deploy+InSampleTest / Evaluation+OutOfSample pair. Kind is read nowhere in the
   selection path except the caller-supplied runKind disambiguator - confirmed by inspection.
4. Unknown refused, twice, distinctly. BacktestReadService.GetGroupRiskAnalysisAsync:
   request.Segment is null yields SegmentNotSpecified; request.Segment == BacktestSegment.Unknown
   yields UnknownSegmentNotSelectable - two separate if blocks, two separate
   GroupRiskAnalysisStatus values. GroupRiskAnalysisRequest.Segment is typed BacktestSegment?,
   confirmed in the DTO source. Both rules independently tested in
   BacktestGroupRiskAnalysisTests.cs lines 106 and 124.
5. No band position. ComputeVaR's MonthlyVar95Percent is monthly.monthlyVar95Percent, computed as
   "initialCapital > 0 ? monthlyVar95 / initialCapital : 0m" (line 870) - no other input.
   VarTarget: null is hard-coded on the backtest DTO (line 565) with a comment stating the adapter
   never derives a band position (D4c); the read service is the only place that could populate it
   and does so only from the shipped BrokerRiskLimits comparison, not from this slice's own
   arithmetic.
6. The two tripwires. BacktestPortfolioRiskTripwireTests.cs:
   Tripwire_NoSliceFileUsesARandomNumberGenerator, Tripwire_NoSliceFileTakesOrSetsASeed,
   Tripwire_TheReadSurfaceEvaluatesExactlyOneGroup, Tripwire_NoAnalysisTypeExposesACollectionOfAnalyses
   - all pass (part of the measured 543/543). Grep is over comment-stripped executable text,
   confirmed by reading StripComments; this is why RunSegmentSelection's doc-comment citation of
   OosWindow.Resolver does not trip the OosWindow absence tripwire while the executable code still
   contains no such reference.
7. Live path bit-identical after all three PRs. Re-ran the regression suite independently (7/7
   pass) rather than assuming PR1/PR2's checks still hold. ComputeCorrelation's live adapter still
   passes AlignmentMode.Union (confirmed unchanged at the live call site); the backtest adapter
   uses Intersection, confirmed by ComputeCorrelation_LivePath_StillAlignsOnTheUnionWhereTheBacktestPathWithholds.
8. Published figures. No test or artifact asserts 199.46 as the published VaR99. DailyVar99 is
   pinned at 199.44229999999999988m in both BacktestPortfolioAnalyticsAdapterTests.cs line 54 and
   BacktestGroupRiskAnalysisTests.cs line 319; the JSON serialization assertion at
   BacktestPortfolioAnalyticsAdapterTests.cs line 417 pins the raw numeral
   "dailyVar99":199.44229999999999988 (not a string), confirming it is genuinely non-withheld.
   MonthlyVar95 is pinned at 400.19m (IST) and 378.62m (OOST) in both test files - positive loss
   magnitudes, matching the sign-convention note added to portfolio-monthly-var/spec.md.
   sorted[38] is separately asserted as -199.46m at line 131 - a correctly-labelled corroborating
   assertion about the raw sorted array, not the published figure - it does not contradict the
   corrected published value.

## The three disclosed gaps - judged

- R7 (heterogeneous-group refusal) unreachable through the endpoint. Confirmed by reading
  GetGroupRiskAnalysisAsync: selection matches the requested segment EXACTLY, so every selected
  run necessarily carries that one segment - the group is homogeneous by construction, and no
  request can produce two different resolved segments among members.
  BacktestGroupRiskAnalysisTests.cs line 355 honestly calls
  BacktestReadService.DescribeSegmentDisagreement directly rather than through a request, with an
  inline comment stating why. Judgment: adequate, not a defect. R7 as specified now describes a
  public, unit-tested, user-facing sentence function with a calculator throw (GroupSegment) as a
  structural backstop for any future caller that can construct a heterogeneous group (e.g. a
  saved-portfolio path that mixes runs). The spec scenario is unreachable today but the guarantee
  it protects (no figure with a mixed label) is real and enforced at two layers. Worth a note in
  the spec's own text at next edit/archive that the scenario is presently exercised only at the
  unit level - not a CRITICAL, since nothing published can currently violate it.
- InitialCapital and PortfolioId added to the request. Confirmed necessary: InitialCapital is
  consumed directly as ComputeVaR's percentage denominator (no other source exists for a bare
  group); PortfolioId is optional and, when absent, every member's weight defaults to 1
  (MemberWeightsAsync returns an empty map, and the read-service call site does
  "weights.TryGetValue(strategyId, out var w) ? w : 1m"), so the D3 NonUnitWeight 422 path is
  reachable when a portfolio is supplied and dead code otherwise - an honest, disclosed trade-off,
  not a silent scope change. Both fields are documented in the DTO's own XML doc comments.
  Judgment: sound, additive, correctly disclosed.
- A refused member refuses the whole analysis. Confirmed in code: the group-level refusal path
  takes the first member whose status is not Resolved and maps its status to the group-level
  status; member rows are still all returned in the payload (every member's GroupRiskMemberDto is
  built in the per-member loop before the disagreement/refusal check runs). The reasoning recorded
  - computing over only the resolved members would silently answer a different question - is sound
  and matches this project's established stance (D4/D8b: a figure whose label would be false is
  not published). Judgment: sound, and the right side of the ambiguity the spec left open.

## Requirement x scenario compliance matrix (17 requirements / 40 scenarios)

### backtest-net-series-bridge (4 requirements / 9 scenarios)

| # | Requirement | Scenario | Backing test | Status |
|---|---|---|---|---|
| 1 | Pairs by RowIndex lookup | Full-sample pairing | bridge full-sample pairing test (PR2, part of 543) | PASS |
| 1 | Pairs by RowIndex lookup | Hand-built subset pairs correctly (defensive) | hand-constructed ResizedTradeSeries subset test, labelled defensive per task 2.5 | PASS |
| 2 | Pairing failure throws | Unmatched RowIndex throws | bridge unmatched-RowIndex test | PASS |
| 2 | Pairing failure throws | Duplicated source RowIndex throws | bridge duplicate-RowIndex test | PASS |
| 2 | Pairing failure throws | Weight refusal is not a throw | weight-refusal-vs-throw test | PASS |
| 3 | Non-unit weight refused | Non-unit weight refused, not applied | weight=1.5 refusal test | PASS |
| 3 | Non-unit weight refused | Unit weight converts | weight=1 conversion test | PASS |
| 3 | Non-unit weight refused | Zero weight is an error | weight=0 refusal test | PASS |
| 4 | Unscalable excluded, counted | Excluded from Nets, counted not zeroed | ExcludedUnscalableCount reconciliation test | PASS |

(Bridge test bodies were verified present and passing as part of the measured 543/543; PR2's own
verify pass already inspected them file/line, and PR3 does not touch this file, so they are not
re-enumerated by exact line here.)

### backtest-portfolio-analytics (11 requirements / 23 scenarios in the actual spec file - one
more than tasks.md's header count of "22"; the grand total across all four capabilities is still
40, matching the brief's figure, so the discrepancy is internal to the tasks.md header rounding,
not a miscount of the total.)

| # | Requirement | Scenario | Backing test | Status |
|---|---|---|---|---|
| 1 | Typed adapters, bit-identical | Shipped output unchanged | PortfolioAnalyticsCalculatorLiveOutputRegressionTests (7/7, re-run independently) | PASS |
| 1 | Typed adapters, bit-identical | Backtest series compute through typed adapter | BacktestPortfolioAnalyticsAdapterTests.ComputeVaR_* | PASS |
| 1 | Typed adapters, bit-identical | No public raw-tuple overload exists | Calculator_ExposesNoPublicOverloadOverAnUntypedDatedNetTuple | PASS |
| 2 | No 250-trim | Gate evaluated over full series | ComputeVaR_BacktestAdapter_PassesNoWindowTrimAtAll | PASS |
| 3 | Segment as metadata | Segment reported as metadata | ComputeVaR_CarriesTheRunSegmentAsMetadata | PASS |
| 3 | Segment as metadata | No date filtering occurs | tripwire 4.3 (CloseTime >= / OosWindow absence) | PASS |
| 4 | Segment required, nullable | No segment, no figures | GetGroupRiskAnalysis_WithNoSegmentSpecified_IsRefusedAndProducesNoFigure | PASS |
| 4 | Segment required, nullable | No run carries requested segment | GetGroupRiskAnalysis_WhenNoRunCarriesTheRequestedSegment_IsTheNoEvidenceState | PASS |
| 5 | Unknown not selectable | Request for Unknown refused | GetGroupRiskAnalysis_ForTheUnknownSegment_IsRefusedForADifferentReasonThanOmission | PASS |
| 5 | Unknown not selectable | Run genuinely Unknown never selected | GetGroupRiskAnalysis_WithARunLabelledUnknown_NeverSelectsItForAMeaningfulSegment | PASS |
| 6 | Bounded two-row selection | No-trades run yields no evidence, never Unknown | BacktestRunSelectionTests row-state tests | PASS |
| 6 | Bounded two-row selection | Disagreeing trades refuse the run | GetGroupRiskAnalysis_WithARunWhoseTradesDisagree_RefusesNamingTheRun | PASS |
| 6 | Bounded two-row selection | Kind never overrides segment match | Select_DeployRunIsInSampleTestAndEvaluationRunIsOutOfSample_PicksDeployForInSampleTest + GetGroupRiskAnalysis_DeployRunIsInSampleTest_SelectsItWithoutConsultingKind | PASS |
| 6 | Bounded two-row selection | Both runs matching refused | Select_WhenBothRunsCarryTheRequestedSegment_IsRefusedNamingBothKinds + GetGroupRiskAnalysis_WhenBothRunsCarryTheSegment_RefusesNamingTheStrategyAndBothKinds | PASS |
| 6 | Bounded two-row selection | Trade-less run does not fail the member | Select_WithOneTradelessRunAndOneMatchingRun_ResolvesFromTheMatchingRun + GetGroupRiskAnalysis_WithATradelessSecondRun_StillResolvesFromTheRunThatMatches | PASS |
| 7 | Group segment disagreement refused | Disagreeing segments refuse group | GetGroupRiskAnalysis_WhenMembersSelectedRunsDisagreeOnSegment_RefusesNamingThem (calls DescribeSegmentDisagreement directly - unreachable via the live endpoint, disclosed above) | PASS (unit-level only) |
| 8 | Density metrics accompany every figure | Density metrics reported, IST fixture | ComputeVaR_ReportedGatingCounts_AreTheCountsThatProducedTheVerdicts + ComputeVaR_TradeLevelCounts_ReconcileAgainstTheSeriesTheyDescribe | PASS |
| 9 | Correlation intersection, co-activity | Too few co-active days withholds cell | ComputeCorrelation_FewerThanTwoCoActiveDays_WithholdsTheCell | PASS |
| 9 | Correlation intersection, co-activity | Valid intersection reports, no caveat | ComputeCorrelation_ValidIntersection_ReportsTheCellWithItsCoActivity | PASS |
| 9 | Correlation intersection, co-activity | Live path keeps union, unchanged | ComputeCorrelation_LivePath_StillAlignsOnTheUnionWhereTheBacktestPathWithholds | PASS |
| 10 | Wholly deterministic | Repeated calls byte-identical | GetGroupRiskAnalysis_CalledTwiceOnUnchangedInputs_ReturnsByteIdenticalPayloads | PASS |
| 10 | Wholly deterministic | One group, no ranking | Tripwire_TheReadSurfaceEvaluatesExactlyOneGroup + Tripwire_NoAnalysisTypeExposesACollectionOfAnalyses | PASS |
| 11 | Simulated-closes qualifier | Qualifier shown alongside disclaimers | completedAnalysis_AlwaysDisclosesDensitySegmentDenominatorAndBothQualifiers (frontend spec) | PASS |

### portfolio-monthly-var delta (1 requirement / 7 scenarios)

| Scenario | Backing test | Status |
|---|---|---|
| Daily VaR95 withheld - IST | ComputeVaR_IstFixture_WithholdsDailyVar95WhileReportingVar99OnTheSameRun | PASS |
| Daily VaR95 withheld - OOST | ComputeVaR_OostPopulation_WithholdsDailyVar95 | PASS |
| VaR99 reports while VaR95 withheld | ComputeVaR_IstFixture_WithholdsDailyVar95WhileReportingVar99OnTheSameRun + IstFixture_TheVar99ReadIndexHoldsTheMeasuredNegativeValue | PASS |
| Clearing non-zero-day threshold does not clear gate | ComputeVaR_ANonZeroDayShareGateWouldPublishAFigureMeasuredToBeExactlyZero | PASS |
| Monthly gate exists, does not fire - IST | ComputeVaR_IstFixture_ReportsTheMonthlyVar95Figure | PASS |
| Monthly gate exists, does not fire - OOST | ComputeVaR_OostPopulation_ReportsTheMonthlyVar95Figure | PASS |
| Real-account daily VaR unchanged | live-path regression suite (Unconditional policy, bit-identical) | PASS |

### trade-risk-normalization delta (1 requirement / 1 scenario)

| Scenario | Backing test | Status |
|---|---|---|
| Obligation discharged by the bridge capability | bridge's non-unit-weight scenarios (1.5/1/0), cross-pinned per design Note D | PASS |

Totals: 17/17 requirements have at least one passing backing test. 40/40 scenarios have a named
covering test that passed at runtime in this session's measured run (backend 543/543, frontend
380/380), except the R7 group-disagreement scenario, which is asserted only at the unit level
against a directly-called helper rather than through the HTTP endpoint - disclosed above as
adequate rather than a gap.

## What is unverifiable / out of this session's scope

- The apply-progress claim that AppDbContext reproduces PR2's anchors "end to end" under EF
  InMemory was corroborated by the presence and passing state of
  BacktestGroupRiskAnalysisTests.cs line 298 (GetGroupRiskAnalysis_OverTheIstFixture_...), part of
  the measured 543/543, but this report did not step through the EF InMemory query plan itself to
  independently confirm it matches the "fenced at one command for 1 and for 30 strategies"
  query-cost claim (task 3.9) beyond re-running BacktestRunSegmentQueryCostTests (included in
  543/543, not individually isolated in this session).
- Frontend suite flake rate: not independently re-measured beyond the two clean runs this session
  performed (one during injection-revert verification, one clean baseline), both 380/380. No flake
  was observed in this session's runs; the reported flake under load is accepted as documented
  context per the task brief and not re-verified further.
- Tasks 0.1 (SeriesDensity provenance decision) and 0.3 (pruning design.md's Spec Dependencies
  ledger) remain open checkboxes in tasks.md. Confirmed by reading tasks.md: 0.1 is functionally
  discharged by PR2's SeriesDensityDto mixed-provenance design (visible in SeriesDensityDto.cs's
  doc comments) but its checkbox is still open; 0.3 is pure documentation housekeeping. Neither
  blocks the runtime behavior verified above.

## Issues

CRITICAL: none.

WARNING: none. (PR2's two WARNINGs were prose-only and are already self-corrected in tasks.md's
own PR2 apply record text, visible in the artifact read for this report.)

SUGGESTION (1):
- Tasks 0.1 and 0.3 checkboxes remain open in tasks.md despite being functionally/documentation
  discharged. Recommend ticking 0.1 (with a one-line pointer to SeriesDensityDto's provenance doc
  comments) and performing the design.md Spec Dependencies prune (0.3) before or during archive,
  since archive is the natural point to close non-blocking documentation debt.

## Verdict

PASS. The complete three-PR chain (55bc2a2 -> 4c211e8 -> e516635) delivers all 17 requirements and
40 scenarios from the four capability/delta specs, backed by passing tests measured independently
in this session (backend 543/543, frontend 380/380, -warnaserror clean, dotnet format clean, git
tree clean). The central claim - a withheld VaR cannot render as 0 - was re-proven by live defect
injection and reversion. D9's structural-refusal/conventional-immunity split was independently
confirmed against source with no bypass found. The three disclosed apply-time gaps (R7
unreachability, the two added request fields, and the whole-analysis-refused policy) were each
judged sound on inspection. No artifact currently asserts an incorrect published figure. Ready for
review and archive.
