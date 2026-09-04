# Verify Report - backtest-portfolio-risk-analysis - PR1 (Phase 1, private cores)

**Commit verified**: 55bc2a2 - **Scope**: PR1 of three (private cores only). PR2 (bridge +
analytics adapters) and PR3 (run selection, endpoint, UI) do not exist yet - correctly absent,
not evaluated as gaps.

## Completeness - Phase 1 tasks (8/8)

| Task | Status | Evidence |
|---|---|---|
| 1.1 backfill regression pin | [x] verified | PortfolioAnalyticsCalculatorLiveOutputRegressionTests.cs - 4 correlation + 3 VaR pins, all pass |
| 1.2 private AlignmentMode + CorrelationMatrixCore | [x] verified | source lines 33-46, 398-422, 435-475 |
| 1.3 SupportedPercentile RED-first predicate table | [x] verified | PortfolioAnalyticsPrivateCoreTests.cs, 22 theory/fact cases including exact boundary cases |
| 1.4 SeriesDensity + Measure | [x] verified | source lines 664-695; 5 tests |
| 1.5 PercentilePolicy required through VarFromDaily/ComputeMonthlyVar | [x] verified | source lines 57-70, 534, 574-576; no overload, no default |
| 1.6 live ComputeVaR passes Unconditional | [x] verified | source lines 317, 327, 330 |
| 1.7 policy regression (sparse series still reports) | [x] verified | ComputeVaR_SparseNegativeSupport_StillReportsEveryShippedFigure |
| 1.8 suites green, bit-identical | [x] verified | measured myself, see below |

No unchecked tasks. Phase 0 (0.1-0.3) and Phases 2A/2B/3/4 are untouched, matching the tasks doc scope note.

## Measured, independently (not taken from the apply report)

