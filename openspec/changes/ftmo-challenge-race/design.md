# Design: The FTMO 2-Step challenge race

## Technical Approach

PR1 (D9) changes how `FtmoReplayCalendar.ElapsedDays` counts trading days. PR2 adds `FtmoChallengeRace`,
a pure unit that **composes** the unchanged `FtmoBreachEvaluator` with a new target scanner. The
evaluator already answers "where does the first breach happen on this trade subset from this capital".
The race only adds "where is the target reached" and compares the two. No floor arithmetic is copied.

## Architecture Decisions

| # | Decision | Rejected | Rationale |
|---|---|---|---|
| 1 | **Compose.** `RunPhase` calls `FtmoBreachEvaluator.Evaluate(phaseTrades, …, capital, dailyPct, maxPct)` and takes its first breach (`FtmoBreachTiming.FirstLimit` over Daily/Max `FirstBreach`). Its own loop only sums `Net` and checks the target | (a) Extract a shared floor stepper: this edits the shipped evaluator, and the golden pin covers one fixture. (b) Copy the floors plus a cross-check: two copies of the cause logic (Unscalable latch, DST flags) would drift | The breach points before the target depend only on the prefix of the trades, so the evaluator's first breach is exactly the race's breach. Phase 1 reuses the shipped evaluation, so D7's cross-check is identity by construction. Phase 2 is `Evaluate` on the phase-2 subset: capital resets, and the day-1 floor uses initial capital with no extra code. The evaluator is not edited and never truncates |
| 2 | **Close groups.** The scanner steps through groups of closes that share a `CloseSource`, in `RowIndex` order. The target check `T` runs after the last close in the group: balance ≥ capital×(1+target), days ≥ 4, and no scalable trade has `Open < T < Close` | Checking every row | Two trades that close at the same instant are one "all positions closed" event. This gives a clean handover: phase 1 is the trades with `Close ≤ T`, phase 2 is those with `Close > T`, and every phase-2 trade has `Open ≥ T` |
| 3 | **Breach wins.** If the first breach falls at or before `(T, lastRow)` → `BreachedFirst` | Target wins on the same close | When prev-midnight > 1.15×capital, a daily breach and balance ≥ target can happen on the same close. A breach ends the challenge |
| 4 | Overlap check: a new O(n log n) **sweep**. `openAt(T) = #{Open < T} − #{Close ≤ T ∧ Open < T}`, over scalable trades only | Reusing `HasConcurrentOpenPosition` (private, O(n²), counts Unscalable rows) | New code has no reason to be O(n²). Unscalable rows were never opened on FTMO (Q2). The evaluator's O(n²) scan stays, to keep it byte-identical. It is **recorded for the multi-start change**, where it gets multiplied per start |
| 5 | **Cutoff rule (D9, shared).** A scalable trade counts toward the trading days at a cutoff `c` iff `Open < c ∨ Close ≤ c`. It counts on its `BookkeepingDay(Open)` if that day is in `[anchor, eventDay]` | Counting by day up to `breachDay` (proposal wording) | Counting by day would include a position opened later on the breach day, after the account died. The race needs a count that stops at a point in time: an open that comes later cannot meet the minimum days at an earlier close |
| 6 | FX: run `RunPhase` at each end and compare the **ends**, not each phase on its own. Chains are ranked `P1 Breached < P1 Neither < P1 Target∧P2 Breached < P1 Target∧P2 Neither < P1 Target∧P2 Target`. The lower chain is reported. On a tie, the less favourable timing of the phase that decided it is reported (earlier breach, later target). A tie on the same row → `BothEnds` with the fxLow values. `FxRoundingSensitive` is set when the two outcome pairs differ | Merging each phase independently | Phase 2 exists only on its own end's phase-1 target, so mixing ends could combine a phase 2 from one end with a phase 1 from the other. For breaches, the timing rule matches shipped Decision 6 (earliest) |
| 7 | Rules: `FtmoChallengeRules.TwoStep` is fixed in code: 0.10 / 0.05 / 4 days, citing `SERVICE_FTMO.md:46-48,157-158,236`. The loss limits come from the stored row, as they do today. If a stored `ProfitTargetPct` is set and ≠ 0.10 → the race is refused, the run stays `Evaluated` | Refusing the whole run | A whole-run refusal would change shipped verdicts |

## Data Flow

