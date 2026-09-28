# Design: FTMO multi-start replay

## Technical Approach

This design adds pure units that compose the shipped race and evaluator. For each monthly start, the
start's suffix goes through `FtmoChallengeRace.RunChain` (unchanged), then a funded phase that calls the
unedited `FtmoBreachEvaluator`. After that, a new 3-phase FX merge runs, then the aggregates. A new read
service reuses the shipped guard chain and projection. Those two pieces are extracted verbatim first.
Before any new logic ships, a benchmark gate decides whether one optimisation is needed.

## Architecture Decisions

| # | Decision | Rejected | Rationale |
|---|---|---|---|
| 0 | **PR0 fixes two bugs in the SHIPPED `FtmoChallengeRace.cs`, before PR1.** Bug A: `RunPhase`'s scanner runs to completion and does not stop at the breach close, so a `BreachedFirst` phase can report `FirstTargetTouchSourceClose`/`MinTradingDaysMetFtmoDay` from after the breach. Fix: on `BreachedFirst`, null both fields unless they occur at or before the breach close; `Outcome` and breach fields (`OutcomeSourceClose`, `BreachLimit`, `BreachPointClass`) are untouched. Bug B: `RunChain`'s handover filters `Close > T` only, so an `Unscalable` row straddling T (`Open < T`, `Close > T`) can enter phase 2 and become its anchor. Fix: `Open >= T AND Close > T`; a zero-duration row at T stays in the current phase. Both RED-first against the shipped `FtmoChallengeRaceTests`/golden pin; **0 existing assertions edited** — verified by grep: every existing `FirstTargetTouchSourceClose`/`MinTradingDaysMetFtmoDay` assertion in `FtmoChallengeRaceTests.cs` (lines ~74, 158, 259, 326, 378–379) is on a `TargetReachedFirst` or `NotStarted` phase, none on a post-breach touch; `PhaseTwoStartsAtTheNextTradeOpenedAfterThePhaseOneDecision` uses no straddling row. The funded phase (Decision 3) and start slicing (Decision 4) are written against the fixed handover from the start, so they never inherit either bug | Leaving both as documented, accepted quirks (the design's original stance) | A bug fix is supposed to change incorrect output, not document it; both bugs are in code this change extends, so fixing them before extending is cheaper than extending buggy behaviour and fixing it under the new call sites later |
| 1 | **Benchmark first (PR1, task 1).** A deterministic synthetic fixture with the gold series' shape: 1,000 trades from 2016-01 to 2025-12 (~8 a month), H1 holds of 1–72 h, 5 zero-duration trades, no overlaps. It has two P/L profiles: *fast* (targets in ~30–50 days) and *never* (no start ever reaches +10%, which is the scanner worst case). The fixture uses the shipped units as proxies: per start, `RunChain(suffix)` plus `Evaluate(suffix)`, at 2 FX ends, for 2 runs. It runs in Release and takes the median of 3 runs (against the PR0-fixed `RunChain`). **Gate: *never* profile > 5 s per request → optimise.** Optimisation = cache each trade's open day once per `RunPhase`, in a race-private `ElapsedDays` equivalent. It is proven by a property test that checks it equals `FtmoReplayCalendar.ElapsedDays` at every close-group cutoff of both fixtures, plus `RunChain` equality on every start. If it is still > 5 s → stop and escalate (async is a new decision) | Reading the DB in tests; a timing assert in CI (flaky); ending the scan at the breach as a *performance* shortcut (that would additionally suppress the touch/day-minimum readouts on the `TargetReachedFirst`/`NeitherByEndOfData` path, which PR0 does not touch — PR0 fixes only the `BreachedFirst` readout, per bug A, not the scan's stopping point) | 5 s is the ceiling for an interactive report GET. Proxy timing needs no new code, so the gate comes before the design is spent. The fixture shape tests (count, months, zero-duration, no overlap) are the RED. The timing test is a `BenchmarkFact` that is **skipped** unless `FTMO_BENCH=1` is set, so it never passes vacuously |
| 2 | **Extract `FtmoChallengeRace.FirstBreach(evaluation) → (BreachPoint, FtmoFirstBreachingLimit)?`**, moving lines 118–147 verbatim. `RunPhase` and the funded phase both call it (PR1) | Calling `FtmoBreachTiming.FirstLimit` (equivalent, but the proof would need a read, not a move); editing the evaluator | The existing race, FX-merge and service tests and the golden pin prove it, **unedited**. A direct 4-branch test (RED: the method does not exist yet) pins it for the funded caller |
| 3 | **Funded phase** = `Evaluate(Open ≥ T2 AND Close > T2, capital, daily, max)` → `FirstBreach`, the same PR0-fixed handover rule as `RunChain`. Elapsed days come from the funded anchor and from **this start's** phase-1 anchor. An empty subset → `NoBreachByEndOfData`, runway 0 | `Close > T2` alone (bug B's shape) | Using the fixed handover rule keeps phase 2 → funded consistent with the PR0-fixed phase 1 → phase 2, so no straddling row can become the funded anchor either |
| 4 | **Series** = the projection sorted by `(Open, RowIndex)`, sliced from the first position with `Open ≥ startOpen`. **There is no leak.** A trade opened at an earlier start that is still open at a later `startOpen` has `Open < startOpen`, so it is outside the later start's slice. Its P/L, its evaluator overlap flag (`HasConcurrentOpenPosition` scans only the passed subset), the `OpenAt` flat-book check, the anchor and the trading days all ignore it. A fresh account never held it | Carrying positions still open at `startOpen` | A RED test uses a straddling −US$5,000 trade. If it leaked, it would breach the later start or block its target |
| 5 | **Merge3**: ranks `P1B 0 < P1N 1 < P2B 2 < P2N 3 < FB 4 < FN 5`. Ranks 0, 2 and 4 use the earlier close (less favourable). Ranks 1, 3 and 5 report fxLow, tagged. The same deciding close → `BothEnds`. The sensitive flag also compares the funded outcome and close. **Pin:** when the lower rank is ≤ 3, `(P1, P2, End, Sensitive)` equals `MergeEnds` | Claiming equality everywhere (false, finding 4) | When both ends reach the funded phase, the funded phase decides by design |
| 6 | **Degenerate band** (`fxLow == fxHigh`) → one chain, `BothEnds`, not sensitive | — | A test pins that the shortcut equals `Merge3(x, x)` on every fixture start |
| 7 | **Order statistics**: nearest rank, `r = ⌈p·n⌉` (1-based) on sorted ints. `n = 0` → every quantile is null | Interpolation | Every quantile is an observed start |
| 8 | **Guard/projection extraction in PR3**, refactor only. `FtmoSimulationInputs.ResolveSharedAsync` covers runs, limits, spec, calibration, FX, grid and zones. `ProjectRun` covers segments → normalizer → `RefuseRunInputs` → low/high projection and counts. `RefuseAll`/`Refused`/evaluation stay in the shipped service | Extracting inside PR4 | It needs its own diff that edits no assertion and has no new consumer. Proof: the golden pin, the challenge-race PR1 snapshot pin, and the full suite, all unedited. PR4 then builds on a surface that is already green and frozen |
| 9 | Synchronous. `ct.ThrowIfCancellationRequested()` before each start | Background job | Decision gate 1 |

## Data Flow

```
GET …/ftmo-breach/multi-start → FtmoSimulationInputs.ResolveSharedAsync ─(refusal)→ RefuseAll
  per run: LoadTrades → ProjectRun ─(refusal)→ Refused
           ProfitTargetMismatch → Evaluated, RaceRefusal set, Starts []
           EnumerateStarts(low) → per start: slice low/high
             → RunChain + Funded (×1 or ×2) → Merge3 → Classify → row
           → Summarize(rows)
```

## File Changes

| File (Infrastructure/Services unless noted) | PR | Action |
|---|---|---|
| `FtmoChallengeRace.cs` | 0 | Bug fixes A and B (Decision 0) |
| `FtmoOpenPositionSweep.cs` | 0 | Doc-comment fix only: the class comment describes it as "an O(n log n) sweep", but `OpenAt` is a single `foreach` (O(n)) with no sort of its own and no ordering precondition (per its own method doc); comment-only, no behaviour change |
| `FtmoChallengeRace.cs` | 1 | `FirstBreach` extraction; the optional cache (Decision 1) |
| `FtmoFundedPhase.cs`, `FtmoMultiStartChain.cs` (Chain3, Classify, Merge3) | 1 | Create |
| `FtmoStartEnumerator.cs`, `FtmoOrderStatistics.cs` | 2 | Create |
| `FtmoSimulationInputs.cs`; `FtmoBreachSimulationReadService.cs` | 3 | Create; delegate to it |
| `FtmoMultiStartReadService.cs`, `Application/Interfaces/IFtmoMultiStartReadService.cs`, `Application/DTOs/Backtests/FtmoMultiStartDto.cs`, `Domain/Enums/{FtmoChainOutcome,FtmoFundedOutcome,FtmoStartGrain}.cs`, `DependencyInjection.cs`, `StrategyBacktestsController.cs` (shared query validation helper + action), `StrategyBacktestsControllerTests.cs` (1 `CreateSut` edit) | 4 | Create/additive |

## Interfaces / Contracts (illustrative)

```csharp
// Enum zeros are non-optimistic: FtmoChainOutcome.Phase1UndecidedAtEndOfData = 0, FtmoFundedOutcome.NotStarted = 0, FtmoStartGrain.Monthly = 0
public sealed record FtmoMultiStartDto(Guid StrategyId, IReadOnlyList<FtmoMultiStartRunDto> Runs);
public sealed record FtmoMultiStartRunDto(Guid RunId, BacktestRunKind Kind, BacktestSegment Segment,
    FtmoSimulationStatus Status, FtmoSimulationRefusal? Refusal, FtmoChallengeRaceRefusal? RaceRefusal,
    decimal? StoredProfitTargetPct, FtmoStartGrain Grain, FtmoChallengeRulesDto Rules,
    IReadOnlyList<FtmoMultiStartRowDto> Starts, FtmoMultiStartSummaryDto? Summary,
    IReadOnlyList<DateOnly> MonthsWithoutStart /* first day of month */, bool Start1DiffersFromSingleStartAnchor,
    decimal? FxLow, decimal? FxHigh, int UnscalableCount, IReadOnlyList<string> NotModelled, string Disclosure);
public sealed record FtmoMultiStartRowDto(int Index, DateTime StartSourceOpen, DateOnly StartFtmoDay,
    DateOnly FtmoMonth, FtmoChainOutcome Outcome, bool IsCensored, int? RunwayCalendarDays,
    int? CalendarDaysToBothTargets, FtmoChallengePhaseDto Phase1, FtmoChallengePhaseDto Phase2,
    FtmoFundedPhaseDto Funded, FtmoFxBandEnd FxBandEnd, bool FxRoundingSensitive);
public sealed record FtmoFundedPhaseDto(FtmoFundedOutcome Outcome, DateTime? StartSourceOpen,
    DateTime? OutcomeSourceClose, FtmoFirstBreachingLimit? BreachLimit, FtmoBreachPointClass? BreachPointClass,
    int? CalendarDaysFromFundedStart, int? FtmoTradingDaysFromFundedStart,
    int? CalendarDaysFromChainStart, int? FtmoTradingDaysFromChainStart);
public sealed record FtmoOutcomeCountDto(FtmoChainOutcome Outcome, bool IsCensored, int Count, decimal Share);
public sealed record FtmoOrderStatisticsDto(int N, int? Min, int? Q1, int? Median, int? Q3, int? Max);
public sealed record FtmoMultiStartSummaryDto(int StartCount, IReadOnlyList<FtmoOutcomeCountDto> Outcomes /* all 6 */,
    int FxRoundingSensitiveCount, FtmoOrderStatisticsDto DaysToPhase1Target, FtmoOrderStatisticsDto DaysToPhase2Target,
    FtmoOrderStatisticsDto DaysToBothTargets, FtmoOrderStatisticsDto FundedDaysToBreachFromFundedStart,
    FtmoOrderStatisticsDto FundedDaysToBreachFromChainStart, FtmoOrderStatisticsDto CensoredRunway);
```

All durations are integer calendar days. Enums serialize as numbers (there is no string converter), and
decimals as JSON numbers. Runway is the censored phase's `CalendarDaysElapsed`, or 0 when that phase had
no trades.

## Testing Strategy (Strict TDD; every RED can fail)

| PR | RED tests |
|---|---|
| 0 | Bug A: a `BreachedFirst` phase whose touch/day-minimum-met close land after the breach close report null for both; a `BreachedFirst` phase whose touch/day-minimum-met close land before the breach close still report them. Bug B: an `Unscalable` row with `Open < T < Close` is excluded from phase 2's subset; a zero-duration row at exactly T stays in phase 1's group. Proof of no regression: **0 assertions edited** in `FtmoChallengeRaceTests.cs`/`FtmoChallengeRaceFxMergeTests.cs` — confirmed by reading every existing `FirstTargetTouchSourceClose`/`MinTradingDaysMetFtmoDay` assertion (none is on a post-breach `BreachedFirst` touch) and the one existing phase-2-handover test (no straddling row); shipped suite and golden pin stay green |
| 1 | Fixture shape. `FirstBreach` 4 branches. Funded: breach, no breach, empty, day-1 floor reset, both elapsed origins (40/135). Classify 6 outcomes. Merge3 every rank pair, tie-breaks, scoped `MergeEnds` pin. Cache equivalence (if built) |
| 2 | A month without a start is listed. A same-instant tie goes to the lower `RowIndex`. Month boundary on Berlin day. Unscalable-only month. Nearest rank for n = 0, 1, 4, 5 |
| 3 | None new. Proof = existing suite green, **0 assertions edited** |
| 4 | Counts sum to the start count. Six outcomes with zeros. Straddle no-leak. Degenerate = `Merge3(x, x)`. Start 1 == single-start `ChallengeRace` when the first trade is scalable. Divergence flag when it is Unscalable. Cancellation mid-run. Banned words over enums and disclosure. Controller 400 |

## Threat Matrix

N/A: the change has no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary.

## Migration / Rollout

No migration. Five PRs are chained: **PR0 ~120** (prod 20, tests 100), **PR1 ~600** (prod 190, tests
410), **PR2 ~250**, **PR3 ~180**, **PR4 ~700** (prod 330, tests 370). The total is ~1,850. PR1 and PR4
need `size:exception`.

## Open Questions

- [ ] None blocking. If the gate still fails after the cache, it escalates (Decision 1).
