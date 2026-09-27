# Proposal: Report WHEN each FTMO loss limit is first breached

> Follow-up to the shipped `ftmo-breach-simulation` capability (archived 2026-09-25). No `explore.md`:
> the change comes from a real run on 2026-09-26. One PR, backend only.
> **This change adds timing. It does not change a single existing verdict** (see D1).

## Intent

On 2026-09-26 the user ran the shipped endpoint on `WF_6_22_XAUUSD_H1_SMA_BB_2.48.334` (10k FTMO
2-Step) at four risk levels:

| Risk/trade | Daily | Max |
|---|---|---|
| 50 | NoBreachObserved | NoBreachObserved |
| 150 | NoBreachObserved | NoBreachObserved |
| 300 | Breached | NoBreachObserved |
| 500 | Breached | NoBreachObserved |

The tool discriminates correctly. But `Max = NoBreachObserved` at US$500 is **misleading**. FTMO
closes the account at the first breach of EITHER limit. If the daily limit broke in month 3, nothing
the max-loss replay found over the next ten years matters. The result does not say WHEN a breach
happened. For the user's decision (buy the challenge or not, and at what risk), the timing matters
more than the verdict.

**Why now**: the endpoint is shipped and in use. Right now its most-read output misleads at exactly
the risk levels where the user is deciding.

## Scope

### In Scope
1. **Per limit**: the first breaching close. That means its source close time, its FTMO trading day,
   the balance after the close, the floor it fell below, whether it was clean or contingent (with
   causes), and the elapsed time from replay start. The first *clean* breaching close is reported
   the same way when it is a different close.
2. **Per run**: which limit breaks first, and when. If both first breaches fall on the same close,
   the result says so explicitly.
3. **Per run**: the replay-start anchor that the elapsed time is measured from, echoed on the result.
4. Carrying the per-point data the evaluator currently drops (see "Shipped-code findings").

### Out of Scope
- **Truncating the replay at the first breach.** Rejected, see R1.
- **Rolling or multiple start dates.** This is the natural next step (see Non-Goals).
- Any Angular change. Verified: nothing in `app.trading.algoritmico.web/src` references the
  breach-simulation endpoint or its DTOs.
- Any change to verdict, cause, disclosure, refusal or resize-count logic.

## Non-Goals (the likely misreading)

**A breach date is not a property of the strategy.** The replay starts at the backtest's first trade
(for example, January 2016). A breach in March 2016 means "an account opened on that date lasted
N days". It is an artefact of that start date. That is why elapsed time is reported next to the
date, and why the start anchor is echoed. A strategy-level survival measure needs a *distribution*
over many start dates. That is rolling start dates, which this change names as the next step but
does not build.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `ftmo-breach-simulation`: ADDED requirements for first-breach timing per limit, first-limit-to-break
  per run, elapsed time from a declared replay-start anchor, and FX-band timing reporting. Also a
  requirement that existing verdicts, causes and disclosure text stay byte-identical. No existing
  requirement is modified or removed.

## Approach

The evaluator already tracks `FirstBreach`/`FirstCleanBreach` as `BreachPoint`s. The shipped design
(design.md Decision 5) says "each finding reports `FirstBreach` and `FirstCleanBreach`", but the
public DTO drops them. The core of this change is to widen `BreachPoint` with the data it currently
loses, then map it through `MergeFinding`. The branches that decide the verdict do not change.

### Architecture decisions

**D1: Annotate, never truncate.** The replay still runs over the full series. `Verdict`, `Causes`
and `DisclosureText` must stay byte-identical. All new data is additive.

**D2: A new nested record, referenced from two places.** `FtmoBreachTimingDto` is one breaching
close. It hangs off `FtmoLimitFindingDto` as `FirstBreach` and `FirstCleanBreach`, because timing
belongs to a limit. It is also used inside a run-level `FtmoFirstLimitBreachDto` on
`FtmoRunSimulationResultDto` as `FirstLimitBreach`, because "which limit broke first" compares two
limits, so it cannot live on either one. Fields:

| Record | Fields |
|---|---|
| `FtmoBreachTimingDto` | `SourceCloseTime`, `FtmoTradingDay` (`DateOnly`, the day the floor bookkeeping used), `BalanceAfterClose`, `FloorLevel`, `PointClass` (`Clean \| Contingent`), `Causes`, `FtmoTradingDaysElapsed`, `CalendarDaysElapsed`, `FxBandEnd` (`FxLow \| FxHigh \| BothEnds`) |
| `FtmoFirstLimitBreachDto` | `Limit` (`Daily \| Max \| BothSameClose`), `Timing` |
| `FtmoRunSimulationResultDto` (added) | `FirstLimitBreach?`, `ReplayStartSourceTime?`, `ReplayStartFtmoDay?` |

- Enums are used instead of booleans, keeping the capability's no-boolean rule. No value is
  defaulted. Null means "no breach of this limit", and `NoBreachObserved`'s existing disclosure
  already says that null is not reassurance. A refused run leaves every new field null.
- **Replay-start anchor**: the FTMO day of the replay's first close. That close may be Unscalable;
  it is the first instant the replay evaluates. `CalendarDaysElapsed` = breach day minus anchor day.
  `FtmoTradingDaysElapsed` = the number of distinct FTMO days, from the anchor through the breach
  day inclusive, that have at least one replayed close. This counts only what the replay contains.
  It does not use a holiday calendar.

