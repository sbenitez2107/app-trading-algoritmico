# Backtest Portfolio Analytics Specification

## Purpose

Publish correlation and VaR figures for one caller-named group of strategies from
`BacktestNetSeries` (built by `backtest-net-series-bridge`), per funding service, with density
disclosure so a figure the backtest data cannot support is withheld rather than shown as zero. No
new statistical estimator, no public surface that re-opens the door D9 closes. This capability
owns the density gates, the currency-vs-band-position boundary, correlation alignment, segment
provenance and run selection, and the reporting surface. The bridge's pairing, throw-vs-status,
and weight-refusal guarantees are specified in `backtest-net-series-bridge` and are not restated
here.

## Requirements

### Requirement: Correlation And VaR Gain Typed Adapters Over Backtest Series, Bit-Identical For The Live Path

`PortfolioAnalyticsCalculator` MUST expose its alignment logic as a private core plus two typed
public entry points: the existing `PortfolioMemberInput[]`-based overload (unchanged) and a new
overload over `BacktestNetSeries[]`. It MUST NOT expose a public overload accepting an untyped
`(label, broker, dated nets)` tuple — that raw door would let a hand-scaled projection bind to
the analytics primitives, re-opening exactly what D9's structural guarantee closes. Every
existing real-account output computed through the `PortfolioMemberInput[]` path MUST remain
bit-identical to its pre-change value for the same inputs.

#### Scenario: Shipped output is unchanged
- GIVEN the existing portfolio correlation and VaR regression suites
- WHEN they run against the refactored calculator
- THEN every asserted figure is bit-identical to its pre-change value

#### Scenario: Backtest series compute through the typed adapter
- GIVEN a `BacktestNetSeries[]` built by the bridge from a backtest group
- WHEN correlation and VaR are computed over it
- THEN the figures are produced through the `BacktestNetSeries[]` adapter, without constructing a `PortfolioMemberInput`

#### Scenario: No public raw-tuple overload exists
- GIVEN the calculator's public surface
- WHEN it is inspected
- THEN no public member accepts an untyped `(label, broker, dated nets)` tuple; only the `PortfolioMemberInput[]` and `BacktestNetSeries[]` entry points are public

### Requirement: The Backtest VaR Adapter Uses The Full Dated Series, Never The 250-Observation Trim

Shipped `ComputeVaR` trims its daily series to the most recent `windowDays` observations
(default 250) before computing the percentile. The backtest adapter MUST pass no such trim and
MUST evaluate every density-gate relation over the run's full dated series. Trimming would change
`N` and therefore the gate's threshold and the withheld/produced outcome (measured: IST's full
series has `N`=3,860, 164 negative days, needing ≥194 to produce; the same series trimmed to 250
observations has only 5 negative days, needing ≥14 — a materially different, and wrong, gate
evaluation).

#### Scenario: Gate evaluated over the full series, not the 250-observation trim
- GIVEN the `ListOfTrades_XAUUSD_H1_IST.csv` fixture's full dated series (`N` = 3,860 days, 164 negative days)
- WHEN the backtest VaR adapter computes the daily VaR95 density gate
- THEN it evaluates the gate against `N` = 3,860 and 164 negative days, never against the most recent 250 observations

### Requirement: The Run's BacktestSegment Travels As Metadata, Not As A Date Filter

`BacktestSegment` (`Unknown` / `InSample` / `OutOfSample` / `InSampleTest`) is constant across
every trade of a run: the importer (`SqxTradeListParserService`) rejects any file whose rows carry
more than one raw `Sample type` value (`SampleTypeRaw`), so a run's trades are wholly `IS`, wholly
`OOSn`, or wholly `IST` in the raw CSV text. This capability performs no date-range filtering by
segment. The segment MUST be read off the run's own trades and carried as metadata alongside
every correlation and VaR figure, stating which sample the figures were computed over. `OosWindow`
(which trades of an `Evaluation` run are genuinely unseen) answers a different, later question and
MUST NOT be invoked here.

