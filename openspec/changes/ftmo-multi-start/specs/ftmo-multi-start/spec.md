# FTMO Multi-Start Specification

## Purpose

The shipped `ftmo-challenge-race` reports one two-phase chain from a single fixed anchor. It never
answers whether the target is reached first *in general*, only from that one start, and it stops at
the funded account boundary, which nothing measures today. This capability replays the same race from
every FTMO-month start the data offers, adds a funded phase after phase 2, and reports the distribution
of chain outcomes — plus their censoring and durations — as a new capability.

This is a NEW capability, not a delta against `ftmo-challenge-race`: it produces a distinct kind of
output (a distribution over many replays, each including a phase `ftmo-challenge-race` does not
compute) built on top of, not inside, the shape that capability defines. `ftmo-challenge-race` and
`ftmo-breach-simulation` keep every requirement unchanged; this capability reads them, per the design
precedent set for `ftmo-breach-simulation` itself over `funding-guardrails`.

## Requirements

### Requirement: Starts Are Enumerated At Monthly Grain From The Data
A start MUST be the first scalable (non-`Unscalable`) trade opened in each FTMO (Berlin) calendar
month, from the first month containing one through the last. A month with no scalable open MUST still
be counted, reported as `MonthsWithoutStart`, not silently skipped.

> At ~8 trades/month (~1,000 over ~128 months), weekly starts (~550) sit ~2 trades apart and mostly
> replay identical chains for ~4× the cost with no more information; every-trade starts (~1,000) are
> ~8× the cost with near-total duplication; quarterly (~42) is too coarse for a distribution. Both
> weekly and every-trade grains were rejected alternatives. *(Proposal D1.)*

#### Scenario: A month with no scalable open is counted, not skipped
- GIVEN a month with zero scalable trade opens
- WHEN starts are enumerated
- THEN no start is produced for that month and it is reported in `MonthsWithoutStart`, and that month
  is neither dropped from the reported range nor treated as an implicit start

#### Scenario: Weekly or every-trade grain is not implemented
- GIVEN the shipped enumeration
- WHEN start count is compared against a hypothetical weekly or every-trade enumeration
- THEN only the monthly grain is produced by this capability

### Requirement: Each Start Runs Phase 1, Phase 2, Then A Funded Phase
Each start's series MUST be every trade with `Open ≥ startOpen`. Phases 1 and 2 MUST run the shipped
`FtmoChallengeRace.RunPhase` unchanged. The funded phase MUST start at the first trade opened after the
phase-2 target close, on a fresh account at Initial Capital with the same loss limits, no profit target
and no day minimum. Its outcome MUST be exactly one of `BreachedFirst`, `NoBreachByEndOfData`, or
`NotStarted` (phase 2 did not reach its target).

> Funded accounts keep the loss limits from the challenge but have no profit target and no day-minimum
> gate (`SERVICE_FTMO.md:236-240`). `NoBreachByEndOfData` replaces "survived", which both shipped specs
> ban. *(Proposal D3; resolved question round, 2026-09-27.)*

#### Scenario: A funded phase starts fresh after phase 2's target
- GIVEN a start whose phase 2 reaches its target
- WHEN the funded phase is produced
- THEN it starts at the first post-target trade, balance reset to Initial Capital, same loss limits,
  no target, no day minimum

#### Scenario: Funded phase is NotStarted when phase 2 does not reach its target
- GIVEN a start whose phase 2 outcome is not `TargetReachedFirst`
- WHEN the chain result is produced
- THEN the funded phase outcome is `NotStarted`

#### Scenario: A trade still open at a later start's anchor does not leak into that start's replay
- GIVEN a trade opened at one start that remains open (by close time) at a later start's `startOpen`
- WHEN the later start's series is sliced and replayed
- THEN that trade is excluded from the later start's series (its `Open` is before that start's
  `startOpen`), and it affects neither the later start's balance, overlap flag, flat-book check, anchor,
  nor trading-day count

#### Scenario: A funded phase with no trades left reports NoBreachByEndOfData with zero runway
- GIVEN a start whose phase 2 reaches its target on the last replayed close, with no trade opening
  afterwards
- WHEN the funded phase is produced
- THEN its outcome is `NoBreachByEndOfData` and its runway is 0