**D3: Capture without touching the verdict branches.** `BreachPoint` gains `RowIndex`, `FtmoDay`
and its own `Causes`. These values are already computed at the point where each point is built, so
recording them does not change any branch. Proof that verdicts are unchanged:
- (a) The shipped Ftmo suites stay green, and no existing assertion is edited.
- (b) A new pinning test runs a breaching fixture: a contingent daily breach followed by a clean one,
  plus a disagreeing FX band. It asserts `Verdict`/`Causes`/`DisclosureText` equal golden values
  captured from `main` before the change.

**D4: The FX band can produce two different first-breach points.** It can, even when both ends
agree on the verdict, because the lot-grid floor and clamps shift balances. When the two ends
disagree on the verdict, one end has no breach at all. The result reports the **earliest** point
across both ends, ordered by `(SourceCloseTime, RowIndex)`. `FxBandEnd` records which end produced
it, or `BothEnds` when both ends hit the same close. Choosing the earliest follows the same
conservative rule as the evaluator's earliest-candidate-day choice, and a result other than
`BothEnds` makes the divergence visible.

**D5: Same-close tie.** If the daily and max first breaches are the same close (same `RowIndex`),
`Limit = BothSameClose`. The result never picks one of them.

### Rejected alternatives

| # | Rejected | Reason |
|---|---|---|
| **R1** | Truncate the replay at the first breach | A first breach may be `BreachContingent`, possibly not a real breach. Truncating would hide a later clean breach. It would also silently change the meaning of a shipped readout. |
| R2 | Timing only on the run result | Loses the per-limit timing the user needs to read each verdict |
| R3 | Report both FX ends' points always | This doubles the shape for the dominant USD case, where the two are identical. D4's `FxBandEnd` shows the divergence at a fraction of the size (see Q2) |
| R4 | Anchor at the backtest's configured start date | That date is not in the trade data, so using it would be a fabricated value |

## Shipped-code findings (make the settled decisions harder than they look)

1. **Causes of a clean-verdict's first breach are discarded.** `BuildFinding` passes `[]` for
   `Breached`, even when `FirstBreach` was an earlier contingent close
   (`FtmoBreachEvaluator.cs:225-240`). Decision 2 needs those causes, so `BreachPoint` must carry
   them.
2. **`BreachPoint` has no FTMO day.** `FtmoTime` is `FtmoLocal`. For an ambiguous or invalid source
   time, its date may differ from the `CandidateDays.Min()` day that the bookkeeping used (line 157).
3. **`MergeFinding` keeps only `low`'s causes when the verdicts agree** (`FtmoBreachSimulationReadService.cs:279-280`).
   This must stay byte-identical, so timing must be merged separately from the verdict.
4. **No `RowIndex` on `BreachPoint`**, so a same-close tie (D5) cannot be detected from time alone.

## Affected Areas

| Path | Impact |
|---|---|
| `Infrastructure/Services/FtmoBreachEvaluator.cs` | Modified: `BreachPoint` widened, replay anchor exposed |
| `Infrastructure/Services/FtmoBreachSimulationReadService.cs` | Modified: timing merge across the FX band, run-level first limit |
| `Application/DTOs/Backtests/FtmoBreachSimulationDto.cs` | Modified: new records and enums, additive fields |
| `Domain/Enums/` | New: `FtmoBreachPointClass`, `FtmoFxBandEnd`, `FtmoFirstBreachingLimit` |
| `tests/.../Ftmo/*`, `StrategyBacktestsControllerTests.cs` | Modified/new: 4 DTO constructions updated |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| A breach date is read as a strategy property | High | Non-Goals; elapsed time and the echoed anchor |
| A verdict drifts silently | Low | D3's golden pin test |
| Null timing is read as reassurance | Med | The existing `NoBreachObserved` disclosure stays; the spec pins that null carries no survival wording |
| Sizing overrun | High | The figures below are a floor |

## Rollback Plan

Revert the single PR. There is no migration, no persisted state and no web consumer. All fields are
additive, so API callers that ignore them are unaffected either way.

## Dependencies

- None blocking. The shipped `ftmo-breach-simulation` capability, the instrument specs, and
  `FtmoDayClock` are used unchanged.

## Sizing

Production ~110, tests ~280. **Floor ~390, realistic ~700–800.** `400-line budget risk: High`.
**One PR.** There is no honest seam: DTO fields without the evaluator capture have nothing to carry,
and the capture without the mapping is invisible. Exceeding the budget is a `size:exception`
conversation, not a split.

## Success Criteria

- [ ] Every shipped Ftmo test passes with its assertions unedited, and the golden pin test is green.
- [ ] A breaching limit reports its first breaching close with all D2 fields. A non-breaching limit
      reports null.
- [ ] The run reports the first limit to break, or `BothSameClose`.
- [ ] Elapsed trading and calendar days are measured from the echoed anchor.
- [ ] A divergent FX band reports the earliest point and names the band end it came from.
- [ ] No new field contains pass or survival wording, and no new field is a boolean.

## Proposal question round (blocked, needs a human)

1. **Q1: Replay-start anchor.** The assumed anchor is the first replayed *close*. The first trade's
   *open* is earlier and is when capital is first at risk. Which one should the user read as
   "account opened"?
2. **Q2: FX band.** Is reporting the earliest point plus `FxBandEnd` enough, or should both ends'
   points always be exposed for EUR symbols (GER40)?
3. **Q3: First *clean* limit to break at run level.** Only the first *any* breach is reported at run
   level. Should the run also say which limit breaks first counting clean breaches only?
