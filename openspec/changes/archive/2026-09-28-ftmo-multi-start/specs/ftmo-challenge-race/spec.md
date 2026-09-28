# Delta for FTMO Challenge Race

## MODIFIED Requirements

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

**On a `BreachedFirst` phase, the first target touch and the day-minimum-met close MUST each be reported
only if that event occurs at or before the phase's breach close; otherwise the corresponding field MUST
be null.** The phase's `Outcome` and its breach fields (`OutcomeSourceClose`, `BreachLimit`,
`BreachPointClass`) are unaffected by this rule and MUST NOT change.
(Previously: the scanner ran to completion regardless of the breach close, so a `BreachedFirst` phase
could report a target touch or a day-minimum-met close that occurred AFTER the account had already
breached — an event from a phase of the account's life that a live FTMO account never reaches.)

> The scanner and the breach evaluator are two independent passes over the same series (spec.md "The
> Race Leaves The Shipped Breach Result Byte-Identical"); the scanner does not stop at the breach, so it
> can keep recording touches and day-minimum closes past the point the account was already closed by the
> breach. Those post-breach readings are not observable on a live account and MUST NOT be surfaced.
> *(Change: ftmo-multi-start PR0, bug A.)*

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

#### Scenario: A breached phase whose balance later touches the target reports no post-breach touch
- GIVEN a phase whose account balance crosses the target percentage and meets the day-4 minimum only on
  a close AFTER the phase's breach close
- WHEN the phase result is produced
- THEN the outcome is `BreachedFirst` (unchanged) and both `FirstTargetTouchSourceClose` and
  `MinTradingDaysMetFtmoDay` are null, since the qualifying event happened after the account had already
  breached

#### Scenario: A breached phase whose balance touched the target before the breach still reports the touch
- GIVEN a phase whose balance touches the target percentage on a close strictly before the phase's
  breach close
- WHEN the phase result is produced
- THEN the outcome is `BreachedFirst` and `FirstTargetTouchSourceClose` reports that earlier, pre-breach
  touch unchanged

### Requirement: Phase Two Starts Fresh At The First Trade Opened After The Phase-One Target Close
When phase 1's outcome is `TargetReachedFirst`, phase 2 MUST start at the first trade in the replayed
series whose open time is after the close that decided phase 1. Phase 2 MUST evaluate that post-target
trade subset using the same breach-evaluation behaviour `ftmo-breach-simulation` applies to a full run
(the same loss-limit floors, overlap and downgrade causes, and FX-band handling), on a fresh account:
balance reset to Initial Capital, and the day-1 floor computed from Initial Capital exactly as phase 1's
day 1 was. No verification delay MUST be modelled between the two phases. When phase 1's outcome is not
`TargetReachedFirst`, phase 2's outcome MUST be reported as `NotStarted` and no phase-2 timing or
decision MUST be produced.

**The post-target trade subset handed to phase 2 MUST contain exactly the rows whose `Open` is at or
after the phase-1 decision close T AND whose `Close` is strictly after T. A row that opens before T and
closes after T (an `Unscalable` row that spans T, ignored by the flat-book check) MUST stay out of phase
2's subset; it belongs to phase 1's already-decided group.** A zero-duration row whose `Open` and `Close`
both equal T belongs to phase 1's close group at T, not to phase 2.
(Previously: the subset was every row with `Close > T`, with no lower bound on `Open`, so a straddling
row could enter phase 2 and become its anchor even though it opened before T.)

> Every row phase 2 receives has `Open >= T` by construction (the design's own invariant); a row that
> opens before T never belongs to a fresh account that starts at T. Filtering on `Close > T` alone does
> not guarantee this when a row spans T. *(Change: ftmo-multi-start PR0, bug B; the funded phase in
> ftmo-multi-start uses the same corrected handover rule.)*

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

#### Scenario: An Unscalable row straddling the phase-1 decision close does not enter phase 2
- GIVEN an `Unscalable` trade whose `Open` is before phase 1's target-deciding close T and whose `Close`
  is strictly after T, with no other trade opening in that gap
- WHEN phase 2's trade subset is built
- THEN that row is excluded from the subset, and phase 2's anchor is the first trade with `Open >= T`,
  not the straddling row

> **Disclosure note**: this fix changes a readout an existing consumer may have already seen — a phase's
> `FirstTargetTouchSourceClose`/`MinTradingDaysMetFtmoDay` value, or a phase-2 anchor derived from a
> straddling row — but it never changes any phase's `Outcome` or breach classification. Every existing
> `TargetReachedFirst`/`BreachedFirst`/`NeitherByEndOfData` verdict already produced stays the same
> verdict; only the two named readout fields and, in the straddling case, the phase-2 subset composition
> can move.
