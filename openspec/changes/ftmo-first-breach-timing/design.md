# Design: Report WHEN each FTMO loss limit is first breached

## Technical Approach

Additive annotation (proposal D1). The evaluator's verdict branches stay as they are. `BreachPoint`
is widened with data the evaluator already computes. A new pure unit (`FtmoReplayCalendar`) owns the
anchor and elapsed-day arithmetic. A second pure unit (`FtmoBreachTiming`) merges timing across the
FX band and picks the first limit. `MergeFinding`'s verdict expressions do not change.

## Architecture Decisions

| # | Decision | Rejected | Rationale |
|---|---|---|---|
| 1 | New DTO members are **`required` init-only properties**, not positional parameters | Trailing positional params; optional `= null` params; plain nullable init props | Positional order and `Deconstruct` stay intact. `required` makes the compiler prove every construction chose a value, so "no defaults" holds and a missed assignment cannot silently read as "no breach". The cost is 4 construction sites in `StrategyBacktestsControllerTests` that gain `{ X = null }` initializers. |
| 2 | `DayAttribution.BookkeepingDay => CandidateDays.Min()` (additive property). The evaluator (line 157), the anchor and the close-day set all use it | Using `FtmoLocal.Date`; duplicating `.Min()` | One source of truth for traps 2 and 4. The reported day is exactly the day whose floor is reported. |
| 3 | `BreachPoint` gains `RowIndex`, `FtmoDay`, `Causes` (appended positionally; only the evaluator constructs it) | A parallel side-list | Trap 1: each point carries its own `dailyCauses`/`maxCauses` copy. Trap 3: `RowIndex` enables tie detection. |
| 4 | Anchor = the earliest `(OpenSource, RowIndex)` across **all** rows, Unscalable rows included, attributed through `FtmoDayClock.Attribute`. Ambiguous or invalid open → `BookkeepingDay` (earliest candidate) | Scalable rows only; first close | Open and close times do not depend on FX, so the anchor and close-day set are identical at both band ends. Excluding Unscalable rows would make the anchor depend on FX. The earliest candidate matches the bookkeeping rule. The `DateTimeKind` guard is inherited, not bypassed. |
| 5 | `CalendarDaysElapsed = breachDay.DayNumber − anchorDay.DayNumber`. `FtmoTradingDaysElapsed` = the number of distinct `BookkeepingDay`s among ALL replayed closes (Unscalable included) with `anchor ≤ d ≤ breachDay` | Counting while the loop runs | A count after the replay is exact even when days are non-monotonic around DST. A breach on the anchor day gives `0` / `1`. If the anchor day has no close, it is not counted. No clamp: a negative value would require `Close < Open` in the source data. |
| 6 | FX merge: take the earliest point by `(SourceTime, RowIndex)`. On the same row → `BothEnds`, with the **fxLow** values. `FirstBreach` and `FirstCleanBreach` are merged independently | Lower balance on a tie; exposing both ends (R3) | This mirrors `MergeFinding`'s preference for low. The point's causes are its own and never gain `FxRoundingSensitive`. That divergence shows in `FxBandEnd`. |
| 7 | Run level: `FtmoFirstLimitBreachDto(Limit, SourceCloseTime, FtmoTradingDay, FtmoTradingDaysElapsed, CalendarDaysElapsed)`. **No nested `Timing`** | Proposal D2's `Timing` field | With `BothSameClose`, one `Timing` would have to pick one limit's floor, causes and class, which contradicts D5. The fields that belong to the close are shared, and each limit's details stay on its own finding. |
| 8 | `PointClass` is derived when mapping (`Causes.Count == 0 ? Clean : Contingent`) | Storing it on `BreachPoint` | There is one source of truth, so the class and the causes cannot disagree. |

## Data Flow

```
ReadService.SimulateRun
  ├─ EvaluateAtFx(low)  → Evaluator → Evaluation{Daily,Max}.{FirstBreach,FirstCleanBreach}: BreachPoint+
  ├─ EvaluateAtFx(high) → (same)
  ├─ FtmoReplayCalendar.Build(raw trades, zones) → Anchor{SourceOpen, FtmoDay}, CloseDays
  ├─ MergeFinding(low,high, timing…)  verdict code unchanged; timing via initializers
  │     └─ FtmoBreachTiming.Earliest(lowPt, highPt) → (BreachPoint, FxBandEnd) → ToDto(calendar)
  └─ FtmoBreachTiming.FirstLimit(dailyMerged, maxMerged) → FtmoFirstLimitBreachDto?
Refused(...) → every new member explicitly null
```

