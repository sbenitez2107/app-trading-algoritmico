# Tasks: FTMO multi-start replay

Strict TDD throughout; every RED precedes its GREEN and every RED must be demonstrably able to fail
(no false-positive test is accepted — see falsification checkpoints). Five chained PRs, per design.md
("Migration / Rollout"): **PR0** (race bug fixes, ~120-line floor) → **PR1** (funded phase + FirstBreach
extraction + benchmark gate, ~600-line floor, `size:exception`) → **PR2** (start enumeration + order
statistics, ~250-line floor) → **PR3** (guard/projection extraction, refactor only, ~180-line floor) →
**PR4** (service, DTO, endpoint, ~700-line floor, `size:exception`). Each PR starts only after the
previous PR's gates are green. Read `.claude/conventions/backend-core.md` and `backend-testing.md`
before any `.cs` file. Every unit cites its spec requirement/scenario or design decision.

## Hard rules — checkpoint at every task that touches these

1. **`FtmoBreachEvaluator.cs` is NOT edited in any PR.** If any task appears to need a change inside
   it, STOP and report instead of editing it.
2. **The golden pin (`FtmoBreachSimulationReadServiceGoldenPinTests.cs`) and the challenge-race PR1
   snapshot pin (task 1.0.1 in the archived race change) stay GREEN and UNEDITED through all five PRs.**
   Re-run both at the end of every PR's gate phase, not just at the very end.
3. **PR0 edits 0 existing assertions.** Its RED tests must fail against the CURRENT shipped behaviour:
   Bug A's RED is a `BreachedFirst` phase whose balance later touches the target and meets the day
   minimum only AFTER the breach close; Bug B's RED is an `Unscalable` row straddling T (`Open < T <
   Close`). Per design.md Decision 0, every existing `FirstTargetTouchSourceClose`/
   `MinTradingDaysMetFtmoDay` assertion in `FtmoChallengeRaceTests.cs` (lines ~74, 158, 259, 326,
   378–379) is already on a `TargetReachedFirst`/`NotStarted` phase, and the existing phase-2-handover
   test uses no straddling row — confirm this by reading them (task 0.1) before writing PR0's REDs. If
   any existing assertion needs to change to go green, STOP and report.
4. **PR1–PR4 edit 0 existing assertions.** The only permitted edits to existing files are `required`
   member initializers on existing DTO constructions that fail to compile once a new required member is
   added (PR4 only) — count the actual broken sites by compiling, per design.md's file-changes table.
   If compiling surfaces MORE sites than the design lists, STOP and report before editing anything.
5. **PR3 changes no output at all.** It is a refactor: `FtmoBreachSimulationReadService.cs`'s guard
   chain and projection move into `FtmoSimulationInputs.cs` verbatim, with no new consumer. Proof =
   golden pin, challenge-race PR1 snapshot pin, and full suite, all green and unedited.
6. **A test that cannot fail is a defect.** The falsification steps below are mandatory, not optional:
   Bug A (0.2.4), Bug B (0.3.4), the no-leak straddling trade (2.5.x / no-leak task), the three-phase
   rank order (Merge3 tie-breaks), nearest-rank with n = 0 and n = 1 (Phase 2), and the `FirstBreach`
   extraction equivalence (Phase 1.2).
7. **The benchmark (PR1, task 1.1) is a design gate, not a CI assertion.** Deterministic synthetic
   fixture shaped like the real gold series (~1,000 trades 2016–2025, H1 holds of 1–72h, 5 zero-duration,
   no overlap, profiles "fast" and "never"); Release build; median of 3; the timing assertion is a
   `BenchmarkFact` **skipped unless `FTMO_BENCH=1`** so it can never pass vacuously. Gate: *never*
   profile > 5s per request → build the cached-open-day optimisation inside the race's own helpers
   (never inside `FtmoBreachEvaluator.cs`), proven by an equivalence property test against
   `FtmoReplayCalendar.ElapsedDays` at every close-group cutoff of both fixtures, plus `RunChain`
   equality on every start. If still > 5s after that, STOP and escalate — do not silently ship async.
8. **No banned survival wording anywhere in this capability's output** ("passed", "safe", "survived",
   "would have passed", or an equivalent affirmation) — extend the existing no-pass-wording test to
   every new DTO, enum (including member names), and the disclosure constant (PR4 task).
9. **Process safety**: never kill a process; the user's API may run from `bin\Debug` and lock DLLs —
   every `dotnet build`/`dotnet test` in these gates uses `-p:BaseOutputPath=bin-scratch/`, and every
   `bin-scratch` directory is deleted after each PR's gate phase completes.
10. **No web change, no database access, no migration** in any PR.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| PR0 estimated lines | prod ~20, tests ~100, floor ~120 |
| PR1 estimated lines | prod ~190, tests ~410, floor ~600 |
| PR2 estimated lines | floor ~250 |
| PR3 estimated lines | floor ~180 (refactor only, no new consumer) |
| PR4 estimated lines | prod ~330, tests ~370, floor ~700 |
| 400-line budget risk | High (PR1, PR4), both `size:exception` — do not trim scope |
| Chained PRs | Yes — PR0 → PR1 → PR2 → PR3 → PR4, per design.md |
| Delivery strategy | ask-on-risk (default), `size:exception` for PR1 and PR4 |

---

## PR0 — Race bug fixes (SHIPPED `FtmoChallengeRace.cs`, ahead of the new multi-start work)

### Phase 0.0 — Reconciliation read (proves hard rule 3 before writing any RED)

- [x] 0.0.1 Read every existing `FirstTargetTouchSourceClose`/`MinTradingDaysMetFtmoDay` assertion in
  `FtmoChallengeRaceTests.cs` (lines ~74, 158, 259, 326, 378–379) and confirm each is on a
  `TargetReachedFirst` or `NotStarted` phase, none on a post-breach touch on a `BreachedFirst` phase.
  Read `PhaseTwoStartsAtTheNextTradeOpenedAfterThePhaseOneDecision` and confirm it uses no straddling
  row (`Open < T`, `Close > T`). Record the confirmation inline (comment or commit note); if either
  check fails, STOP and report before writing any RED test.
  _Satisfies: design.md Decision 0's own reconciliation; hard rule 3._
- [x] 0.0.2 Confirm the shipped `FtmoChallengeRaceTests`/`FtmoChallengeRaceFxMergeTests` suite and the
  golden pin are GREEN on unmodified code, before any PR0 production change.

### Phase 0.1 — Bug A: no post-breach touch/day-minimum readout on `BreachedFirst`

- [x] 0.1.1 RED: `FtmoChallengeRaceTests.ABreachedPhaseWhoseBalanceLaterTouchesTheTarget_ReportsNoPostBreachTouch` —
  a phase whose account balance crosses the target percentage and meets the day-4 minimum only on a
  close AFTER the phase's breach close; asserts outcome `BreachedFirst` (unchanged) and both
  `FirstTargetTouchSourceClose` and `MinTradingDaysMetFtmoDay` are null.
  _Satisfies: ftmo-challenge-race spec.md "A breached phase whose balance later touches the target
  reports no post-breach touch" scenario._
- [x] 0.1.2 RED: `..._ABreachedPhaseWhoseBalanceTouchedTheTargetBeforeTheBreach_StillReportsTheTouch` —
  a phase whose balance touches the target percentage on a close strictly before the phase's breach
  close; asserts outcome `BreachedFirst` and `FirstTargetTouchSourceClose` still reports that earlier,
  pre-breach touch unchanged.
  _Satisfies: ftmo-challenge-race spec.md "A breached phase whose balance touched the target before the
  breach still reports the touch" scenario._
- [x] 0.1.3 GREEN: in `RunPhase`, on the `BreachedFirst` return path, null `FirstTargetTouchSourceClose`
  and `MinTradingDaysMetFtmoDay` unless each occurred at or before `breachPoint.Value.SourceTime`.
  `Outcome`, `OutcomeSourceClose`, `BreachLimit`, `BreachPointClass` are unaffected — do not touch them.
  Confirm 0.1.1–0.1.2 pass.
  _Satisfies: ftmo-challenge-race spec.md "Every Non-Refused Race Reports Full Per-Phase Timing And The
  Fixed Rules" requirement, its new sentence on `BreachedFirst`; design.md Decision 0, bug A._
- [x] 0.1.4 Falsification (mandatory, hard rule 6): confirm 0.1.1 fails against the CURRENT (pre-fix)
  `RunPhase` (i.e. run it before 0.1.3's edit and observe RED), and confirm restoring the unconditional
  readout (temporarily revert 0.1.3) makes 0.1.1 fail again.

### Phase 0.2 — Bug B: handover requires `Open ≥ T AND Close > T`

- [x] 0.2.1 RED: `..._AnUnscalableRowStraddlingTheDecisionClose_DoesNotEnterPhaseTwo` — an `Unscalable`
  trade whose `Open` is before phase 1's target-deciding close T and whose `Close` is strictly after T,
  with no other trade opening in that gap; asserts that row is excluded from phase 2's subset, and
  phase 2's anchor is the first trade with `Open >= T`, not the straddling row.
  _Satisfies: ftmo-challenge-race spec.md "An Unscalable row straddling the phase-1 decision close does
  not enter phase 2" scenario._
- [x] 0.2.2 RED: `..._AZeroDurationRowAtExactlyT_StaysInPhaseOnesGroup` — a row whose `Open` and
  `Close` both equal T; asserts it belongs to phase 1's close group at T, not phase 2's subset.
  _Satisfies: ftmo-challenge-race spec.md handover requirement, zero-duration-row clause._
- [x] 0.2.3 GREEN: in `RunChain`, change the phase-2 subset filter from `Close > T` alone to `Open >= T
  AND Close > T`. Confirm 0.2.1–0.2.2 pass, and confirm the existing
  `PhaseTwoStartsAtTheNextTradeOpenedAfterThePhaseOneDecision` test still passes unedited.
  _Satisfies: ftmo-challenge-race spec.md "Phase Two Starts Fresh At The First Trade Opened After The
  Phase-One Target Close" requirement, straddling-row clause; design.md Decision 0, bug B._
- [x] 0.2.4 Falsification (mandatory, hard rule 6): confirm 0.2.1 fails against the CURRENT (pre-fix)
  `Close > T`-only filter, and confirm re-relaxing the filter (temporarily revert 0.2.3) makes 0.2.1
  fail again.

### Phase 0.3 — `FtmoOpenPositionSweep` doc-comment fix (no behaviour change)

- [x] 0.3.1 GREEN: edit the class doc-comment on `FtmoOpenPositionSweep` to stop describing `OpenAt` as
  "an O(n log n) sweep" (it is a single `foreach`, O(n), with no sort of its own and no ordering
  precondition per its own method doc). Comment-only; confirm no test file changes and no assertion
  changes anywhere.
  _Satisfies: design.md File Changes table, `FtmoOpenPositionSweep.cs` row._

### Phase 0.4 — PR0 gates

- [x] 0.4.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror -p:BaseOutputPath=bin-scratch/ > build.log 2>&1` — zero warnings.
- [x] 0.4.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log 2>&1` — no diffs.
- [x] 0.4.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=bin-scratch/ > test.log 2>&1`
  (full suite, once) — confirm the **937 pre-existing tests** all still pass with **0 existing
  assertions edited**, plus every new PR0 test passes.
