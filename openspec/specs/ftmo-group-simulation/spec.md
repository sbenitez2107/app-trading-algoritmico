# FTMO Group Simulation Specification

## Purpose

Every shipped FTMO result is one strategy on one account. The user's real question is whether a
manually chosen group of strategies, sharing ONE FTMO account, reaches the targets before a breach. This
capability answers it by merging the members' trades into one chronological series and replaying it
through the shipped multi-start machinery, once per run kind, reporting the same distribution of chain
outcomes plus group-specific diagnostics (per-member contribution, first-breach attribution, peak
concurrent positions).

This is a NEW capability, not a delta against `ftmo-multi-start`, `ftmo-breach-simulation` or
`ftmo-challenge-race`. It reads them: the chain outcomes, order statistics, censoring, FX whole-chain
rule, funded phase, refusal vocabulary, banned wording and non-independence disclosures defined there
apply unchanged to a group's merged series and are NOT restated here. `FtmoBreachEvaluator.cs` and every
shipped engine file (race, funded phase, enumerator, sweep, calendar, `ComputeRun`) keep every
requirement and stay unedited.

Definitions used below. A **member** is one distinct strategy of the group. A **kind** is Deploy or
Evaluation (one held run per member per kind). A **merged series** is the union of the members' trades of
one kind, after window trimming, sorted and renumbered. `origRow` is a trade's per-run `RowIndex` as it
exists inside its own member's run. `memberOrder` is the member's position after sorting the distinct
`StrategyId` values ascending. A **group-wide refusal** refuses the whole request (both kinds). A
**member-level refusal** refuses only one kind's panel (or both, when the failing cause belongs to the
member's symbol) and lists the failing members.

## Requirements

### Requirement: The Group Endpoint Contract And Its Validation

The system MUST expose `POST api/ftmo-simulations/group` on the FTMO simulations controller, protected by
`[Authorize]` (an unauthenticated call MUST get 401, like every sibling FTMO endpoint). The request body
MUST carry: `memberStrategyIds`, `broker`, `initialCapital`, `targetRiskPerTrade`, the four SOURCE
lot-grid fields (`sizeDecimals`, `step`, `minLot`, `maxLots`), and an optional FX band (`fxLow`,
`fxHigh`). It MUST NOT carry a symbol: each member's symbol is resolved from its own run. It MUST carry
exactly one global `targetRiskPerTrade` (no per-member risk), and no source-grid default is ever applied.
The request MUST stay serialisable as plain JSON so a later "save as portfolio" can reuse it.

The endpoint MUST answer HTTP 400 with `{ message }` when: `memberStrategyIds` is missing, null or empty;
any id is the empty GUID; any of `broker`, `initialCapital`, `targetRiskPerTrade`, `sizeDecimals`, `step`,
`minLot`, `maxLots` is missing (`sizeDecimals = 0` is a legal value); or the number of DISTINCT ids
exceeds `MaxMembers`. A value that is present but unusable (non-positive capital or risk, or an invalid
source grid) MUST NOT be a 400: it refuses the whole request with the group refusal `InvalidRequest`,
exactly as the single-strategy endpoints do. An id that matches no strategy MUST NOT be a 400 either: it
is a group-wide refusal `MemberNotFound` that names the unknown ids.

> Missing-vs-unusable split mirrors `StrategyBacktestsController.TryValidateFtmoBreachQuery` (required
> fields -> 400) and `FtmoBreachSimulationRequest.TryBuildSourceGrid` (bad values -> `InvalidRequest`).
> The controller mirrors those rules in its own validation; the shipped controller is not edited.
> `broker` is carried because the FTMO limits row is keyed by broker.

#### Scenario: An unauthenticated call is rejected
- GIVEN a request with no valid JWT
- WHEN it is sent to `POST api/ftmo-simulations/group`
- THEN the response is 401 and nothing is computed

#### Scenario: A missing required field is a 400
- GIVEN a body with no `targetRiskPerTrade`
- WHEN it is posted
- THEN the response is 400 with a message naming the required fields, and no computation runs

#### Scenario: An empty member list is a 400
- GIVEN a body whose `memberStrategyIds` is empty
- WHEN it is posted
- THEN the response is 400

#### Scenario: An unknown strategy id is a group-wide refusal naming the id
- GIVEN a body containing an id that matches no strategy
- WHEN it is posted
- THEN the response is 200 with the group refusal `MemberNotFound`, the unknown id is named, and no kind runs

