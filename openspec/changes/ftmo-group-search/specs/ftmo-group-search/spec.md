# FTMO Group Search Specification

## Purpose

`ftmo-group-simulation` answers whether ONE hand-picked group passes the FTMO two-step chain. This capability
answers the user's real question: which 2..4 strategies of a pool, sharing ONE FTMO account, show the lowest
elimination risk, and how fast. It enumerates candidate groups deterministically, narrows them with a cheap
proxy, runs the full group computation on a bounded shortlist only, and ranks the result lexicographically.
The search runs as an in-memory background job that the client starts, polls and cancels.

It reads, and does not restate, `ftmo-group-simulation` (member merge, window, refusal vocabulary, FX rule,
banned wording, group disclosures, `MaxMembers`), `ftmo-multi-start` (chain outcomes, order statistics,
censoring) and `ftmo-breach-simulation` (daily and max-loss rules). It composes the pure group computation
per candidate and never changes it. `FtmoBreachEvaluator.cs`, `ComputeRun` and the tripwire's 18 slice files
stay unedited.

Definitions. A **candidate** is a set of distinct strategies of one account, size k in [2, `MaxMembers`]
(`MaxMembers` = 4 today, from `FtmoGroupSimulationLimits`). A **kind** is Deploy or Evaluation. The **breach
share** of a kind is `(Phase1Breached + Phase2Breached + FundedBreached) / StartCount`. The **funded
no-breach share** is `FundedNoBreachAtEndOfData / StartCount` (right-censored; never called survival in any
user-facing text). **Median days to both targets** is `DaysToBothTargets.Median`, null when N = 0. All shares
and headroom values travel as fractions (0.05 = 5%), never as percentages.

## Requirements

### Requirement: The Search Endpoint Contract And Its Validation

The system MUST expose, under the `api/ftmo-simulations` route prefix (served by a new controller; the shipped
`FtmoSimulationsController` is not edited) and protected by `[Authorize]` (unauthenticated
calls get 401): `POST api/ftmo-simulations/group-search` (start), `GET api/ftmo-simulations/group-search/{jobId}`
(poll), `GET api/ftmo-simulations/group-search/current` (re-attach: 200 with the retained job, 204 when none; a
user decision of 2026-10-04: leaving the UI does not cancel the job) and `DELETE api/ftmo-simulations/group-search/{jobId}` (cancel). The start body MUST carry:
`tradingAccountId`, `broker`, `initialCapital`, `targetRiskPerTrade` (exactly one risk), the four SOURCE
lot-grid fields, an optional FX band (`fxLow`, `fxHigh`), `minMembers`, `maxMembers`, `maxPerInstrument`,
`includeIdenticalDeployEval`, `applyOnePercentRule`, optional `eliminationCeiling`, and optional budget
overrides. Missing required fields MUST be HTTP 400 (`sizeDecimals = 0` legal). `minMembers < 2`,
`maxMembers > MaxMembers`, `minMembers > maxMembers`, `maxPerInstrument < 1`, a budget above its named ceiling,
or an empty/unknown `tradingAccountId` MUST be 400 with `{ message }` and start no job. A present-but-unusable
value (non-positive capital or risk, invalid grid) MUST start no job and answer 400. A successful start MUST
answer 202 with the job id; no combination is computed in the request thread.

#### Scenario: Unauthenticated calls are rejected
- GIVEN no valid JWT
- WHEN any of the three endpoints is called
- THEN the response is 401 and nothing starts, reads or cancels

#### Scenario: A start is accepted without computing
- GIVEN a valid body
- WHEN it is posted
- THEN the response is 202 with a job id and the job is reported as running or queued on the next poll

#### Scenario: Size bounds are validated
- GIVEN `maxMembers = MaxMembers + 1`, or `minMembers = 1`, or `minMembers > maxMembers`
- WHEN it is posted
- THEN the response is 400, the message states the allowed bounds, and no job starts

#### Scenario: A missing required field is a 400
- GIVEN a body with no `targetRiskPerTrade`
- WHEN it is posted
- THEN the response is 400 naming the required fields and no job starts

### Requirement: Eligibility Excludes Strategies With A Visible Reason

