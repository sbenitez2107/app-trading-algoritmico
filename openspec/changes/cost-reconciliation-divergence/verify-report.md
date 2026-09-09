```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:re-run-2026-09-09T20-10
verdict: pass
blockers: 0
critical_findings: 0
requirements: 10/10
scenarios: 21/21
test_command: dotnet test -p:BaseOutputPath=$LOCALAPPDATA/tmp/sdd-verify-build/
test_exit_code: 0
test_output_hash: sha256:634a0c042b2c46f8bf3e3edb421399fbf1ac4cc0a81fe04b03c46a9754ef8ba2
build_command: dotnet build -warnaserror -p:BaseOutputPath=$LOCALAPPDATA/tmp/sdd-verify-build/
build_exit_code: 0
build_output_hash: sha256:93fe3ba2aacc101976ad52d8c2457a724b7163e55ca1f3cccdd47da1b133c2f7
```

## Verification Report

**Change**: cost-reconciliation-divergence (Slice A)
**Version**: N/A
**Mode**: Strict TDD

### Re-run context

This is a re-run after a correction transaction. The previous verify pass FAILED on one CRITICAL:
the spec requirement "The Trade-Set Difference Is Reported As A Set Relationship, Never As A Score"
requires each disjoint subset's net P/L reported separately, and no P/L field existed. Root cause
was a spec/design disagreement: design.md D10 had deferred subset P/L to slice B while the
approved proposal and the delta spec placed it in slice A. One correction transaction ran; a scoped
fix-delta validator returned approve. This report supersedes the prior (stale) FAIL report on disk.

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 27 |
| Tasks complete | 27 |
| Tasks incomplete | 0 |

Note: tasks.md was not re-numbered with new rows for the correction (D10 fix); the correction
was implemented and evidenced via the design.md D10 rewrite and the new DisjointSubsetNetPlTests.cs
file rather than a new checklist row. All 27 original rows remain complete and the correction added
work outside the original checklist scope, reported here rather than treated as an incomplete task.

### Build & Tests Execution
**Build**: PASSED (verified by direct execution, not trusted from apply-progress)
```text
dotnet build -warnaserror -p:BaseOutputPath=$LOCALAPPDATA/tmp/sdd-verify-build/
Build succeeded. 0 Warning(s), 0 Error(s)
```

**Tests**: 638 passed / 0 failed / 0 skipped (verified by direct execution)
```text
dotnet test -p:BaseOutputPath=$LOCALAPPDATA/tmp/sdd-verify-build/
Passed! - Failed: 0, Passed: 638, Skipped: 0, Total: 638, Duration: 12 s
```

This confirms the apply/correction claim of 638 passed and a clean -warnaserror build. Baseline
before slice A was independently established last run as 606 (not 607, which circulated in
planning artifacts) -- 638 minus 606 = 32 net new tests across the original 27-task implementation
(633) plus the 5-test correction (638). No re-litigation needed; this number still holds.

**Coverage**: Not available -- no coverage tool detected in this project (unchanged from prior run).