#### Scenario: Segment reported as metadata
- GIVEN a run whose trades all carry `BacktestSegment.InSampleTest`
- WHEN the group risk analysis is produced
- THEN the output states `InSampleTest` as the segment the figures were computed over, and no trade was excluded by date

#### Scenario: No date filtering occurs
- GIVEN a run whose trades all carry `BacktestSegment.OutOfSample`
- WHEN the group risk analysis is produced
- THEN every trade of the run participates in the analysis; `OosWindow` is never consulted and no `CloseTime` comparison is performed

### Requirement: A Requested Segment Is Required, And Evidence For It May Be Absent

Every group risk analysis MUST require an explicit `BacktestSegment` selection before computing
any figure — without one, every figure would be silently in-sample, which the proposal names as
the number most likely to be optimistic. The request's segment field MUST be nullable
(`BacktestSegment?`): a non-nullable field cannot express "not specified", because an omitted JSON
property binds to `0` (`Unknown`), making a forgetful caller indistinguishable from one who
deliberately asked for `Unknown`. For a member with no run carrying the requested segment, the
result MUST be an explicit "no evidence for this segment" state for that member — no series, not
an empty one. This state previously described a trade-level filter result; filtering no longer
exists (see the metadata requirement above), so the state now describes an entire missing run
rather than a filtered-out sub-range, but it is not dropped.

#### Scenario: No segment, no figures
- GIVEN a group risk analysis request whose segment field is omitted (null)
- WHEN it is submitted
- THEN it is refused and no correlation or VaR figure is produced

#### Scenario: No run carries the requested segment
- GIVEN a member whose runs include none carrying the requested segment
- WHEN the group risk analysis is produced
- THEN that member's result is an explicit "no evidence for this segment" state, not an empty series

### Requirement: Unknown Is Not A Selectable Segment

`BacktestSegment.Unknown` is the enum's default and exists, per its own documentation, so an
unrecognised future label degrades safely instead of pointing at a meaningful segment. A run whose
trades are genuinely `Unknown` carries a label the parser could not classify — its raw text is
preserved but its meaning is unestablished. Publishing a figure labelled "computed over the
Unknown sample" asserts something the data does not support, the same failure class as publishing
a `0.00` VaR. A request for `Unknown` MUST be refused, and a run whose trades are `Unknown` MUST
NOT be selected for any requested segment. This is distinct from a trade-less run (which yields no
segment and must never be coerced to `Unknown`, per the run-selection requirement below) — both
lead to no series, by different reasoning, and both MUST be specified.

#### Scenario: A request for Unknown is refused
- GIVEN a group risk analysis request whose segment field is explicitly `BacktestSegment.Unknown`
- WHEN it is submitted
- THEN it is refused

#### Scenario: A run genuinely labelled Unknown is never selected
- GIVEN a run whose trades all carry `BacktestSegment.Unknown`
- WHEN any segment is requested for that member
- THEN the run is never selected as the member's input

### Requirement: A Member's Run Is Selected By A Bounded Two-Row Segment Match, Never Inferred From Kind

`BacktestRunConfiguration` makes `(StrategyId, Kind)` unique, so a strategy has at most one
`Deploy` run and one `Evaluation` run — selecting a member's run is a choice among at most two
rows, never a search. `BacktestRun` carries no `Segment` field; the segment MUST be derived from
`Min`/`Max` of `BacktestSegment` over the run's own trades. `Kind` and `Segment` are independent
axes and neither may be inferred from the other — a `Deploy` run's trades MAY be `InSampleTest`.

Given a requested segment and a strategy's up to two runs:
- A run with no trades (`Min` is null) yields no segment and no evidence for that run — never
  coerced to `Unknown`.
- A run whose trades disagree (`Min != Max`), reachable only by a hand-edited database, MUST be
  refused, naming the run.