- [x] 0.4.4 Confirm the golden pin and the challenge-race PR1 snapshot pin are both present in the final
  green run and unedited.
- [x] 0.4.5 Delete `bin-scratch/`.
- [ ] 0.4.6 If any pre-existing test fails, or a warning/format diff appears, or any existing assertion
  needed editing, STOP and investigate before patching.

---

## PR1 — Benchmark gate, `FirstBreach` extraction, funded phase, three-phase merge

Do not start PR1 until PR0's Phase 0.4 gates are all green. `size:exception` — do not trim scope.

### Phase 1.1 — Benchmark gate (design gate, task 1 — must run before any new production logic)

- [x] 1.1.1 GREEN: build the deterministic synthetic fixture: ~1,000 trades from 2016-01 to 2025-12
  (~8/month), H1 holds of 1–72h, 5 zero-duration trades, no overlaps, two P/L profiles ("fast": targets
  in ~30–50 days; "never": no start ever reaches +10%). Write fixture-shape RED tests first (count,
  month span, zero-duration count, no-overlap invariant) against the fixture builder.
  _Satisfies: design.md Decision 1, task 1; hard rule 7._
  _Apply note: `FtmoMultiStartBenchmarkFixture.cs` (1,000 rows, evenly spaced ~87.7h apart across
  2016-01-01..2025-12-31, 5 zero-duration rows at indexes 100/300/500/700/900, "Fast" = +1%/trade net,
  "Never" = 0 net). RED confirmed first (13 shape tests failed to compile — fixture did not exist),
  then GREEN (13/13 pass) in `FtmoMultiStartBenchmarkFixtureTests.cs`._
- [x] 1.1.2 GREEN: `[BenchmarkFact]` (skipped unless `FTMO_BENCH=1`) running, per start, `RunChain(suffix)`
  plus `Evaluate(suffix)` at 2 FX ends, for 2 runs, against the PR0-fixed `RunChain`, in Release, median
  of 3. Gate: *never* profile > 5s per request.
  _Satisfies: design.md Decision 1; hard rule 7._
  _Apply note: `BenchmarkFactAttribute.cs` (runtime `Skip` getter reading `FTMO_BENCH`) +
  `FtmoMultiStartBenchmarkTests.cs`. Confirmed skipped without `FTMO_BENCH=1` (2/2 Skipped). First
  measurement (pre-optimisation, Release, `FTMO_BENCH=1`, median of 3): "never" profile = **1m 36.96s**
  (gate FAILED, >> 5s); "fast" profile test did not get to run — the test host process crashed after
  the "never" test's 96s+ single run exceeded the harness's own patience for the remaining runs._
- [x] 1.1.3 Record the benchmark result (pass/fail against the 5s gate) inline in this file's PR1 section
  or a commit note. If the gate fails, proceed to 1.1.4; otherwise skip 1.1.4 and its property test.
  _Apply note: gate FAILED on first measurement (1m 36.96s >> 5s on the "never" profile) → proceeded to
  1.1.4._
