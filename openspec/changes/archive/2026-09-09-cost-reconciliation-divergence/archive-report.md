# Archive Report: cost-reconciliation-divergence (Slice A)

**Date**: 2026-09-09  
**Status**: ARCHIVED — Slice A complete and verified  
**Archive Path**: `openspec/changes/archive/2026-09-09-cost-reconciliation-divergence/`

## SDD Artifact Chain (Engram IDs for Traceability)

- **Proposal**: #2568 — `sdd/cost-reconciliation-divergence/proposal`
- **Spec**: #2569 — `sdd/cost-reconciliation-divergence/spec` (corrected delta, all 10 requirements)
- **Design**: #2570 — `sdd/cost-reconciliation-divergence/design` (10 ADRs: D1-D10, Slice A only)
- **Tasks**: #2571 — `sdd/cost-reconciliation-divergence/tasks` (7 phases, 27 tasks, all checked)
- **Verify-Report**: #2573 — `sdd/cost-reconciliation-divergence/verify-report` (PASS: 0 CRITICAL, 3 WARNING, 1 SUGGESTION)
- **Archive Report**: #2576 (Engram) + this file

## Spec Merge Summary

### Created: NEW Main Spec

**Path**: `openspec/specs/demo-backtest-comparability/spec.md`  
**Action**: Copied delta spec from `openspec/changes/cost-reconciliation-divergence/specs/demo-backtest-comparability/spec.md` to create the authoritative main spec.

**All 10 Requirements Present** (byte-identical from corrected delta):

1. Exact-Minute Pairing Detects Price-Series Comparability, Not Trade Correspondence
2. Same-Minute Duplicates On Either Side Are Refused And Counted, Never Resolved
3. The Offset Is Reported Per Month, Never As A Single Aggregated Score
4. Sign Consistency Is Reported, And No Directional Performance Claim Is Made
5. `ComparabilityBasis` Is A Non-Nullable, Non-Droppable Disclosure
6. The Offset Readout Is Explicitly Transitional, Not A Permanent Verdict
7. The Trade-Set Difference Is Reported As A Set Relationship, Never As A Score
8. Ranking Across Strategies Orders By Absolute Mean Offset, And Never Grades
9. Figures Publish At Any Paired Count Above Zero; Nulls Mean Undefined, Not Withheld
10. The Calculator Is Deterministic

**No Requirements Dropped or Reworded**: All requirements survived the orchestrator's pre-archive review, which corrected the delta spec against the design. The three corrections applied to the spec before archive (direction-in-pairing-key, renamed `…Points` to `…PriceUnits`, withheld-threshold removal) are incorporated in the archived and merged version.

### Load-Bearing Decisions Preserved (Non-Optional for Correctness)

The following critical decisions are preserved verbatim in the merged spec and MUST NOT be modified without explicit future design change:

1. **Exact-minute pairing is correct for MEASURING an offset; WRONG for COMPOSING series** (Requirement 1, design D1 rationale). Pairing requires no tolerance rule — the minute matches or it does not. But merging series via minute-pairing is unsound. This distinction is load-bearing: generalization to non-measurement use cases is explicitly forbidden in the spec.

2. **Direction IS part of the pairing key, even though every measured trade is a buy** (Requirement 1). A demo buy and a backtest sell opening in the same minute MUST NOT pair. This is a structural/defensive requirement: the measured data today contains only buy trades (1,698 demo "buy", 1,406 backtest "Buy"), so this rule is never exercised in production, but a future short strategy would silently reintroduce mispairing if direction were dropped. Marked in the spec as a requirement that MUST NOT be removed as dead code.

3. **DST is disclosed, never corrected** (Design D2). A one-hour DST shift moves the pairing key, so affected trades stop pairing rather than producing a wrong offset. The failure signature is a `PairedCount` collapse in a DST-transition month (March, October, November in 2026), which is observable in the already-reported counts. This decision is load-bearing because correcting DST would assert a timezone conversion the stored data does not carry.