### Requirement: Six Chain Outcomes, Three Right-Censored And Labelled As Such
Each start MUST resolve to exactly one of `Phase1Breached`, `Phase1UndecidedAtEndOfData`,
`Phase2Breached`, `Phase2UndecidedAtEndOfData`, `FundedBreached`, `FundedNoBreachAtEndOfData`. The last
three MUST be reported as right-censored and MUST NOT be dropped, merged, or mixed with the other
categories. Each censored start MUST report its **runway** (days from its phase start to the last
close).

> A minimum-runway cutoff and Kaplan–Meier were both rejected: funded has no natural horizon, so any
> cutoff is either a fabricated constant or circular; a cutoff would also drop the most recent starts;
> Kaplan–Meier assumes independent samples and reads as a probability, which the disclosure explicitly
> denies. *(Proposal D5.)*

#### Scenario: Every start lands in exactly one of the six outcomes
- GIVEN any completed multi-start run
- WHEN outcomes are counted
- THEN the six outcome counts sum to the total start count, with no start uncounted or double-counted

#### Scenario: A censored start reports its runway, not a fabricated cutoff
- GIVEN a start whose chain outcome is `FundedNoBreachAtEndOfData`
- WHEN the start's result is produced
- THEN it reports the runway (days from phase start to last close), with no minimum-runway cutoff or
  Kaplan–Meier survival estimate applied

#### Scenario: Each of the six chain outcomes is produced by some fixture
- GIVEN a fixture set covering `Phase1Breached`, `Phase1UndecidedAtEndOfData`, `Phase2Breached`,
  `Phase2UndecidedAtEndOfData`, `FundedBreached`, and `FundedNoBreachAtEndOfData`
- WHEN each fixture's chain is classified
- THEN each fixture lands on its intended one of the six outcomes, with none misclassified into a
  neighbouring outcome

### Requirement: Aggregates Are Counts, Shares, And Order Statistics — No Mean, No Invented Bucket
The aggregate MUST report counts and shares per chain outcome over ALL starts (zeros included), and
order statistics (min, Q1, median, Q3, max, n) by nearest rank for days to phase-1 target, days to
phase-2 target, funded days to breach, and censored runway. It MUST NOT report a mean alone in place of
these order statistics, and MUST NOT report a "within N days" bucket.

> Nearest-rank order statistics guarantee every reported quantile is an observed start; a mean alone
> hides the distribution's shape, and any "within N days" bucket boundary would be an invented value
> with no source. *(Proposal D6.)*

#### Scenario: Aggregate shares include outcomes with zero occurrences
- GIVEN a run where `FundedBreached` never occurs
- WHEN the aggregate is produced
- THEN `FundedBreached` is reported with count 0 and share 0, not omitted

#### Scenario: Every outcome's share is computed over the full start count, censored or not
- GIVEN a run with a mix of censored and non-censored outcomes
- WHEN shares are computed
- THEN every outcome's share uses the total start count as the denominator, and the six shares sum to 1

#### Scenario: Order statistics with zero observations report every quantile as null
- GIVEN an order-statistics input with n = 0 observations
- WHEN the order statistics are computed
- THEN `N` is 0 and `Min`, `Q1`, `Median`, `Q3`, and `Max` are all null

#### Scenario: Order statistics with one observation report that value for every quantile
- GIVEN an order-statistics input with exactly one observation
- WHEN the order statistics are computed by nearest rank
- THEN `N` is 1 and `Min`, `Q1`, `Median`, `Q3`, and `Max` all equal that single observed value

### Requirement: Funded Duration Is Reported From Both The Funded Start And The Chain Start
Every funded-phase result MUST report elapsed days measured from the funded phase's own start AND
separately from that same start's chain start — its own phase-1 anchor, not start 1's. Each start's
"chain start" basis is local to that start: two different starts' funded phases are never measured
against a shared, single chain-start anchor.

> Resolved question round, 2026-09-27: which figure the UI headlines is a UI-change decision; both are
> reported here so that choice is not blocked on this change.

#### Scenario: Funded elapsed days are reported in both bases
- GIVEN a funded phase that breaches 40 days after its own start, on a chain whose phase 1 began 95
  days earlier
- WHEN the funded result is produced
- THEN it reports 40 elapsed days from the funded start and 135 elapsed days from the chain start