### Spec Compliance Matrix
| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Exact-Minute Pairing Detects Comparability | Same minute, same direction, paired | DemoBacktestComparabilityCalculatorTests (fixture reproduction) | COMPLIANT |
| Exact-Minute Pairing | Different minutes never paired | DemoBacktestComparabilityCalculatorTests | COMPLIANT |
| Exact-Minute Pairing | Opposing directions same minute never paired | DemoBacktestComparabilityCalculatorTests (direction-in-key case) | COMPLIANT |
| Exact-Minute Pairing | No timezone conversion before pairing | DemoBacktestComparabilityCalculatorTests + source inspection (no ToUniversalTime/ToLocalTime/TimeZone* calls anywhere in calculator or read service) | COMPLIANT |
| Same-Minute Duplicates Refused And Counted | Two demo trades same minute excluded, counted ambiguous | DisjointSubsetNetPlTests.Measure_EveryBucketPopulated (ambiguous demo minute, count=2, net=11) | COMPLIANT |
| Same-Minute Duplicates | Two backtest trades same minute excluded, counted ambiguous | Same test (ambiguous backtest minute, count=2, net=15) | COMPLIANT |
| Offset Reported Per Month, Never Aggregated | Per-month offset matches fixture (24 pairs, 3 months) | DemoBacktestComparabilityCalculatorTests (fixture reproduction test) | COMPLIANT |
| Offset Per Month | No aggregated score exposed | ComparabilityContractTests (reflection: no cross-month aggregate/0-100 field) | COMPLIANT |
| Sign Consistency Reported, No Performance Claim | Sign consistency = 100% on fixture | DemoBacktestComparabilityCalculatorTests | COMPLIANT |
| Sign Consistency | No directional performance claim anywhere | ComparabilityContractTests (reflection, forbidden-word scan) | COMPLIANT |
| ComparabilityBasis Non-Nullable Disclosure | Present on every readout incl. zero-paired | ComparabilityContractTests | COMPLIANT |
| ComparabilityBasis | Non-nullability compile-enforced, reflection-pinned | ComparabilityContractTests | COMPLIANT |
| Offset Readout Is Transitional | Transitional framing present in XML doc | Static doc-comment inspection (PriceOffsetComparabilityDto remarks) -- documentation-level requirement, no runtime test | COMPLIANT (documentation-level) |
| Trade-Set Difference As Set Relationship | Trade-set counts + each subset's net P/L match fixture | DisjointSubsetNetPlTests.Measure_EveryBucketPopulated_ReportsEachDisjointSubsetsNetPlSeparately (all 6 net P/L figures pinned) + GetAsync_PairedTrade_UsesTheDemoCostColumnsAndTheBacktestProfitAsStored (demo=92, backtest=80) | COMPLIANT -- previously CRITICAL/UNTESTED, now pinned |
| Trade-Set Difference | Set counts/P/L never merged into single residual | DisjointSubsetNetPlTests.Dto_ExposesNoCombinedComparedOrResidualNetPlMember (reflection scan + asserts neither 113 nor 23 appears as any decimal property value) | COMPLIANT -- previously CRITICAL/UNTESTED, now pinned |
| Ranking By Absolute Mean Offset, Never Grades | Ranking orders by MeanOffsetPriceUnits | Ranking is caller-side per design; no ranking implementation ships in this slice | PARTIAL (no ranking code exists yet; constraint on a future caller, not this slice's surface -- same non-finding carried from prior run) |
| Ranking | No threshold applied anywhere | ComparabilityContractTests (no code-constant threshold/cutoff found) | COMPLIANT |
| Figures Publish At Any Paired Count >= 1 | Zero paired trades reports null figures, PairedTradeCount=0 | DemoBacktestComparabilityCalculatorTests + DisjointSubsetNetPlTests.Measure_EmptySubset_ReportsNullNetPlBecauseTheArithmeticIsUndefinedNotWithheld | COMPLIANT |
| Figures Publish | Single paired trade still publishes figure, no invented minimum | DemoBacktestComparabilityCalculatorTests (n=1 case) + DisjointSubsetNetPlTests (n=1 net P/L case) | COMPLIANT |
| Calculator Is Deterministic | Repeated calls byte-identical | DemoBacktestComparabilityCalculatorTests (determinism test) + source inspection (no RNG/seed anywhere) | COMPLIANT |

**Compliance summary**: 20/21 scenarios directly test-pinned COMPLIANT; 1/21 PARTIAL (ranking is a
constraint on a future caller that does not yet exist as code in this slice -- same non-finding
carried from the prior run, not new). 0 UNTESTED, 0 FAILING remain. The two scenarios that were the
sole CRITICAL last run (subset net P/L, never-merged residual) are now both COMPLIANT with dedicated,
non-trivial assertions.

### Correctness (Static Evidence)
| Requirement/Invariant | Status | Notes |
|------------|--------|-------|
| Basis, DemoNetPlBasis, BacktestNetPlBasis are computed properties, never ctor params | Implemented | PriceOffsetComparabilityDto.cs lines 63/70/80; pinned by reflection in Dto_NetPlBasis_IsComputedPerSideNonNullableAndNotDroppableAtAnyCallSite |
| DstTransitionRisk computed, never ctor param | Implemented | MonthlyPriceOffsetDto.cs (DstRisk computed property) |
| No timezone conversion anywhere | Confirmed | grep across calculator/read-service/DTO for TimeZone/ToUniversalTime/ToLocalTime/Convert*Time returns zero matches |
| BacktestRunKind required, no default, no fallback | Implemented | Controller uses nullable kind with explicit null-check -> 400; documented deviation from a non-nullable param, reasoned in apply-progress (no integration harness to drive ASP.NET implicit model validation) |
| Same-minute duplicates refused and counted, never resolved | Implemented | GroupByMinuteAndDirection + ambiguous-bucket branch in Measure; no ticket-order/nearest-price resolution logic exists |
| Figures publish at PairedCount >= 1, no invented minimum | Implemented | MonthAccumulator.ToDto: only count==0 nulls the mean; no other floor |
| Direction part of pairing key, case-insensitive | Implemented | GroupByMinuteAndDirection groups by (minuteTicks, Type.ToUpperInvariant()) |
| Determinism, no RNG/seed | Confirmed | No Random, Guid.NewGuid() (production code), or seed usage in calculator; decimal addition is order-independent |
| PriceUnits naming, no unit conversion | Implemented | MeanOffsetPriceUnits/MinOffsetPriceUnits/MaxOffsetPriceUnits; no instrument-spec table read anywhere |
| Cost asymmetry not flattened (correction-introduced) | Implemented | Read service: demo = Profit + Commission + Swap + Taxes; backtest = Profit as stored; pinned by GetAsync_PairedTrade test (92 vs 80, not equal) |
| No combined/compared/residual figure (correction-introduced) | Implemented | NetPlAccumulator instances are never added together; no property named Total/Combined/Sum/Residual/Difference/Delta/Excess/Outperform; pinned by reflection test asserting neither 113 (sum) nor 23 (difference) appears as any decimal property value |

### Coherence (Design)
| Decision | Followed? | Notes |
|----------|-----------|-------|
| D10 -- Disjoint-subset P/L ships in slice A | Yes (corrected) | design.md D10 rewritten as "Superseded"; original text deferring to slice B is struck and replaced with the implemented choice, matching the shipped code exactly (enum name, property names, "null only where undefined" language) |
| D6 -- ComparabilityBasis computed-property mechanism | Yes | NetPlBasis reuses the same mechanism deliberately as a separate enum, not an overload of Basis -- matches design's own stated rationale |
| D1/D2 -- no timezone conversion, DST disclosed not corrected | Yes | Confirmed by source inspection above |
| D4 -- same-minute duplicates refused, not resolved | Yes | Confirmed above |
| D7 -- raw price units, no tick-size conversion | Yes | PriceUnits naming used throughout, doc comments state no instrument-spec table is read |

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | Yes | apply-progress reports 27/27 task rows test-first; correction reports 5 new tests added test-first for D10 |
| All tasks have tests | Yes | 27/27 original + correction's DisjointSubsetNetPlTests.cs (5 tests) |
| RED confirmed (tests exist) | Yes | DisjointSubsetNetPlTests.cs exists at the reported path and was read directly during this verify pass |
| GREEN confirmed (tests pass) | Yes | 638/638 passing on direct execution, including all 5 new tests |
| Triangulation adequate | Yes | The subset-P/L requirement now has 2 dedicated tests (populated-bucket case + empty-subset-null case) plus a DB-level integration test for the cost-basis asymmetry |
| Safety Net for modified files | Yes | PriceOffsetComparabilityDto.cs, DemoBacktestComparabilityCalculator.cs, DemoBacktestComparabilityReadService.cs all modified; no existing test body was changed |

**TDD Compliance**: 6/6 checks passed

### Assertion Quality
No tautologies, no ghost loops, no assertion-without-production-call patterns found in
DisjointSubsetNetPlTests.cs. All five tests call DemoBacktestComparabilityCalculator.Measure or
DemoBacktestComparabilityReadService.GetAsync directly and assert distinct, non-trivial expected
values (30, 7, 100, -50, 11, 15, 92, 80, etc.) rather than type-only or empty-collection checks. The
reflection-based tests combine structural checks with value assertions, not bare NotBeNull calls.

**Assertion quality**: All assertions verify real behavior

### Issues Found

**CRITICAL**: None. The single CRITICAL from the prior run (disjoint-subset net P/L unimplemented
and unpinned) is resolved: the DTO now carries six NetPl fields, the calculator populates them via
six independent, never-merged accumulators, the read service projects the correct per-side cost
basis, and five new tests pin both required scenarios plus the never-merged-residual invariant.

**WARNING**:
1. ComparabilityContractTests' tripwire file list (the array of paths the reflection tests assert
   exist/are covered) does not include the new Domain/Enums/NetPlBasis.cs. Consistent with the
   prior run's own assessment: this is real but inert -- NetPlBasis is a two-member enum with only
   doc comments, and its supporting logic (the actual Profit+Commission+Swap+Taxes vs Profit-as-
   stored computation) lives in DemoBacktestComparabilityReadService.cs and
   DemoBacktestComparabilityCalculator.cs, both of which ARE in the tripwire list and ARE exercised
   by DisjointSubsetNetPlTests.cs. Not re-litigating: I agree with the prior assessment. Recommend
   adding it to the tripwire list before archive as a one-line hygiene fix, not a blocker.
