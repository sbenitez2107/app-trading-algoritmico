# Tasks: Report WHEN each FTMO loss limit is first breached

Strict TDD, except task 0 (golden pin, written on unchanged `main`, must stay green unedited
throughout). Every other RED precedes its GREEN. Load-bearing arithmetic units (elapsed-days,
same-close tie detection) get an explicit falsification step. One PR, `size:exception`
(`400-line budget risk: High`, floor ~700). Read `.claude/conventions/backend-core.md` and
`backend-testing.md` before any `.cs` file. Every unit cites its spec requirement/scenario or
design decision.

**HARD RULE, applies to every task below:** no existing test assertion may be edited. The only
permitted edits to existing test files are the 4 `required` initializers (`{ X = null }`) in
`StrategyBacktestsControllerTests` (task 8.2). If any other existing assertion needs to change to
go green, STOP — a verdict changed — and report it instead of editing the assertion.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | Production ~170, tests ~550, floor ~700 (realistic ~700–800) |
| 400-line budget risk | High |
| Chained PRs recommended | No — no honest seam (proposal.md Sizing) |
| Delivery strategy | ask-on-risk (default), `size:exception` |

---

## Phase 0 — Golden pin (written FIRST, on unchanged code — NOT RED-first)

- [x] 0.1 Write `FtmoBreachSimulationReadServiceGoldenPinTests.cs` (or add to the existing read
  service test file) on unmodified `main`: a fixture with a contingent daily breach (concurrent
  open position) followed by a later clean daily breach, on a EUR symbol (`GER40.cash`-style FX
  band) whose `fxLow`/`fxHigh` disagree on the Max-loss verdict. Capture the exact `Verdict`,
  `Causes`, and `DisclosureText` literals produced by running this fixture through the current,
  unmodified `FtmoBreachSimulationReadService`/`FtmoBreachEvaluator`. This is a characterization
  test, not a RED/GREEN pair: it MUST be green today, before any production change in this PR, and
  it MUST stay green, unedited, through every later task.
  _Satisfies: spec.md "A verdict, its causes, and its disclosure text are unchanged by adding
  timing" scenario; design.md Decision 9 (D3(b) in proposal.md)._
- [x] 0.2 Run 0.1 now and confirm GREEN before touching any production file. Record the captured
  literals in the test itself (not in a side file) so the pin is self-contained.

---

## Phase 1 — New enums (pure, no I/O)

- [x] 1.1 GREEN: create `Domain/Enums/FtmoBreachPointClass.cs` (`Clean`, `Contingent`),
  `Domain/Enums/FtmoFxBandEnd.cs` (`FxLow`, `FxHigh`, `BothEnds`),
  `Domain/Enums/FtmoFirstBreachingLimit.cs` (`Daily`, `Max`, `BothSameClose`). No compile-time
  test required (no logic), but confirm each enum is referenced only where later tasks need it —
  do not leave them dangling unused past this PR.
  _Satisfies: spec.md "New Timing Fields Use Enums, Never Booleans Or Survival Wording" requirement._

## Phase 2 — `FtmoDayClock.BookkeepingDay` (design.md Decision 2)

- [x] 2.1 RED: `FtmoDayClockTests.BookkeepingDay_AmbiguousCandidateDays_ReturnsTheMinimum` — an
  ambiguous source time producing two candidate days returns `CandidateDays.Min()`, matching the
  evaluator's existing line-157 rule.
  _Satisfies: design.md Decision 2 ("one source of truth for traps 2 and 4")._
- [x] 2.2 GREEN: add `DayAttribution.BookkeepingDay => CandidateDays.Min()` as an additive property
  on the existing `readonly record struct DayAttribution`. Confirm 2.1 passes.
