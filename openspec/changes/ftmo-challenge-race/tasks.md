# Tasks: The FTMO 2-Step challenge race

Strict TDD throughout; every RED precedes its GREEN and every RED must be demonstrably able to fail
(no false-positive test is accepted — see the falsification checkpoints below). Two chained PRs, per
design.md ("Migration / Rollout"): **PR1** (`FtmoTradingDaysElapsed` recalculation, ~160-line floor)
lands and goes fully green first; **PR2** (the race itself, ~850-line floor, `size:exception` —
do not trim scope) starts only after PR1's gates are green. Read `.claude/conventions/backend-core.md`
and `backend-testing.md` before any `.cs` file. Every unit cites its spec requirement/scenario or
design decision.

## Hard rules — checkpoint at every task that touches these

1. **`FtmoBreachEvaluator.cs` is NOT edited in PR1 or PR2** (design Decision 1: `RunPhase` composes
   the unchanged evaluator; phase 1 calls `Evaluate` on the whole trade series, phase 2 on the
   post-handover subset). If any task appears to need a change inside `FtmoBreachEvaluator.cs`,
   STOP and report instead of editing it.
2. **`FtmoBreachSimulationReadServiceGoldenPinTests.cs` stays GREEN and UNEDITED** through both PRs.
   Re-run it at the end of every phase in both PRs (not just at the very end).
3. **PR1 edits exactly ONE existing assertion**: `FtmoReplayCalendarTests.cs`
   `AnchorDayItselfHasNoReplayedClose_IsNotCountedInFtmoTradingDaysElapsed` (currently asserts
   `tradingDays.Should().Be(1)` at line ~150; becomes `Be(2)`, per design.md's own note "1 becomes 2"
   — trade 0 opens day 1 / closes day 3, trade 1 opens day 3 / closes day 3; counting by OPEN now
   attributes a trading day to day 1 (trade 0's open) as well as day 3 (trade 1's open)). PR1's proof
   that nothing else changed is a snapshot captured on unchanged code BEFORE the change (task 1.1),
   asserted with `BeEquivalentTo` excluding only the `FtmoTradingDaysElapsed` paths — written FIRST,
   like a golden pin. If any OTHER existing assertion needs to change to go green, STOP and report —
   do not edit it.
4. **PR2 edits NO existing assertion.** The only permitted edits to existing files are `required`
   member initializers (`ChallengeRace = null`, or equivalent) on existing DTO constructions that fail
   to compile once `FtmoRunSimulationResultDto` gains the new required member. Count the actual broken
   sites by compiling (task 2.10.1); design.md's file-changes table names exactly 2
   (`StrategyBacktestsControllerTests.cs`). If compiling surfaces MORE than 2, STOP and report before
   editing anything.
5. **A test that cannot fail is a defect.** The falsification steps below are mandatory, not optional:
   the cutoff rule (1.4), the sweep vs. its brute-force oracle (2.5), close groups (2.6), the
   breach-wins tie (2.7), and the whole-chain FX ranking (2.9).
6. **No banned survival wording anywhere in this capability's output** ("passed", "safe", "survived",
   "would have passed", or an equivalent affirmation) — extend the existing no-pass-wording test to
   every new DTO, enum, and the disclosure constant (task 2.12).
