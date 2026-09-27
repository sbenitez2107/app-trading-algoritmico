# FTMO Challenge Race Specification

## Purpose

The shipped `ftmo-breach-simulation` capability reports whether and when a loss limit breaks. It does
not report whether the replayed account reaches the profit target first, which is the actual pass/fail
decision of an FTMO 2-Step challenge. This capability adds that race: per phase (Challenge, then
Verification), it reports whichever of "target reached" or "first breach" happens first in the
replayed data, at a single fixed start date (the replay-start anchor already defined by
`ftmo-breach-simulation`).

This is a NEW capability. It does not touch, truncate, or reorder the shipped breach replay; it runs a
separate, truncating loop over the same trade series and reports a distinct result.

## Requirements

### Requirement: Two-Step Challenge And Verification Rules Are Fixed In Code, Not Configurable
The race MUST evaluate exactly the FTMO 2-Step ruleset: a phase-1 (Challenge) profit target of 10% of
Initial Capital, a phase-2 (Verification) profit target of 5% of Initial Capital, both non-compounding
(computed against Initial Capital, never against an intermediate balance), a minimum of 4 FTMO trading
days per phase, and an unlimited time limit for both phases. These values MUST be fixed in code, keyed
on `FtmoProduct.TwoStep`, and MUST be echoed on every non-refused race result. A `BrokerRiskLimits` row
whose `FtmoProduct` is null or not `TwoStep` MUST cause the race to be refused, the same as the shipped
breach simulation.

> Both targets, the non-compounding basis, the per-phase day minimum, and the unlimited time limit are
> confirmed HIGH-confidence facts (`SERVICE_FTMO.md:44-52` for targets and basis, `:67` for unlimited
> time, `:157-158` for the 4-day minimum). `LossLimits` rows have a single `ProfitTargetPct` column,
> which cannot hold both 10% and 5% at once, and `Stages` are rejected on `LossLimits` rows
> (`RiskLimitsService.cs:118-119`) — there is no storage shape for a per-phase configurable target, so
> fixing both values in code is the only option that does not invent a schema this system does not have.

#### Scenario: Both phase targets and the day minimum are echoed on a non-refused race
- GIVEN a race run against a `TwoStep` `BrokerRiskLimits` row
- WHEN the result is produced
- THEN it echoes a 10% phase-1 target, a 5% phase-2 target, a 4-trading-day minimum for both phases,
  and no time limit

#### Scenario: A non-TwoStep or missing product refuses the race
- GIVEN a `BrokerRiskLimits` row with `FtmoProduct` null or `OneStep`
- WHEN a challenge race is requested against it
- THEN the race is refused with an explicit reason and no phase result is produced

#### Scenario: Loss limits are read unchanged across both phases
- GIVEN a `TwoStep` `BrokerRiskLimits` row with a given `DailyLossLimitPct` and `MaxLossLimitPct`
- WHEN phase 1 and phase 2 are each evaluated
- THEN both phases evaluate breaches against the same stored `DailyLossLimitPct` and `MaxLossLimitPct`,
  unchanged between phases

### Requirement: A Stored Profit Target Percentage That Disagrees With The Fixed Rule Refuses The Race, Not The Run
When the supplied `BrokerRiskLimits` row's `ProfitTargetPct` is present and is not 0.10, the race MUST
be refused with a typed reason that echoes both the stored value and the fixed 0.10 rule value. This
refusal is scoped to the race only: the run itself stays `Evaluated`, and the shipped
`ftmo-breach-simulation` breach findings for that run MUST still be reported unaffected, with
`ChallengeRace` present and carrying `Refusal = ProfitTargetMismatch` alongside the echoed stored
value and the fixed rule value, and `Phase1`/`Phase2` null. `ChallengeRace` is set to null only when
the whole run is refused. A null `ProfitTargetPct` MUST use the fixed 2-Step target rule without
refusing.

> `BrokerRiskLimits` percentages are user-sourced (`BrokerRiskLimits.cs:9`); silently overriding a
> stored, explicitly set value with a code constant would contradict a fact the user entered, so the
> race refuses instead of overriding it silently. *(Resolved in the proposal's question round,
> 2026-09-27: orchestrator decision.)* This does not fire for the user's current stored FTMO row, which
> holds 0.10 today.

#### Scenario: A stored target of 0.10 does not refuse
- GIVEN a `BrokerRiskLimits` row with `ProfitTargetPct = 0.10`
- WHEN a challenge race is requested against it
- THEN the race proceeds and is not refused on account of `ProfitTargetPct`