- [x] 2.3 RED + GREEN (compile-level, per design.md's note that trap 2 is likely unreachable):
  rewrite `FtmoBreachEvaluator.cs:157` (`var day = attribution.CandidateDays.Min();`) to
  `var day = attribution.BookkeepingDay;`. RED here is the file failing to compile against 2.2
  until `BookkeepingDay` exists (already satisfied by 2.2) — do not construct a contrived fixture
  to reach a behavioral difference; add one assertion confirming the evaluator's chosen day still
  equals `attribution.CandidateDays.Min()` for an ambiguous-day fixture (reusing an existing
  evaluator ambiguous-day test input, not a new contrived one).
  _Satisfies: design.md's explicit note that trap 2 is probably unreachable in current code — do
  not plan a fixture pretending otherwise._

## Phase 3 — `BreachPoint` widened (design.md Decision 3)

- [x] 3.1 RED: `FtmoBreachEvaluatorTests.Evaluate_ContingentDailyBreach_FirstBreachCausesEqualTheCloseOwnCauses` —
  a daily-loss limit whose first breaching close carries `ConcurrentOpenPosition`; assert
  `Daily.FirstBreach.Value.Causes` equals exactly `[ConcurrentOpenPosition]`, independent of the
  run-level `Causes` (which the shipped `BuildFinding` still passes as `[]` for a `Breached`
  verdict).
  _Satisfies: spec.md "A first breach's own causes are not the run-level discarded causes"
  scenario; proposal.md Shipped-code finding #1._
- [x] 3.2 RED: `..._FirstCleanBreach_CausesAreEmpty_WhenTheCleanCloseHasNoCauses` — the distinct
  later clean close's `BreachPoint.Causes` is empty even though `FirstBreach.Causes` is not.
  _Satisfies: spec.md "A first breach that is contingent has a distinct first clean breach"
  scenario._
- [x] 3.3 RED: `..._BreachPoint_CarriesRowIndexAndFtmoDay_MatchingTheSourceTradeAndBookkeepingDay` —
  `RowIndex` equals the source trade's `RowIndex`; `FtmoDay` equals the close's `BookkeepingDay`
  from Phase 2.
  _Satisfies: proposal.md Shipped-code finding #2 and #4; design.md Decision 3._
- [x] 3.4 GREEN: widen `BreachPoint` (currently `FtmoTime, SourceTime, Balance, Level,
  MarginPastLevel`) with three appended positional members: `RowIndex` (int), `FtmoDay`
  (`DateOnly`, from `BookkeepingDay`), `Causes` (`IReadOnlyList<BreachContingencyCause>` — the
  close's own causes, captured at construction, distinct from the run-level `Causes` that
  `BuildFinding` still zeroes out for `Breached`). Update every `new BreachPoint(...)` call site
  inside `FtmoBreachEvaluator.Evaluate` (the daily and max breach-point construction sites) to pass
  the already-computed `trade.RowIndex`, `day`/`attribution.BookkeepingDay`, and the per-limit
  `dailyCauses`/`maxCauses` list already built in the loop. Do not touch any verdict branch,
  `BuildFinding`, or the `Contingent`/`BreachedAt`/`Clean` factories' verdict logic. Confirm
  3.1–3.3 pass and confirm 0.1 (golden pin) is STILL GREEN, unedited.
  _Satisfies: design.md Decision 3; proposal.md D3 ("proof that verdicts are unchanged")._

## Phase 4 — `FtmoReplayCalendar` (pure, new file — anchor and elapsed-day arithmetic)

- [x] 4.1 RED: `FtmoReplayCalendarTests.Build_EarliestOpenAcrossAllRows_IncludingUnscalable_IsTheAnchor` —
  the anchor is the earliest `(OpenSource, RowIndex)` across all rows, including rows whose `Net`
  is null (Unscalable), attributed via `FtmoDayClock.Attribute`.
  _Satisfies: spec.md "The anchor is the first trade's open, not its close" scenario; design.md
  Decision 4._
- [x] 4.2 RED: `..._TiedOpenSourceAcrossTwoRows_TieBrokenByRowIndex` — two rows share the identical
  `OpenSource` instant; the anchor picks the lower `RowIndex`, deterministically.
  _Satisfies: design.md Decision 4 (`RowIndex` tie-break)._
- [x] 4.3 RED: `..._AmbiguousOrInvalidOpenSource_AnchorUsesTheEarliestCandidateDay` — an
  ambiguous/invalid open time resolves to `BookkeepingDay` (earliest candidate), matching the
  evaluator's own conservative rule.
  _Satisfies: design.md Decision 4 ("Ambiguous or invalid open → BookkeepingDay")._
- [x] 4.4 GREEN: create `Infrastructure/Services/FtmoReplayCalendar.cs` (`internal static`),
  implementing `Build(IReadOnlyList<FtmoTradeProjector.ProjectedTrade> trades, TimeZoneInfo
  sourceZone, TimeZoneInfo berlinZone) -> ReplayAnchor(DateTime SourceOpen, DateOnly FtmoDay)` per
  design.md Decision 4. Confirm 4.1–4.3 pass.
- [x] 4.5 RED: `..._ElapsedFtmoTradingDays_CountsOnlyDistinctBookkeepingDaysWithAReplayedClose` —
  anchor day D, breach on D+10, with replayed closes landing on exactly 7 distinct `BookkeepingDay`
  values (Unscalable rows' closes included) between D and D+10 inclusive; asserts `7`, not `10`,
  and asserts no holiday-calendar table or external date library is consulted (no such dependency
  exists to call — confirm this by inspecting the implementation, not a runtime assertion).
  _Satisfies: spec.md "Elapsed FTMO trading days counts only days with a replayed close" scenario;
  design.md Decision 5._
- [x] 4.6 RED: `..._ElapsedCalendarDays_IsAPlainDateDifference` — anchor day D, breach on D+45,
  `CalendarDaysElapsed == 45`.
  _Satisfies: spec.md "Calendar days elapsed is a plain date difference" scenario._
- [x] 4.7 RED: `..._BreachOnTheAnchorDay_GivesZeroCalendarDaysAndOneTradingDay` — a first breach
  falling on the anchor's own `BookkeepingDay` yields `CalendarDaysElapsed == 0` and
  `FtmoTradingDaysElapsed == 1`.
  _Satisfies: design.md Decision 5 ("A breach on the anchor day gives 0 / 1")._
- [x] 4.8 RED: `..._WeekendGapBetweenAnchorAndBreach_StillCountsOnlyDaysWithAReplayedClose` — a
  breach several calendar days after the anchor with a multi-day gap containing no replayed
  closes (e.g. a weekend); `FtmoTradingDaysElapsed` counts only the days that actually contain a
  close, not every calendar day in the span.
  _Satisfies: design.md's Testing Strategy table row 3 ("a weekend gap")._
- [x] 4.9 RED: `..._AnchorDayItselfHasNoReplayedClose_IsNotCountedInFtmoTradingDaysElapsed` — the
  anchor's `BookkeepingDay` (derived from the first trade's OPEN) has no close on that same day;
  confirm that day is excluded from the `FtmoTradingDaysElapsed` count (only days WITH a replayed
  close count, per spec.md's wording — the anchor day is not automatically day 1 if it has no
  close).
  _Satisfies: design.md's Testing Strategy table row 3 ("an anchor day with no close")._
- [x] 4.10 GREEN: implement `CalendarDaysElapsed` and `FtmoTradingDaysElapsed` per design.md
  Decision 5: count DISTINCT `BookkeepingDay` values among ALL replayed closes (Unscalable
  included) with `anchor ≤ d ≤ breachDay`, computed as a post-replay count, not an in-loop running
  counter. Confirm 4.5–4.9 pass.
- [x] 4.11 Falsification (mandatory, load-bearing unit): temporarily change the
  `FtmoTradingDaysElapsed` implementation to count ALL calendar days in the span (ignoring which
  days actually have a replayed close) — confirm 4.5 and 4.8 both go RED. Restore the
  distinct-close-day counting and confirm 4.5 and 4.8 are green again.
- [x] 4.12 RED: `..._DateTimeKindGuard_UnspecifiedOpenTimeFlowsThrough_NonUnspecifiedThrows` — an
  `OpenSource` with `DateTimeKind.Unspecified` is accepted; an `OpenSource` with
  `DateTimeKind.Utc` or `DateTimeKind.Local` throws (via `FtmoDayClock.Attribute`'s existing
  `EnsureWallClock` guard, now reached through the anchor's own call path).
  _Satisfies: design.md's noted new failure mode — the evaluator/anchor path now runs `OpenTime`
  through `FtmoDayClock`'s `DateTimeKind` guard._
- [x] 4.13 GREEN: confirm 4.12 passes against 4.4's implementation (no new guard code expected;
  `FtmoDayClock.Attribute` already throws — this test only confirms the anchor's call path reaches
  it). If it does not reach the guard, add the missing call.

## Phase 5 — `FtmoBreachTiming` (pure, new file — FX merge, first-limit, DTO mapping)

- [x] 5.1 RED: `FtmoBreachTimingTests.Earliest_LowEarlierThanHigh_ReturnsLowTaggedFxLow` —
  `fxLow`'s point has an earlier `SourceTime` than `fxHigh`'s; result is `fxLow`'s point tagged
  `FxLow`.
  _Satisfies: spec.md "Both FX ends agree on the verdict but produce different first-breach
  points" scenario; design.md Decision 6._
- [x] 5.2 RED: `..._HighEarlierThanLow_ReturnsHighTaggedFxHigh` — symmetric case.
- [x] 5.3 RED: `..._SameRowOnBothEnds_ReturnsBothEndsWithFxLowValues` — `fxLow` and `fxHigh` points
  share the same `RowIndex` (same underlying row) but carry DIFFERENT `Balance` values (lot-grid
  clamp divergence); result is tagged `BothEnds` AND its reported `Balance`/`Level`/`Causes`/etc.
  values are `fxLow`'s values, not `fxHigh`'s and not a synthesized blend. Pin the RESOLVED design
  open question explicitly: assert the reported `Balance` equals `fxLow`'s `Balance`, not
  `fxHigh`'s, even though both are `BothEnds`.
  _Satisfies: spec.md "FX ends producing the same first-breach row report BothEnds" scenario;
  design.md Decision 6 ("On the same row → BothEnds, with the fxLow values"); resolves design.md's
  Open Question — pinned to fxLow, consistent with `MergeFinding`'s existing fxLow preference on
  verdict agreement._
- [x] 5.4 RED: `..._OneEndNull_ReturnsTheOtherEndAlone` — `fxLow` has no breach (null point),
  `fxHigh` has a breach; result is `fxHigh`'s point tagged `FxHigh`. And the symmetric case:
  `fxHigh` null, `fxLow` present → `fxLow` tagged `FxLow`.
  _Satisfies: spec.md "A disagreeing FX band still reports the earliest available breach point"
  scenario._
- [x] 5.5 RED: `..._BothEndsNull_ReturnsNull` — neither end has a breach; result is null.
  _Satisfies: spec.md "A never-breached limit reports null timing on both fields" scenario._
- [x] 5.6 RED: `..._SameCurrencySymbol_SingleEvaluationRun_NoBandComparisonNeeded` — when the caller
  passes the same point for both `low` and `high` (the same-currency, single-evaluation-run case),
  the result carries that point with a deterministic single-end tag (not `BothEnds` from a
  meaningless "comparison" — confirm which tag the implementation actually produces and pin it:
  either the merge short-circuits before comparing, or the same-row branch legitimately yields
  `BothEnds`; whichever the implementation does, this test pins it as intentional).
  _Satisfies: spec.md "A same-currency symbol's timing needs no band comparison" scenario._
- [x] 5.7 GREEN: create `Infrastructure/Services/FtmoBreachTiming.cs` (`internal static`),
  implementing `Earliest(BreachPoint? low, BreachPoint? high) -> (BreachPoint Point, FtmoFxBandEnd
  End)?` ordered by `(SourceTime, RowIndex)`, same-row → `BothEnds` with `low`'s values, per design.md
  Decision 6. Confirm 5.1–5.6 pass.
- [x] 5.8 RED: `..._FirstLimit_DailyBreaksBeforeMax_ReturnsDaily` — the daily limit's merged
  first-breach point's `(SourceTime, RowIndex)` is earlier than the max limit's; result is `Daily`.
  _Satisfies: spec.md "The daily limit breaks first" scenario._
- [x] 5.9 RED: `..._FirstLimit_SameRow_ReturnsBothSameClose` — daily's and max's merged first-breach
  points share the same `RowIndex`; result is `BothSameClose`, and neither `Daily` nor `Max` alone.
  _Satisfies: spec.md "Both limits break on the same close" scenario; design.md Decision 7's
  `BothSameClose` handling._
- [x] 5.10 RED: `..._FirstLimit_SameSourceTimeButDifferentRows_IsNotTreatedAsATie` — daily's and
  max's first-breach points carry an IDENTICAL `SourceTime` but DIFFERENT `RowIndex` values;
  result correctly reports the earlier-`RowIndex` (or otherwise genuinely earlier-ordered) limit
  alone, and is NEVER `BothSameClose` purely from the timestamp match.
  _Satisfies: spec.md "Identical timestamps on different rows are not treated as a tie" scenario —
  the falsifiable case named in the spec's own commentary about row identity vs timestamp
  identity._
- [x] 5.11 RED: `..._FirstLimit_NeitherLimitBreached_ReturnsNull` — both daily and max merged
  results are null; result is null.
  _Satisfies: spec.md "Neither limit is breached" scenario._
- [x] 5.12 GREEN: implement `FirstLimit(BreachPoint? dailyMerged, BreachPoint? maxMerged) ->
  FtmoFirstBreachingLimit?` using row-identity comparison (`RowIndex` equality), never
  timestamp-only comparison. Confirm 5.8–5.11 pass.
- [x] 5.13 Falsification (mandatory, load-bearing unit): temporarily change 5.12's tie detection
  from `RowIndex` equality to `SourceTime` equality alone — confirm 5.10 goes RED (the
  different-row, same-timestamp fixture now falsely reports `BothSameClose`). Restore the
  row-identity comparison and confirm 5.10 is green again.
- [x] 5.14 RED: `..._ToDto_MapsAllNineFieldsFromABreachPointAndCalendar` — mapping a merged
  `(BreachPoint, FtmoFxBandEnd)` plus the `FtmoReplayCalendar` anchor into `FtmoBreachTimingDto`
  populates `SourceCloseTime`, `FtmoTradingDay`, `BalanceAfterClose`, `FloorLevel`, `PointClass`
  (derived: `Causes.Count == 0 ? Clean : Contingent`, per design.md Decision 8 — never stored
  separately), `Causes`, `FtmoTradingDaysElapsed`, `CalendarDaysElapsed`, `FxBandEnd` — all nine
  fields traced to their source value.
  _Satisfies: design.md Interfaces/Contracts `FtmoBreachTimingDto`; design.md Decision 8._
- [x] 5.15 GREEN: implement the `ToDto` mapping. Confirm 5.14 passes.

## Phase 6 — DTO shape (design.md Decision 1, 7)

- [x] 6.1 GREEN: in `Application/DTOs/Backtests/FtmoBreachSimulationDto.cs`, add
  `public sealed record FtmoBreachTimingDto(DateTime SourceCloseTime, DateOnly FtmoTradingDay,
  decimal BalanceAfterClose, decimal FloorLevel, FtmoBreachPointClass PointClass,
  IReadOnlyList<BreachContingencyCause> Causes, int FtmoTradingDaysElapsed, int
  CalendarDaysElapsed, FtmoFxBandEnd FxBandEnd)` and `public sealed record
  FtmoFirstLimitBreachDto(FtmoFirstBreachingLimit Limit, DateTime SourceCloseTime, DateOnly
  FtmoTradingDay, int FtmoTradingDaysElapsed, int CalendarDaysElapsed)` — no nested `Timing`
  field, per design.md Decision 7 (rejects proposal.md D2's original shape).
  _Satisfies: design.md Decision 7; design.md Interfaces/Contracts section._
- [x] 6.2 GREEN: widen `FtmoLimitFindingDto` with `required FtmoBreachTimingDto? FirstBreach { get;
  init; }` and `required FtmoBreachTimingDto? FirstCleanBreach { get; init; }`. Widen
  `FtmoRunSimulationResultDto` with `required FtmoFirstLimitBreachDto? FirstLimitBreach { get;
  init; }`, `required DateTime? ReplayStartSourceTime { get; init; }`, `required DateOnly?
  ReplayStartFtmoDay { get; init; }`. Use `required` init-only properties, not positional
  parameters, per design.md Decision 1 (preserves positional order and `Deconstruct` for existing
  callers).
  _Satisfies: design.md Decision 1; spec.md's per-limit and per-run timing requirements._
  **Note:** this task alone does not yet compile every existing construction site — task 8.2
  fixes the 4 known sites in `StrategyBacktestsControllerTests`. If `dotnet build` after this task
  surfaces MORE than 4 broken construction sites, STOP and report the count before proceeding —
  proposal.md's affected-areas table names exactly 4.

## Phase 7 — Wiring in `FtmoBreachSimulationReadService`

- [x] 7.1 RED: `FtmoBreachSimulationReadServiceTests.Simulate_UsdSymbol_SameCurrencyBoth_FirstBreachReportsBothEnds` —
  a same-currency (USD) symbol's daily-limit `FirstBreach.FxBandEnd` is whatever Phase 5.6 pinned
  as the single-evaluation-run tag; assert against that exact pinned value (not "BothEnds" by
  assumption).
  _Satisfies: design.md Testing Strategy row 5 ("USD → BothEnds")._
- [x] 7.2 RED: `..._RefusedRun_EveryNewFieldIsNull` — a refused result (any
  `FtmoSimulationRefusal` reason) reports `Daily`/`Max` as null (existing behavior, unchanged) AND
  `FirstLimitBreach`, `ReplayStartSourceTime`, `ReplayStartFtmoDay` all null.
  _Satisfies: spec.md "A refused run reports no anchor and no elapsed time" scenario; design.md
  Testing Strategy row 5 ("refused → all null")._
- [x] 7.3 RED: `..._NonRefusedRun_EchoesTheReplayStartAnchor` — any evaluated (non-refused) run
  reports `ReplayStartSourceTime` and `ReplayStartFtmoDay` matching `FtmoReplayCalendar.Build`'s
  anchor for that run's trades.
  _Satisfies: spec.md "The anchor is echoed on every non-refused result" scenario; design.md
  Testing Strategy row 5 ("anchor echoed")._
- [x] 7.4 RED: `..._DisagreeingFxBandOnMaxLimit_MaxFirstBreachTimingReportsTheEarliestPointRegardlessOfVerdictDivergence` —
  a `GER40.cash`-style run where `fxLow` yields `NoBreachObserved` on Max and `fxHigh` yields a
  clean breach (the existing `FxRoundingSensitive` case); `Max.FirstBreach` reports `fxHigh`'s
  breaching close tagged `FxHigh`, while `Max.Verdict`/`Max.Causes` remain governed by the
  existing FX-rounding-sensitivity rule, UNCHANGED (cross-check against 0.1's golden pin values
  for this exact fixture shape).
  _Satisfies: spec.md "A disagreeing FX band still reports the earliest available breach point"
  scenario._
- [x] 7.5 GREEN: wire `EvaluateAtFx`'s two results through `FtmoBreachTiming.Earliest` (per limit,
  per field: `FirstBreach` and `FirstCleanBreach` independently) and `FtmoBreachTiming.FirstLimit`
  into `MergeFinding`'s call sites and the `FtmoRunSimulationResultDto` construction, per design.md
  Data Flow. Call `FtmoReplayCalendar.Build` once per run and echo its anchor. Update `Refused(...)`
  to pass explicit nulls for the four new members (already required by `required`, so a missed
  assignment is a compile error, not a silent default). Confirm 7.1–7.4 pass AND confirm 0.1
  (golden pin) is STILL GREEN, unedited.
  _Satisfies: design.md Data Flow section; design.md Decision 6's independent-merge note
  ("FirstBreach and FirstCleanBreach are merged independently")._

## Phase 8 — Existing test compile fixes (the ONLY permitted existing-test edits)

- [x] 8.1 Run `dotnet build` after Phase 6/7 and enumerate every construction site that fails to
  compile against the new `required` members.
- [x] 8.2 GREEN: add `{ FirstBreach = null, FirstCleanBreach = null }`-style initializers (and the
  run-level `{ FirstLimitBreach = null, ReplayStartSourceTime = null, ReplayStartFtmoDay = null }`
  where applicable) to the exactly 4 known construction sites in
  `StrategyBacktestsControllerTests.cs`, per design.md Decision 1 and proposal.md's affected-areas
  table. Add NO other edit to this file. If the compiler surfaces sites outside this file, STOP
  and report — do not edit any assertion anywhere to make it compile.

## Phase 9 — No-boolean, no-survival-wording coverage

- [x] 9.1 RED: extend the existing no-pass-wording test (the shipped
  `..._NoOutputContainsPassSurvivedSafeWording` in `FtmoBreachEvaluatorTests` or its
  read-service equivalent) to additionally inspect every new field introduced by this change
  (`PointClass`, `FxBandEnd`, `FtmoFirstBreachingLimit`, and every string on
  `FtmoBreachTimingDto`/`FtmoFirstLimitBreachDto`) for "passed", "safe", "survived", "would have
  passed", or an equivalent affirmation, and assert none of `PointClass`, `FxBandEnd`,
  `FtmoFirstBreachingLimit` is typed as `bool`.
  _Satisfies: spec.md "Every new discriminator is inspected for boolean or survival wording"
  scenario._
- [x] 9.2 GREEN: confirm 9.1 passes against the Phase 1/5/6 implementation (no production code
  expected — this is a coverage-only extension of an existing green test).

## Phase 10 — Static gates and full suite

- [x] 10.1 `timeout 300 dotnet build AppTradingAlgoritmico.slnx -warnaserror > build.log 2>&1` —
  zero warnings.
- [x] 10.2 `timeout 120 dotnet format AppTradingAlgoritmico.slnx --verify-no-changes > format.log
  2>&1` — no diffs.
- [x] 10.3 `timeout 600 dotnet test AppTradingAlgoritmico.slnx > test.log 2>&1` (full suite, once)
  — confirm the **854 pre-existing tests** all still pass, with NO existing assertion edited
  (task 8.2's 4 initializers are the only permitted existing-file change), plus every new test
  from Phases 0–9 passes.
- [x] 10.4 Confirm 0.1 (golden pin) is present in the final green run and unedited since task 0.2.
- [x] 10.5 If any pre-existing test fails or a warning/format diff appears, STOP and investigate
  before patching — a failing pre-existing test means a verdict silently changed, which task 0
  exists to catch.

---

## Flagged: items too vague for a checkable task, or requiring a judgment call at apply time

1. **Task 5.6's exact tag for the same-currency single-run case is not pinned by spec/design.**
   The spec only says "no FX-band-end comparison is performed"; it does not say whether the
   implementation should short-circuit to a distinct code path or legitimately fall through the
   same-row branch and land on `BothEnds`. Task 5.6 requires the apply phase to pick one behavior
   and pin it as a test, rather than leaving it to whichever the code happens to do.
2. **Task 6.2's "no more than 4 broken construction sites" assumption** comes from
   proposal.md's affected-areas table, not from a compiler run performed during this tasks phase.
   Task 8.1 is the actual verification step; if it finds more than 4, apply must stop and report
   rather than silently editing extra assertions.
3. **Task 4.9 (anchor day with no close)** is a design.md-named test case whose exact assertion
   shape (what the anchor day's own contribution should read as, if not counted) is left to the
   implementer, since neither spec.md nor design.md states the exact number for this edge case
   beyond "not counted."

## Flagged items — resolved by the orchestrator (2026-09-27)

1. **Task 5.6, same-currency symbols.** A USD-settled symbol evaluates the identity band (1, 1), so both ends are the same value: tag the point `BothEnds`. That is the truthful label; no comparison is skipped, the two ends simply coincide.
2. **Task 6.2, number of broken construction sites.** "Exactly 4" is the proposal's estimate. Task 8.1 counts the real number by compiling. If it is not 4, stop and report before editing anything.
3. **Task 4.9, an anchor day with no close.** Calendar days count from the anchor day. FTMO trading days count only distinct days that contain a replayed close, so an anchor day without a close does not contribute. Example: first trade opens Monday and closes Wednesday, breach on that Wednesday close → calendar days 2, FTMO trading days 1. This matches the definition in design Decision 5, where a breach on an anchor day that has a close gives 0 calendar days and 1 trading day.
