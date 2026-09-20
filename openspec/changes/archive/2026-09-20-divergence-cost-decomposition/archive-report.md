# Archive Report: divergence-cost-decomposition (Slice B)

**Date**: 2026-09-20
**Status**: ARCHIVED — Slice B complete and verified
**Archive Path**: `openspec/changes/archive/2026-09-20-divergence-cost-decomposition/`

## Verification Basis

- **Result**: PASS
- **Findings**: 0 CRITICAL, 0 WARNING, 0 SUGGESTION
- **Commits shipped**: `05669fd` (PR B1 — data coverage decomposition), `a52e941` (PR B2 — swap,
  embedded cost, execution residual)
- **Working tree**: clean at `a52e941`
- **Full suite**: 704 tests passing
- **Build**: clean under `-warnaserror`
- **Format**: clean

## Task Completion

**All 94 implementation tasks marked complete** (`- [x]`), 0 unchecked, across both PRs:

- **PR B1 — Coverage** (Phases 1-9): domain enums, slice B's own projection type, coverage
  calculator, application DTOs (coverage-only), read service/interface, DI/controller wiring,
  contract/tripwire tests, determinism, static gates and full suite.
- **PR B2 — Swap, embedded cost, residual** (Phases 10-18): domain enums, swap component, embedded
  cost component, composing calculator with the comparability-gate signature, execution residual,
  extended contract/tripwire tests, full-composition determinism, endpoint completion, static gates
  and full suite.

Source: `openspec/changes/archive/2026-09-20-divergence-cost-decomposition/tasks.md`.

## Spec Merge Summary

### Created: NEW Main Spec

**Path**: `openspec/specs/demo-backtest-cost-decomposition/spec.md`
**Action**: Copied delta spec verbatim from
`openspec/changes/divergence-cost-decomposition/specs/demo-backtest-cost-decomposition/spec.md` to
create the authoritative main spec — no prior main spec existed for this capability, matching the
precedent set by slice A's archive (`2026-09-09-cost-reconciliation-divergence`), which likewise
created `openspec/specs/demo-backtest-comparability/spec.md` from its delta with no intermediate
merge document, because all requirements fit cleanly into the new main spec structure.

**All 8 Requirements Present** (byte-identical from the verified delta):

1. The Decomposition Is Diagnostic Only, Never A Filter, Gate, Score, Or Recommendation
2. The Comparability Gate Is A Structural Ordering Dependency, Never A Computed Threshold
3. Data Coverage Discloses Itself As A Presumption From Absence, Never As A Proof
4. Swap Is Reported As An Isolated Component, Never Netted Silently
5. Embedded Backtest Cost Surfaces Four Calibration States Distinctly, With No Fallback
6. The Execution Residual Is Computed Over The Paired Subset Only
7. No Instrument Is Named In Code
8. The Calculator Is Deterministic
   (plus: The Decomposition Status Discloses Which Component Was Producible At All — 9th requirement
   present in the source spec under the same section, retained verbatim)

Plus the **Definitions** section (coverage-window derivation) and the **Non-Goals** section, both
carried over unmodified.

**No Requirements Dropped or Reworded.**

## Load-Bearing Content Verified to Have Survived the Merge

All six items called out as at-risk were checked against the merged file at
`openspec/specs/demo-backtest-cost-decomposition/spec.md` after the copy. Each is quoted from that
file below.

### 1. The `Definitions` section — dense coverage window, observed endpoints, "observed months only" REJECTED

Present in full under `## Definitions`. Quoted:

> **The window** is the DENSE calendar-month span from the earliest to the latest trade open timestamp
> across the UNION of the demo and backtest trade sets for the strategy and run kind being decomposed.
> Every calendar month inside that inclusive span produces a row — including an interior month with no
> trades on either side — because the endpoints are OBSERVED from the trade data itself, never
> configured or defaulted; no "lookback period" setting exists and none may be added.

And the rejection, same section:

> That "observed months only" shape is REJECTED here: an interior month with no trades on either side
> is exactly the signal this capability exists to surface (measured: a DAX case where an entire month
> of source data was silently absent). Under an observed-months-only rule that month would simply
> never render, reproducing the exact silent-omission failure this capability is built to prevent.

Confirmed present, unmodified.

### 2. "Residual over the full disjoint partition" REJECTED, with reason

Present under the Execution Residual requirement as a blockquote:

> **Rejected: computing the residual over the full disjoint partition (paired + demo-only +
> backtest-only).** `SIMULATOR_ROADMAP.md`'s component table originally specified the residual this
> way. That is rejected here: folding demo-only and backtest-only P/L into one residual figure mixes
> two different questions — "the same signal produced a different result" (what the paired-subset
> residual measures) and "this trade never had a counterpart at all" (what the coverage component
> exists to isolate). Recording this rejection here is necessary because the roadmap's own prior
> wording could otherwise reintroduce it; this document, not the roadmap, is now authoritative on the
> residual's scope.

Confirmed present, unmodified. (`SIMULATOR_ROADMAP.md`'s layer-0 row has also been updated in this
archive pass — see "Also Updated" below — so it no longer conflicts with this rejection.)

### 3. Diagnostic-only prohibition — no threshold, and the `IsComparableEnoughToDecompose` ban

Present as its own requirement, "The Decomposition Is Diagnostic Only, Never A Filter, Gate, Score, Or
Recommendation":

> The capability MUST NOT compute, expose, or accept a threshold, cutoff, configurable limit,
> "acceptable" band, pass/fail label, grade, or recommendation of any kind, on any component or on the
> decomposition as a whole. … This is stated positively here because no such number exists in any
> measured or vendor source (`INDEX.md` §5) …

And the named-member ban, under the comparability-gate requirement:

> **A field named anything like `IsComparableEnoughToDecompose` MUST NOT exist.** … This requirement
> names the forbidden shape explicitly so a later reader who wants to "just add a boolean gate" finds
> this paragraph first.

Confirmed present, unmodified, plus a dedicated scenario ("No member named `IsComparableEnoughToDecompose`,
or matching `IsComparable*`, exists anywhere in the decomposition surface").

### 4. Coverage as presumption from absence, never proof; SQX Data Manager GUI-only; both-sides-empty ambiguity

Present under "Data Coverage Discloses Itself As A Presumption From Absence, Never As A Proof":

> Every readout MUST carry a non-nullable, computed `CoverageBasis` property with a single member,
> `PresumedFromBacktestTradeAbsence`, disclosing that a month labelled as a coverage gap is a
> presumption drawn from the absence of backtest trades, not a confirmed finding; for a
> `NoTradesEitherSide` month, that same disclosure additionally covers the ambiguity that neither side
> trading could mean the strategy did not signal that month, or that source data was absent on one or
> both sides … The capability cannot, and MUST NOT claim to, distinguish "the source data was absent"
> from "the strategy did not signal" — the only independent corroboration, SQX Data Manager's gap
> percentage, has no ingestion path anywhere in the codebase (verified: GUI-only) and is out of scope.

Confirmed present, unmodified, and reinforced in Non-Goals: "**Ingesting SQX Data Manager gap
percentages.** GUI-only; no ingestion path exists anywhere in the codebase (verified)."

### 5. Four embedded-cost states, no fallback, `CalibrationStatus` cannot be reused, no staleness concept

Present under "Embedded Backtest Cost Surfaces Four Calibration States Distinctly, With No Fallback":

> The capability MUST classify the embedded-cost readiness of the backtest's symbol calibration as
> exactly one of four states — `NoCalibrationRow`, `InsufficientSamples`, `Inconsistent`, or
> `Calibrated` … the pre-existing three-member `CalibrationStatus` enum cannot express this distinction
> and MUST NOT be reused directly for this purpose. … `CalibratedAt` MUST be reported verbatim beside
> the embedded-cost figure whenever a calibration row exists, compared to nothing — no staleness, TTL,
> or recency rule exists as a domain concept (verified: all references to `CalibratedAt` in the
> codebase are writes, storage configuration, or projections; nothing compares it), and none MUST be
> invented here.

Confirmed present, unmodified.

### 6. `CoverageComponentOnly = 0` retained although unreachable — reason preserved

This exact enum-naming rationale is not spelled out verbatim under that identifier inside
`spec.md` itself (the spec states the four calibration states and the zero-member defensive-default
rationale for the calibration enum, quoted above and below), but the equivalent zero-slot defensive
rationale for the sibling enum is present as a named blockquote under the embedded-cost requirement:

> The new state-carrying type's zero member MUST be `NoCalibrationRow` — the state that asserts
> nothing usable — not `Calibrated`. This codebase has twice been bitten by an optimistic enum zero
> (`PlatformType.MT4 = 0`, `FundingService.Other = 0`), where the CLR default silently asserted a
> specific meaning nobody chose. … placing the safe, non-committal state at zero is the defensive
> choice for any new consumer of the calibration row that has not yet been audited …

The specific `CostDecompositionStatus.CoverageComponentOnly = 0` naming and its "would otherwise
promote `Decomposed` to the zero slot" rationale live in `design.md` (moved to
`openspec/changes/archive/2026-09-20-divergence-cost-decomposition/design.md`, phases 10-13 context;
not re-derived in `spec.md`'s prose, which documents the calibration-enum instance of the same pattern
instead). Both files are preserved verbatim in the archive folder, so the reasoning is not lost — it
is retained in `design.md` rather than duplicated word-for-word into `spec.md`, since `spec.md` did
not carry that specific enum-zero rationale in the pre-archive source file. This is noted under
"Dropped or Reworded" below for transparency.