### Requirement: The FX Whole-Chain Rule Extends To Three Phases
The race's whole-chain FX-band comparison MUST extend to the funded phase, ranked
`P1Breached < P1Undecided < P2Breached < P2Undecided < FundedBreached < FundedNoBreach`, an earlier
close being less favourable within a breach rank and the fxLow end being reported (tagged when the ends
differ) within an undecided rank. This merge MUST be new code, not an edit to the shipped two-phase
`MergeEnds`. A degenerate band (`fxLow == fxHigh`, every USD symbol) MUST evaluate one end only, and that
single run MUST be reported as `BothEnds`.

> A new merge keeps the shipped two-phase `MergeEnds` untouched. A test pins that the three-phase merge's
> phase-1 and phase-2 outputs equal the shipped `MergeEnds` on the same inputs ONLY when the less
> favourable ("lower rank") end's chain does not itself reach the funded phase (rank ≤ 3, i.e. it
> resolves at or before `Phase2Undecided`); when both FX ends reach the funded phase, the funded
> phase's own outcome decides the reported chain and the two-phase `MergeEnds` result is not
> reproduced by construction. *(Proposal D7; corrected — equality is conditional, not unconditional.)*

#### Scenario: An earlier funded breach is less favourable between FX ends
- GIVEN two FX ends that both reach both targets, one then breaching funded earlier than the other
- WHEN the three-phase merge is produced
- THEN the earlier funded breach is reported as the less favourable chain

#### Scenario: A degenerate band evaluates one end only
- GIVEN a USD-settling symbol where `fxLow == fxHigh`
- WHEN the three-phase chain is produced
- THEN only one FX end is evaluated and the result is reported as `BothEnds`

### Requirement: Start 1 May Diverge From The Single-Start Anchor; The Single-Start Endpoint Stays Untouched
When the backtest's first trade is `Unscalable`, start 1 (first scalable trade of the first month) MAY
differ from the single-start endpoint's anchor (which includes `Unscalable` rows), and this MUST be
disclosed. The single-start endpoint, its result shape, `FtmoBreachEvaluator.cs`, and the golden pin
MUST remain byte-identical and unedited by this capability.

> Accepted and disclosed in the resolved question round, 2026-09-27; the single-start result is not
> changed. *(Proposal Risk row, Success Criteria.)*

#### Scenario: An Unscalable first trade makes start 1 diverge from the anchor, disclosed
- GIVEN a backtest whose first trade is `Unscalable`
- WHEN start 1 and the single-start anchor are compared
- THEN they differ, and the multi-start result discloses that divergence

#### Scenario: The single-start endpoint is untouched
- GIVEN the shipped single-start endpoint's result for a fixture
- WHEN the multi-start capability is added
- THEN that result is byte-identical to its value before this capability existed

### Requirement: No Survival Wording Anywhere In This Capability's Output
No field, enum member, method, comment, or user-facing label produced by this capability MUST use
"passed", "safe", "survived", "would have passed", or any equivalent affirmation of survival.

#### Scenario: Every enum member and label is free of banned wording
- GIVEN every enum type and disclosure string this capability produces
- WHEN their member names and text are inspected
- THEN none contains "passed", "safe", "survived", "would have passed", or an equivalent affirmation

### Requirement: Every Run Discloses Non-Independence And Optimistic Bias
Every produced run MUST disclose: consecutive monthly starts share most of their trades, so N starts
are not N independent trials; the aggregates describe this backtest and are not probabilities or a
forecast; unmodelled swap and closed-trade replay make targets easier and understate breaches; and the
funded phase models no reward withdrawal or Scaling Plan, which keeps profit as cushion and is also
optimistic.

> Withdrawal cadence and Scaling Plan timing have no data source; modelling either would require an
> invented value. The omission is optimistic and disclosed rather than modelled. *(Resolved question
> round, 2026-09-27.)*

#### Scenario: Disclosure covers non-independence, optimism, and unmodelled withdrawals
- GIVEN any produced multi-start run
- WHEN its disclosure text is inspected
- THEN it states starts are not independent trials, the figures are not probabilities, unmodelled swap
  and closed-trade replay understate breaches, and unmodelled funded withdrawals/Scaling Plan are an
  optimistic omission