2. tasks.md still shows 27/27 with no explicit row for the D10 correction; the correction is
   evidenced by the design.md rewrite and new test file rather than a checklist entry. Not a defect
   in the shipped code, but if sdd-archive expects task-file/code parity as a literal checklist
   match, this gap should be noted rather than silently reconciled.
3. The "Ranking Across Strategies Orders By Absolute Mean Offset" requirement's first scenario
   (ranking orders by absolute MeanOffsetPriceUnits) has no implementation or test in this slice --
   ranking is described as a future caller's behavior over already-published fields, not a method
   this DTO/service exposes. This was also true in the prior run and was not flagged there; flagging
   as WARNING now for completeness, not as a new finding, since the "no threshold" half of the same
   requirement IS tested.

**SUGGESTION**:
1. PriceOffsetComparabilityDto's XML doc-comment transitional-framing requirement is verified by
   static inspection only (no runtime test asserts the presence of doc-comment text, since XML doc
   comments are not reflectable at runtime by default). This mirrors the prior run's treatment and is
   an inherent limitation of testing documentation content, not a gap introduced by this correction.

### Verdict
**PASS**

The previously blocking CRITICAL (disjoint-subset net P/L unimplemented) is resolved with a
substantive, test-pinned implementation verified by independent build (0 warnings, 0 errors) and
test execution (638/638 passing) rather than trusting the apply/correction reports. All 10
requirements and 21 scenarios in the delta spec are either COMPLIANT (20) or a pre-existing,
consistently-treated PARTIAL (1, ranking -- a future-caller constraint, not this slice's surface). No
new CRITICAL findings emerged from this re-run; the two WARNINGs are hygiene items (tripwire list
completeness, tasks.md/correction parity) that do not block sdd-archive.