#### Scenario: A stored target other than 0.10 refuses with both values echoed
- GIVEN a `BrokerRiskLimits` row with `ProfitTargetPct = 0.08`
- WHEN a challenge race is requested against it
- THEN the race is refused with a typed reason that echoes both the stored `0.08` and the fixed `0.10`
  rule value, and no phase result is produced

#### Scenario: A race-only refusal leaves the run Evaluated with its breach findings reported
- GIVEN a `BrokerRiskLimits` row with `ProfitTargetPct = 0.08` whose shipped breach evaluation would
  otherwise proceed
- WHEN the run is simulated
- THEN the run's status stays `Evaluated`, its `ftmo-breach-simulation` breach findings are reported
  exactly as they would be without the race feature present, and `ChallengeRace` is present with
  `Refusal = ProfitTargetMismatch`, the stored and fixed rule values echoed, and `Phase1`/`Phase2` null

#### Scenario: A null stored target uses the fixed rule without refusing
- GIVEN a `BrokerRiskLimits` row with `ProfitTargetPct = null`
- WHEN a challenge race is requested against it
- THEN the race proceeds using the fixed 10%/5% two-phase rule and is not refused

### Requirement: A Phase Reaches Its Target Only At A Flat Close, With The Day Minimum Already Met
A phase MUST be reported as having reached its target at the first replayed close where all of the
following hold simultaneously: the replayed balance is at or above Initial Capital times one plus the
phase's fixed target percentage; the phase has already accumulated at least 4 FTMO trading days by that
close; and no other replayed scalable position is open at that close. A close where the balance meets
the target and the day minimum but another position remains open MUST NOT be reported as the phase
reaching its target at that close; it MUST continue to be evaluated at later closes.

Closes that share the same source close instant form a single "all positions closed" event: the target
check MUST be evaluated once, after the last close in that group, not independently per close within
the group. Because of this grouping, the trade series divides cleanly into phase 1 (every trade whose
close is at or before the target-deciding close, inclusive of every trade in that group) and phase 2
(every trade whose close is strictly after it); no trade that shares the target's close instant falls
into neither phase.

The open-position check for the target MUST exclude `Unscalable` trades: an `Unscalable` row spans a
position the FTMO account could never have opened, so it MUST NOT be treated as an open sibling that
defers the target decision.

> FTMO's own passing condition is "target reached with all positions closed" (`SERVICE_FTMO.md:48`), so
> an open sibling position at an otherwise-qualifying close is a hard disqualifying condition for that
> close, not a contingency to flag and accept anyway. *(Proposal D1.)* Grouping same-instant closes
> into one event, and excluding `Unscalable` rows from the open-position check, keeps the phase 1/phase
> 2 handover clean and matches the shipped evaluator's own treatment of `Unscalable` trades as never
> having been opened on the FTMO account. *(Design Decisions 2 and 4.)*

#### Scenario: Target, day minimum, and flat book together decide the phase
- GIVEN a phase whose balance first reaches the target percentage at a close on FTMO trading day 6,
  with no other position open at that close
- WHEN the phase result is produced
- THEN the phase outcome is decided at that close, since the day minimum (4) is already met and the
  book is flat

#### Scenario: An open sibling position defers the decision to a later close
- GIVEN a phase whose balance reaches the target percentage at a close where a second position remains
  open, and reaches it again (book flat) at a later close
- WHEN the phase result is produced
- THEN the phase is not reported as having reached its target at the earlier, non-flat close; it is
  reported as reached at the later, flat close

#### Scenario: Two trades closing at the same instant are one event; the target is crossed by the pair
- GIVEN a phase where two trades share the same source close instant, the day minimum is already met,
  and neither trade's individual `Net` alone brings the balance to the target but their combined `Net`
  does
- WHEN the phase result is produced
- THEN the target is evaluated once, after both closes in the group, and the phase is reported as
  reaching its target at that shared close instant

#### Scenario: An Unscalable position spanning the target close does not block the target
- GIVEN a phase whose balance and day minimum both qualify at a close, with only an `Unscalable`
  position still open (`Open` before the close, `Close` after it) and no other scalable position open
- WHEN the phase result is produced
- THEN the phase is reported as reaching its target at that close; the open `Unscalable` position does
  not defer the decision

#### Scenario: The day minimum not yet met defers the decision
- GIVEN a phase whose balance reaches the target percentage at a close on FTMO trading day 2, book
  flat, and no breach occurs before FTMO trading day 4