4. **No threshold, and why none may be invented** (Requirement 9, Design D5). `MinHistoryDays = 90` and the monthly-VaR density gate are recorded user decisions with external calibration. No such decision exists for this capability, and n=1 gives no basis to calibrate one. Inventing a threshold would violate `INDEX.md` §5 (forbids fabricated domain assumptions). A future reader cannot "improve" this by adding a minimum paired-count gate; any such gate must come from external configuration, not code.

5. **The two cost bases are never summed, subtracted, or ranked** (Requirement 7, Design D10). `PairedDemoNetPl`, `PairedBacktestNetPl`, `DemoOnlyNetPl`, `BacktestOnlyNetPl`, and the ambiguous-minute subsets are exposed as separate fields. They are never combined into a single residual or aggregated figure. This binding constraint gates slice B (cost decomposition).

6. **Nulls mean the arithmetic is undefined, not withheld** (Requirement 9). Fields are null only when computed over an empty set (e.g., `PairedTradeCount == 0`). Nulls do not signal "data was withheld" — they signal "the operation has no defined result over this subset". A single paired trade (n=1) publishes its offset beside its count.

## Slice Scope and Outstanding Work

### ✅ SHIPPED: Slice A — Price-Series Comparability Check

**Delivered**: Backend-only demo-vs-backtest price-series offset detection and per-month reporting.

**Implementation**: 
- Domain enums: `ComparabilityBasis`, `DstTransitionRisk`, `ComparabilityReadoutStatus`, `NetPlBasis`
- Infrastructure services: `DemoBacktestComparabilityCalculator`, `DemoBacktestComparabilityReadService`
- Application DTO: `PriceOffsetComparabilityDto` with 6 disjoint-subset net P/L fields
- WebAPI endpoint: `StrategyBacktestsController.GetComparability(…)`
- Tests: 32 new tests (5 correction tests + 27 original task tests), all passing

**Verification**: PASS — 0 CRITICAL, 3 WARNING, 1 SUGGESTION. Backend 638/638 tests passing, 0 warnings under `-warnaserror`.

### ⏳ NOT BUILT: Slice B — Cost Decomposition

**Scope**: Break the net P/L divergence into three components: swap (Σ demo Swap, exact), embedded backtest cost (inferred from point-value and configured $/lot), execution residual.

**Binding Constraint**: Slice A's output (6 separate net P/L fields, one per disjoint subset) gates this work. Slice B must report these decompositions per subset, never folding subsets into a single aggregate.

**Status**: Out of scope for this archive. Proposal #2568 proposes slice B as ~400-500 lines, gated on slice A PASS.

### ⏳ NOT BUILT: Slice C — `SourcePlatform` Column on `BacktestRun`

**Scope**: Add `PlatformType? SourcePlatform` (nullable, set at import) to `BacktestRun` entity, required before October 2026 MT5 migration.

**Status**: Out of scope for this archive. Proposal #2568 proposes slice C as ~120-180 lines, independent but wanted before MT5 work begins.

**Note**: Slice C does not gate slices A or B; A and B do not depend on it.

## Task Completion

**All 27 implementation tasks marked complete** (✅) in `openspec/changes/archive/2026-09-09-cost-reconciliation-divergence/tasks.md`.

- Phase 1: Domain enums — 3 tasks, all complete
- Phase 2: Calculator core algorithm — 12 tasks (TDD strict, including synthetic direction/ambiguity tests), all complete
- Phase 3: DTO contract + reflection/tripwire tests — 3 tasks, all complete
- Phase 4: Application interface — 1 task, complete
- Phase 5: Read service — 2 tasks, complete
- Phase 6: WebAPI endpoint — 3 tasks, complete
- Phase 7: Full-suite verification — 1 task, complete (638/638 passing, 0 warnings)

## Verification Summary

**Report**: `openspec/changes/archive/2026-09-09-cost-reconciliation-divergence/verify-report.md`

**Result**: **PASS** (re-run after correction transaction)