A strategy MUST enter the funnel only when it holds BOTH a Deploy and an Evaluation run, has an FTMO
instrument spec and a usable calibration for each run's symbol, shares the pool's source time zone, and, when
it settles in a non-USD currency, the request carries a valid FX band. Every excluded strategy MUST be counted
and listed by reason in the job result; nothing is dropped silently. The eligible pool is the strategies of the
requested account only.

#### Scenario: A strategy without an Evaluation run is excluded
- GIVEN a strategy holding only a Deploy run
- WHEN the search runs
- THEN it appears in the exclusions with a missing-kind reason and no candidate contains it

#### Scenario: A non-USD strategy without an FX band is excluded
- GIVEN a EUR-settling strategy and no FX band in the request
- WHEN the search runs
- THEN it is excluded with an FX reason and USD strategies still form candidates

#### Scenario: Exclusion counts reconcile
- GIVEN an account with P strategies
- WHEN the search completes
- THEN eligible count plus the sum of exclusion counts equals P

#### Scenario: Fewer eligible strategies than the minimum size
- GIVEN fewer eligible strategies than `minMembers`
- WHEN the search runs
- THEN the job completes with zero candidates, an empty ranking and the exclusions listed

### Requirement: Candidates Respect The Per-Instrument Cap

A candidate MUST contain at most `maxPerInstrument` strategies of the same verbatim instrument symbol. The
default is 1. The cap MUST be applied before the proxy so capped candidates cost nothing further, and the
count of candidates removed by the cap MUST be reported.

#### Scenario: The default cap forbids two strategies of one symbol
- GIVEN strategies A and B both on EURUSD and `maxPerInstrument = 1`
- WHEN candidates are enumerated
- THEN no candidate contains both A and B

#### Scenario: Raising the cap admits them
- GIVEN the same strategies and `maxPerInstrument = 2`
- WHEN candidates are enumerated
- THEN a candidate containing A and B exists

#### Scenario: The cap removal is counted
- GIVEN a pool where the cap removes some combinations
- WHEN the search completes
- THEN the reported funnel counts show enumerated, removed by cap, and remaining, and they reconcile

### Requirement: Identical Deploy And Evaluation Series Are Excluded Unless Opted In

A strategy whose projected Deploy series equals its projected Evaluation series MUST be excluded by default
with the reason `IdenticalDeployEval`. When `includeIdenticalDeployEval = true` it MUST be included and every
candidate result containing it MUST carry a flag naming those members. Equality MUST be decided on the
projected series at the request's risk and FX band, not on run ids.

#### Scenario: Identical runs are excluded by default
- GIVEN a strategy whose Deploy and Evaluation runs project to equal series
- WHEN the search runs with the default
- THEN it is excluded as `IdenticalDeployEval`

#### Scenario: The opt-in includes and flags
- GIVEN the same strategy and `includeIdenticalDeployEval = true`
- WHEN the search runs
- THEN candidates may contain it and each such result flags it as identical Deploy/Eval

### Requirement: The Academy 1 Percent Rule Is An Optional Candidate Constraint

When `applyOnePercentRule = true`, a candidate whose `k x targetRiskPerTrade` exceeds 1% of `initialCapital`
MUST be removed before the proxy and counted. When false (the default) no such filter applies.

#### Scenario: The rule removes oversized groups
- GIVEN risk 50, capital 10000 and the rule on
- WHEN candidates are enumerated
- THEN every candidate with k >= 3 is removed and k = 2 remains (2 x 50 = 100 = 1%)

#### Scenario: The rule is off by default
- GIVEN a body without `applyOnePercentRule`
- WHEN candidates are enumerated
- THEN no candidate is removed by this rule

### Requirement: The Proxy Is Computed On Each Candidate's Actual Common Window

For each surviving candidate the proxy MUST be computed from the cached per-member projections trimmed to
THAT candidate's own replay window (the intersection defined by `ftmo-group-simulation`), never to the pool's
window or any other candidate's. The proxy MUST use the worst summed day against the daily limit and the peak
concurrency. A candidate with an empty window or a member with no in-window trades MUST be removed with a
counted reason and MUST NOT be shortlisted.

#### Scenario: Adding a late-starting member shrinks the window used by the proxy
- GIVEN A and B cover Jan-Dec and C covers Oct-Dec, with C's trades the only ones on a losing day
- WHEN the proxy of {A,B} and of {A,B,C} is computed
- THEN {A,B} uses the Jan-Dec window and {A,B,C} uses Oct-Dec, and each value equals the proxy computed
  from scratch on that window alone

