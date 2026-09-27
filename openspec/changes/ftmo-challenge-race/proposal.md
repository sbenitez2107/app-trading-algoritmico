# Proposal: The FTMO 2-Step challenge race — profit target versus first breach

> Next step (ii) of roadmap layer 1 (`SIMULATOR_ROADMAP.md:210`). Backend only. No `explore.md`.
> **Existing verdicts stay byte-identical.** One shipped readout is recalculated (D9) and disclosed.

## Intent

The shipped simulation says whether and when a loss limit breaks. It does not say whether the account
reaches the profit target first, which is what decides a 2-Step challenge. The time limit is
unlimited (`SERVICE_FTMO.md:67`), so the question is only which event comes first.

## Scope

### In Scope
1. Per run: a phase-1 (Challenge, 10%) race and, only if phase 1 reaches its target, a phase-2
   (Verification, 5%) race on a fresh account.
2. A typed outcome per phase, event timing, and a disclosure on every race.
3. Fixing the shipped trading-day defect (D9).

### Out of Scope
- Multiple start dates. That is the next step; D6 prepares for it.
- Funded mode, swap, intraday equity, portfolio level, any Angular change. Verified: nothing in
  `app.trading.algoritmico.web/src` references this endpoint.

## Capabilities

### New Capabilities
- `ftmo-challenge-race`: per-phase target-versus-breach race, outcomes, timing, disclosure.

### Modified Capabilities
- `ftmo-breach-simulation`: MODIFIED "Elapsed Time Is Measured From The Replay-Start Anchor".
  `FtmoTradingDaysElapsed` now counts days with a position opened. ADDED: the race leaves
  `Verdict`/`Causes`/`DisclosureText` byte-identical.

## Approach

| # | Decision |
|---|---|
| D1 | A phase reaches its target at the first close where balance ≥ initial × (1 + target), the phase has ≥ 4 trading days, and **no other position is open** ("all positions closed", `:48`). An open sibling position means the phase has not passed at that close; it is not a contingency. |
| D2 | Between the target and day 4 the replay keeps trading at full risk. The balance may fall back below the target, and a breach still ends the phase. |
| D3 | Any first breach ends the phase, clean or contingent (`Limit ∈ Daily/Max/BothSameClose`). The point's class is reported. |
| D4 | Phase 2 starts at the **first trade opened after the phase-1 target close**, at initial capital, with the day-1 floor at initial capital. D1 guarantees that no position spans the handover. No verification delay is modelled, because that would be a fabricated value. |
| D5 | `PhaseOutcome ∈ {TargetReachedFirst, BreachedFirst, NeitherByEndOfData, NotStarted}`. No booleans, and no "pass" wording anywhere. |
| D6 | A new pure `FtmoChallengeRace.RunPhase(orderedTrades, startOpen, capital, rules)`. Phase 2 already calls it from a start that is not the anchor, so rolling starts are a loop over `startOpen`. |
| D7 | It is a separate loop. `FtmoBreachEvaluator` is not touched: the race truncates at the first breach, while the shipped evaluator must not (R1). A cross-check test pins that the phase-1 breach row equals the shipped `FirstBreach` row whenever the breach precedes the target. |
| D8 | The 2-Step rules are fixed in code, keyed on `FtmoProduct.TwoStep`, cite the KB (`:46-47`, `:157-158`, `:236`), and are echoed on the result. The single `ProfitTargetPct` column cannot hold 10% and 5%, and `Stages` are rejected on `LossLimits` rows (`RiskLimitsService.cs:118-119`). |
| D9 | **Defect**: recalculate `FtmoTradingDaysElapsed`, keeping its name, to count distinct FTMO days with at least one replayed (non-Unscalable) position **opened**. One meaning, shared by the race. A rename was rejected because PowerShell reads a missing property as `$null` without any error. |
| D10 | FX band: run the race at both ends. If the two ends disagree, report the less favourable outcome with `FxRoundingSensitive` and the `FxBandEnd`. |
| D11 | DTO: `ChallengeRace` (required, null when the run is refused) on `FtmoRunSimulationResultDto`, holding `Phase1`, `Phase2`, the rules, and `Disclosure`. Each phase carries its outcome, start, first target touch, day the minimum days were met, outcome close, and elapsed calendar and trading days. |

Disclosure: a target reached first is optimistic twice. Unmodelled swap makes the target arrive
earlier than it would, and a closed-trade replay understates breaches. A breach before the target
remains a strong result. The disclosure must not use the word "passed".

## Affected Areas

| Path | Impact |
|---|---|
| `Infrastructure/Services/FtmoChallengeRace.cs` | New |
| `Infrastructure/Services/FtmoReplayCalendar.cs` | Modified (D9) |
| `Infrastructure/Services/FtmoBreachSimulationReadService.cs` | Modified: wiring |
| `Application/DTOs/Backtests/FtmoBreachSimulationDto.cs`, `Domain/Enums/` | Additive |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| `TargetReachedFirst` is read as a prediction | High | Disclosure on every race |
| The race's copy of the floor logic drifts from the evaluator | Med | D7 cross-check |
| The recalculated trading days surprise the user | Med | Disclosure. The recorded roadmap measurements are calendar days and are unaffected |

## Rollback Plan

Revert the PR(s). There is no migration and no persisted state.

## Sizing

Floor ~700 lines, realistic 1000+. `400-line budget risk: High`. There are two reviewable intents,
so this is two chained PRs: PR1 is D9 (~120 lines), PR2 is the race.

## Success Criteria

- [ ] Shipped Ftmo suites are green with assertions unedited, except the D9 elapsed-days scenarios.
- [ ] Every run that is not refused carries both phases, typed outcomes, timing and the disclosure.
- [ ] The D7 cross-check holds.

## Proposal question round (blocked, needs a human)

1. **Q1**: If the stored FTMO row's `ProfitTargetPct` is set and is not 0.10, should the race ignore
   it and echo the value (assumed), or refuse?
2. **Q2**: Is it acceptable to exclude Unscalable rows (source `Size ≤ 0`) from trading days (assumed)?
3. **Q3**: D2 keeps full-risk trading after the target, which is pessimistic compared with a trader
   who places minimal trades to reach day 4. Keep that, or model minimal trades?

## Question round — resolved (2026-09-27)

1. **A stored `ProfitTargetPct` that is set and differs from 0.10** → the race **refuses** with a typed reason, echoing both values. `BrokerRiskLimits` percentages are user-sourced (`BrokerRiskLimits.cs:9`); silently overriding one with a code constant would contradict a fact the user entered. A null `ProfitTargetPct` uses the 2-Step rule. The user's stored FTMO row holds 0.10 today, so this does not fire now. *(Orchestrator decision.)*
2. **Unscalable rows (source `Size <= 0`) do not count as trading days.** They would never have been opened on the FTMO account. The anchor still includes them, so an anchor day may count zero trading days; that is consistent, not a defect. *(Orchestrator decision.)*
3. **Between reaching the target and reaching the 4-day minimum, the replay keeps trading the strategy at normal risk** — confirmed by the user. Modelling minimum-lot "day-filler" trades was rejected: it would invent a trading behaviour the strategies do not have. To make the effect visible, each phase reports **when the target was first touched** as well as when the phase was decided, so the user can see how often the target arrived before day 4 and what happened in that window. *(User decision.)*