## File Changes

| File | Action |
|---|---|
| `Infrastructure/Services/FtmoDayClock.cs` | Modify: add `BookkeepingDay` |
| `Infrastructure/Services/FtmoBreachEvaluator.cs` | Modify: widen `BreachPoint`; line 157 → `BookkeepingDay` |
| `Infrastructure/Services/FtmoReplayCalendar.cs` | Create: anchor and elapsed-day arithmetic (pure) |
| `Infrastructure/Services/FtmoBreachTiming.cs` | Create: FX earliest merge, first limit, mapping to DTO (pure) |
| `Infrastructure/Services/FtmoBreachSimulationReadService.cs` | Modify: wiring; `Refused` sets nulls |
| `Application/DTOs/Backtests/FtmoBreachSimulationDto.cs` | Modify: `FtmoBreachTimingDto`, `FtmoFirstLimitBreachDto`, `required` members |
| `Domain/Enums/FtmoBreachPointClass.cs`, `FtmoFxBandEnd.cs`, `FtmoFirstBreachingLimit.cs` | Create |
| `tests/.../Ftmo/*` (new tests), `StrategyBacktestsControllerTests.cs` (4 initializers) | Create/Modify |

## Interfaces / Contracts

```csharp
public sealed record FtmoBreachTimingDto(
    DateTime SourceCloseTime, DateOnly FtmoTradingDay, decimal BalanceAfterClose, decimal FloorLevel,
    FtmoBreachPointClass PointClass, IReadOnlyList<BreachContingencyCause> Causes,
    int FtmoTradingDaysElapsed, int CalendarDaysElapsed, FtmoFxBandEnd FxBandEnd);
// FtmoLimitFindingDto:        required FtmoBreachTimingDto? FirstBreach, FirstCleanBreach { get; init; }
// FtmoRunSimulationResultDto: required FtmoFirstLimitBreachDto? FirstLimitBreach;
//                             required DateTime? ReplayStartSourceTime; required DateOnly? ReplayStartFtmoDay
```

## Testing Strategy (Strict TDD)

**Hard rule for apply:** every shipped Ftmo test passes with NO assertion edited. The only permitted
edits to existing tests are the 4 `required` initializers in `StrategyBacktestsControllerTests`.

| Order | Unit | RED test |
|---|---|---|
| 0 | **Golden pin** (read service), written FIRST on unchanged `main` | Fixture: a contingent daily breach (concurrent open) followed by a clean one, on a EUR symbol with a band where Max disagrees. Pins `Verdict`/`Causes`/`DisclosureText` literals captured by running on `main`. It is a characterization test: green before the change, and it must stay green unedited. |
| 1 | `BookkeepingDay` | An ambiguous source time → the candidate min |
| 2 | `BreachPoint` | `Daily.FirstBreach.Causes == [ConcurrentOpenPosition]`, `FirstCleanBreach.Causes` empty, verdict `Breached`, `RowIndex`/`FtmoDay` set; a contingent finding's `Causes` equal `FirstBreach.Causes` |
| 3 | `FtmoReplayCalendar` | Earliest open including an Unscalable row; a tie on `RowIndex`; an ambiguous open → earliest candidate; a breach on the anchor day → 0/1; a weekend gap; an anchor day with no close |
| 4 | `FtmoBreachTiming` | Low earlier, high earlier, same row → `BothEnds` with low values, one end null; first limit Daily/Max/`BothSameClose`/null |
| 5 | Service wiring | USD → `BothEnds`; refused → all null; anchor echoed |

## Threat Matrix

N/A: there is no routing, shell, subprocess, VCS/PR automation, executable-file classification or process-integration boundary.

## Migration / Rollout

No migration required. Sizing confirmed: production ~170, tests ~550, realistic **~700–800**, one PR (`size:exception`).

## Open Questions

- [ ] When both ends breach on the same row with different balances (lot-grid clamps), `BothEnds` reports the fxLow values. Is that acceptable, or should the tie go to the lower balance?