- WHEN the phase result is produced
- THEN the phase is not reported as having reached its target on day 2; the phase's `FirstTargetTouch`
  reports day 2, and the phase decision is deferred until the day-4 minimum is met

### Requirement: Trading Continues At Normal Risk Between First Target Touch And The Day Minimum
The replay MUST continue evaluating the phase's ordinary trade series, at unmodified risk, for every
close between the first close where the target percentage was touched and the close where the phase is
finally decided. The balance MUST be allowed to fall back below the target percentage in that window,
and a breach detected in that window MUST end the phase as `BreachedFirst`, overriding an earlier target
touch that had not yet met the day-4 minimum.

> Confirmed by the user (resolved question round, 2026-09-27): the replay does not model day-filler or
> minimal-lot trades to preserve a touched target while waiting out the day minimum, because the
> strategies being simulated have no such behaviour and inventing one would misrepresent them. Each
> phase reports both when the target was first touched and when the phase was actually decided, so this
> window's effect is visible rather than hidden. *(Proposal D2; user decision.)*

#### Scenario: A breach after a premature target touch ends the phase as breached
- GIVEN a phase whose balance touches the target percentage on FTMO trading day 2 (day minimum not yet
  met, book flat) and whose daily loss limit is breached on FTMO trading day 3
- WHEN the phase result is produced
- THEN the phase outcome is `BreachedFirst`, not `TargetReachedFirst`, even though the target was
  touched first

#### Scenario: The phase reports both the first touch and the final decision
- GIVEN the same phase as above
- WHEN the phase result is produced
- THEN it reports `FirstTargetTouch` at FTMO trading day 2 and the outcome-deciding close at FTMO
  trading day 3, as two distinct, separately reported points

### Requirement: Any First Breach Ends The Phase, Clean Or Contingent
A phase MUST end at its first detected breach of either loss limit, whether the breach's classification
under `ftmo-breach-simulation`'s rules is clean (`Breached`) or downgraded (`BreachContingent`). The
phase outcome MUST report `BreachedFirst` and MUST report which limit breached and that breach's class.

When a breach and the target both land on the same close (or the same close-instant group), the phase
outcome MUST be `BreachedFirst`, not `TargetReachedFirst`. The breach wins the tie.

> A live FTMO account is closed by the broker at the first breach regardless of whether the simulator's
> own downgrade causes (overlap, an excluded `Unscalable` trade, an FX-rounding disagreement, or a
> DST-mismatch window) would make the breach's evidentiary weight informal from the simulator's own
> point of view. The race reports which class occurred so the user can weigh a contingent breach
> differently from a clean one, but it does not treat a contingent breach as if the phase continued.
> *(Proposal D3.)* A same-close tie between a breach and the target is decided pessimistically, in
> favour of the breach: the balance can exceed 1.15× capital on the previous midnight and still cross
> the target and a daily breach on the very same close, and the capability's posture throughout is to
> never report a favourable reading on an ambiguous or tied close. *(Design Decision 3.)*

#### Scenario: A clean breach ends the phase
- GIVEN a phase whose max-loss limit is cleanly breached before the target is reached
- WHEN the phase result is produced
- THEN the outcome is `BreachedFirst`, reporting the max limit and a clean classification

#### Scenario: A contingent breach also ends the phase
- GIVEN a phase whose daily-loss limit is breached with a downgraded (contingent) classification before
  the target is reached
- WHEN the phase result is produced
- THEN the outcome is `BreachedFirst`, reporting the daily limit and a contingent classification

#### Scenario: A breach and the target on the same close both resolve to BreachedFirst
- GIVEN a phase whose balance both breaches a loss limit and crosses the target percentage (day minimum
  already met, book flat) at the same close, or the same close-instant group
- WHEN the phase result is produced
- THEN the outcome is `BreachedFirst`, not `TargetReachedFirst`, reporting the breached limit and its
  classification

### Requirement: Phase Two Starts Fresh At The First Trade Opened After The Phase-One Target Close
When phase 1's outcome is `TargetReachedFirst`, phase 2 MUST start at the first trade in the replayed
series whose open time is after the close that decided phase 1. Phase 2 MUST evaluate that post-target
trade subset using the same breach-evaluation behaviour `ftmo-breach-simulation` applies to a full run
(the same loss-limit floors, overlap and downgrade causes, and FX-band handling), on a fresh account:
balance reset to Initial Capital, and the day-1 floor computed from Initial Capital exactly as phase 1's
day 1 was. No verification delay MUST be modelled between the two phases. When phase 1's outcome is not
`TargetReachedFirst`, phase 2's outcome MUST be reported as `NotStarted` and no phase-2 timing or
decision MUST be produced.