- Exactly one run matching the requested segment MUST be used as the member's input.
- Both runs matching the requested segment MUST be refused, naming the strategy and both `Kind`s
  — two runs sharing a segment are two different parameter sets over the same sample, and picking
  either would make the published figure depend on an arbitrary choice. An optional
  `BacktestRunKind` on the request MAY disambiguate; absent it, the ambiguity is refused rather
  than guessed.

**A trade-less run is non-fatal to the member.** A half-populated strategy — one `Deploy` or
`Evaluation` slot imported, the other not yet — is the normal intermediate state of this bounded
two-row constraint, not an edge case. A run yielding no segment (its `Min` is null) MUST be
excluded from the segment match and contribute nothing to it; the member MUST still resolve from
whichever remaining run matches the requested segment. The member-level "no evidence for this
segment" refusal (see the requirement above) fires only when **no** run matches after trade-less
runs are excluded — not merely because one of the strategy's (at most two) runs happens to be
trade-less.

#### Scenario: A run with no trades yields no evidence, never Unknown
- GIVEN a run with no trades
- WHEN its segment is derived
- THEN it yields no segment and no evidence for that run; it is never coerced to `BacktestSegment.Unknown`

#### Scenario: Disagreeing trades refuse the run
- GIVEN a run whose trades' `Segment` values disagree
- WHEN its segment is derived
- THEN the run is refused, naming it

#### Scenario: Kind never overrides a segment match
- GIVEN a strategy whose `Deploy` run's trades all carry `BacktestSegment.InSampleTest` and whose `Evaluation` run's trades all carry `BacktestSegment.OutOfSample`, with `InSampleTest` requested
- WHEN the member's run is selected
- THEN the `Deploy` run is used; `Kind` is never used to infer or override the segment match

#### Scenario: Both runs matching the segment are refused
- GIVEN a strategy whose `Deploy` and `Evaluation` runs both carry `BacktestSegment.InSampleTest`
- WHEN `InSampleTest` is requested without a `BacktestRunKind`
- THEN the member is refused, naming the strategy and both `Kind`s

#### Scenario: A trade-less run does not fail the member when the other run matches
- GIVEN a strategy whose `Deploy` run has no trades and whose `Evaluation` run's trades all carry `BacktestSegment.InSampleTest`, with `InSampleTest` requested
- WHEN the member's run is selected
- THEN the trade-less `Deploy` run is excluded from the match and contributes nothing, and the member resolves using the `Evaluation` run — it is not refused merely because one of its two runs has no trades

### Requirement: A Group Whose Members Disagree On Segment Is Refused

A correlation or VaR figure implies a single sample label. A group whose members' selected runs
carry different `BacktestSegment` values MUST be refused, naming the disagreeing members and their
segments. No figure is computed with a "mixed" label, and no majority segment is silently assumed.

#### Scenario: Disagreeing segments refuse the group
- GIVEN a group where one member's selected run carries `BacktestSegment.InSampleTest` and another's carries `BacktestSegment.OutOfSample`
- WHEN the group risk analysis is requested
- THEN it is refused, naming both members and their segments; no correlation or VaR figure is produced

### Requirement: A Non-Positive Initial Capital Is A Request Refusal, And An Incomputable Percentage Is Always Null

`InitialCapital` is the denominator of every percentage the analysis publishes. It is a
non-nullable decimal on a query-bound request record, so an OMITTED query parameter binds to `0`:
left unvalidated, "the caller named no capital" and "the caller asked for zero capital" are the
same request. A request whose `InitialCapital` is not strictly positive MUST therefore be refused
with its own status, mapped to **400** — a REQUEST that did not state what it must state, the same
class as an omitted segment, and not a **422** about data that cannot support a figure.

Independently of that refusal, the payload MUST carry ONE convention for a figure it cannot
compute: `null`, NEVER `0`. Any VaR percentage whose denominator is absent MUST be `null`, on the
daily figures and the monthly figure alike, at group level and in the per-service breakdown. A
payload carrying `dailyVar95Percent: null` beside `monthlyVar95Percent: 0` states "zero percent of
capital at risk over a month" where it means "not computable", which is the same failure class as
publishing a `0.00` VaR. This applies to the BACKTEST path only; the real-account path's shipped
behaviour is unchanged.