```
SimulateRun ─ EvaluateAtFx(low/high) (unchanged) ─┐
            └ FtmoChallengeRace.Run(end.Projected, end.Evaluation, rules, zones, capital)  ×2
                 ├ P1 = RunPhase(all, shippedEval)   ── scanner + sweep → target T?
                 └ P2 = T ? RunPhase({Close>T}, Evaluate(subset)) : NotStarted
            └ FtmoChallengeRace.MergeEnds(lowChain, highChain) → FtmoChallengeRaceDto
```

## File Changes

| File | PR | Action |
|---|---|---|
| `Infrastructure/Services/FtmoReplayCalendar.cs` | 1 | Modify `ElapsedDays(anchor, eventDay, eventSourceClose, …)`: count opens by the cutoff rule |
| `Infrastructure/Services/FtmoBreachSimulationReadService.cs` | 1, 2 | 1: pass `Point.SourceTime`. 2: wiring, race refusal, `ChallengeRace = null` in `Refused` |
| `Infrastructure/Services/FtmoChallengeRace.cs`, `FtmoOpenPositionSweep.cs` | 2 | Create (pure) |
| `Application/DTOs/Backtests/FtmoBreachSimulationDto.cs` | 2 | Additive |
| `Domain/Enums/FtmoPhaseOutcome.cs`, `FtmoChallengeRaceRefusal.cs` | 2 | Create |
| `StrategyBacktestsControllerTests.cs` | 2 | 2 `ChallengeRace = null` initializers (sites verified) |

## Interfaces / Contracts

```csharp
public sealed record FtmoChallengePhaseDto(FtmoPhaseOutcome Outcome, DateTime? StartSourceOpen,
    DateTime? FirstTargetTouchSourceClose, DateOnly? MinTradingDaysMetFtmoDay,
    DateTime? OutcomeSourceClose, FtmoFirstBreachingLimit? BreachLimit, FtmoBreachPointClass? BreachPointClass,
    int? CalendarDaysElapsed, int? FtmoTradingDaysElapsed, FtmoFxBandEnd? FxBandEnd);
public sealed record FtmoChallengeRaceDto(FtmoChallengeRaceRefusal? Refusal, decimal? StoredProfitTargetPct,
    FtmoChallengeRulesDto Rules, FtmoChallengePhaseDto? Phase1, FtmoChallengePhaseDto? Phase2,
    bool FxRoundingSensitive, string Disclosure);
// FtmoRunSimulationResultDto: public required FtmoChallengeRaceDto? ChallengeRace { get; init; }  // null iff run Refused
```

`Neither` measures the time elapsed up to the phase's last replayed close. `NotStarted` → every timing field is null.

**Disclosure** (constant; the banned-word test covers it):
> "This race replays closed trades only and is not a prediction. A profit target reached first is an optimistic reading in two ways: swap is not modelled, so the target can arrive earlier than on a live account, and a closed-trade replay cannot see intraday equity dips, so breaches are understated. A breach reached first is a strong result, because both biases push against it. Neither by end of data means the replayed history ended before either event occurred."

## Testing Strategy (Strict TDD, each RED must be able to fail)

| PR | Unit | RED test |
|---|---|---|
| 1 | Characterization, captured on `main` FIRST | A fixture where a trade opens on day 1 and closes on day 3, then a breach. Assert the whole DTO `BeEquivalentTo` the captured snapshot, **excluding** the two `FtmoTradingDaysElapsed` paths. Then pin the new values (2, not 1) |
| 1 | Cutoff rule | An open after the breach on the breach day is not counted. An Unscalable-only day is not counted. The `:135` test is the ONE edited assertion (1→2, renamed) |
| 2 | Sweep | A property test against a brute-force oracle (the shipped predicate over scalable rows); a zero-duration trade at `T` |
| 2 | Scanner | Target before day 4, then fall back; sibling open at `T`; same-instant group; breach and target on the same close → `BreachedFirst` |
| 2 | Composition | Phase-1 breach row == shipped `FirstBreach` row **via the service** (fails if wired to `FirstCleanBreach` or the wrong end). The scanner's balance == `BreachPoint.Balance`. Phase-2 day-1 floor reset |
| 2 | FX merge | Every chain rank pair, timing tie-breaks, `BothEnds` |
| 2 | Service | Race refusal on 0.08 leaves the verdicts unchanged. The run refused → `ChallengeRace` null. The golden pin stays green UNEDITED |

## Threat Matrix

N/A: there is no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary.

## Migration / Rollout

No migration. Sizing floors: **PR1 ~160** (prod 40, tests 120); **PR2 ~850** (prod 330, tests 520), `size:exception`.

## Open Questions

- [ ] None blocking.