> The phase-1 decision close requires all positions flat (see the flat-close requirement above), so no
> trade spans the handover — the first trade opened after that close is a genuine fresh start, not a
> position inherited from phase 1. No FTMO-documented verification delay applies to this simulated
> handover; modelling one would be a fabricated value with no source. *(Proposal D4.)*

#### Scenario: Phase 2 starts at the next trade opened after the phase-1 decision
- GIVEN a phase 1 that reaches its target at a close on FTMO trading day 20, and a next trade opening
  on FTMO trading day 21
- WHEN phase 2's result is produced
- THEN phase 2's start is FTMO trading day 21's trade, its balance resets to Initial Capital, and its
  day-1 floor is computed from that Initial Capital

#### Scenario: Phase 2 is NotStarted when phase 1 does not reach its target
- GIVEN a phase 1 whose outcome is `BreachedFirst` or `NeitherByEndOfData`
- WHEN the race result is produced
- THEN phase 2's outcome is `NotStarted`, and no phase-2 timing or decision is reported

#### Scenario: Phase 1 reaches its target on the last replayed close, with no further data
- GIVEN a phase 1 that reaches its target at the LAST close in the replayed series, with no trade
  opening afterwards
- WHEN phase 2's result is produced
- THEN phase 2's outcome is `NeitherByEndOfData` (not `NotStarted`, since phase 1 genuinely reached its
  target), with a null `StartSourceOpen` since no post-handover trade exists to start it

### Requirement: Phase Outcome Is A Four-State Enum, Never A Boolean, With No Survival Wording For A Reached Target
Each phase's outcome MUST be exactly one of `TargetReachedFirst`, `BreachedFirst`,
`NeitherByEndOfData`, or `NotStarted`. No boolean pass/fail flag MUST exist anywhere in this
capability's output. No field, method, comment, or user-facing label produced by this capability MUST
use the words "passed", "safe", "survived", "would have passed", or any equivalent affirmation of
survival, including for a `TargetReachedFirst` outcome.

> Reusing `ftmo-breach-simulation`'s no-boolean, no-survival-wording rule keeps the two capabilities'
> vocabulary consistent; a race result and a breach finding sit side by side in the same DTO and must
> not use different conventions for the same underlying caution about closed-trade replay and unmodelled
> swap. *(Proposal D5, D11.)*

#### Scenario: A reached target is not worded as passing
- GIVEN a phase whose outcome is `TargetReachedFirst`
- WHEN every field, label, and disclosure string on the phase result is inspected
- THEN none contains "passed", "safe", "survived", "would have passed", or an equivalent affirmation
  of survival

#### Scenario: Every phase reports one of the four enum values, never a boolean
- GIVEN any produced race result that is not refused
- WHEN each phase's outcome field is inspected
- THEN it is typed as the four-state enum and is one of `TargetReachedFirst`, `BreachedFirst`,
  `NeitherByEndOfData`, or `NotStarted`, and no boolean pass/fail field is present anywhere in the
  result

#### Scenario: Data ending before either event resolves is reported honestly
- GIVEN a phase whose replayed data ends with neither the target reached nor a breach detected
- WHEN the phase result is produced
- THEN the outcome is `NeitherByEndOfData`, not a silently assumed pass or fail