**Metrics**:
- Backend build: 0 errors, 0 warnings (`-warnaserror` clean)
- Tests: 638/638 passing (27 net new tests from tasks)
- Spec coverage: 20/21 scenarios COMPLIANT, 1/21 PARTIAL (ranking-across-strategies; deferred to future feature, not slice B/C scope)
- Severity: 0 CRITICAL, 3 WARNING, 1 SUGGESTION

**Warnings** (non-blocking):
1. `NetPlBasis.cs` missing from `ComparabilityContractTests` tripwire file list (already added; inert before slice B implementation)
2. Direction-in-key and ambiguous-minute tests are synthetic (measured data has no opposing-direction or duplicate-minute trades); high scrutiny recommended on future short-strategy testing
3. Ranking scenario untested in this slice (A-only scope; deferred to slice B or future cross-strategy feature)

**Suggestion** (informational):
- Transitional framing of offset readout verified by static inspection only (not yet tested against UI rendering; backend-only scope confirmed)

## Archive Integrity

**Filesystem move**: `git mv openspec/changes/cost-reconciliation-divergence → openspec/changes/archive/2026-09-09-cost-reconciliation-divergence`
- ✅ Source path gone: verified
- ✅ Archive contains all artifacts: proposal, design, tasks, verify-report, explore context, specs/
- ✅ Byte-identical to verified originals: `git status` shows rename (not modifications)
- ✅ No stubs, pointers, or placeholders left in openspec/changes/

**Main spec created**: 
- ✅ Path: `openspec/specs/demo-backtest-comparability/spec.md`
- ✅ Matches naming convention of sibling specs (e.g., `backtest-portfolio-analytics/spec.md`)
- ✅ All 10 requirements copied verbatim from delta
- ✅ Load-bearing decision notes preserved

## Decisions Made During Archive

**No destructive changes**: This merge involved creating a NEW main spec (no existing spec to replace), so no requirements were dropped or reworded. The delta spec was corrected at the orchestrator gate (design D1-D10 review), and those corrections are now in the archived and merged version.

**Preserve-as-copy decision**: The delta spec is byte-identical to the new main spec. No intermediate "merge" document was created because all 10 requirements fit cleanly into the new main spec structure.

**Outstanding slices explicitly noted**: Proposal identifies slices B and C as future work, gated/independent respectively. This archive records the boundary: Slice A is complete and closed; B and C are NOT part of this archive and require their own future change cycles.

## Recommendations for Next Work

1. **Slice B (Cost Decomposition)**: Ready to start once Slice A passes review and is merged. Gate is satisfied. Estimate ~400-500 lines. Binding constraint: maintain the 6 disjoint-subset net P/L structure from Slice A's DTO.

2. **Slice C (SourcePlatform)**: Can start independently (does not depend on A or B). Wanted before October 2026 MT5 migration. Estimate ~120-180 lines. No bindings to enforce.

3. **Cross-Strategy Ranking Feature**: Deferred from Slice A (out of scope, n=1 strategy today). When attempted, use the `|MeanOffsetPriceUnits|` ordering rule recorded in Requirement 8; never invent a threshold or grade.

4. **Short-Strategy Testing**: When a backtest or demo strategy with sell trades is added, Slice A's direction-in-key requirement will be exercised. The synthetic test case (task 2.4, opposing-direction same-minute pairing rejection) pinned the structural contract; verify it fires correctly on real short-strategy data.

## Compliance Notes

- **SDD Cycle Complete**: Proposal → Spec → Design → Tasks → Apply → Verify → Archive. All phases executed, all gates passed, all artifacts persisted.
- **Artifact Store**: Hybrid mode — archive report saved to Engram with observation IDs; filesystem merge and git move completed.
- **No Overrides**: Archive proceeded under normal gates; no exceptional stale-checkbox reconciliation or override approvals recorded.
- **Clean Pipeline**: Backend 638/638 tests, dotnet build -warnaserror clean, git status shows only the expected `git mv` (rename, not modifications).