#### Scenario: sizeDecimals zero is accepted
- GIVEN a body with every required field present and `sizeDecimals = 0`
- WHEN it is posted
- THEN the request passes validation

#### Scenario: A present-but-unusable value refuses the group, it is not a 400
- GIVEN a body with `initialCapital = 0`
- WHEN it is posted
- THEN the response is 200 with the group refusal `InvalidRequest` and no kind runs

### Requirement: The Member Count Is Capped By A Named Constant Set From The Benchmark

The number of distinct members MUST NOT exceed a single named constant, `MaxMembers` (in
`FtmoGroupSimulationLimits`; one source of truth for validation, the candidates read and the UI). Its
value MUST be `min(8, the largest k whose group benchmark meets its gate)` (see the benchmark
requirement), set from measured evidence and recorded with that evidence. Until that measurement is
recorded the endpoint MUST NOT ship.

#### Scenario: A group at the cap is accepted
- GIVEN `MaxMembers` distinct, valid members
- WHEN the request is posted
- THEN it passes validation

#### Scenario: A group above the cap is a 400
- GIVEN `MaxMembers + 1` distinct members
- WHEN the request is posted
- THEN the response is 400 and the message states the cap value

#### Scenario: The cap counts distinct members, after dedup
- GIVEN `MaxMembers` distinct ids with one id repeated
- WHEN the request is posted
- THEN it passes validation, because the repeated id is not a second member

### Requirement: Duplicate Ids Are Deduplicated, Members Are Ordered By StrategyId, And Duplicate Names Warn

Repeated `memberStrategyIds` values MUST be collapsed server-side to one member, and the removed
duplicates MUST be echoed in `DuplicateIdsRemoved`. Members MUST then be sorted by `StrategyId` ascending,
and that order is the `memberOrder`. The group is a set: the result MUST NOT depend on the order of the
ids in the request. The response MUST echo the members in effect, in `memberOrder`. Two DISTINCT members
sharing the same `Name` (compared case-insensitively; the same logical strategy imported into two
accounts) MUST NOT be refused or merged: the response MUST carry a warning in `DuplicateNameWarnings`
listing those members, and they remain two independent members.

#### Scenario: A repeated id counts once
- GIVEN `memberStrategyIds = [B, A, B]`
- WHEN the group is simulated
- THEN the echoed members are A and B in ascending `StrategyId` order, B is listed in
  `DuplicateIdsRemoved`, B's trades appear once in the merged series, and k is 2

#### Scenario: Request order does not change the result
- GIVEN the same set of ids posted as `[A, B, C]` and as `[C, A, B]`
- WHEN both are simulated
- THEN the two responses are identical

#### Scenario: The same name on two accounts warns
- GIVEN two distinct strategy ids with the same `Name` (differing only in letter case) on different accounts
- WHEN the group is simulated
- THEN both stay members and `DuplicateNameWarnings` names both

### Requirement: Members Are Merged Into One Chronologically Ordered, Globally Renumbered Series

For each kind, the merged series MUST be sorted by `(Open, memberOrder, origRow)`. After sorting,
`RowIndex` MUST be renumbered sequentially so it is unique across the whole merged series, and the merge
MUST retain a map from each merged `RowIndex` to `(member, origRow)`. The merged series, not the
per-member runs, is what the shipped replay consumes. A trade's sizing (the risk normalisation of its
member's whole run) MUST be fixed by the member's complete run BEFORE any window trim, so trimming never
re-sizes a trade.

> Without renumbering, `FtmoChallengeRace.AttributeOpenDays` (writes by `trade.RowIndex`) and
> `FtmoBreachEvaluator.HasConcurrentOpenPosition` (skips others sharing a `RowIndex`) silently go wrong
> on members whose per-run indices coincide. The shipped evaluator is not edited; the merge is the fix.

#### Scenario: Merged RowIndex values are globally unique and sequential
- GIVEN two members whose per-run `RowIndex` values both start at 0
- WHEN the merged series is built
- THEN every merged `RowIndex` is distinct and the sequence is gapless in sorted order

#### Scenario: The row map is a correct reverse map
- GIVEN any merged series
- WHEN each merged `RowIndex` is looked up in the row map
- THEN it returns the exact `(member, origRow)` the trade came from, and every member's trade count in the
  map equals that member's trade count in the merged series