### Requirement: FX Band Race Reports The Whole-Chain Outcome Less Favourable To Reaching The Target
When the simulated symbol requires an FX band (per `ftmo-breach-simulation`'s FX-band requirement), the
race MUST be run once at `fxLow` and once at `fxHigh`, each end producing its own full two-phase chain
(phase 1's outcome, and phase 2's outcome when phase 1 reaches its target). The race MUST compare the
two ends' whole chains, not each phase independently, ranked from least to most favourable to reaching
the target:
`P1 Breached < P1 Neither < (P1 Target, P2 Breached) < (P1 Target, P2 Neither) < (P1 Target, P2 Target)`.
The race MUST report the less favourable chain of the two ends. When the two ends' chains land on the
same rank (an outcome tie), the race MUST report the less favourable timing between them: whichever end
reports the earlier breach, or, absent a breach, whichever end reports the later target. When both the
outcome and the deciding close are identical between the two ends (the same row), the race MUST report
`BothEnds` with the `fxLow` values. The race MUST tag its result with cause `FxRoundingSensitive`
whenever the two ends' rows are NOT identical — that is, whenever either end's phase 1 outcome, phase 1
deciding close, phase 2 outcome, or phase 2 deciding close differs from the other end's. A timing-only
disagreement at the same rank (both ends reaching the same outcome pair, but at a different deciding
close) is tagged exactly like an outcome disagreement: only a genuinely identical row is untagged.

> Consistent with `ftmo-breach-simulation`'s existing FX-band handling, which never picks a favourable
> reading silently on disagreement; the race applies the same principle to its own target-versus-breach
> decision rather than inventing a separate tie-break rule. *(Proposal D10.)* Comparing whole chains
> rather than merging each phase independently avoids combining a phase 2 from one FX end with a phase 1
> from the other end, since phase 2 only exists on its own end's phase-1 target; for breaches, the
> earlier-wins timing tie-break matches the shipped evaluator's own timing rule. *(Design Decision 6.)*

#### Scenario: Disagreeing FX ends report the less favourable outcome
- GIVEN a `GER40.cash` race where `fxLow` yields `TargetReachedFirst` for phase 1 and `fxHigh` yields
  `BreachedFirst` for the same phase
- WHEN the phase result is produced
- THEN the phase outcome is `BreachedFirst`, tagged `FxRoundingSensitive` and the end (`FxHigh`) that
  produced it

#### Scenario: Agreeing FX ends need no rounding-sensitivity tag
- GIVEN a `GER40.cash` race where `fxLow` and `fxHigh` both yield `TargetReachedFirst` for the same
  phase, at the same underlying close
- WHEN the phase result is produced
- THEN the phase outcome is `TargetReachedFirst` without an `FxRoundingSensitive` cause

#### Scenario: A same-currency symbol needs no FX-band comparison
- GIVEN an `XAUUSD` race requiring no FX band
- WHEN the phase result is produced
- THEN no FX-band-end comparison is performed and the outcome reflects the single evaluation run

#### Scenario: A phase 2 outcome tie between ends is broken by timing, not by outcome alone
- GIVEN a `GER40.cash` race where `fxLow` yields chain `(P1 Target, P2 Breached)` and `fxHigh` yields
  chain `(P1 Target, P2 Breached)` at the same rank, but `fxLow`'s phase-2 breach close is earlier than
  `fxHigh`'s
- WHEN the race result is produced
- THEN the reported chain is `fxLow`'s, tagged `FxRoundingSensitive` and `FxLow`, because an earlier
  breach is the less favourable timing between two chains at the same rank

#### Scenario: Identical outcome and deciding close on both ends reports BothEnds
- GIVEN a `GER40.cash` race where `fxLow` and `fxHigh` yield the same chain rank, the same outcome pair,
  and the same deciding close for every phase
- WHEN the race result is produced
- THEN the reported `FxBandEnd` is `BothEnds`, carrying the `fxLow` values, without an
  `FxRoundingSensitive` cause, since the two ends' outcome pairs are identical

#### Scenario: A tie between two undecided chains reports the fxLow end
- GIVEN a `GER40.cash` race where `fxLow` and `fxHigh` both yield `P1 NeitherByEndOfData` (or both yield
  `P1 TargetReachedFirst, P2 NeitherByEndOfData`), at the same rank, with no breach or target decided on
  either end for the tied phase
- WHEN the race result is produced
- THEN the reported chain is `fxLow`'s values, tagged `FxRoundingSensitive` only if the two ends' rows
  (outcome and deciding close for both phases) are not identical — the same convention used for a
  same-row tie at any other rank, with no separate "later elapsed time" tie-break

### Requirement: The Race Leaves The Shipped Breach Result Byte-Identical
Producing a challenge race result MUST NOT change `Verdict`, `Causes`, `DisclosureText`,
`FtmoTradingDaysElapsed`, or any other field of the shipped `ftmo-breach-simulation` result for the same
run. The race MUST be computed as a separate pass over the trade series, never by truncating or
otherwise altering the shipped breach-evaluation loop. A refused race MUST report `ChallengeRace` as
null on the run result, alongside an unaffected (or itself independently refused) breach result.

> The shipped evaluator must keep replaying the full series after a first breach (existing requirement,
> `ftmo-breach-simulation`), which the race's own truncate-at-first-breach behaviour cannot be allowed to
> disturb — the two must remain two independent loops over the same input, not one loop serving both
> readouts. *(Proposal D7.)*

#### Scenario: Adding the race does not change the shipped breach fields
- GIVEN a fixture whose shipped breach result reports a specific `Verdict`, `Causes`, and
  `DisclosureText` for each limit
- WHEN the challenge race is computed alongside it
- THEN the shipped `Verdict`, `Causes`, `DisclosureText`, and `FtmoTradingDaysElapsed` fields are
  byte-identical to their value before the race was added

#### Scenario: A refused race reports null ChallengeRace without disturbing the breach result
- GIVEN a run refused only on account of the race's own `ProfitTargetPct` mismatch, while the shipped
  breach evaluation for the same `BrokerRiskLimits` row would otherwise proceed
- WHEN the run result is produced
- THEN `ChallengeRace` is null, and the shipped breach result is produced exactly as it would be without
  the race feature present

### Requirement: The Race's Phase-One Breach Matches The Shipped First Breach When It Precedes The Target
When phase 1's outcome is `BreachedFirst`, the breach reported by the race for phase 1 MUST be the same
underlying close, limit, and classification as the shipped `ftmo-breach-simulation` result's
`FirstBreach` (or `FirstCleanBreach`, matching the race's own reported classification) for that limit,
whenever that shipped first breach occurs before the phase-1 target would otherwise have been reached.

> This is the cross-check that keeps the race's own copy of the floor and overlap logic from drifting
> from the shipped evaluator's: since both loops read the same trade series and the same loss-limit
> rules, a phase-1 breach that precedes the target must be the identical event in both readouts, not a
> pair of independently-computed answers that happen to usually agree. *(Proposal D7.)*

#### Scenario: A phase-1 breach before the target matches the shipped first breach
- GIVEN a run whose shipped daily-loss `FirstBreach` occurs before phase 1's target would have been
  reached
- WHEN the race's phase-1 result is produced
- THEN the race reports `BreachedFirst` for the same close, same limit, and same classification as the
  shipped `FirstBreach` record

### Requirement: Every Non-Refused Race Reports Full Per-Phase Timing And The Fixed Rules
A non-refused race result MUST report, for each phase evaluated (not `NotStarted`): the phase's outcome;
its start point (the anchor for phase 1, the first post-handover trade for phase 2); its first target
touch, if any, independent of whether the touch decided the phase; the close and FTMO trading day at
which the 4-day minimum was first met; the close that decided the phase's outcome, if decided; and
elapsed calendar days and elapsed FTMO trading days (as recalculated by `ftmo-breach-simulation`'s
trading-day requirement) from the phase's own start point. The result MUST also carry a disclosure that
does not use "passed", "safe", "survived", or "would have passed", and that states the direction of each
known bias: unmodelled swap and closed-trade replay make reaching the target look easier than a live
account would; continued full-risk trading between a premature target touch and the day-4 minimum makes
it look harder. The disclosure MUST state that a `TargetReachedFirst` outcome is an optimistic replay
result, not a prediction, and that a `BreachedFirst` outcome remains a strong result.

> Reporting the first touch separately from the deciding close is how the day-4-minimum window's effect
> (full-risk trading continuing after an early touch) becomes visible to the user rather than absorbed
> into a single date, per the resolved question round. The two-directional bias disclosure follows the
> same shape as `ftmo-breach-simulation`'s existing swap/closed-trade-replay disclosure, applied to a
> different readout. *(Proposal D2, D11, disclosure section.)*

#### Scenario: A TargetReachedFirst phase reports all required timing fields
- GIVEN a phase whose outcome is `TargetReachedFirst`
- WHEN the phase result is produced
- THEN it reports the outcome, start point, first target touch (if distinct from the deciding close),
  the close and day the 4-day minimum was met, the deciding close, and elapsed calendar and FTMO
  trading days from the phase's start

#### Scenario: The disclosure states both bias directions without survival wording
- GIVEN any non-refused race result
- WHEN its disclosure text is inspected
- THEN it states that unmodelled swap and closed-trade replay make the target look easier to reach,
  that continued full-risk trading after a premature touch makes it look harder, that a
  `TargetReachedFirst` result is optimistic rather than a prediction, and that a `BreachedFirst` result
  remains a strong result, using none of "passed", "safe", "survived", or "would have passed"

#### Scenario: A NotStarted phase 2 carries no timing fields
- GIVEN a race whose phase 1 outcome is not `TargetReachedFirst`
- WHEN the race result is produced
- THEN phase 2 is reported as `NotStarted` with no start point, touch, deciding close, or elapsed-time
  fields populated