7. **Existing whole-run refusal already covers non-`TwoStep`/missing product.** Confirmed by reading
   `FtmoBreachSimulationReadService.cs`: `sharedRefusal = ... FtmoProduct is null || != TwoStep ?
   FtmoSimulationRefusal.ProductNotTwoStep : ...` already refuses the ENTIRE run (not just the race)
   before any race code would run. Spec.md's "A non-TwoStep or missing product refuses the race"
   scenario is therefore satisfied for free by the existing whole-run refusal path — PR2 does NOT need
   new refusal logic for that case, only a test confirming `ChallengeRace` is null on that
   already-refused run (task 2.11.2). The NEW refusal logic PR2 must add is narrower: only the
   `ProfitTargetPct` mismatch case (spec.md's second requirement), which is race-only and leaves the
   run `Evaluated`.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| PR1 estimated lines | prod ~40, tests ~120, floor ~160 |
| PR2 estimated lines | prod ~330, tests ~520, floor ~850 |
| 400-line budget risk | High (PR2), `size:exception` — do not trim scope |
| Chained PRs | Yes — PR1 (trading-day recalculation) then PR2 (the race), per design.md |
| Delivery strategy | ask-on-risk (default), `size:exception` for PR2 |

---

## PR1 — `FtmoTradingDaysElapsed` recalculation (count by OPEN, not close)

### Phase 1.0 — Snapshot pin (written FIRST, on unchanged code — NOT RED-first)

- [x] 1.0.1 Write a characterization test (new file or addition to
  `FtmoBreachSimulationReadServiceTests.cs`) on unmodified code: run an existing multi-limit,
  multi-day fixture (reuse a fixture already exercising `FtmoTradingDaysElapsed`, e.g. the
  first-breach-timing suite's day-gap fixture) through the CURRENT, unmodified
  `FtmoBreachSimulationReadService`. Capture the full result with `BeEquivalentTo`, EXCLUDING only
  the `FtmoTradingDaysElapsed` path(s) on `FtmoBreachTimingDto`/`FtmoFirstLimitBreachDto`. This MUST
  be green today, before any production change in PR1, and MUST stay green, unedited, through PR1.
  _Satisfies: spec.md "The Race Leaves The Shipped Breach Result Byte-Identical" posture applied to
  PR1's own scope; design.md Testing Strategy row 1 ("Characterization, captured on main FIRST")._
- [x] 1.0.2 Run 1.0.1 now and confirm GREEN before touching any production file.

### Phase 1.1 — Cutoff-rule characterization and correction

- [x] 1.1.1 RED: `FtmoReplayCalendarTests.ElapsedFtmoTradingDays_CountsDistinctDaysWithAScalablePositionOpened_NotClosed` —
  anchor day D, breach on D+10, with non-`Unscalable` position OPENS on 6 distinct FTMO trading days
  between D and D+10 inclusive, and closes (no same-day opens) on 2 further distinct days in that
  range; asserts `FtmoTradingDaysElapsed == 6`, not 8, and no holiday calendar is consulted.
  _Satisfies: spec.md (breach-simulation MODIFIED) "Elapsed FTMO trading days counts distinct days
  with a position opened, not closed" scenario; design.md Decision 5 in the challenge-race design._
- [x] 1.1.2 RED: `..._APositionOpenedLaterOnTheBreachDayAfterTheBreachClose_IsNotCounted` — a breach
  whose source close instant is `c`; a position opens later that same FTMO day, strictly after `c`,
  with no other position opened that day before `c`; asserts that later open does not satisfy
  `Open < c ∨ Close ≤ c` and does not contribute to the count, even though its FTMO day is within
  `[anchor, eventDay]`. **This is the falsification-bearing cutoff-rule test (hard rule 5).**
  _Satisfies: spec.md "A position opened later on the breach day, after the breach close, is not
  counted" scenario._
- [x] 1.1.3 RED: `..._ADayContainingOnlyACloseNoOpen_IsNotCounted` — a day within the elapsed window
  where a position opened on a prior day closes, and no position opens that day; asserts that day
  does not contribute.
  _Satisfies: spec.md "A day containing only a close, no open, is not counted" scenario._
- [x] 1.1.4 RED: `..._AnUnscalableOpen_DoesNotCountAsATradingDay` — a day whose only replayed open is
  `Unscalable`; asserts that day does not contribute.
  _Satisfies: spec.md "An Unscalable open does not count as a trading day" scenario._
- [x] 1.1.5 RED: `..._AFlatCountByCloseImplementation_WouldFailThisRequirement` — reuse 1.1.1's series
  and assert the shipped (pre-PR1) count-by-close behavior produces a DIFFERENT number than the
  by-open count, proving the two definitions are observably distinct (guards against a no-op "fix").
  _Satisfies: spec.md "A flat count-by-close implementation would fail this requirement" scenario._
- [x] 1.1.6 RED: `..._CalendarDaysElapsedIsUnaffectedByTheTradingDayCorrection` — anchor day D, breach
  on D+45; `CalendarDaysElapsed == 45` both before and after the correction.
  _Satisfies: spec.md "Calendar days elapsed is unaffected by the trading-day correction" scenario._
- [x] 1.1.7 GREEN: modify `FtmoReplayCalendar.ElapsedDays` to accept the close's own source-time
  cutoff (`Point.SourceTime`, per design.md's file-changes note "1: pass Point.SourceTime") and count
  distinct `BookkeepingDay(Open)` values for scalable trades satisfying `Open < c ∨ Close ≤ c`, where
  the trade's attributed day falls within `[anchor.FtmoDay, eventDay]`. Exclude `Unscalable` trades
  (`Net is null`, matching the existing exclusion convention) from the count entirely; the anchor
  itself is unaffected (still computed from the first OPEN regardless of `Unscalable`). Leave
  `CalendarDaysElapsed` (`breachDay.DayNumber - anchor.FtmoDay.DayNumber`) untouched. Confirm 1.1.1–1.1.6
  pass, and confirm 1.0.1 (the snapshot pin) still passes EXCLUDING only the `FtmoTradingDaysElapsed`
  path.
  _Satisfies: spec.md (breach-simulation MODIFIED) "Elapsed Time Is Measured From The Replay-Start
  Anchor" requirement; design.md Decision 5 (cutoff rule, shared with PR2's sweep)._
- [x] 1.1.8 Wire the corrected cutoff instant through `FtmoBreachSimulationReadService`: pass each
  breach/clean-breach point's own `SourceTime` as the cutoff `c` into `ElapsedDays`, replacing
  whatever cutoff value is passed today. Confirm 1.1.1–1.1.6 and 1.0.1 all still pass.
  _Satisfies: design.md File Changes table row `FtmoBreachSimulationReadService.cs` ("1: pass
  Point.SourceTime")._

### Phase 1.2 — The one permitted existing-assertion edit

- [x] 1.2.1 Edit `FtmoReplayCalendarTests.AnchorDayItselfHasNoReplayedClose_IsNotCountedInFtmoTradingDaysElapsed`:
  change `tradingDays.Should().Be(1)` to `tradingDays.Should().Be(2)` (trade 0 opens day 1, closes day
  3; trade 1 opens day 3, closes day 3 — counting by open now attributes day 1 AND day 3). Update the
  inline comment accordingly. Confirm this is the ONLY existing assertion edited anywhere in PR1
  (hard rule 3); if any other existing test needs an edit to pass, STOP and report instead.
  _Satisfies: hard rule 3; design.md "Testing Strategy" row 2 ("The :135 test is the ONE edited
  assertion (1→2, renamed)")._

### Phase 1.3 — PR1 gates

- [x] 1.3.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror > build.log 2>&1` — zero
  warnings.
- [x] 1.3.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log
  2>&1` — no diffs.
- [x] 1.3.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx > test.log 2>&1` (full suite, once) —
  confirm the **888 pre-existing tests** all still pass with NO existing assertion edited other than
  1.2.1, plus every new PR1 test passes.
- [x] 1.3.4 Confirm the golden pin (`FtmoBreachSimulationReadServiceGoldenPinTests.cs`) and the 1.0.1
  snapshot pin are both present in the final green run and unedited since their creation.
- [x] 1.3.5 If any pre-existing test fails or a warning/format diff appears, STOP and investigate
  before patching — a failing pre-existing test means a verdict silently changed.

---

## PR2 — The FTMO 2-Step challenge race

Do not start PR2 until PR1's Phase 1.3 gates are all green.

### Phase 2.1 — New enums (pure, no I/O)

- [x] 2.1.1 GREEN: create `Domain/Enums/FtmoPhaseOutcome.cs` (`TargetReachedFirst`, `BreachedFirst`,
  `NeitherByEndOfData`, `NotStarted`) and `Domain/Enums/FtmoChallengeRaceRefusal.cs` (at minimum a
  `ProfitTargetMismatch` member carrying/echoing both the stored and fixed values — confirm the exact
  refusal-reason shape against task 2.8's echoed-values requirement before finalizing member names).
  No production logic yet; confirm each enum is referenced only where later tasks need it.
  _Satisfies: spec.md "Phase Outcome Is A Four-State Enum, Never A Boolean" requirement; spec.md "A
  Stored Profit Target Percentage That Disagrees With The Fixed Rule Refuses The Race" requirement._

### Phase 2.2 — `FtmoOpenPositionSweep` (pure, new file — O(n log n) overlap check)

- [x] 2.2.1 RED: `FtmoOpenPositionSweepTests.OpenAt_NoOverlappingScalablePosition_ReturnsFalse` — a
  cutoff `T` with no scalable trade open at that instant returns `false`/zero.
  _Satisfies: design.md Decision 4 (the sweep)._
- [x] 2.2.2 RED: `..._AScalableTradeOpenBeforeTAndClosingAfterT_ReturnsTrue` — a trade with
  `Open < T < Close` is counted as open at `T`.
  _Satisfies: design.md Decision 4 formula `openAt(T) = #{Open < T} − #{Close ≤ T ∧ Open < T}`._
- [x] 2.2.3 RED: `..._AnUnscalableTradeOpenAcrossT_IsExcluded` — an `Unscalable` row spanning `T`
  does NOT count as an open sibling.
  _Satisfies: spec.md "The open-position check for the target MUST exclude Unscalable trades"
  requirement; design.md Decision 4 ("Unscalable rows were never opened on FTMO")._
- [x] 2.2.4 RED: `..._ATradeClosingExactlyAtT_IsNotOpenAtT` — `Close == T` is treated as closed by
  `T`, not open.
  _Satisfies: design.md Decision 4 formula's `Close ≤ T` term; design.md Testing Strategy row
  ("a zero-duration trade at T")._
- [x] 2.2.5 GREEN: implement `FtmoOpenPositionSweep.OpenAt(IReadOnlyList<ProjectedTrade> trades,
  DateTime cutoff) -> bool` (or an equivalent count) as an O(n log n) sweep over scalable trades only,
  per design.md Decision 4's formula. Confirm 2.2.1–2.2.4 pass.
  _Satisfies: design.md Decision 4._
- [x] 2.2.6 Falsification (mandatory, hard rule 5): write a brute-force oracle
  (`#{trades : Open < T < Close}` over scalable rows, O(n) per query, matching the shipped
  `HasConcurrentOpenPosition`'s semantics minus its `Unscalable` inclusion) and a property-style test
  generating randomized trade sets and cutoffs; assert the sweep and the oracle agree on every
  generated case. Temporarily break the sweep (e.g. flip a `<`/`≤` boundary) and confirm this test
  goes RED; restore and confirm green.
  _Satisfies: design.md Testing Strategy row ("A property test against a brute-force oracle (the
  shipped predicate over scalable rows)")._

### Phase 2.3 — `FtmoChallengeRules` (pure constants)

- [x] 2.3.1 GREEN: create the fixed 2-Step rule constants (phase-1 target 0.10, phase-2 target 0.05,
  4-trading-day minimum, unlimited time), keyed on `FtmoProduct.TwoStep`, citing
  `SERVICE_FTMO.md:46-48,157-158,236`. No test required beyond a values-are-correct assertion (pure
  constant data), but assert they are echoed correctly once wired in Phase 2.8.
  _Satisfies: spec.md "Two-Step Challenge And Verification Rules Are Fixed In Code" requirement._

### Phase 2.4 — Target scanner: close groups and the flat-close check

- [x] 2.4.1 RED: `FtmoChallengeRaceScannerTests` (or equivalent, inside `FtmoChallengeRaceTests`) —
  `TargetDayMinimumAndFlatBook_TogetherDecideThePhase` — balance first reaches the target at a close
  on FTMO trading day 6 with no other position open; asserts the phase outcome is decided at that
  close.
  _Satisfies: spec.md "Target, day minimum, and flat book together decide the phase" scenario._
- [x] 2.4.2 RED: `..._AnOpenSiblingPositionDefersTheDecisionToALaterClose` — balance reaches the
  target at a close with a second position open, then again (book flat) at a later close; asserts the
  phase is reported reached at the LATER, flat close, not the earlier one.
  _Satisfies: spec.md "An open sibling position defers the decision to a later close" scenario._
- [x] 2.4.3 RED: `..._TwoTradesClosingAtTheSameInstant_AreOneEvent_TargetCrossedByThePair` — two
  trades share the same source close instant; day minimum already met; neither trade's own `Net`
  alone reaches the target but their combined `Net` does; asserts the target is evaluated ONCE after
  both closes and the phase reaches its target at that shared instant. **Falsification-bearing
  (hard rule 5, close groups):** additionally assert that evaluating each close independently (a
  deliberately-broken per-row implementation) would NOT report the target reached at that instant —
  demonstrate by a temporary per-row evaluation and confirm it disagrees, then restore the
  group-based evaluation.
  _Satisfies: spec.md "Two trades closing at the same instant are one event" scenario; design.md
  Decision 2 (close groups)._
- [x] 2.4.4 RED: `..._AnUnscalablePositionSpanningTheTargetClose_DoesNotBlockTheTarget` — balance and
  day minimum qualify at a close; only an `Unscalable` position remains open (`Open` before, `Close`
  after); asserts the phase reaches its target at that close.
  _Satisfies: spec.md "An Unscalable position spanning the target close does not block the target"
  scenario._
- [x] 2.4.5 RED: `..._TheDayMinimumNotYetMet_DefersTheDecision` — balance reaches the target on FTMO
  trading day 2 (book flat), no breach before day 4; asserts the phase is NOT decided on day 2, that
  `FirstTargetTouch` reports day 2, and that the decision is deferred to day 4.
  _Satisfies: spec.md "The day minimum not yet met defers the decision" scenario._
- [x] 2.4.6 GREEN: implement the scanner: step through close groups (rows sharing a `CloseSource`
  instant, in `RowIndex` order, per design.md Decision 2); after the last close in each group, check
  balance ≥ capital × (1 + target), FTMO trading days ≥ 4 (via the shared cutoff-rule day count from
  PR1/Phase 1.1), and `!FtmoOpenPositionSweep.OpenAt(scalableTrades, groupCloseInstant)`. Track and
  report `FirstTargetTouch` (the first close where balance alone crosses the target, independent of
  day-minimum/flat-book) separately from the deciding close. Confirm 2.4.1–2.4.5 pass.
  _Satisfies: spec.md "A Phase Reaches Its Target Only At A Flat Close" requirement; spec.md "Trading
  Continues At Normal Risk Between First Target Touch And The Day Minimum" requirement; design.md
  Decision 2._

### Phase 2.5 — Composition: `RunPhase` calls the unchanged `FtmoBreachEvaluator`

- [x] 2.5.1 RED: `..._APhaseBreachAndTheTargetOnTheSameClose_BothResolveToBreachedFirst` — balance
  both breaches a loss limit and crosses the target (day minimum met, book flat) on the same close (or
  close-instant group); asserts outcome `BreachedFirst`, not `TargetReachedFirst`. **Falsification-bearing
  (hard rule 5, breach-wins tie):** additionally assert that a deliberately target-favoring tie-break
  (temporarily swap the comparison order) would report `TargetReachedFirst` instead, then restore the
  breach-wins order.
  _Satisfies: spec.md "A breach and the target on the same close both resolve to BreachedFirst"
  scenario; design.md Decision 3 ("Breach wins")._
- [x] 2.5.2 RED: `..._ACleanBreach_EndsThePhase` — max-loss cleanly breached before the target;
  outcome `BreachedFirst`, reporting the max limit and clean classification.
  _Satisfies: spec.md "A clean breach ends the phase" scenario._
- [x] 2.5.3 RED: `..._AContingentBreach_AlsoEndsThePhase` — daily-loss breached with a downgraded
  classification before the target; outcome `BreachedFirst`, reporting the daily limit and contingent
  classification.
  _Satisfies: spec.md "A contingent breach also ends the phase" scenario._
- [x] 2.5.4 RED: `..._ABreachAfterAPrematureTargetTouch_EndsThePhaseAsBreached` — balance touches the
  target on day 2 (day minimum not met, book flat), daily loss breached on day 3; outcome
  `BreachedFirst`, not `TargetReachedFirst`.
  _Satisfies: spec.md "A breach after a premature target touch ends the phase as breached" scenario._
- [x] 2.5.5 RED: `..._ThePhaseReportsBothTheFirstTouchAndTheFinalDecision` — same fixture as 2.5.4;
  asserts `FirstTargetTouch` reports day 2 and the outcome-deciding close reports day 3, as two
  distinct fields.
  _Satisfies: spec.md "The phase reports both the first touch and the final decision" scenario._
- [x] 2.5.6 RED: `..._DataEndingBeforeEitherEventResolves_IsReportedHonestly` — replayed data ends
  with neither the target reached nor a breach detected; outcome `NeitherByEndOfData`.
  _Satisfies: spec.md "Data ending before either event resolves is reported honestly" scenario._
- [x] 2.5.7 RED: `..._Phase1BreachRow_EqualsTheShippedFirstBreachRow_ViaTheService` — a run whose
  shipped daily-loss `FirstBreach` occurs before phase 1's target would have been reached; asserts the
  race's phase-1 result reports `BreachedFirst` for the SAME close, limit, and classification as the
  shipped `FirstBreach` record, exercised THROUGH THE SERVICE (not a hand-built `RunPhase` call) so
  the test fails if wired to `FirstCleanBreach` or the wrong FX end.
  _Satisfies: spec.md "The Race's Phase-One Breach Matches The Shipped First Breach" requirement and
  its scenario; design.md Decision 1 (D7 cross-check is "identity by construction")._
- [x] 2.5.8 GREEN: implement `FtmoChallengeRace.RunPhase(orderedTrades, startOpen, capital, rules)`:
  call `FtmoBreachEvaluator.Evaluate(phaseTrades, ..., capital, dailyPct, maxPct)` unmodified and take
  its first breach across both limits (`FtmoBreachTiming.FirstLimit` over Daily/Max `FirstBreach`);
  run the Phase 2.4 scanner over the same trade subset for the target; compare the breach close (if
  any) against the target-deciding close and apply breach-wins-on-tie (2.5.1). Do NOT edit
  `FtmoBreachEvaluator.cs` (hard rule 1). Confirm 2.5.1–2.5.7 pass, and confirm the golden pin is
  STILL GREEN, unedited.
  _Satisfies: design.md Decision 1 (Compose); design.md Data Flow section._
- [x] 2.5.9 RED: `..._PhaseTwoStartsAtTheNextTradeOpenedAfterThePhaseOneDecision` — phase 1 reaches
  its target at a close on FTMO trading day 20, next trade opens on day 21; asserts phase 2's start is
  day 21's trade, balance resets to Initial Capital, and the day-1 floor is computed from that
  Initial Capital.
  _Satisfies: spec.md "Phase 2 starts at the next trade opened after the phase-1 decision" scenario;
  design.md Decision 1 ("Phase 2 is Evaluate on the phase-2 subset: capital resets... no extra code")._
- [x] 2.5.10 RED: `..._PhaseTwoIsNotStartedWhenPhaseOneDoesNotReachItsTarget` — phase 1 outcome
  `BreachedFirst` or `NeitherByEndOfData`; asserts phase 2's outcome is `NotStarted` with no
  phase-2 timing or decision fields populated.
  _Satisfies: spec.md "Phase 2 is NotStarted when phase 1 does not reach its target" scenario; spec.md
  "A NotStarted phase 2 carries no timing fields" scenario._
- [x] 2.5.11 GREEN: implement the phase-1/phase-2 handover: split the trade series into `Close ≤ T`
  (phase 1) and `Close > T` (phase 2, all with `Open ≥ T` by construction per design.md Decision 2),
  call `RunPhase` again on the phase-2 subset with capital reset to Initial Capital. Confirm 2.5.9 and
  2.5.10 pass.
  _Satisfies: spec.md "Phase Two Starts Fresh At The First Trade Opened After The Phase-One Target
  Close" requirement._

### Phase 2.6 — Race-only refusal (`ProfitTargetPct` mismatch)

- [x] 2.6.1 RED: `FtmoChallengeRaceRefusalTests.AStoredTargetOf010_DoesNotRefuse` — `ProfitTargetPct
  = 0.10`; race proceeds, not refused.
  _Satisfies: spec.md "A stored target of 0.10 does not refuse" scenario._
- [x] 2.6.2 RED: `..._AStoredTargetOtherThan010_RefusesWithBothValuesEchoed` — `ProfitTargetPct =
  0.08`; race refused with a typed reason echoing both `0.08` and the fixed `0.10`; no phase result
  produced.
  _Satisfies: spec.md "A stored target other than 0.10 refuses with both values echoed" scenario._
- [x] 2.6.3 RED: `..._ARaceOnlyRefusal_LeavesTheRunEvaluatedWithItsBreachFindingsReported` —
  `ProfitTargetPct = 0.08` whose shipped breach evaluation would otherwise proceed; asserts the run's
  status stays `Evaluated`, its shipped breach findings are reported exactly as without the race
  feature, and only `ChallengeRace` is null.
  _Satisfies: spec.md "A race-only refusal leaves the run Evaluated with its breach findings
  reported" scenario; hard rule 7._
- [x] 2.6.4 RED: `..._ANullStoredTarget_UsesTheFixedRuleWithoutRefusing` — `ProfitTargetPct = null`;
  race proceeds using the fixed 10%/5% rule, not refused.
  _Satisfies: spec.md "A null stored target uses the fixed rule without refusing" scenario._
- [x] 2.6.5 GREEN: implement the race-only refusal check (stored `ProfitTargetPct` present and ≠
  0.10 → `FtmoChallengeRaceRefusal`), scoped so it does NOT affect the shared/whole-run refusal path
  or the shipped breach evaluation. Confirm 2.6.1–2.6.4 pass.
  _Satisfies: spec.md "A Stored Profit Target Percentage That Disagrees With The Fixed Rule Refuses
  The Race, Not The Run" requirement._
- [x] 2.6.6 RED: `..._ANonTwoStepOrMissingProduct_RefusesTheRace` — a `BrokerRiskLimits` row with
  `FtmoProduct` null or `OneStep`; asserts the race is refused with no phase result. Exercise this
  THROUGH THE SERVICE (confirming the existing whole-run refusal already produces `ChallengeRace ==
  null`, per hard rule 7) rather than adding new refusal logic.
  _Satisfies: spec.md "A non-TwoStep or missing product refuses the race" scenario; hard rule 7._
- [x] 2.6.7 RED: `..._LossLimitsAreReadUnchangedAcrossBothPhases` — a `TwoStep` row with given
  `DailyLossLimitPct`/`MaxLossLimitPct`; asserts both phases evaluate against the same stored values,
  unchanged between phases.
  _Satisfies: spec.md "Loss limits are read unchanged across both phases" scenario._
- [x] 2.6.8 GREEN (if needed): confirm 2.6.6–2.6.7 pass against the existing wiring (2.5.8/2.5.11);
  no new production code expected for 2.6.6 (coverage-only, per hard rule 7), and none expected for
  2.6.7 beyond what 2.5.8 already does (both phases read the same `dailyPct`/`maxPct` closure values).

### Phase 2.7 — Rules and phase timing echoed on the DTO

- [x] 2.7.1 RED: `..._BothPhaseTargetsAndTheDayMinimum_AreEchoedOnANonRefusedRace` — asserts the
  result echoes a 10% phase-1 target, 5% phase-2 target, 4-trading-day minimum for both phases, and no
  time limit.
  _Satisfies: spec.md "Both phase targets and the day minimum are echoed on a non-refused race"
  scenario._
- [x] 2.7.2 RED: `..._ATargetReachedFirstPhase_ReportsAllRequiredTimingFields` — outcome
  `TargetReachedFirst`; asserts outcome, start point, first target touch (if distinct from the
  deciding close), the close/day the 4-day minimum was met, the deciding close, and elapsed calendar
  and FTMO trading days from the phase's own start are all reported.
  _Satisfies: spec.md "A TargetReachedFirst phase reports all required timing fields" scenario._
- [x] 2.7.3 RED: `..._EveryProducedRaceResultReports_TheFourStateEnumNeverABoolean` — inspects every
  non-refused race result's phase outcome fields; asserts each is one of the four enum values and no
  boolean pass/fail field exists anywhere in the result.
  _Satisfies: spec.md "Every phase reports one of the four enum values, never a boolean" scenario._
- [x] 2.7.4 GREEN: implement `FtmoChallengePhaseDto`/`FtmoChallengeRaceDto` mapping per design.md
  Interfaces/Contracts, populating rules, per-phase timing (start, first touch, day-minimum-met
  close/day, deciding close, elapsed calendar/FTMO trading days computed via the PR1-corrected
  `FtmoReplayCalendar.ElapsedDays` from the phase's own start), and `NotStarted` phase 2 with every
  timing field null. Confirm 2.7.1–2.7.3 pass.
  _Satisfies: design.md Interfaces/Contracts; spec.md "Every Non-Refused Race Reports Full Per-Phase
  Timing And The Fixed Rules" requirement._

### Phase 2.8 — FX-band whole-chain ranking

- [x] 2.8.1 RED: `FtmoChallengeRaceFxMergeTests.DisagreeingFxEnds_ReportTheLessFavourableOutcome` —
  `fxLow` yields `TargetReachedFirst` for phase 1, `fxHigh` yields `BreachedFirst` for the same phase;
  asserts outcome `BreachedFirst`, tagged `FxRoundingSensitive` and end `FxHigh`.
  _Satisfies: spec.md "Disagreeing FX ends report the less favourable outcome" scenario._
- [x] 2.8.2 RED: `..._AgreeingFxEnds_NeedNoRoundingSensitivityTag` — both ends yield
  `TargetReachedFirst` at the same underlying close; asserts outcome without `FxRoundingSensitive`.
  _Satisfies: spec.md "Agreeing FX ends need no rounding-sensitivity tag" scenario._
- [x] 2.8.3 RED: `..._ASameCurrencySymbol_NeedsNoFxBandComparison` — an `XAUUSD`-style race requiring
  no FX band; asserts no end comparison is performed and the outcome reflects the single run.
  _Satisfies: spec.md "A same-currency symbol needs no FX-band comparison" scenario._
- [x] 2.8.4 RED: `..._APhaseTwoOutcomeTieBetweenEnds_IsBrokenByTimingNotOutcomeAlone` — both ends
  yield chain `(P1 Target, P2 Breached)` at the same rank, but `fxLow`'s phase-2 breach close is
  earlier; asserts the reported chain is `fxLow`'s, tagged `FxRoundingSensitive` and `FxLow`.
  _Satisfies: spec.md "A phase 2 outcome tie between ends is broken by timing, not by outcome alone"
  scenario._
- [x] 2.8.5 RED: `..._IdenticalOutcomeAndDecidingClose_ReportsBothEnds` — both ends yield the same
  chain rank, same outcome pair, and same deciding close for every phase; asserts `FtmoFxBandEnd ==
  BothEnds`, carrying `fxLow`'s values, without `FxRoundingSensitive`.
  _Satisfies: spec.md "Identical outcome and deciding close on both ends reports BothEnds" scenario._
- [x] 2.8.6 GREEN: implement whole-chain ranking per design.md Decision 6:
  `P1 Breached < P1 Neither < (P1 Target, P2 Breached) < (P1 Target, P2 Neither) < (P1 Target, P2
  Target)`; report the lower chain; on a rank tie, report the less favourable timing (earlier breach,
  or absent a breach, later target); on an identical-row tie, report `BothEnds` with `fxLow`'s values;
  tag `FxRoundingSensitive` whenever the two ends' outcome pairs (phase 1, phase 2) differ. Confirm
  2.8.1–2.8.5 pass.
  _Satisfies: spec.md "FX Band Race Reports The Whole-Chain Outcome Less Favourable To Reaching The
  Target" requirement; design.md Decision 6._
- [x] 2.8.7 Falsification (mandatory, hard rule 5): temporarily change the ranking to merge each phase
  independently (mixing a phase 2 from one end with a phase 1 from the other) and confirm 2.8.4 or an
  equivalent mixed-chain fixture goes RED (or produces an impossible chain); restore whole-chain
  comparison and confirm green again.
  _Satisfies: design.md Decision 6 rationale ("mixing ends could combine a phase 2 from one end with
  a phase 1 from the other")._

### Phase 2.9 — Byte-identical shipped result and no-survival-wording coverage

- [x] 2.9.1 RED: `..._AddingTheRaceDoesNotChangeTheShippedBreachFields` — a fixture whose shipped
  breach result reports a specific `Verdict`, `Causes`, `DisclosureText`; asserts these plus
  `FtmoTradingDaysElapsed` are byte-identical to their value with the race computed alongside them.
  _Satisfies: spec.md "Adding the race does not change the shipped breach fields" scenario; spec.md
  "The Race Leaves The Shipped Breach Result Byte-Identical" requirement._
- [x] 2.9.2 RED: `..._ARefusedRaceReportsNullChallengeRace_WithoutDisturbingTheBreachResult` — a run
  refused only on account of the race's `ProfitTargetPct` mismatch, whose shipped breach evaluation
  would otherwise proceed; asserts `ChallengeRace` is null and the shipped breach result is produced
  exactly as without the race feature.
  _Satisfies: spec.md "A refused race reports null ChallengeRace without disturbing the breach
  result" scenario._
- [x] 2.9.3 RED: extend the existing no-pass-wording test to every new field/enum/DTO/string
  introduced by this capability (`FtmoPhaseOutcome`, `FtmoChallengeRaceRefusal`,
  `FtmoChallengeRaceDto`/`FtmoChallengePhaseDto`, and the disclosure constant); assert none of
  "passed", "safe", "survived", "would have passed" appears anywhere, including on a
  `TargetReachedFirst` outcome, and that no boolean pass/fail field exists anywhere in the result.
  _Satisfies: spec.md "A reached target is not worded as passing" scenario; hard rule 6._
- [x] 2.9.4 RED: `..._TheDisclosureStatesBothBiasDirectionsWithoutSurvivalWording` — inspects any
  non-refused race result's disclosure text; asserts it states unmodelled swap and closed-trade
  replay make the target look easier, that continued full-risk trading after a premature touch makes
  it look harder, that `TargetReachedFirst` is optimistic rather than a prediction, and that
  `BreachedFirst` remains a strong result — with none of the banned words.
  _Satisfies: spec.md "The disclosure states both bias directions without survival wording" scenario._
- [x] 2.9.5 GREEN: implement the disclosure constant (design.md's exact text) and confirm 2.9.1–2.9.4
  pass, and confirm the golden pin (and the PR1 snapshot pin) are STILL GREEN, unedited.
  _Satisfies: design.md "Disclosure" section._

### Phase 2.10 — DTO wiring and the exactly-2 compile-break sites

- [x] 2.10.1 Run `dotnet build` after adding `public required FtmoChallengeRaceDto? ChallengeRace {
  get; init; }` to `FtmoRunSimulationResultDto` and enumerate every construction site that fails to
  compile.
- [x] 2.10.2 GREEN: add `{ ChallengeRace = null }`-style initializers to the exactly 2 known
  construction sites in `StrategyBacktestsControllerTests.cs` per design.md's file-changes table. Add
  NO other edit to this file. If the compiler surfaces sites outside this file, or more than 2 sites,
  STOP and report — do not edit any assertion anywhere to make it compile (hard rule 4).
- [x] 2.10.3 GREEN: wire `FtmoChallengeRace.Run`/`MergeEnds` into `FtmoBreachSimulationReadService`'s
  `SimulateRun` per design.md Data Flow: call it once per FX end alongside the existing
  `EvaluateAtFx`, merge ends, and set `ChallengeRace` on the result (null on any refusal, race-only or
  whole-run). Confirm all of Phase 2.1–2.9's tests pass end-to-end through the service, and confirm
  the golden pin is STILL GREEN, unedited.
  _Satisfies: design.md Data Flow section; design.md File Changes table._

### Phase 2.11 — Additional refusal/composition coverage carried over from Phase 2.6

- [x] 2.11.1 (covered by 2.6.6) — no separate task; listed here only to confirm it is exercised
  end-to-end through 2.10.3's wiring, not just against a hand-built `RunPhase` call.
- [x] 2.11.2 RED/GREEN: confirm (via the service) that a whole-run refusal (e.g.
  `LimitsNotConfigured`, `ProductNotTwoStep`) produces `ChallengeRace == null` alongside the existing
  `Refused(...)` result shape, with no new refusal-plumbing code required beyond passing `null`
  through the existing `Refused(...)` factory (per hard rule 7).
  _Satisfies: spec.md "A non-TwoStep or missing product refuses the race" scenario; hard rule 7._

### Phase 2.12 — PR2 gates

- [x] 2.12.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror > build.log 2>&1` —
  zero warnings.
- [x] 2.12.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log
  2>&1` — no diffs.
- [x] 2.12.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx > test.log 2>&1` (full suite, once) —
  confirm all pre-PR1 pre-existing tests plus PR1's new tests plus every new PR2 test all pass, with
  NO existing assertion edited beyond PR1's single 1.2.1 edit and PR2's ≤2 `ChallengeRace = null`
  initializers.
- [x] 2.12.4 Confirm the golden pin and the PR1 snapshot pin (task 1.0.1) are both present in the
  final green run and unedited since their creation.
- [x] 2.12.5 If any pre-existing test fails, or a warning/format diff appears, or more than 2
  construction sites needed editing, STOP and investigate before patching.

---

## Flagged: items too vague for a checkable task, or requiring a judgment call at apply time

1. **Task 2.1.1's exact `FtmoChallengeRaceRefusal` member shape** is not pinned by spec/design beyond
   "a typed reason that echoes both the stored value and the fixed 0.10 rule value." The apply phase
   must choose between an enum-with-payload (a record) versus a bare enum plus separate echoed
   `decimal?` fields on the DTO; design.md's Interfaces/Contracts section shows `StoredProfitTargetPct`
   as a top-level `FtmoChallengeRaceDto` field, suggesting the enum stays bare and the stored value is
   echoed at the DTO level, not inside the refusal reason itself — task 2.6.2/2.6.5 should confirm
   this shape before finalizing 2.1.1.
2. **Task 2.10.1's "exactly 2 broken construction sites"** is an estimate from design.md's file-changes
   table (`StrategyBacktestsControllerTests.cs`), not a compiler run performed during this tasks
   phase. Task 2.10.1 is the actual verification step; if it finds a different count, apply must stop
   and report rather than silently editing extra assertions.
3. **The exact field carrying the "day the 4-day minimum was first met"** (`MinTradingDaysMetFtmoDay`
   per design.md's Interfaces/Contracts) versus the deciding close is straightforward when the target
   touch, the day-minimum date, and the deciding close are three different points (spec.md's day-2/day-4
   scenario), but the DTO shape for a `TargetReachedFirst` phase where the day minimum was ALREADY met
   at first touch (i.e., day-minimum-met date coincides with the deciding close) is not explicitly
   given a distinct test — task 2.4.1's fixture (day 6, day minimum already met) exercises this
   implicitly; confirm at apply time that `MinTradingDaysMetFtmoDay` is still populated (not null) in
   that case, since the spec's requirement lists it as always reported for a decided phase.

## Flagged items — resolved by the orchestrator (2026-09-27)

1. **Refusal payload shape.** `FtmoChallengeRaceRefusal` is a bare enum. The stored `ProfitTargetPct` that caused the refusal is echoed as a separate top-level field on the race DTO, as design.md's contract already shows. No payload record.
2. **Compile-break count in `StrategyBacktestsControllerTests`.** "Exactly 2" is the design's estimate. Task 2.10.1 counts it by compiling; if it is not 2, stop and report before editing.
3. **Task count.** This checklist holds 75 items (PR1 16, PR2 59), counted by grepping the checkboxes; the planning report said 77.