#### Scenario: Ties on Open break by member order, then original row
- GIVEN two trades from different members with an identical `Open`
- WHEN the series is sorted
- THEN the trade from the member with the lower `StrategyId` comes first, and two trades of the same
  member and `Open` keep their `origRow` order

#### Scenario: The Low and High ends carry the same renumbering
- GIVEN a merged series built for the low and the high FX end
- WHEN it is built
- THEN both ends have identical `RowIndex` per trade, and a mismatch between the two ends is a defect that
  fails loudly, never a silent result

#### Scenario: Equal per-run RowIndex values on different days are BOTH counted in trading days
- GIVEN member A has a scalable trade with per-run `RowIndex 5` opened on Berlin day D1, and member B has
  a scalable trade with per-run `RowIndex 5` opened on Berlin day D2 (D1 != D2), and no other trades
- WHEN trading days are counted over the merged series
- THEN the count is 2 (both days are seen), not 1

#### Scenario: Equal per-run RowIndex values on overlapping trades are both seen by the concurrency check
- GIVEN member A's trade with per-run `RowIndex 5` open 10:00-12:00, and member B's trade with per-run
  `RowIndex 5` open 11:00-13:00
- WHEN the merged series is evaluated for concurrent open positions
- THEN a concurrent-open-position condition is detected for the overlap, and the evaluation result equals
  that of the same two trades given distinct indices

#### Scenario: Starts are enumerated on the merged series
- GIVEN a merged series
- WHEN monthly starts are enumerated
- THEN each start is the first scalable trade opened in a Berlin month across ALL members, per
  `ftmo-multi-start`

#### Scenario: Trimming does not re-size trades
- GIVEN a member whose run has trades both inside and outside the window
- WHEN the member's trades are trimmed
- THEN each surviving trade keeps the size it had in the member's full run

### Requirement: The Replay Window Is The Intersection Of The Members' Ranges

For each kind, a member's range MUST be `[min of its trades' Open, max of its trades' Close]`, over ALL
its projected trades (Unscalable included). The window MUST be `[max over members of range start, min over
members of range end]`. Every trade MUST be kept only when `Open >= window start` and `Close <= window
end`; a trade opened before the window start, or straddling the window end, MUST be excluded BEFORE the
replay. The response MUST echo, per kind, the window and each member's coverage: its first open, its last
close, and its number of in-window trades. When the window is empty (start after end), that kind MUST be
refused with the group refusal `NoCommonWindow`, which blames no member and echoes every member's
coverage; the other kind is independent.

#### Scenario: Trimming drops trades outside the window
- GIVEN member A covers Jan-Dec and member B covers Apr-Dec
- WHEN the Deploy series is merged
- THEN the window starts at B's first Open, A's earlier trades are excluded, and A's in-window count
  excludes them

#### Scenario: A straddling trade is excluded
- GIVEN a trade opened inside the window but closing after the window end
- WHEN the series is trimmed
- THEN that trade is excluded and not counted in its member's in-window count

#### Scenario: An empty intersection is refused with coverage
- GIVEN two members whose date ranges do not overlap
- WHEN the kind is simulated
- THEN that kind is refused `NoCommonWindow` with no starts, no member is blamed, and each member's
  first/last date is echoed

#### Scenario: NoCommonWindow is evaluated per kind and never hides the other kind
- GIVEN members whose Deploy ranges do not overlap but whose Evaluation ranges do
- WHEN the group is simulated
- THEN the Deploy result is refused `NoCommonWindow` with every member's coverage, and the Evaluation result
  runs with its own window and starts (and the same holds with the kinds swapped)

#### Scenario: The window and coverage are echoed on a successful run
- GIVEN a kind that runs
- WHEN the response is produced
- THEN it carries the window start/end and one coverage entry per member

### Requirement: Kinds Are Paired Strictly, Never Silently Shrunk

Deploy MUST be paired only with Deploy and Evaluation only with Evaluation (one held run per
`(strategy, kind)`). When any member lacks a held run of a kind, that kind's result MUST be refused with
the group refusal `MemberMissingKind` listing every member that lacks it; the group MUST NOT be silently
simulated with fewer members. The other kind MUST still run if it is itself valid. A member with no
imported run at all makes both kinds refuse.

#### Scenario: One kind missing on one member refuses only that kind
- GIVEN members A and B, B having no Evaluation run
- WHEN the group is simulated
- THEN the Deploy result runs, the Evaluation result is refused `MemberMissingKind` listing B