#### Scenario: The proxy of a candidate does not depend on enumeration context
- GIVEN the same candidate reached in two different pools
- WHEN its proxy is computed in each
- THEN the two values are equal

#### Scenario: A candidate with no common window is removed visibly
- GIVEN two members whose ranges do not overlap
- WHEN the proxy stage runs
- THEN the candidate is counted under no-common-window and is not shortlisted

### Requirement: Enumeration, Shortlist And Ranking Are Deterministic With No Randomness

Candidates MUST be enumerated in ascending `StrategyId` order (lexicographic over the sorted id tuple, by
size). The shortlist MUST be the best M candidates by ascending proxy score, ties broken by the sorted
`StrategyId` tuple; M is a named constant set from the benchmark. The ranking of fully computed candidates
MUST be lexicographic over, in order:

1. both Deploy and Evaluation evaluated with no refusal (evaluated first);
2. lowest breach share, taking the worse (higher) of Deploy and Evaluation;
3. highest limit headroom (see the headroom requirement), taken on the worse kind (the lower of the two
   kinds' headroom);
4. highest funded no-breach share (worse of the two kinds);
5. lowest median days to both targets (worse of the two kinds; null last);
6. fewer members, then lower peak concurrency, then ascending `StrategyId` tuple.

Candidates not evaluated on both kinds MUST follow all evaluated ones, in `StrategyId` order, with their
refusal. No component of enumeration, shortlist or ranking MAY use a random source, clock or hash-order.

#### Scenario: The same input yields an identical result
- GIVEN two runs with identical inputs and data
- WHEN both complete without budget stop
- THEN the shortlist, ranking, counts and every metric are identical

#### Scenario: No randomness exists in the generator
- GIVEN the generator sources
- WHEN a tripwire test scans them
- THEN none references a random generator, a shuffle, `Guid.NewGuid` or an unordered-set iteration

#### Scenario: Ties fall through the order
- GIVEN two candidates equal on criteria 1 to 5 with 3 and 4 members
- WHEN ranked
- THEN the 3-member candidate ranks first, and equal sizes and concurrency fall to `StrategyId` order

#### Scenario: A refused or one-kind candidate ranks after clean ones
- GIVEN a candidate with a refused Evaluation and a clean one with a worse breach share
- WHEN ranked
- THEN the clean candidate ranks first

### Requirement: Limit Headroom Is Defined Per Kind Outside The Evaluator

Per kind, computed from the candidate's merged in-window series, without editing the evaluator:

- **Worst daily loss** (a): the largest daily loss across all starts and all phases of the chain, as a
  fraction of the daily allowance (`DailyLossLimitPct x initialCapital`), where each day's loss is measured
  from the balance at the previous CE(S)T midnight, as `FtmoBreachEvaluator` does.
- **Worst drawdown** (b): the largest drawdown from initial capital across all starts, as a fraction of the
  max-loss allowance (`MaxLossLimitPct x initialCapital`). The median across starts MUST also be reported.

When the candidate has an FX band, both values MUST be taken at the worse of the Low and High FX ends, and the
end used MUST be reported. Ranking MUST use the worst values, never the median. A value of 1.0 means the
allowance is fully used. Headroom is `1 - max(a, b)` and is shown per kind.

#### Scenario: Daily loss uses the previous-midnight balance
- GIVEN a day opened at balance 10500 after a prior-day close, with a 400 loss
- WHEN (a) is computed
- THEN the loss is measured from 10500 and divided by the daily allowance, not from the initial capital

#### Scenario: Drawdown is worst across starts, with the median shown
- GIVEN starts with drawdowns of 2%, 4% and 8% of capital against a 10% allowance
- WHEN (b) is computed
- THEN the worst is 0.8, the median is 0.4, and ranking uses 0.8

#### Scenario: The worse FX end is used and reported
- GIVEN a non-USD member where the High end produces the larger loss
- WHEN headroom is computed
- THEN the High-end value is used and the response names High as the end

#### Scenario: Both kinds report headroom separately
- GIVEN a candidate valid on both kinds
- WHEN the result is produced
- THEN Deploy and Evaluation each carry their own (a), (b) and median (b), and none is blended across kinds

### Requirement: Full Computation Runs On The Shortlist Only

The full group computation MUST run only for shortlisted candidates, once per kind, and every shortlisted
candidate's reported metrics MUST equal a direct `POST api/ftmo-simulations/group` of the same members and
inputs. A candidate whose kind is refused MUST carry that kind's refusal reason and no metrics for it.

#### Scenario: Metrics equal the direct group run
- GIVEN a shortlisted candidate
- WHEN its metrics are compared with the group endpoint for the same members, risk, capital, grid and FX
- THEN outcome counts, shares and order statistics are equal

#### Scenario: Non-shortlisted candidates are never fully simulated
- GIVEN a completed job
- WHEN the full-simulation count is read
- THEN it is at most M x 2 kind runs and equals the shortlist size times the kinds evaluated

### Requirement: The Job Lifecycle Is Start, Poll, Cancel

A job MUST have a status of `Running`, `Completed`, `StoppedAtBudget`, `Cancelled` or `Failed`; the default
(zero) value MUST NOT read as `Completed`. `GET` MUST return status, progress (stage, candidates enumerated,
remaining after each funnel stage, full simulations done and planned, elapsed time) and the ranked results
found so far. Only one job MAY run at a time: a second `POST` while a job runs MUST answer 409 carrying the
running job's id and start nothing. `DELETE` MUST request cancellation, answer 204, and the job MUST reach
`Cancelled` with no work still running; `DELETE` on a finished job is a no-op 204, on an unknown id 404. `GET`
on an unknown id MUST be 404. Jobs live in memory only and MUST be lost on API restart; a poll for a lost id
is therefore 404, and the client-facing contract MUST disclose this.

#### Scenario: Progress is observable while running
- GIVEN a running job
- WHEN it is polled twice
- THEN status is `Running` and the full-simulation count does not decrease

#### Scenario: A second start returns 409 with the running id
- GIVEN a running job J
- WHEN another start is posted
- THEN the response is 409 carrying J's id and no second job exists

#### Scenario: Cancel stops all work and keeps partial results
- GIVEN a running job with some candidates fully computed
- WHEN `DELETE` is called
- THEN status becomes `Cancelled`, no computation continues, and the ranked partial results remain readable

#### Scenario: A new start is allowed after a terminal state
- GIVEN a job in `Completed`, `StoppedAtBudget`, `Cancelled` or `Failed`
- WHEN a new start is posted
- THEN it is accepted

#### Scenario: A restart loses jobs visibly
- GIVEN a job id issued before an API restart
- WHEN it is polled after the restart
- THEN the response is 404

### Requirement: A Budget Bounds The Job And Stops Visibly

The budget MUST bound the number of full group simulations and the wall-clock time, each by a named constant
(defaults and ceilings set from the PR1 benchmark). On reaching either limit the job MUST stop, finish the
unit in flight or abandon it, report `StoppedAtBudget` with which limit was hit, and return the ranking of the
candidates completed so far, with the count not completed.

#### Scenario: The simulation budget stops the job
- GIVEN a shortlist larger than the full-simulation budget
- WHEN the budget is reached
- THEN status is `StoppedAtBudget`, the limit reached is the simulation count, and the ranking covers only
  completed candidates in the deterministic order

#### Scenario: The wall-clock budget stops the job
- GIVEN a time budget shorter than the work
- WHEN it elapses
- THEN status is `StoppedAtBudget` naming the time limit and no work continues

#### Scenario: A budget stop is never shown as complete
- GIVEN a `StoppedAtBudget` job
- WHEN it is read
- THEN it carries the number of shortlisted candidates not computed

### Requirement: The Search Composes The One-Group Computation Without Changing It

The search MUST call the pure group computation once per candidate and kind from its own code. The
`ftmo-group-simulation` files MUST NOT contain a loop over combinations, ranking or randomness. A per-job
projection cache MUST be discarded when the job ends. Member resolution MUST be extracted from
`FtmoGroupSimulationReadService` into a reusable unit as a behaviour-preserving refactor, proven by the
existing group, multi-start and breach test suites passing UNEDITED and the group endpoint's responses being
byte-identical. New tripwire tests MUST assert that the evaluator, `ComputeRun` and the 18 slice files are
unchanged and that no looping or ranking call appears in the one-group capability files.

#### Scenario: Existing suites pass unedited
- GIVEN the shipped FTMO test suites with no edits
- WHEN they run after the extraction
- THEN they all pass

#### Scenario: The group endpoint response is unchanged
- GIVEN a fixture group
- WHEN the group endpoint is called before and after the extraction
- THEN the responses are byte-identical

#### Scenario: The one-group capability has no combination loop
- GIVEN the one-group capability sources
- WHEN the new tripwire test scans them
- THEN no enumeration of combinations or ranking appears there

#### Scenario: The cache is released
- GIVEN a finished or cancelled job
- WHEN the job ends
- THEN its projection cache is no longer held

### Requirement: Disclosures Always Accompany A Search Result

Every job response MUST carry the disclosures: (1) "N groups examined"; N is the number of candidates
enumerated before any filtering, and the response MUST also give the shortlist and fully simulated counts, and
that choosing the best of many backtests is optimistic (selection bias); (2) the figures are elimination risk
on closed-trade daily aggregates, not a certification or forecast; (3) the existing group disclosures
(concurrent breaches, same-close ordering, eligibility rules not modelled); (4) results are not persisted and
are lost on API restart. No field, enum member or text MUST use "passed", "safe", "survived", "would have
passed" or an equivalent affirmation.

#### Scenario: Selection bias and N are always present
- GIVEN any job response, including `Running` and `StoppedAtBudget`
- WHEN inspected
- THEN it carries the examined count and the selection-bias statement

#### Scenario: Banned wording is absent
- GIVEN every enum member and disclosure text of this capability
- WHEN inspected
- THEN none contains the banned wording, and the funded no-breach share is not named survival

### Requirement: The Proxy Is Calibrated Against Full Simulation Before It Is Trusted

A calibration test MUST compare the proxy's ordering with the full simulation on the P = 24 SBDEMO2 pool and
record the result with the PR. The shortlist size M and the default budget MUST be set from that record. The
calibration MUST show that every one of the top 10 candidates by full-simulation ranking, within the
calibration set, is inside the shortlist at the chosen M; otherwise M MUST be raised or the proxy revised and
the result re-recorded. Until recorded, the search endpoint MUST NOT ship.

#### Scenario: The recorded calibration justifies M
- GIVEN the recorded calibration
- WHEN M is compared with it
- THEN the full-simulation top 10 are all within the proxy shortlist at that M

#### Scenario: A proxy that over-prunes is detected
- GIVEN a calibration where a full-simulation top-10 candidate falls outside the shortlist
- WHEN the calibration test runs
- THEN it fails

### Requirement: A Benchmark Gates Funnel Performance And Sets The Budget

A benchmark, using the gating mechanism of the shipped benchmarks (opt-in, Release, median of 3), MUST measure
the enumeration plus proxy over the P = 24 pool (12,926 candidates) and the full group computation at k = 2, 3
and 4, and record the table with the PR. The proxy stage over the P = 24 pool MUST complete within 60 seconds.
The default budget MUST be derived from the measured full-simulation time so a default job ends within its
wall-clock default. The fixture MUST be closed-form with no randomness.

#### Scenario: The proxy stage meets its gate
- GIVEN the P = 24 closed-form fixture
- WHEN enumeration and proxy run under the gated benchmark
- THEN the median of 3 is within 60 seconds

#### Scenario: The budget default follows the measurement
- GIVEN the recorded table
- WHEN the default simulation budget is compared with it
- THEN simulation budget x measured time per group is within the default wall-clock budget

## Non-Goals

The risk grid (50/75/100 re-run, optional later slice); a deterministic null model; persistence, job history
and "save as portfolio"; live data; Darwinex Zero and Axi Select rules; per-member risk; exhaustive full
simulation of every combination; group-simulator charts (the next change `ftmo-group-simulator-charts`);
any edit to the single-strategy or group endpoints.

## Open Questions

- Exact route names and the 202/204 status choices are spec assumptions for design to confirm.
- How (a) and (b) combine into the single ranking "headroom" (this spec uses the worse of the two).
- The numeric defaults of M, budget and the 60 s proxy gate wait on the PR1 benchmark.