- Backend: **448/448** passed (dotnet test AppTradingAlgoritmico.slnx, 21s). Matches reported baseline 419/419 -> 448/448 (+29).
- Build: dotnet build AppTradingAlgoritmico.slnx -warnaserror -> **0 Warning(s), 0 Error(s)**.
- Frontend 371/371 - not re-run (PR1 touches zero web files; the commit diff contains only .cs and openspec/** files plus incidental package.json/environment.ts version bumps, so there is no code path by which PR1 could have changed frontend test outcomes). Treated as unverifiable-by-me-but-structurally-consistent rather than passed on faith.
- Working tree: clean before and after my own three injected-defect runs (see below); git status --short empty at both checkpoints.
- Changed lines: **228 insertions / 21 deletions** in PortfolioAnalyticsCalculator.cs (249 production diff) + **248 + 234 = 482** new authored test lines. Total **731**, confirmed via git diff --numstat 0e02731 55bc2a2. The overrun (~330 estimated) is entirely tests: production diff (249) alone is close to the ~330 estimate; the two new test files (482 lines) are the delta. Independently confirmed ComputeCorrelation had zero references anywhere in the pre-existing PortfolioAnalyticsCalculatorTests.cs before this commit, so its first pin necessarily had to be authored rather than extended.

## Injected-defect re-proof (re-run myself, not accepted from the report)

All three defects were introduced with sed, run, observed, then reverted with git checkout --
followed by a full 448/448 green re-run and git status --short empty.

**1. AlignmentMode.Intersection on the live correlation call site (line 420)**

    Expected correlation.Matrix[0] to be equal to {1M, 0.8182M}, but {1M, 1M} differs at index 1.

Matches the reported output verbatim. 1 test failed (ComputeCorrelation_UnionAlignment_PinsCoefficientAndAverage), rest unaffected in that filtered run.

**2. RequireSupport on all three live call sites (lines 317, 327, 330)**

    System.InvalidOperationException : Nullable object must have a value.
       at ...PortfolioAnalyticsCalculator.ComputeVaR(...) ...PortfolioAnalyticsCalculator.cs:line 318

3 failures in the filtered PortfolioAnalytics run (ComputeVaR_SparseNegativeSupport_StillReportsEveryShippedFigure, ComputeVaR_EmptySeries_PinsShippedZeros, ComputeVaR_MonthlyVar_ZeroFilledDaysDoNotDistortSums), all throwing at the exact .Value on the nulled gatedVar95/gatedVar99 - confirms the deliberate "impossible null fails loudly, never becomes a silent 0" design choice (D4b) is real, not aspirational.

**3. RequireSupport on the monthly call site alone (line 330)**

    Expected service.MonthlyVar95 to be -300M because Unconditional never gates: 1 negative window < the 4 the relation needs, but found <null>.
    Expected svc.MonthlyVar95 to be 0M because 89 of 91 windows touch neither event and sum to exactly 0, but found <null>.

2 failures: the new PR1 regression test, plus **ComputeVaR_MonthlyVar_ZeroFilledDaysDoNotDistortSums** - confirmed by reading PortfolioAnalyticsCalculatorTests.cs:301-321 to be a pre-existing test (present before this commit, not added by 55bc2a2) that nobody wrote for this change and that nonetheless catches a mis-wired monthly-only policy. This is the claimed "shipped test not written for this change" and it checks out.

**Revert proof**: git checkout -- on the one modified file after each injection; final git status --short empty; final full-suite run **448/448** green.

**Percentile own body**: read lines 596-607 - untouched, matches the design's explicit "no shipped number moves" claim. SupportedPercentile (line 630) is a distinct new private method beside it, never called from the live adapters (VarFromDaily/ComputeMonthlyVar route to it only under RequireSupport, which no live call site passes) - confirmed the live path structurally cannot reach the gate.

**The load-bearing assertion**: ComputeVaR_SparseNegativeSupport_StillReportsEveryShippedFigure (lines 152-203) exists and is real: a 100-day series with exactly 1 negative day (which WOULD fail the daily support relation, 1 < 5, and the monthly one, 1 < 4) still returns Var95 = -10m, Var99 = 0.10m, MonthlyVar95 = -300m - every figure present, because the live adapter passes Unconditional. This is what the design calls the assertion that proves the shared helpers were parameterised, not re-behaved, and it is present and passing.

## Specific checklist items

**1. Density predicate is index/count relation, never hard-coded 5% or non-zero share.**
Confirmed at source lines 630-639: required = floor(p*(N-1))+1; negativeCount >= required. No
literal 0.05/5% constant in the predicate body (the 0.05/0.01 literals only ever appear as
the caller-supplied p argument at the two call sites, never inside SupportedPercentile).
The wrong-predicate pinning test exists and would fail against a non-zero-share gate:
SupportedPercentile_HighNonZeroShareDoesNotClearTheGate constructs 164 negative + 164 positive
(8.50% non-zero, clears any 5% non-zero-share bar) and asserts the gate still returns null - a
non-zero-share implementation would report here, so the test is a genuine tripwire, not a tautology.

**2. Per-confidence-level gating, IST fixture indices.**
SupportedPercentile_ReportsOnlyWhenNegativeCountReachesThePercentileIndex theory row
InlineData(3860, 0.01, 164, true) combined with InlineData(3860, 0.05, 164, false) pins
exactly the claim: the same 164-negative-day population supports p=0.01 (needs 39) while failing
p=0.05 (needs 193). I did not independently re-derive sorted[38] = -199.46 / sorted[192] =
0.00 against the real CSV fixtures (no fixture-driven end-to-end test exists in PR1 - that
requires PR2 bridge to produce BacktestNetSeries[]), so the exact IST literal values are
unverified by me at the fixture level, but the underlying index arithmetic that would produce
them is verified against the synthetic populations that mirror the fixture measured counts.

**3. Sign convention - VaR published as positive loss magnitude.**
Source: VarFromDaily returns (p95 is null ? null : -p95.Value, p99 is null ? null : -p99.Value,
worst, best) at line 549; ComputeMonthlyVar returns monthlyVar95 = -p05.Value at line 591.
portfolio-monthly-var/spec.md lines 14-18 now state this explicitly: a reported VaR is a
positive loss magnitude, not the raw percentile - a percentile of -400.19 is published as
400.19. Every scenario in that spec (lines 46-79) asserts the published positive figure.
No scenario found asserting a negative published figure. Confirmed sound and consistent with the
disclosed correction of an earlier sign error.

**4. Policy required, not defaulted.**
PercentilePolicy has no default value on either private helper signature (line 534 VarFromDaily,
line 576 ComputeMonthlyVar), and no overload exists that omits it. Zero matches found for any
defaulted PercentilePolicy parameter in the file. Confirmed.

**5. Each gate has one branch no fixture reaches.**
Monthly gate: both fixtures report (design D4, measured 1148/3831 and 1203/3775, both clearing
their thresholds by roughly 6x) - the withhold branch is fixture-unreached. Synthetic boundary
theory rows exist for the daily gate at exactly the threshold and one more (192 false / 193 true
at N=3860); since ComputeMonthlyVar routes through the identical LowPercentile to
SupportedPercentile code path, the daily boundary pin is evidence for the monthly branch
correctness too, but there is no separate monthly-labeled boundary theory row - flagged as a
suggestion below.
Daily gate: both fixtures withhold (both fail their thresholds) - the report branch is
fixture-unreached for the daily gate at those N, but IS exercised by
SupportedPercentile_WhenSupported_ReturnsTheUngatedPercentileVerbatim and the boundary row at 193.

**6. Three disclosed deviations - checked, all sound.**
CorrelationMatrixCore drops the labels parameter the design File Changes table names (design.md
line 122 vs source line 436: CorrelationMatrixCore(dayMaps, mode), no labels). Confirmed real:
labels are built and passed to the DTO constructor by the caller (ComputeCorrelation lines
400-421), never touching the math - legitimate, matches the stated rationale that PR2 needs a
differently-celled DTO over the same core.
Tasks 1.3/1.4 pinned by reflection: confirmed InternalsVisibleTo does not appear anywhere in the
repo, so no public/test-visible surface exists for these two non-public cores in PR1. Reflection
is the only available mechanism; sound.
Policy-aware empty-series handling (RequireSupport yields null, Unconditional yields shipped 0m):
confirmed at source lines 537-542. This is new behavior beyond the literal task wording but does
not regress any live output (Unconditional still returns 0m for an empty series, matching
ComputeVaR_EmptySeries_PinsShippedZeros), and is required for RequireSupport to have any
coherent meaning on an empty series. Sound.

## Design coherence

D4, D4a, D4b as described in design.md match the shipped code line-for-line, including the exact
line-reference claims for VarFromDaily/ComputeMonthlyVar/Percentile (design cites lines 440/460/478;
current shipped lines are 534/574/597 - shifted, expected drift from being written before the
final diff landed; the relative placement - gate beside percentile, policy threaded through both -
is exactly as designed).

## Corrections to artifact claims found while verifying

None beyond what apply-progress already self-corrected (stale 365/365 baseline, corrected sign
convention). No new factual error found in this round - the claims re-measured (the two
commit-verbatim injection outputs checked character-for-character, the changed-line count, and
the zero-prior-coverage claim) all held exactly as stated.

## What is unverifiable in this pass, stated plainly

- The literal fixture percentile values sorted[38] = -199.46 and sorted[192] = 0.00 against the
  real IST CSV are not independently re-derived here - no end-to-end fixture path exists yet in
  PR1 (that requires PR2 BacktestNetSeries[] bridge). The synthetic-population tests verify the
  same index arithmetic at the same N, which is the strongest evidence available pre-PR2, but it
  is not the same as running the real fixture through a real adapter.
- Frontend 371/371 was not re-run (no frontend file changed in this commit; re-running would
  spend time verifying a path this PR cannot have touched).
- The monthly-gate withhold boundary is exercised through the same shared SupportedPercentile
  function as the daily boundary rather than through a monthly-labeled theory row - functionally
  equivalent given the shared code path, but a monthly-named test does not exist as such.

## Requirements PR2 is responsible for (not evaluated as gaps)

Every requirement in specs/backtest-net-series-bridge/spec.md and
specs/backtest-portfolio-analytics/spec.md (pairing, throw-vs-status, unscalable accounting, run
selection, Unknown refusal, backtest correlation intersection alignment, provenance, reporting)
requires the BacktestNetSeries[] adapters, confirmed absent from src/ (no matches found for
BacktestNetSeries anywhere under app.trading.algoritmico.api/src). These correctly await PR2 and
are not findings against PR1.

## Issues

CRITICAL: none.

WARNING: none.

SUGGESTION:
- When PR2 lands, add a monthly-gate-specific boundary theory row (paralleling the daily boundary
  pair at N=3860) rather than relying on the shared-code-path argument alone - cheap, and removes
  the one inferential step in checklist item 5 above.

## Verdict

PASS. All 8 Phase 1 tasks are complete and each is backed by a real, currently-passing test.
Backend 448/448, build 0/0 warnings, working tree clean - all measured directly, not taken on
trust. The bit-identical guarantee was proven, not merely asserted, via three independently
re-run and reverted defect injections whose output matched the reported transcripts verbatim. The
three disclosed deviations are sound. No scenario in portfolio-monthly-var/spec.md retains an
inverted sign. PR2 requirements are correctly and verifiably out of scope for this slice.