#### Scenario: An omitted or non-positive initial capital is refused
- GIVEN a group risk analysis request whose `initialCapital` is `0` (the value an omitted query parameter binds to) or negative
- WHEN it is submitted
- THEN it is refused with its own status, answered as **400**, and no correlation or VaR figure is produced

#### Scenario: An incomputable percentage is withheld, never zero
- GIVEN a backtest analysis computed with a non-positive capital base whose monthly VaR95 currency figure IS supported
- WHEN the payload is produced
- THEN `monthlyVar95` carries the currency figure while `monthlyVar95Percent` is `null` — matching the daily percentages beside it, in the group payload and in every per-service entry

### Requirement: Density Metrics Accompany Every Figure

The output MUST report, alongside every correlation and VaR figure: trade count, dense
calendar-day count, non-zero-day share, and — for each correlation coefficient — the count of
co-active days used for that pair.

#### Scenario: Density metrics reported with the IST fixture
- GIVEN the `ListOfTrades_XAUUSD_H1_IST.csv` group (329 trades, 3,860 dense days, 164 negative-net days = 4.25%, 318 non-zero-net days = 8.24%)
- WHEN its analysis is produced
- THEN the reported density metrics show those day-level figures alongside the VaR output

### Requirement: Backtest Correlation Aligns On Pairwise Intersection And Reports Co-Activity

Unlike the real-account path (which MUST remain aligned on the union of trading days,
bit-identical to shipped behavior), the backtest-derived correlation path MUST align each pair on
the intersection of their active trading days. Intersection removes co-absence from the computed
cell rather than merely disclosing it, so no co-absence caveat applies here. Each cell MUST report
`CoActiveDays` and `CoActiveShare`, with no invented minimum-overlap threshold. A cell MUST be
withheld (`decimal?` null) when `CoActiveDays < 2` or either series is constant over the
intersection. The matrix-level `AverageCorrelation` MUST be nullable, computed only over reported
cells, alongside a reported `WithheldCellCount`.

#### Scenario: Too few co-active days withholds the cell
- GIVEN two members whose intersection of active days has fewer than 2 observations
- WHEN their coefficient is computed
- THEN the cell is withheld (null) and counted in `WithheldCellCount`

#### Scenario: A valid intersection reports co-activity, no co-absence caveat
- GIVEN two members with a valid intersection of active days
- WHEN their coefficient is computed
- THEN it is reported with `CoActiveDays` and `CoActiveShare`, and carries no co-absence disclosure

#### Scenario: The live path keeps union alignment, unchanged
- GIVEN the real-account `StrategyTrade` correlation path
- WHEN correlation is computed
- THEN it continues to align on the union of trading days, bit-identical to shipped behavior

### Requirement: The Slice Is Wholly Deterministic

No computation in this capability MAY use a random number generator or a seed. Identical inputs
MUST return byte-identical figures and density metrics. The capability MUST evaluate exactly one
caller-specified group; it MUST NOT iterate over or rank candidate groups.

#### Scenario: Repeated calls are byte-identical
- GIVEN identical inputs supplied twice
- WHEN the analysis runs both times
- THEN the returned figures and density metrics are byte-identical

#### Scenario: One group, no ranking
- GIVEN a caller-specified group of strategies
- WHEN the analysis runs
- THEN it computes figures for that one group only, never comparing it against alternative groupings

### Requirement: Every Rendered Figure Carries A Simulated-Closes Qualifier

In addition to the existing approximation disclaimer and capital-base denominator label
(`funding-guardrails`, `portfolio-monthly-var`), every rendered correlation and VaR figure MUST
carry an explicit "simulated closes" qualifier.

#### Scenario: Qualifier shown alongside existing disclaimers
- GIVEN a backtest group risk analysis rendered in the UI
- WHEN the VaR band position and correlation matrix are shown
- THEN both carry the simulated-closes qualifier alongside the existing disclaimer and denominator label