#### Scenario: Every missing member is listed
- GIVEN three members, two lacking Deploy
- WHEN the group is simulated
- THEN the Deploy refusal lists both members

#### Scenario: A member with no imported backtests refuses both kinds
- GIVEN a member with no held runs
- WHEN the group is simulated
- THEN both kinds are refused `MemberMissingKind` listing that member

### Requirement: Group-Wide Refusals Refuse The Whole Request

A group-wide refusal MUST refuse the whole request: no kind runs, every kind carries no findings, and the
envelope carries the refusal. The group-wide refusals are:

- `InvalidRequest`: a present-but-unusable value (see the endpoint requirement).
- `MemberNotFound`: an unknown strategy id (see the endpoint requirement).
- `SharedInputsRefused`: the broker limits row refuses (`LimitsNotConfigured`, `ProductNotTwoStep`,
  `DrawdownModelNotStatic`), carrying the shipped `FtmoSimulationRefusal` reason in `SharedRefusal`. These
  are properties of the broker row and apply under their shipped meaning. `TimeZoneDataUnavailable` (the
  Berlin zone, or a member's source zone, cannot be resolved on this host) is also group-wide under
  `SharedInputsRefused`, because zones are a property of the group (added by tasks, 2026-10-03).
- `MixedSourceTimeZones`: the members' source time zones are not all identical. The refusal MUST list each
  member with its zone, and no series is merged.

Together with the HTTP 400 for shape errors and an over-cap member count, these are the only whole-request
refusals. Every member-level problem is handled by the next requirement and MUST NOT refuse the whole
request.

#### Scenario: Mixed time zones refuse the whole request
- GIVEN two members whose specs declare different source time zones
- WHEN the group is simulated
- THEN the response is refused `MixedSourceTimeZones` listing each member and its zone, and neither kind runs

#### Scenario: A limits refusal refuses the whole request
- GIVEN a broker whose limits row is not configured
- WHEN the group is simulated
- THEN the response is refused `SharedInputsRefused` with `SharedRefusal = LimitsNotConfigured`, and neither
  kind runs

#### Scenario: The refusal enum zero value is InvalidRequest
- GIVEN a default-constructed `FtmoGroupRefusal`
- WHEN its value is read
- THEN it is `InvalidRequest` (value 0), so an unset field never reads as a different refusal

### Requirement: A Member-Level Problem Refuses Only The Affected Kind's Panel

Member-level problems MUST NOT refuse the whole group. For each kind, every member MUST be checked and ALL
failing members MUST be listed with their own reason (never just the first); refusing members MUST NOT be
dropped to let the rest run. The reasons reuse the shipped `FtmoSimulationRefusal` values and are carried
by the group refusal `MemberRunRefused`: `RiskNotEstimable` (the risk normalizer cannot estimate this
member's risk), `PointValueNotCalibrated` (calibration status is not Calibrated or point value unusable),
`InstrumentSpecMissing` (no usable FTMO instrument spec for the member's symbol), and the FX reasons of the
FX requirement.

The kind that is refused follows where the problem belongs. `RiskNotEstimable` belongs to one run, so it
refuses ONLY that member's kind (Deploy and Evaluation are different backtests, so a member can be
estimable in one and not the other). `InstrumentSpecMissing`, `PointValueNotCalibrated` and the FX
reasons belong to the member's symbol, so they refuse BOTH kinds, each listing the member. The other kind
runs whenever it has no failing member, and a member missing a kind, or with no in-window trades, refuses
that kind only: `MemberMissingKind` (see its requirement) and `MemberHasNoTradesInWindow` (a member with
zero trades inside the window, listed per member). A refused kind MUST carry no starts, no summary and no
diagnostics.

#### Scenario: A risk problem in one kind refuses only that kind
- GIVEN members A and B, B's Deploy run has unestimable risk and B's Evaluation run is estimable
- WHEN the group is simulated
- THEN the Deploy panel is refused `MemberRunRefused` listing B with `RiskNotEstimable`, and the Evaluation
  result runs with both members

#### Scenario: A symbol-level problem refuses both kinds
- GIVEN members A (spec present) and B (symbol with no FTMO instrument spec)
- WHEN the group is simulated
- THEN each kind is refused `MemberRunRefused` listing B with `InstrumentSpecMissing`, and A is not
  simulated alone

#### Scenario: Several failing members are all listed with their own reasons
- GIVEN member B uncalibrated (`PointValueNotCalibrated`) and member C with unestimable risk
  (`RiskNotEstimable`) in the Deploy run only
- WHEN the group is simulated
- THEN the Deploy refusal lists B with `PointValueNotCalibrated` and C with `RiskNotEstimable`, and the
  Evaluation refusal lists only B

#### Scenario: A member with no trades in the window refuses its kind
- GIVEN a kind whose intersection window is non-empty but one member has zero trades inside it
- WHEN the kind is simulated
- THEN that kind is refused `MemberHasNoTradesInWindow` listing that member, and the other kind is unaffected

#### Scenario: A member-level problem is not a whole-request refusal
- GIVEN one failing member and no group-wide refusal
- WHEN the group is simulated
- THEN the response status is not a group-wide refusal, and at most the affected kinds are refused

#### Scenario: A refused kind carries no findings
- GIVEN a refused kind
- WHEN the response is produced
- THEN it carries no starts, no summary and no diagnostics for that kind

### Requirement: One FX Band Applies Only To Non-USD Members

The request's single FX band (`fxLow`, `fxHigh`) MUST be applied only to members whose instrument settles
in a non-USD currency; USD members MUST be unaffected, project at `(1, 1)` and need no band. When at least
one non-USD member is present and no band is given, the members concerned MUST be refused
`FxRateNotDeclared`; when the band is invalid (inverted, non-positive) and a non-USD member is present,
`InvalidFxBand`, per the shipped meanings. These are member-level, symbol-level reasons: each kind is
refused `MemberRunRefused` listing those members. When every member settles in USD the band MUST be
ignored and no FX refusal applies. The FX whole-chain rule of `ftmo-multi-start` applies to the merged
chain. The response MUST echo each member's applied FX band, and the group's echoed band is the declared
band when any member is non-USD, otherwise `(1, 1)`.

#### Scenario: An all-USD group needs no band
- GIVEN members whose instruments all settle in USD and no `fxLow`/`fxHigh`
- WHEN the group is simulated
- THEN no FX refusal is produced

#### Scenario: A non-USD member without a band refuses
- GIVEN a group containing one EUR-settling member and no FX band
- WHEN the group is simulated
- THEN each kind is refused `MemberRunRefused` listing that member with `FxRateNotDeclared`

#### Scenario: The band is applied to the non-USD member only
- GIVEN a group of one USD member and one EUR member with a valid band
- WHEN the merged series is built
- THEN the EUR member's P/L is converted over the band and the USD member's is not

#### Scenario: An invalid band with a non-USD member refuses
- GIVEN a non-USD member and `fxLow > fxHigh`
- WHEN the group is simulated
- THEN each kind is refused `MemberRunRefused` listing that member with `InvalidFxBand`

### Requirement: The Response Envelope, Refusal Enum And Per-Kind Result Shape

The response MUST be one `FtmoGroupSimulationDto` envelope carrying: `Status`; `Refusal` (an
`FtmoGroupRefusal`, or null); `SharedRefusal` (the shipped `FtmoSimulationRefusal` of a limits refusal, or
null); `DailyLossLimitPct` and `MaxLossLimitPct`; `Members` (id, name, memberOrder, profit currency, source time zone id and the
applied FX low/high); `DuplicateIdsRemoved`; `DuplicateNameWarnings`; `Kinds`; and `Disclosures`.

`FtmoGroupRefusal` MUST contain exactly these members: `InvalidRequest = 0`, `SharedInputsRefused`,
`MemberNotFound`, `MemberMissingKind`, `MemberRunRefused`, `MixedSourceTimeZones`, `NoCommonWindow` and
`MemberHasNoTradesInWindow`.

Each element of `Kinds` MUST be one kind result carrying: `Kind`; `Status`; `Refusal`; `MemberRefusals`
(the failing members, each with its own reason); `Window`; `Coverage`; `Run`; and `Diagnostics`. A
successful kind's `Run` MUST be one `FtmoMultiStartRunDto` (the shipped shape, unchanged) with `RunId` the
empty GUID, because a merged series is not a held run, and with the shipped multi-start `Disclosure`
unchanged. Within `Run`: `Segment` is the members' common segment when they all agree and `Unknown`
otherwise; `UnscalableCount` is the number of Unscalable trades INSIDE the window (the in-window sum over
members); `ProfitTargetMismatch` comes from the broker-wide limits row and is passed through unchanged.
Deploy and Evaluation results MUST be separate and never merged. The envelope MUST NOT carry the member
cap or a risk echo: the cap is supplied by the candidates read, and the worst simultaneous risk `k x
targetRiskPerTrade` is derived by the client from the member count and the typed risk.

#### Scenario: Deploy and Eval results are separate
- GIVEN a group valid on both kinds
- WHEN the response is produced
- THEN there is one multi-start result for Deploy and one for Evaluation, with no figure shared

#### Scenario: A successful run has the shipped shape with an empty RunId
- GIVEN a valid kind
- WHEN the response is produced
- THEN the result has every field of `FtmoMultiStartRunDto` and `RunId` is the empty GUID

#### Scenario: The envelope carries the loss limits and the members in effect
- GIVEN a group that runs
- WHEN the response is produced
- THEN `DailyLossLimitPct`, `MaxLossLimitPct` and one `Members` entry per member (in `memberOrder`, with the
  applied FX band) are present

#### Scenario: Segment is common or Unknown
- GIVEN members of the same segment, and then members of different segments
- WHEN each group is simulated
- THEN the first result echoes the common segment and the second echoes `Unknown`

#### Scenario: The unscalable count is the in-window sum
- GIVEN a member with an Unscalable trade outside the window and another inside it
- WHEN the kind runs
- THEN `UnscalableCount` counts only the one inside the window

### Requirement: A One-Member Group Equals The Single-Strategy Multi-Start Result

For a group of exactly one member, per kind, over the same inputs (broker, capital, risk, source grid, FX
band), the group's `Run` MUST equal that strategy's single-strategy multi-start run for the same kind in
EVERY field except `RunId` (the empty GUID in the group result, the held run's id in the single result).
`Disclosure` is NOT an exception: the group disclosures live in the envelope's `Disclosures`, and `Run`
keeps the shipped multi-start `Disclosure`. The compared fields include `Kind`, `Segment`, `Status`,
`Refusal`, `RaceRefusal`, `StoredProfitTargetPct`, `Grain`, `Rules`, `Starts`, `Summary`,
`MonthsWithoutStart`, `Start1DiffersFromSingleStartAnchor`, `FxLow`, `FxHigh`, `UnscalableCount`,
`NotModelled` and `Disclosure`.

The equivalence holds WHENEVER no two trades of that member share a `CloseSource` instant with an `origRow`
order that differs from their Open order, because renumbering follows `(Open, origRow)` and ties on close
break by Open instead of file row. The window trim is the identity for one member, so no trade is
excluded. The system MUST NOT claim equality for a run that has such a close tie. The equivalence MUST be
pinned by a test on a fixture without such ties (the test asserts the fixture has none), and a second
test MUST document the tie case.

#### Scenario: A one-member group matches the single-strategy result
- GIVEN one strategy with a Deploy and an Evaluation run, no same-close tie that disagrees with open
  order, and identical parameters
- WHEN the group endpoint and the single-strategy multi-start endpoint are both called
- THEN, for each kind, every field of `Run` is equal except `RunId`

#### Scenario: A one-member refusal matches the single-strategy refusal
- GIVEN one strategy whose symbol has no FTMO instrument spec
- WHEN both endpoints are called
- THEN the group reports that member refused `InstrumentSpecMissing` and the single endpoint reports
  `Refused` with the same reason

#### Scenario: A same-close tie that disagrees with open order is not claimed equal
- GIVEN a run with two trades sharing a close instant whose file-row order differs from their Open order
- WHEN the one-member equivalence is documented
- THEN the system makes no equality claim for that run, and the documenting test shows the difference

### Requirement: Group Diagnostics Are Derived Per Kind From The Merged Series

Each successful kind MUST carry diagnostics derived without editing any engine file, from the merged
series, its row map and the kind's `Run`:

1. **Per-member contribution, over the window (not per start)**: for each member, its in-window trades,
   its scalable trades, its net P/L at the low end and at the high end, and its counts of trades raised to
   the minimum lot, capped at the maximum lot, and Unscalable.
2. **First-breach attribution**: for each start, the deciding breach is the Phase 1 breach if there is
   one, else the Phase 2 breach, else the Funded breach. The member(s) credited are every member that has
   a trade closing at that breach's `OutcomeSourceClose` (and that satisfies the phase-subset rule), resolved
   through the row map. When more than one member has a trade closing at that instant, ALL are credited and
   the start MUST be marked as tied (a shared-close start), never assigned to one member. The aggregate
   MUST report, per member, the starts credited by phase (Phase 1, Phase 2, Funded) and its sole-contributor
   starts, and, per kind, the shared-close (tied) starts.
3. **Peak concurrent open positions**: the maximum number of simultaneously open positions over the
   in-window scalable trades with `Close > Open`, with the first instant it is reached and the members at
   that peak. At an identical instant, closes are processed before opens (the shipped open-position sweep
   convention).

#### Scenario: Per-member contribution sums to the merged total
- GIVEN a group of three members
- WHEN diagnostics are produced
- THEN the members' in-window trade counts sum to the merged series length and their net contributions
  sum to the merged net P/L at each end

#### Scenario: A breach is attributed to the member that closed it
- GIVEN a start whose phase 1 breaches at a close of member B's trade, no other member closing then
- WHEN the attribution is produced
- THEN the start is a sole-contributor start of B for Phase 1

#### Scenario: A same-instant close by two members credits both and is marked tied
- GIVEN a breaching close shared at the same instant by a trade of A and a trade of B
- WHEN the attribution is produced
- THEN both A and B are credited, and the start counts toward the kind's shared-close (tied) starts

#### Scenario: Attribution reconciles with the deciding breaches
- GIVEN a kind whose starts include N with a deciding breach
- WHEN the aggregate attribution is produced
- THEN the sole-contributor starts of all members plus the shared-close starts equal N

#### Scenario: Peak concurrency counts across members
- GIVEN member A's trade open 10:00-12:00 and member B's open 11:00-13:00 and C's open 11:30-11:45
- WHEN diagnostics are produced
- THEN the peak concurrent open positions is 3 and the members at the peak are A, B and C

#### Scenario: A close and an open at the same instant do not overlap
- GIVEN member A's trade closing at 12:00 and member B's trade opening at 12:00
- WHEN diagnostics are produced
- THEN the two do not count as concurrent at 12:00

### Requirement: Group Disclosures Always Accompany A Result

Every produced group result (and every refusal) MUST carry in the envelope's `Disclosures`, in addition to
the shipped multi-start disclosure kept in each `Run`, the three group texts stating that: (1) in a group,
concurrent-open-position breaches are expected to dominate, so the Clean/Contingent split of a breach
loses its usual meaning; (2) when several closes share an identical instant, the order in which they are
applied is a modelling choice that can create or remove a breach; (3) eligibility rules of the funding
programme (for example a martingale or grid ban) are NOT modelled. No field, enum member or text of this
capability MUST use "passed", "safe", "survived", "would have passed" or an equivalent affirmation of
survival.

#### Scenario: The three group disclosures are present
- GIVEN any produced group result or refusal
- WHEN its envelope `Disclosures` is inspected
- THEN it states concurrent breaches dominate, same-close ordering is a modelling choice, and eligibility
  rules are not modelled

#### Scenario: Banned wording is absent
- GIVEN every enum member and disclosure text of this capability
- WHEN inspected
- THEN none contains the banned wording

### Requirement: The Shipped Engine Is Untouched And The Input Refactor Is Behaviour-Preserving

`FtmoBreachEvaluator.cs`, the race, funded phase, enumerator, open-position sweep, replay calendar,
`ComputeRun`, the single-strategy endpoints and the analytics tripwire slice files
(`BacktestReadService.cs`, `BacktestsController.cs`, `GroupRiskAnalysisRequest.cs`) MUST remain unedited.
The only shipped file this capability MAY change is `FtmoSimulationInputs`, by splitting limits
resolution from per-symbol resolution; that split MUST be behaviour-preserving, proven by the existing
FTMO test suites passing unedited. The group computation MUST be a pure function of already-resolved
member inputs and parameters (no I/O) so a later generator can compose it. The capability simulates
exactly one caller-chosen group per request: no loop over combinations, no ranking, no randomness.

#### Scenario: The evaluator is byte-identical
- GIVEN the repository before and after this capability
- WHEN `FtmoBreachEvaluator.cs` is compared
- THEN it is unchanged

#### Scenario: Existing FTMO suites pass unedited after the refactor
- GIVEN the shipped FTMO test suites with no edits
- WHEN they run after the `FtmoSimulationInputs` split
- THEN they all pass

#### Scenario: Single-strategy endpoints are unchanged
- GIVEN the shipped single-start and multi-start results for a fixture
- WHEN this capability is added
- THEN those results are byte-identical to before

### Requirement: A Group Benchmark Gates Performance And Sets The Cap

A benchmark test MUST exercise groups of k members, each with ~1,000 trades, through the merge and the
group computation of BOTH kinds, with the Never and Fast profiles, using the same gating mechanism as the
shipped `FtmoMultiStartBenchmarkTests` (opt-in, Release, median of 3). Its fixture MUST be closed-form
(no randomness) and force cross-member overlap and duplicate per-run indices. The gate MUST be 5 seconds
for the Never profile. The measurement MUST cover k in {2, 4, 6, 8, 10}, and the measured table MUST be
recorded with the test and the PR. `MaxMembers` MUST be `min(8, largest k whose Never median is within the
gate)`. The gate test MUST run at `k = MaxMembers`, so raising the constant forces a new measurement. If
even k = 6 fails the gate, the cap is lowered; no engine file is edited to meet it.

#### Scenario: The group benchmark runs at the cap and meets its gate
- GIVEN a fixture of `MaxMembers` members of ~1,000 trades each
- WHEN the group computation runs under the gated benchmark
- THEN the median of 3 is within 5 seconds

#### Scenario: The recorded measurement justifies the cap
- GIVEN the recorded benchmark table
- WHEN `MaxMembers` is compared against it
- THEN the value is `min(8, the largest k within the gate)`

### Requirement: A Candidates Read Supplies The Picker's Facts

A dedicated, `[Authorize]`d read, `GET api/ftmo-simulations/candidates?tradingAccountId={guid}` on the
same controller, MUST list candidate strategies scoped to ONE account, one entry per strategy, and MUST
also expose `MaxMembers` and the account id. A missing or empty `tradingAccountId` MUST be a 400. Each
entry MUST carry: id, name, verbatim symbol, a flag telling whether the same name exists on another
account, and, per kind (Deploy and Evaluation), either the held run's facts or absence. A held run's
facts are: run id, symbol, trade count, first open, last close, whether an FTMO instrument spec exists for
its symbol, whether its calibration is usable, its profit currency, whether it needs an FX band
(non-USD), and its source time zone id. Flags are per run, because Deploy and Evaluation symbols could
differ. The read MUST NOT reuse the cross-broker resizer path, and it MUST NOT change `GET
api/strategies/candidates`.

#### Scenario: Candidates are scoped to one account
- GIVEN strategies on two accounts
- WHEN the candidates of account X are requested
- THEN only account X's strategies are returned

#### Scenario: Run presence and range are reported per kind
- GIVEN a strategy with a Deploy run only
- WHEN its candidate entry is produced
- THEN Deploy is present with count and date range and Evaluation is reported absent

#### Scenario: Spec and calibration availability are flagged per run
- GIVEN a strategy whose symbol has no FTMO instrument spec
- WHEN its candidate entry is produced
- THEN the spec flag is false for each held run of that symbol

#### Scenario: The cap and the same-name flag are supplied
- GIVEN a candidate whose name also exists on another account
- WHEN the read is produced
- THEN the response carries `MaxMembers` and that candidate's same-name flag is true

#### Scenario: A missing account id is a 400
- GIVEN a request with no `tradingAccountId`
- WHEN it is sent
- THEN the response is 400

## Non-Goals

Automatic combination generation or ranking of groups, live data, Darwinex Zero or Axi Select rules,
per-member risk, persistence of groups (no `Portfolio` write), correlation analysis, eligibility-rule
modelling, and any edit to the single-strategy endpoints or modal.

## Open Questions

- The final `MaxMembers` value and the recorded benchmark table wait on the B2 benchmark.
- Whether SQX file-row order matches Open order within a run. This only affects the one-member tie
  criterion; verify on real SBDEMO2 data during B2.
- RESOLVED in tasks.md: a kind whose failing members have mixed causes carries `MemberRunRefused`, with a
  per-member list where each member keeps its own reason.
- RESOLVED in tasks.md: `MixedSourceTimeZones` lists each member's zone through a new
  `FtmoGroupMemberDto.SourceTimeZoneId` field (the zone is not repeated in the envelope).