## Accepted Debt Recorded

- **Unbounded coverage span.** No cap was added because any cap would be a fabricated number, which
  this capability's diagnostic-only requirement forbids; the month-advance loop terminates only
  because the observed span between two real timestamps is finite. Recorded in `design.md`
  (preserved in the archive folder) and consistent with the Definitions section's "never configured
  or defaulted" stance on the window's endpoints.
- **Duplicated pairing logic.** `CostDecompositionCalculator`'s pairing logic duplicates slice A's
  calculator. Accepted as deliberate, pinned by an equivalence test (task list, Phase 15/16 contract
  and determinism tests). Recorded in `tasks.md` and `design.md`, both preserved in the archive
  folder.

## Also Updated

**`openspec/SIMULATOR_ROADMAP.md`** — layer 0's table row updated to record that both slices have
shipped:

> **0** | **Cost reconciliation and divergence** — **SHIPPED** (slice A `demo-backtest-comparability` +
> slice B `demo-backtest-cost-decomposition`) | Separate **data coverage** / swap / embedded backtest
> cost / execution residual per strategy. Emits a per-strategy divergence decomposition, diagnostic
> only — no threshold; a human decides. The capability is instrument-agnostic by construction (gated
> structurally on slice A's comparability output, never on an instrument literal) and gold is the
> first instrument to qualify; NQ and DAX remain out of scope for decomposition pending
> Darwinex-sourced data, not because the code excludes them. | 1, and every later crossing

This deliberately avoids overstating scope: the capability itself never names an instrument (spec
Requirement "No Instrument Is Named In Code"), so the roadmap note frames NQ/DAX exclusion as a data
availability gap, not a code-level restriction.

## Archive Integrity

**Filesystem move**: `git mv` of `explore.md`, `proposal.md`, `design.md`, `tasks.md`, and `specs/`
from `openspec/changes/divergence-cost-decomposition/` to
`openspec/changes/archive/2026-09-20-divergence-cost-decomposition/`.

- Source path gone: verified (`ls openspec/changes/divergence-cost-decomposition` → No such file or
  directory)
- Archive contains all artifacts: `design.md`, `explore.md`, `proposal.md`, `tasks.md`, `specs/`,
  plus this `archive-report.md`
- No stubs, pointers, or placeholders left behind in `openspec/changes/`

**Main spec created**:

- Path: `openspec/specs/demo-backtest-cost-decomposition/spec.md`
- Matches naming convention of sibling specs (`demo-backtest-comparability/spec.md`)
- All 8 (plus the status-disclosure requirement, 9 total) requirements copied verbatim from the
  verified delta
- `Definitions` and `Non-Goals` sections copied verbatim

## Decisions Made During Archive

- **No destructive changes**: creating a NEW main spec (no existing spec to replace), so nothing was
  dropped or reworded in `spec.md` itself.
- **Preserve-as-copy**: the delta spec is byte-identical to the new main spec, matching slice A's
  precedent — no intermediate merge document was needed.
- **`git commit` intentionally not run** — user retains commit ownership per instruction; changes are
  left staged/unstaged in the working tree for the user to commit.

## Compliance Notes

- **Verification basis**: PASS, 0 CRITICAL / 0 WARNING / 0 SUGGESTION, provided directly by the
  requester (this archive did not re-run verification).
- **Clean pipeline claimed by requester**: 704 tests passing, `-warnaserror` clean build, format
  clean, working tree clean at `a52e941` before this archive's file moves.
- **No commit performed**: per explicit instruction, this archive leaves all file moves and edits
  uncommitted in the working tree.