- [x] 1.1.4 (Conditional — only if 1.1.2's gate fails) GREEN: build the cached-open-day optimisation
  inside the race's own helpers (a race-private `ElapsedDays` equivalent that memoises each trade's
  open-day attribution once per `RunPhase`), proven by a property test asserting it equals
  `FtmoReplayCalendar.ElapsedDays` at every close-group cutoff of both fixture profiles, plus `RunChain`
  equality on every start against the unoptimised implementation. If still > 5s after this, STOP and
  escalate (async is a new decision outside this change's scope).
  _Satisfies: design.md Decision 1's optimisation clause; hard rule 7._
  _Apply note: built `FtmoChallengeRace.CachedOpenDays` (private nested struct, race-private, does NOT
  touch `FtmoBreachEvaluator.cs`) — memoises each trade's own-open `FtmoDayClock.Attribute(...).BookkeepingDay`
  ONCE per `RunPhase` call instead of re-attributing it on every close-group cutoff (the O(n)
  TimeZoneInfo conversion, done n times, was the dominant cost). `RunPhase`'s 3
  `FtmoReplayCalendar.ElapsedDays` call sites now call the private `ElapsedDaysCached` helper against
  this cache instead. Equivalence proven in
  `FtmoChallengeRaceElapsedDaysCacheEquivalenceTests.cs` (4/4 pass, ~59s): (a) the cache's trading-day
  count equals `FtmoReplayCalendar.ElapsedDays` at every distinct close-group cutoff of both fixture
  profiles; (b) `RunChain` on every monthly start of both profiles is byte-identical against an
  unoptimised reference reimplementation (a verbatim behavioural copy of the pre-cache scanner, kept
  test-local). Re-measured (Release, `FTMO_BENCH=1`, median of 3): "never" profile = **5.53s** (still
  marginally > 5s — gate FAILS); "fast" profile = **PASSED** (well under 5s). Full existing
  `FtmoChallengeRaceTests`/`FtmoChallengeRaceFxMergeTests` suite and the golden pin stayed green,
  unedited (243 passed, 2 skipped [the benchmark facts], 0 failed), confirming the cache changed no
  output. Per this task's own escalation clause, **STOPPING here rather than optimising further**: the
  cache reduced runtime by ~94.3% (96.96s → 5.53s) but the "never" profile still exceeds the 5s ceiling
  by ~0.53s. Any further optimisation (e.g. avoiding the `O(n)` HashSet rebuild per close-group, or an
  async/background execution model) is a new design decision outside this task's scope — escalating to
  the user/orchestrator rather than silently extending the fix._

### Phase 1.2 — `FirstBreach` extraction (verbatim move)

- [x] 1.2.1 RED: `FtmoChallengeRaceFirstBreachTests` — 4 branches (daily only, max only, both same
  close, both different close with the earlier-by-(SourceTime, RowIndex) winning) against
  `FtmoChallengeRace.FirstBreach(evaluation) -> (BreachPoint, FtmoFirstBreachingLimit)?`, which does not
  exist yet.
  _Satisfies: design.md Decision 2._
  _Apply note: RED confirmed — build failed with 5×`CS0117 'FtmoChallengeRace' does not contain a
  definition for 'FirstBreach'` before the extraction existed._
- [x] 1.2.2 GREEN: extract lines 118–147 of the shipped `RunPhase` (the breach-limit selection block)
  verbatim into `FirstBreach`, and call it from `RunPhase`. Confirm 1.2.1 passes, and confirm the
  shipped `FtmoChallengeRaceTests`/`FtmoChallengeRaceFxMergeTests` suite and the golden pin remain green,
  unedited.
  _Satisfies: design.md Decision 2; hard rule 6 (extraction equivalence)._
  _Apply note: GREEN — 96/96 passed (`FtmoChallengeRaceFirstBreachTests` +
  `FtmoChallengeRaceTests`/`FtmoChallengeRaceFxMergeTests`/golden pin/service tests), 0 existing
  assertions edited. Falsification: swapped the tie-break comparator's `< 0` to `> 0`; the new
  `BothAtDifferentCloses_TheEarlierByTimeThenRowIndexWins` test went RED (1 failed); restored, re-ran
  green (38/38 in the race/FirstBreach/FX-merge slice)._

### Phase 1.3 — Funded phase (`FtmoFundedPhase.cs`, new file)

- [x] 1.3.1 RED: `FtmoFundedPhaseTests.AFundedPhaseThatBreaches_ReportsBreachedFirstFromBothOrigins` —
  a funded phase (fresh account, Initial Capital, same loss limits, no target, no day minimum) that
  breaches 40 days after its own start on a chain whose phase 1 began 95 days earlier; asserts outcome
  `BreachedFirst`, 40 elapsed days from the funded start, and 135 elapsed days from the chain start.
  _Satisfies: ftmo-multi-start spec.md "Funded elapsed days are reported in both bases" scenario;
  design.md Decision 3._
- [x] 1.3.2 RED: `..._AFundedPhaseWithNoBreachByEndOfData_ReportsNoBreachAtEndOfData` — a funded phase
  whose replayed data ends with no loss-limit breach; asserts outcome `NoBreachByEndOfData`.
  _Satisfies: ftmo-multi-start spec.md phase 3 (funded) requirement._
- [x] 1.3.3 RED: `..._AFundedPhaseWithNoTradesLeft_ReportsNoBreachByEndOfDataWithZeroRunway` — phase 2
  reaches its target on the last replayed close, with no trade opening afterwards; asserts the funded
  phase outcome is `NoBreachByEndOfData` and its runway is 0.
  _Satisfies: ftmo-multi-start spec.md "A funded phase with no trades left reports NoBreachByEndOfData
  with zero runway" scenario._
- [x] 1.3.4 RED: `..._APhaseTwoThatDoesNotReachItsTarget_LeavesTheFundedPhaseNotStarted` — phase 2
  outcome is not `TargetReachedFirst`; asserts the funded phase outcome is `NotStarted`.
  _Satisfies: ftmo-multi-start spec.md "Funded phase is NotStarted when phase 2 does not reach its
  target" scenario._
- [x] 1.3.5 RED: `..._TheFundedDayOneFloorResetsFromInitialCapital` — asserts the funded phase's day-1
  floor is computed from Initial Capital exactly as phase 1's day 1 was, not carried over from phase 2's
  ending balance.
  _Satisfies: ftmo-multi-start spec.md "A funded phase starts fresh after phase 2's target" scenario._
- [x] 1.3.6 GREEN: implement `FtmoFundedPhase.Run`: build the subset `Open >= T2 AND Close > T2` (the
  same PR0-fixed handover rule as `RunChain`), call `Evaluate` on it with capital reset to Initial
  Capital, same loss limits, then `FirstBreach` (1.2's extraction) for the outcome; an empty subset
  produces `NoBreachByEndOfData` with runway 0. Compute elapsed days from both the funded anchor and
  from this start's own phase-1 anchor. Confirm 1.3.1–1.3.5 pass.
  _Satisfies: design.md Decision 3; ftmo-multi-start spec.md "Each Start Runs Phase 1, Phase 2, Then A
  Funded Phase" and "Funded Duration Is Reported From Both The Funded Start And The Chain Start"
  requirements._
  _Apply note: RED confirmed first (build failed, `CS0103 'FtmoFundedPhase' does not exist` × 5)
  because `FtmoFundedPhase.cs` and `Domain/Enums/FtmoFundedOutcome.cs` did not exist yet. GREEN on
  first implementation: all 5 new tests passed immediately (5/5). Falsification (hard rule 6): broke
  the capital reset (`capital * 0.5m` instead of `capital`) — `TheFundedDayOneFloorResetsFromInitialCapital`
  went RED (`BreachedFirst` instead of the expected `NoBreachByEndOfData`), confirming the test actually
  exercises the reset; restored, re-ran green (5/5). Full suite: 968 passed / 2 skipped (958 baseline +
  10 new: 5 `FirstBreach` + 5 funded-phase tests), 0 existing assertions edited. Golden pin
  (`FtmoBreachSimulationReadServiceGoldenPinTests`) and the challenge-race PR1 snapshot pin (inside
  `FtmoBreachSimulationReadServiceTests.cs`) both green, unedited, in the same full-suite run.
  **Deviation from design.md's File Changes table** (noted per apply-progress rules, not silently):
  created `Domain/Enums/FtmoFundedOutcome.cs` in PR1 instead of PR4, because `FtmoFundedPhase.Run`'s own
  outcome (task 1.3, PR1) needs the enum to compile; the type is pure with no PR4 DTO/service
  dependency, so pulling only this one enum forward changes no PR4 file count or scope — flagged for
  the user/orchestrator to confirm before PR4 starts._

_Apply follow-up (option A, requested after the initial PR1 gate run above still failed the "never"
profile by ~0.53s): the benchmark's own call shape had a second, redundant cost the Fenwick cache did not
touch — the test called `Evaluate(suffix)` a second time on the EXACT SAME series `RunChain`'s own
phase-1 `RunPhase` already evaluates internally (480 of 960 "never" calls, 480 of 1,436 "fast" calls),
and `CachedOpenDays`' own-open attribution was rebuilt once per phase AND once per simulated FX end even
though it is FX-independent (verified in `FtmoTradeProjector.Project`: `Net is null`/`OpenSource` depend
only on `trade.Size`/`trade.OpenTime`, never on `fxRate`)._
_Implemented: `FtmoChallengeRace.RunChain`/`RunPhase`/top-level `Evaluate` gained optional
`precomputedPhase1Evaluation`/`precomputedOpenDayAttribution` parameters (default `null`, so every
existing caller is unchanged); a new `FtmoChallengeRace.AttributeOpenDays` computes the own-open day
once per chain (shared across both FX ends, keyed by `RowIndex`); `CachedOpenDays` gained an optional
precomputed-attribution constructor parameter. `FtmoBreachSimulationReadService.SimulateRun` now passes
its own already-computed `lowEvaluation`/`highEvaluation` into `FtmoChallengeRace.Evaluate`. The
benchmark test now mirrors the PR4 production call shape: one `Evaluate` per simulated FX end, passed
into `RunChain`, plus one shared `AttributeOpenDays` per suffix — no separate redundant `Evaluate` call._
_Proof (hard rule 6): `FtmoChallengeRaceSharedEvaluationTests.cs` (new) — an equivalence theory (both
fixture profiles, 5 starts each) proving the shared-parameter path produces `BeEquivalentTo`-identical
`ChainResult`s to the unshared path; and a falsification fact proving a precomputed evaluation of a
DIFFERENT (deliberately breaching) series changes the phase-1 outcome (`BreachedFirst` instead of the
correct `TargetReachedFirst`) — the parameter is demonstrably consumed, not silently ignored. 3/3 pass._
_Re-measured (Release, `FTMO_BENCH=1`, 3 separate outer test-process runs, `find.exe` NOT running in any
of them): both `NeverProfile_...`/`FastProfile_...` facts PASSED the 5s gate in all 3 runs (test-host
wall time ~4s per run for BOTH facts together, each fact's own internal median-of-3 assertion green).
Full suite (Debug, no `FTMO_BENCH`): **1003 passed / 2 skipped** (1000 baseline + 3 new shared-evaluation
tests), 0 existing assertions edited, 42s. Golden pin and the challenge-race PR1 snapshot pin both
independently re-confirmed green, unedited, via filtered re-runs. `dotnet format --verify-no-changes`
clean. `dotnet build -warnaserror`: 0 warnings, 0 errors. All 5 `bin-scratch` directories deleted and
confirmed absent via `find . -type d -name bin-scratch`. No process was killed._

### Phase 1.4 — Chain classification (`FtmoMultiStartChain.cs`, `Classify`)

- [x] 1.4.1 RED: six fixtures, one per chain outcome (`Phase1Breached`, `Phase1UndecidedAtEndOfData`,
  `Phase2Breached`, `Phase2UndecidedAtEndOfData`, `FundedBreached`, `FundedNoBreachAtEndOfData`); asserts
  each fixture's chain classifies to its intended outcome, none misclassified into a neighbouring one.
  _Satisfies: ftmo-multi-start spec.md "Each of the six chain outcomes is produced by some fixture"
  scenario._
  _Apply note: RED confirmed — build failed (`CS0234`/missing type) before `FtmoMultiStartChain.cs`
  existed. `FtmoMultiStartChainTests.EachOfTheSixChainOutcomes_ClassifiesToItsIntendedOutcome` (Theory,
  6 cases via `EachChainOutcome()` MemberData)._
- [x] 1.4.2 GREEN: implement `Classify(phase1, phase2, funded) -> FtmoChainOutcome`, mapping
  `BreachedFirst`/`NeitherByEndOfData` (renamed `UndecidedAtEndOfData` at this API surface per the new
  enum) per phase to its named outcome. Confirm 1.4.1 passes.
  _Satisfies: ftmo-multi-start spec.md "Six Chain Outcomes, Three Right-Censored And Labelled As Such"
  requirement._
  _Apply note: GREEN on first implementation, 6/6 pass. Falsification (hard rule 6 style, per apply
  instructions): changed the `BreachedFirst` branch to return `Phase1UndecidedAtEndOfData`; 2 tests went
  RED (`EachOfTheSixChainOutcomes_...(Phase1Breached case)` and `EveryStartLandsInExactlyOneOfTheSixOutcomes`,
  which dropped from 6 to 5 distinct outcomes); restored, re-ran green (8/8 in the chain slice)._
- [x] 1.4.3 RED: `..._EveryStartLandsInExactlyOneOfTheSixOutcomes` — a mixed fixture set; asserts the
  six outcome counts sum to the total start count, with no start uncounted or double-counted.
  _Satisfies: ftmo-multi-start spec.md "Every start lands in exactly one of the six outcomes" scenario._
  _Apply note: RED confirmed alongside 1.4.1 (same missing-type build failure); GREEN with 1.4.2's
  implementation._

### Phase 1.5 — Three-phase FX merge (`Merge3`)

- [x] 1.5.1 RED: `FtmoMultiStartMergeTests.AnEarlierFundedBreach_IsLessFavourableBetweenFxEnds` — two
  FX ends that both reach both targets, one then breaching funded earlier than the other; asserts the
  earlier funded breach is reported as the less favourable chain.
  _Satisfies: ftmo-multi-start spec.md "An earlier funded breach is less favourable between FX ends"
  scenario._
  _Apply note: RED confirmed — build failed (`CS0234`/missing type) before `FtmoMultiStartChain.Merge3`
  existed._
- [x] 1.5.2 RED: `..._ADegenerateBand_EvaluatesOneEndOnly` — a USD-settling symbol where `fxLow ==
  fxHigh`; asserts only one FX end is evaluated and the result is reported as `BothEnds`, not sensitive.
  _Satisfies: ftmo-multi-start spec.md "A degenerate band evaluates one end only" scenario; design.md
  Decision 6._
- [x] 1.5.3 RED: every rank pair (0–5) and every tie-break, exercising ranks 0/2/4 (earlier close wins)
  and 1/3/5 (fxLow reported, tagged) per design.md Decision 5's rank table
  `P1B 0 < P1N 1 < P2B 2 < P2N 3 < FB 4 < FN 5`.
  _Satisfies: ftmo-multi-start spec.md "The FX Whole-Chain Rule Extends To Three Phases" requirement._
  _Apply note: rank 1 (`Phase1UndecidedAtEndOfData`) never carries a deciding close in production, so
  two rank-1 fixtures are indistinguishable under the identical-row check and resolve as `BothEnds` —
  same limitation the shipped two-phase `MergeEnds` has at its own rank 1; covered by a dedicated fact
  (`RankOne_TwoUndecidedPhase1ChainsAreIndistinguishable_ResolveAsBothEnds`) instead of the fxLow-tagged
  theory, which now covers ranks 3 and 5 only._
- [x] 1.5.4 RED: `..._ScopedMergeEndsPin` — the three-phase merge's phase-1 and phase-2 outputs equal
  the shipped `MergeEnds` on the same inputs ONLY when the less favourable end's chain resolves at or
  before `Phase2Undecided` (rank ≤ 3); when both FX ends reach the funded phase, asserts the two-phase
  `MergeEnds` result is NOT reproduced (the funded outcome decides instead). **Falsification-bearing
  (hard rule 6):** construct one fixture in each half (rank ≤ 3 vs. both ends funded) and confirm each
  half's assertion direction actually distinguishes them — a version that always compares equal must go
  RED on the "both ends funded" half.
  _Satisfies: design.md Decision 5's pin, corrected ("equality is conditional, not unconditional")._
  _Apply note: implemented as `ScopedMergeEndsPin_EqualsMergeEndsOnlyWhenNeitherEndReachesFunded`, one
  fixture pair per half within the same test._
- [x] 1.5.5 GREEN: implement `Merge3` per design.md Decision 5: `Rank` extended to 6 values, degenerate
  band shortcut, tie-break by earlier close (breach ranks) or fxLow-tagged (undecided/no-breach ranks),
  identical-row → `BothEnds`. This is NEW code — the shipped two-phase `MergeEnds` is not edited. Confirm
  1.5.1–1.5.4 pass.
  _Satisfies: design.md Decision 5/6._
  _Apply note: `FtmoMultiStartChain.cs` created (Decision.md's "Chain3, Classify, Merge3" row), housing
  `ChainResult3`, `Classify`, and `Merge3` together with `Rank3`. GREEN on first implementation after one
  test-fixture correction (rank 1's identical-row limitation, above): 20/20 pass across
  `FtmoMultiStartChainTests` + `FtmoMultiStartMergeTests`. Falsification (hard rule 6): flipped the
  breach-rank tie-break comparator (`lowClose < highClose` → `lowClose > highClose`); 5 tests went RED
  (`AnEarlierFundedBreach_...`, `BreachRanks_...` ×3, `ScopedMergeEndsPin_...`); restored, re-ran green
  (20/20). Full suite: 988 passed / 2 skipped (968 baseline + 20 new: 8 Classify + 12 Merge3), 0 existing
  assertions edited. Golden pin and the challenge-race PR1 snapshot pin both green, unedited, in the same
  full-suite run._

### Phase 1.6 — PR1 gates

- [x] 1.6.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror -p:BaseOutputPath=bin-scratch/ > build.log 2>&1` — zero warnings.
- [x] 1.6.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log 2>&1` — no diffs.
- [x] 1.6.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=bin-scratch/ > test.log 2>&1`
  (full suite, once) — confirm all pre-PR0 tests, PR0's new tests, and every new PR1 test pass, with
  **0 existing assertions edited**. 1000 passed / 2 skipped (999 baseline + 1 new equivalence test).
- [x] 1.6.4 Confirm the golden pin and the challenge-race PR1 snapshot pin are both present in the final
  green run and unedited.
- [x] 1.6.5 Delete `bin-scratch/`.
- [x] 1.6.6 If any pre-existing test fails, a warning/format diff appears, or the benchmark gate (1.1)
  was skipped without recording a result, STOP and investigate before patching. `FTMO_BENCH=1` run,
  Release, both profiles green under the 5s gate (task 1.1.4's Fenwick-backed incremental cache; see
  `FtmoChallengeRace.CachedOpenDays`).
  _Apply follow-up: the Fenwick cache alone left the "never" profile marginally over gate (5.53s). Option
  A (shared phase-1 evaluation + chain-wide, FX-shared open-day attribution — see 1.1.4's follow-up note
  above) closed the remaining gap; both profiles now pass reliably (3 separate outer runs, ~4s test-host
  wall time each, `find.exe` not running)._

---

## PR2 — Start enumeration and order statistics

Do not start PR2 until PR1's Phase 1.6 gates are all green.

### Phase 2.1 — `FtmoStartEnumerator.cs` (new file)

- [x] 2.1.1 RED: `FtmoStartEnumeratorTests.AMonthWithNoScalableOpen_IsCountedNotSkipped` — a month with
  zero scalable trade opens; asserts no start is produced for that month, it is reported in
  `MonthsWithoutStart`, and the month is neither dropped from the reported range nor treated as an
  implicit start.
  _Satisfies: ftmo-multi-start spec.md "A month with no scalable open is counted, not skipped"
  scenario._
  _Apply note: RED confirmed — build failed with `CS0103 'FtmoStartEnumerator' does not exist` before
  the file existed (24 compile errors across both new test files, same build)._
- [x] 2.1.2 RED: `..._ASameInstantTie_GoesToTheLowerRowIndex` — two trades opening at the same instant
  in the same month; asserts the enumerated start uses the lower `RowIndex`.
  _Satisfies: design.md Data Flow, `EnumerateStarts(low)` determinism._
- [x] 2.1.3 RED: `..._MonthBoundaryOnBerlinDay` — a trade opening at a Berlin-day instant that straddles
  a calendar-month boundary in the source zone; asserts the month is attributed by Berlin day, not
  source-zone day.
  _Satisfies: ftmo-multi-start spec.md "Starts Are Enumerated At Monthly Grain From The Data"
  requirement (FTMO/Berlin calendar month)._
- [x] 2.1.4 RED: `..._AnUnscalableOnlyMonth_ProducesNoStart` — a month whose only opens are
  `Unscalable`; asserts no start is produced and the month is reported in `MonthsWithoutStart`.
  _Satisfies: ftmo-multi-start spec.md "Starts Are Enumerated At Monthly Grain From The Data"
  requirement (non-`Unscalable` clause)._
- [x] 2.1.5 RED: `..._WeeklyOrEveryTradeGrainIsNotImplemented` — asserts the enumerator's start count
  matches the monthly grain only, not a hypothetical weekly or every-trade enumeration on the same
  fixture.
  _Satisfies: ftmo-multi-start spec.md "Weekly or every-trade grain is not implemented" scenario;
  design.md/proposal.md Decision D1._
- [x] 2.1.6 GREEN: implement `FtmoStartEnumerator.Enumerate(projected) -> (starts, monthsWithoutStart)`:
  group by FTMO (Berlin) calendar month from the first month containing a scalable open to the last;
  per month, the first scalable trade ordered by `(Open, RowIndex)`; months with none go to
  `MonthsWithoutStart`. Confirm 2.1.1–2.1.5 pass.
  _Satisfies: ftmo-multi-start spec.md "Starts Are Enumerated At Monthly Grain From The Data"
  requirement; design.md/proposal.md Decision D1._
  _Apply note: GREEN on first implementation, 5/5 pass in the enumerator slice (11/11 across the full
  PR2 slice with order statistics)._

### Phase 2.2 — Start-series no-leak (slicing)

- [x] 2.2.1 RED: `..._ATradeStillOpenAtALaterStartsAnchor_DoesNotLeakIntoThatStartsReplay` — a trade
  opened at one start that remains open (by close time) at a later start's `startOpen`, using a
  straddling −US$5,000 trade per design.md Decision 4; asserts that trade is excluded from the later
  start's series (its `Open` is before that start's `startOpen`), and affects neither the later start's
  balance, overlap flag, flat-book check, anchor, nor trading-day count. **Falsification-bearing (hard
  rule 6):** confirm a deliberately-leaking slice (temporarily include trades with `Open <
  startOpen` that are still open) makes this test breach or block the target where the correct slice
  does not.
  _Satisfies: ftmo-multi-start spec.md "A trade still open at a later start's anchor does not leak into
  that start's replay" scenario; design.md Decision 4._
  _Apply note: implemented as `FtmoStartEnumerator.SliceFromStart`, per the tasks phase's own flagged
  item 3 (PR2's no-leak slicing has no separate file pinned by design.md's File Changes table — housed
  in `FtmoStartEnumerator.cs` alongside `Enumerate`, since it is a pure slicing helper). Falsification
  (hard rule 6): temporarily changed the `SkipWhile` predicate from `t.OpenSource < startOpen` to
  `t.CloseSource < startOpen` (the leaking variant, keeping trades still open at `startOpen`) — both
  `ATradeStillOpenAtALaterStartsAnchor_...` and `SliceFromStart_ExcludesTradesOpenedBeforeStartEven...`
  went RED (2 failed, the straddling −US$5,000 trade wrongly included); restored, re-ran green (2/2)._
- [x] 2.2.2 GREEN: implement the per-start slice as the projection sorted by `(Open, RowIndex)`, sliced
  from the first position with `Open >= startOpen`. Confirm 2.2.1 passes.
  _Satisfies: design.md Decision 4._
  _Apply note: GREEN on first implementation, 2/2 pass._

### Phase 2.3 — `FtmoOrderStatistics.cs` (nearest rank)

- [x] 2.3.1 RED: `FtmoOrderStatisticsTests.NObservations_Zero_EveryQuantileIsNull` — `n = 0`; asserts
  `N` is 0 and `Min`, `Q1`, `Median`, `Q3`, `Max` are all null.
  _Satisfies: ftmo-multi-start spec.md "Order statistics with zero observations report every quantile as
  null" scenario._
  _Apply note: RED confirmed — build failed with `CS0103 'FtmoOrderStatistics' does not exist` before
  the file existed. Falsification (hard rule 6): temporarily returned `new FtmoOrderStatisticsDto(0, 0,
  0, 0, 0, 0)` for `n = 0` instead of nulls — this test went RED (`Did not expect a value, but found
  0`); restored, re-ran green._
- [x] 2.3.2 RED: `..._NObservations_One_EveryQuantileEqualsThatValue` — `n = 1`; asserts `N` is 1 and
  every quantile equals that single observed value.
  _Satisfies: ftmo-multi-start spec.md "Order statistics with one observation report that value for
  every quantile" scenario._
  _Apply note: Falsification (hard rule 6): temporarily removed the `Math.Max(1, r)` clamp so `p = 0`
  computes `r = 0` — this test went RED (`ArgumentOutOfRangeException`, index -1); restored, re-ran
  green._
- [x] 2.3.3 RED: `..._NObservations_FourAndFive_NearestRankMatchesHandComputedValues` — n = 4 and n = 5
  fixtures with known sorted values; asserts each quantile equals the nearest-rank formula `r =
  ⌈p·n⌉` (1-based) hand-computed for that n.
  _Satisfies: ftmo-multi-start spec.md "Aggregates Are Counts, Shares, And Order Statistics" requirement;
  design.md Decision 7._
  _Apply note: implemented as two separate facts (`NObservations_Four_...` and
  `NObservations_Five_...`), one per n, both hand-computed per the design's `r = ceil(p*n)` formula._
- [x] 2.3.4 GREEN: implement `FtmoOrderStatistics.Compute(sortedInts) -> FtmoOrderStatisticsDto` using
  nearest rank, `r = ⌈p·n⌉` (1-based) on sorted ints; `n = 0` → every quantile null. Confirm
  2.3.1–2.3.3 pass.
  _Satisfies: design.md Decision 7._
  _Apply note: GREEN on first implementation, 4/4 pass (11/11 across the full PR2 slice). **Deviation
  from design.md's File Changes table** (flagged, not silent): `FtmoOrderStatisticsDto` is design.md's
  PR4 DTO group (`Application/DTOs/Backtests/FtmoMultiStartDto.cs`), but task 2.3.4 requires
  `Compute` to return exactly this shape. Per the apply instructions' own decision tree, pulled ONLY
  this one record forward into a new `Application/DTOs/Backtests/FtmoMultiStartDto.cs` file (the
  location the design names) — it is pure with no dependency on the PR4 service, DI, controller, or any
  sibling PR4 record, so this changes no PR4 file count or scope. Flagged in the file's own doc-comment
  for confirmation before PR4 starts (same precedent as PR1's `FtmoFundedOutcome` pull-forward)._

### Phase 2.4 — PR2 gates

- [x] 2.4.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror -p:BaseOutputPath=bin-scratch/ > build.log 2>&1` — zero warnings.
  _Apply note: 0 warnings, 0 errors._
- [x] 2.4.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log 2>&1` — no diffs.
  _Apply note: empty log, no diffs, exit 0._
- [x] 2.4.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=bin-scratch/ > test.log 2>&1`
  (full suite, once) — confirm all pre-PR1 tests plus every new PR2 test pass, with **0 existing
  assertions edited**.
  _Apply note: **999 passed / 2 skipped** (988 PR1 baseline + 11 new PR2 tests: 5 enumerator + 2
  slicing + 4 order-statistics), 0 existing assertions edited, 32s._
- [x] 2.4.4 Confirm the golden pin and the challenge-race PR1 snapshot pin are both present in the final
  green run and unedited.
  _Apply note: both included in the same full-suite run (`FtmoBreachSimulationReadServiceGoldenPinTests`
  and the challenge-race PR1 snapshot pin inside `FtmoBreachSimulationReadServiceTests.cs`); confirmed
  independently passing via a filtered re-run (`--filter FullyQualifiedName~GoldenPin`, 1/1 passed)._
- [x] 2.4.5 Delete `bin-scratch/`.
  _Apply note: all 5 `bin-scratch` directories deleted; confirmed absent via `find . -type d -name
  bin-scratch` (no output)._
- [ ] 2.4.6 If any pre-existing test fails or a warning/format diff appears, STOP and investigate before
  patching.
  _Apply note: not triggered — no pre-existing test failed, no format diff appeared._

---

## PR3 — Guard/projection extraction (refactor only, no output change)

Do not start PR3 until PR2's Phase 2.4 gates are all green.

### Phase 3.1 — Extraction

- [x] 3.1.1 Create `FtmoSimulationInputs.cs`. Move `ResolveSharedAsync` (runs, limits, spec, calibration,
  FX, grid, zones resolution) out of `FtmoBreachSimulationReadService.cs` verbatim, and `ProjectRun`
  (segments → normalizer → `RefuseRunInputs` → low/high projection and counts) verbatim. `RefuseAll`/
  `Refused`/evaluation stay in the shipped service, delegating to the new class. No new consumer in this
  PR.
  _Satisfies: design.md Decision 8._
  _Apply note: `FtmoSimulationInputs` created as `internal static class` (matches `FtmoTradeProjector`'s
  own accessibility — `ProjectedTrade` is `internal`, so a `public` consumer would not compile; CS0051
  caught this at build time). `ResolveSharedAsync` returns a `SharedResolution` record carrying `NoRuns`
  (the runs.Count==0 early-return path, preserved as its own flag so it isn't conflated with
  `Refusal is null`), `Refusal`, `Runs`, `DailyPct`/`MaxPct`, `Spec`, `SourceGrid`/`FtmoGrid`,
  `PointValue`, `FxBand`, `SourceZone`/`BerlinZone`, `ProfitTargetPct`. `ProjectRun` returns a
  `RunProjection` record; the shipped `EvaluateAtFx` bundled projection AND evaluation together, so
  `EvaluateAtFx`/`FxEvaluation` were removed and `FtmoBreachEvaluator.Evaluate` is now called directly
  in `SimulateRun` on `ProjectedLow`/`ProjectedHigh` from the extracted `ProjectRun` — same values, same
  order, split at the boundary design.md names (projection vs. evaluation) rather than the shipped
  method boundary. `SimulateAsync`/`SimulateRun` delegate to both; `RefuseAll`/`Refused` and all
  evaluation/merge/challenge-race logic stay in `FtmoBreachSimulationReadService.cs`, unedited in
  substance (only their inputs' origin changed from local variables to `resolution`/`projection`
  fields). No DI registration needed (static class, no interface)._
- [x] 3.1.2 Confirm this PR adds **0 new tests** — proof is the existing suite green, unedited, per
  design.md's Testing Strategy row 3 ("None new. Proof = existing suite green, 0 assertions edited").
  _Satisfies: hard rule 5._
  _Apply note: confirmed — no test file created or edited; only `FtmoBreachSimulationReadService.cs`
  was modified (production code) and `FtmoSimulationInputs.cs` was created (production code)._

### Phase 3.2 — PR3 gates

- [x] 3.2.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror -p:BaseOutputPath=bin-scratch/ > build.log 2>&1` — zero warnings.
  _Apply note: 0 Warning(s), 0 Error(s). (First attempt hit CS0051 inconsistent-accessibility on the
  `public` records against `internal FtmoTradeProjector.ProjectedTrade`; fixed by making
  `FtmoSimulationInputs` and its records/methods `internal`.)_
- [x] 3.2.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log 2>&1` — no diffs.
  _Apply note: empty log, exit 0._
- [x] 3.2.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=bin-scratch/ > test.log 2>&1`
  (full suite, once) — confirm every pre-PR3 test still passes, unedited, with **0 new tests and 0
  edited assertions**.
  _Apply note: **999 passed / 2 skipped** (exact PR2 baseline), 30s. 0 new tests, 0 edited assertions._
- [x] 3.2.4 Confirm the golden pin and the challenge-race PR1 snapshot pin are both present in the final
  green run and unedited.
  _Apply note: both included in the same full-suite run; golden pin independently re-confirmed via
  `--filter FullyQualifiedName~GoldenPin` (1/1 passed)._
- [x] 3.2.5 Delete `bin-scratch/`.
  _Apply note: all 5 `bin-scratch` directories deleted; confirmed absent via `find . -type d -name
  bin-scratch` (no output)._
- [x] 3.2.6 If ANY output differs from before this PR (not just tests — diff the DTO shapes/behaviour
  manually if in doubt), STOP and investigate before patching; this PR must change no output at all.
  _Apply note: not triggered — full suite green and unedited, golden pin and snapshot pin unedited,
  falsification (swapping `RunSegmentsDisagree`/`RiskNotEstimable` in `ProjectRun`) proved 2 tests go RED
  against the moved code and pass again once restored, confirming the extraction changed no output._

---

## PR4 — Service, DTO, endpoint

Do not start PR4 until PR3's Phase 3.2 gates are all green. `size:exception` — do not trim scope.

### Phase 4.1 — New enums (pure, no I/O)

- [ ] 4.1.1 GREEN: create `Domain/Enums/FtmoChainOutcome.cs` (`Phase1UndecidedAtEndOfData = 0`,
  `Phase1Breached`, `Phase2Breached`, `Phase2UndecidedAtEndOfData`, `FundedBreached`,
  `FundedNoBreachAtEndOfData`), `Domain/Enums/FtmoFundedOutcome.cs` (`NotStarted = 0`, `BreachedFirst`,
  `NoBreachByEndOfData`), `Domain/Enums/FtmoStartGrain.cs` (`Monthly = 0`). Zeros are non-optimistic per
  design.md's Interfaces/Contracts note. No banned wording in any member name (hard rule 8).
  _Satisfies: ftmo-multi-start spec.md "Six Chain Outcomes" requirement; design.md Interfaces/Contracts._

### Phase 4.2 — DTOs (`Application/DTOs/Backtests/FtmoMultiStartDto.cs`)

- [ ] 4.2.1 GREEN: create the DTO records exactly per design.md's Interfaces/Contracts:
  `FtmoMultiStartDto`, `FtmoMultiStartRunDto`, `FtmoMultiStartRowDto`, `FtmoFundedPhaseDto`,
  `FtmoOutcomeCountDto`, `FtmoOrderStatisticsDto`, `FtmoMultiStartSummaryDto`. No production logic beyond
  the shapes; confirm each field name matches the design's contract before wiring the service.
  _Satisfies: design.md Interfaces/Contracts, D9._

### Phase 4.3 — `IFtmoMultiStartReadService`, `FtmoMultiStartReadService.cs`

- [ ] 4.3.1 RED: `FtmoMultiStartReadServiceTests.EveryStartLandsInExactlyOneOfTheSixOutcomes` — a mixed
  fixture through the service; asserts the six outcome counts sum to the total start count, with no
  start uncounted or double-counted.
  _Satisfies: ftmo-multi-start spec.md "Every start lands in exactly one of the six outcomes" scenario,
  exercised end-to-end._
- [ ] 4.3.2 RED: `..._AggregateSharesIncludeOutcomesWithZeroOccurrences` — a run where `FundedBreached`
  never occurs; asserts it is reported with count 0 and share 0, not omitted.
  _Satisfies: ftmo-multi-start spec.md "Aggregate shares include outcomes with zero occurrences"
  scenario._
- [ ] 4.3.3 RED: `..._EveryOutcomesShareIsComputedOverTheFullStartCount` — a mix of censored and
  non-censored outcomes; asserts every outcome's share uses the total start count as denominator, and
  the six shares sum to 1.
  _Satisfies: ftmo-multi-start spec.md "Every outcome's share is computed over the full start count,
  censored or not" scenario._
- [ ] 4.3.4 RED: `..._ACensoredStartReportsItsRunwayNotAFabricatedCutoff` — a `FundedNoBreachAtEndOfData`
  start; asserts it reports the runway (days from phase start to last close), with no minimum-runway
  cutoff or Kaplan–Meier estimate applied.
  _Satisfies: ftmo-multi-start spec.md "A censored start reports its runway, not a fabricated cutoff"
  scenario._
- [ ] 4.3.5 RED: `..._StartOneEqualsTheSingleStartChallengeRaceWhenTheFirstTradeIsScalable` — a backtest
  whose first trade is scalable; asserts start 1's phases 1 and 2 equal the single-start
  `ChallengeRace`'s phases 1 and 2 on the same fixture, exercised THROUGH THE SERVICE.
  _Satisfies: ftmo-multi-start spec.md "The single-start endpoint is untouched" scenario; proposal.md
  Success Criteria._
- [ ] 4.3.6 RED: `..._AnUnscalableFirstTradeMakesStartOneDivergeFromTheAnchorDisclosed` — a backtest
  whose first trade is `Unscalable`; asserts start 1 and the single-start anchor differ, and the result
  discloses that divergence (`Start1DiffersFromSingleStartAnchor`).
  _Satisfies: ftmo-multi-start spec.md "An Unscalable first trade makes start 1 diverge from the anchor,
  disclosed" scenario._
- [ ] 4.3.7 RED: `..._TheSingleStartEndpointIsUntouched` — the shipped single-start endpoint's result on
  a fixture, captured BEFORE and AFTER this service exists; asserts byte-identical equality (a snapshot
  pin, written like the archived change's task 1.0.1).
  _Satisfies: ftmo-multi-start spec.md "The single-start endpoint is untouched" scenario; hard rule 2._
- [ ] 4.3.8 RED: `..._CancellationMidRunThrowsBeforeTheNextStart` — cancel a `CancellationToken` mid-run;
  asserts `ct.ThrowIfCancellationRequested()` is honoured before the next start, not silently ignored.
  _Satisfies: design.md Decision 9 (D10)._
- [ ] 4.3.9 GREEN: implement `IFtmoMultiStartReadService`/`FtmoMultiStartReadService`: per design.md's
  Data Flow — `FtmoSimulationInputs.ResolveSharedAsync` (refusal) → per run: `LoadTrades` → `ProjectRun`
  (refusal) → profit-target-mismatch check → `RaceRefusal`/`Starts []` → `EnumerateStarts(low)` → per
  start: slice low/high → `RunChain` + `Funded` (×1 or ×2 per FX degeneracy) → `Merge3` → `Classify` →
  row → `Summarize(rows)`. Synchronous, `ct.ThrowIfCancellationRequested()` before each start (Decision
  9). Confirm 4.3.1–4.3.8 pass, and confirm the golden pin and challenge-race PR1 snapshot pin are
  STILL green, unedited.
  _Satisfies: design.md Data Flow; ftmo-multi-start spec.md requirements 1–7 exercised end-to-end._

### Phase 4.4 — Disclosure and banned-wording coverage

- [ ] 4.4.1 RED: `..._DisclosureCoversNonIndependenceOptimismAndUnmodelledWithdrawals` — inspects any
  produced multi-start run's disclosure text; asserts it states starts are not independent trials, the
  figures are not probabilities, unmodelled swap and closed-trade replay understate breaches, and
  unmodelled funded withdrawals/Scaling Plan are an optimistic omission.
  _Satisfies: ftmo-multi-start spec.md "Every Run Discloses Non-Independence And Optimistic Bias"
  requirement._
- [ ] 4.4.2 RED: extend the existing no-pass-wording test to every enum type and label this capability
  produces (`FtmoChainOutcome`, `FtmoFundedOutcome`, `FtmoStartGrain`, `FtmoMultiStartDto` and every
  nested record, the disclosure constant); assert none contains "passed", "safe", "survived", "would
  have passed", or an equivalent affirmation.
  _Satisfies: ftmo-multi-start spec.md "No Survival Wording Anywhere In This Capability's Output"
  requirement; hard rule 8._
- [ ] 4.4.3 GREEN: implement the disclosure constant per design.md's "Disclosure" section, and the
  banned-wording sweep passing over all new types. Confirm 4.4.1–4.4.2 pass.
  _Satisfies: design.md "Disclosure" section._

### Phase 4.5 — DI, controller, endpoint

- [ ] 4.5.1 GREEN: register `IFtmoMultiStartReadService` in `DependencyInjection.cs`.
- [ ] 4.5.2 RED: `StrategyBacktestsControllerTests.MultiStart_ReturnsTheServiceResult` — a happy-path
  request through `GET api/strategies/{id}/ftmo-breach/multi-start`, same request shape as the shipped
  single-start endpoint; asserts the controller returns the service's DTO.
  _Satisfies: design.md Decision 8 (D8), placement._
- [ ] 4.5.3 RED: `..._MultiStart_InvalidRequestReturns400` — an invalid query (matching the shared query
  validation helper's existing rules); asserts 400, reusing the existing shared query validation helper
  rather than duplicating it.
  _Satisfies: design.md File Changes table, controller row ("shared query validation helper + action")._
- [ ] 4.5.4 GREEN: add the controller action and route, sharing the existing query-validation helper.
  Confirm 4.5.2–4.5.3 pass.
  _Satisfies: design.md Decision 8; File Changes table._
- [ ] 4.5.5 Run `dotnet build` after wiring and enumerate every construction site in
  `StrategyBacktestsControllerTests.cs` that fails to compile due to any new required member elsewhere in
  the DTO graph (if any). Design.md's file-changes table names exactly 1 file with an edit
  (`StrategyBacktestsControllerTests.cs`, "1 `CreateSut` edit"); if compiling surfaces more than that,
  STOP and report before editing anything (hard rule 4).
- [ ] 4.5.6 GREEN: apply the exactly 1 `CreateSut` edit named by design.md's file-changes table. No
  other edit to this file, and no edit to any other existing file's assertions.

### Phase 4.6 — PR4 gates

- [ ] 4.6.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror -p:BaseOutputPath=bin-scratch/ > build.log 2>&1` — zero warnings.
- [ ] 4.6.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log 2>&1` — no diffs.
- [ ] 4.6.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx -p:BaseOutputPath=bin-scratch/ > test.log 2>&1`
  (full suite, once) — confirm the **937 pre-existing tests** plus PR0–PR3's new tests plus every new
  PR4 test all pass, with **0 existing assertions edited** beyond the single `CreateSut` edit at 4.5.6.
- [ ] 4.6.4 Confirm the golden pin and the challenge-race PR1 snapshot pin are both present in the final
  green run and unedited.
- [ ] 4.6.5 Delete `bin-scratch/`.
- [ ] 4.6.6 If any pre-existing test fails, a warning/format diff appears, or more than 1 construction
  site needed editing, STOP and investigate before patching.

---

## Flagged: items too vague for a checkable task, or requiring a judgment call at apply time

1. **Task 1.1.4's cached-open-day optimisation is conditional** on the benchmark gate (1.1.2) actually
   failing at 5s on the *never* profile. Apply must run 1.1.2 first and record the real result before
   deciding whether 1.1.4 is in scope; this tasks phase cannot pre-determine which branch fires.
2. **Task 4.5.5's "exactly 1 `CreateSut` edit"** is design.md's own estimate from the file-changes
   table, not a compiler run performed during this tasks phase. Task 4.5.5 is the actual verification
   step; if it finds a different count, apply must stop and report rather than silently editing extra
   assertions.
3. **PR2's exact new-file boundary between `FtmoStartEnumerator.cs` and the no-leak slicing logic
   (Phase 2.2)** is not pinned by design.md as a separate file — design.md's File Changes table lists
   only `FtmoStartEnumerator.cs` and `FtmoOrderStatistics.cs` for PR2. Apply should place the no-leak
   slicing logic (Phase 2.2) inside `FtmoStartEnumerator.cs` or the PR4 service, whichever the actual
   call graph makes more natural, since it is a pure slicing helper rather than an enumeration concern
   — confirm at apply time and note the choice.
</content>
