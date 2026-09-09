# Demo-Backtest Comparability Specification (Slice A)

## Purpose

Before any cost is decomposed between a strategy's demo trades and its backtest runs, determine
whether the two series describe the same tradable instrument at all. Decomposing costs between two
different price series is arithmetic dressed as signal (measured:
`.agents/knowledge/imox/MEASURED_Demo_vs_Backtest_Divergence.md`). This capability pairs demo and
backtest trades that open at the exact same minute, reports the entry-price offset between the pair,
and states — structurally, not just in a comment — that the readout is a comparability diagnostic,
never a performance score.

This capability gates `demo-backtest-cost-decomposition` (a future slice, out of scope here) and the
`SourcePlatform` column on `BacktestRun` (also a future slice, out of scope here). Both are named
only as follow-ons.

## Requirements

### Requirement: Exact-Minute Pairing Detects Price-Series Comparability, Not Trade Correspondence

For a strategy with both demo trades and backtest trades, the calculator MUST pair a demo trade and
a backtest trade only when their `OpenTime` values are identical to the minute AND their trade
direction (demo `Type` vs. backtest `Type`) matches, case-insensitively, with no tolerance window and
no timezone conversion applied to either side before pairing. Direction is part of the pairing key:
a demo trade and a backtest trade opening in the same minute but with opposing directions MUST NOT
pair. This is a structural requirement, not a currently-observable one — measured today, every row of
both `StrategyTrade.Type` (1,698 rows) and `BacktestTrade.Type` (1,406 rows) is a single buy value
("buy" / "Buy"), so no short trade exists anywhere in the current data to exercise this rule. A future
short strategy would silently reintroduce cross-direction mispairing if direction were dropped from
the key, so this requirement MUST NOT be read as dead code and MUST NOT be removed on the basis that
every observed trade today is a buy. Exact-minute pairing is sound for this measurement because it
needs no tolerance rule at all — the minute matches or it does not — but it MUST NOT be reused to
merge, combine, or otherwise treat the two series as one dataset for any other computation; that use
remains out of scope and unsound, as recorded in `explore.md` (revision "B — Aligning the two
series"). Any value comparison downstream of pairing (mean/min/max offset, sign consistency) MUST be
computed only over the paired subset.

#### Scenario: Trades opening at the exact same minute, same direction, are paired
- GIVEN a demo trade and a backtest trade whose `OpenTime` values are identical to the minute and
  whose `Type` values denote the same direction, compared case-insensitively
- WHEN the comparability calculator runs
- THEN the two trades are paired and their entry-price offset is included in the readout

#### Scenario: Trades opening at different minutes are never paired
- GIVEN a demo trade and a backtest trade whose `OpenTime` values differ, even by one minute
- WHEN the comparability calculator runs
- THEN the two trades are not paired and contribute no offset to the readout

#### Scenario: Same-minute trades with opposing directions are never paired
- GIVEN a demo trade and a backtest trade whose `OpenTime` values are identical to the minute but
  whose `Type` values denote opposing directions (e.g. demo "buy" vs. backtest "Sell")
- WHEN the comparability calculator runs
- THEN the two trades are not paired and contribute no offset to the readout, even though no such
  pair exists in the currently measured data

#### Scenario: No timezone conversion is applied before pairing
- GIVEN a demo trade and a backtest trade whose stored `OpenTime` values are identical to the minute
- WHEN the comparability calculator runs
- THEN pairing succeeds without converting either `OpenTime` to another timezone first

### Requirement: Same-Minute Duplicates On Either Side Are Refused And Counted, Never Resolved

When either side (demo or backtest) has two or more trades opening in the same minute, that minute
MUST be excluded from pairing entirely, and its trades MUST be counted in a dedicated ambiguous-minute
bucket for that side, distinct from the paired, demo-only, and backtest-only buckets. The calculator
MUST NOT resolve the ambiguity by ticket order, row order, or nearest-price matching — set membership,
not ordering, decides which trades are excluded. This is specified defensively: in the currently
measured strategy, this case does not occur (zero same-minute duplicates on either side across the
measured window), but the rule MUST hold structurally for any strategy and window.

#### Scenario: Two demo trades in the same minute are excluded and counted as ambiguous
- GIVEN two demo trades whose `OpenTime` values fall in the same minute
- WHEN the comparability calculator runs
- THEN neither demo trade is paired for that minute, and both are counted in the demo ambiguous-minute
  bucket rather than in `DemoOnlyCount`

#### Scenario: Two backtest trades in the same minute are excluded and counted as ambiguous
- GIVEN two backtest trades whose `OpenTime` values fall in the same minute
- WHEN the comparability calculator runs
- THEN neither backtest trade is paired for that minute, and both are counted in the backtest
  ambiguous-minute bucket rather than in `BacktestOnlyCount`

### Requirement: The Offset Is Reported Per Month, Never As A Single Aggregated Score

For each calendar month with at least one paired trade, the readout MUST report `PairedTradeCount`,
`MeanOffsetPriceUnits`, `MinOffsetPriceUnits`, and `MaxOffsetPriceUnits`, where the offset for a pair
is demo entry price minus backtest entry price, in raw price units — not instrument points, because no
instrument-specification table is read and no tick-size conversion is performed; naming the fields
"points" would assert a conversion the code does not do. The capability MUST NOT compute or
expose any single aggregated figure across months, and MUST NOT expose any 0-100 or otherwise
normalized "quality score" — such a score would read as strategy performance and invite a threshold,
which is explicitly rejected (proposal.md D2).

#### Scenario: Per-month offset matches the measured fixture
- GIVEN the `WF_7_30_NQ_H_CW_H_O_H1_2.34.172` fixture with 24 exact-minute-paired trades across
  2026-04, 2026-05 and 2026-06
- WHEN the comparability readout is produced
- THEN it reports 2026-04: n=5, mean +22.06, range 16.2-25.5; 2026-05: n=14, mean +22.55, range
  11.4-31.9; 2026-06: n=5, mean +40.90, range 38.5-44.3

#### Scenario: No aggregated score is ever exposed
- GIVEN the comparability readout's DTO
- WHEN it is inspected
- THEN it exposes no single cross-month aggregate figure and no 0-100 or normalized score field

### Requirement: Sign Consistency Is Reported, And No Directional Performance Claim Is Made

The readout MUST report `SignConsistency` as the share of paired offsets sharing the same sign as
the majority. The capability MUST NOT state, imply, or expose any field asserting that the demo
series outperforms the backtest series, or vice versa — a paired-trade sample of the size this
capability operates on cannot establish direction (measured: 24 paired trades, all positive, KB
states this explicitly cannot show demo outperforms backtest).

#### Scenario: Sign consistency computed on the measured fixture
- GIVEN the 24 paired trades from `WF_7_30_NQ_H_CW_H_O_H1_2.34.172`, all with a positive offset
- WHEN the readout is produced
- THEN `SignConsistency` is reported as 100%, with no field or label asserting that demo outperformed
  the backtest

#### Scenario: No directional performance claim exists anywhere in the output
- GIVEN the comparability readout's DTO and any rendering of it
- WHEN it is inspected
- THEN no field, label, or computed value asserts that one series outperforms the other

### Requirement: `ComparabilityBasis` Is A Non-Nullable, Non-Droppable Disclosure

The DTO MUST carry a non-nullable `ComparabilityBasis` computed property mirroring the structural
precedent of `BreachBasis` (`Domain/Enums/BreachBasis.cs`): a caveat encoded as a type rather than a
comment, so it cannot be dropped at a call site. `ComparabilityBasis` MUST disclose that the offset
is measured on the exact-minute-paired subset only, and MUST be present and non-null on every
readout, including one with zero paired trades.

#### Scenario: ComparabilityBasis is present on every readout
- GIVEN any comparability readout, including one for a strategy pair with zero paired trades
- WHEN the readout is inspected
- THEN `ComparabilityBasis` is non-null

#### Scenario: Non-nullability is compile-enforced and test-pinned
- GIVEN the DTO's `ComparabilityBasis` property declaration
- WHEN it is inspected by a reflection test mirroring the pattern used for `BacktestNetSeries`
- THEN the property's type is non-nullable, so a caller cannot construct a readout without it

### Requirement: The Offset Readout Is Explicitly Transitional, Not A Permanent Verdict

The DTO and its documentation MUST state that the offset readout reflects the current data-symbol
binding and is expected to change once the underlying price series is rebound to a
comparably-sourced instrument (`explore.md`, root cause: `USATECHIDXUSD_M1_UTC02` bound to
`NDX_DARWINEX`). The readout MUST NOT be framed as a stable or permanent property of the strategy.

#### Scenario: Transitional framing is present in the DTO documentation
- GIVEN the comparability DTO's public documentation (XML doc comments)
- WHEN it is inspected
- THEN it states that the offset reflects the current price-data binding and may no longer apply
  after the data symbol is rebound, rather than describing the offset as a permanent trait

### Requirement: The Trade-Set Difference Is Reported As A Set Relationship, Never As A Score

Alongside the offset, the readout MUST report `PairedCount`, `DemoOnlyCount`, and
`BacktestOnlyCount` as a disjoint partition of the demo and backtest trade sets for the strategy and
window, with each subset's net P/L reported separately. The capability MUST NOT fold any of these
counts or their P/L into the offset computation or into any other single residual figure.

#### Scenario: Trade-set counts match the measured fixture
- GIVEN the measured window for `WF_7_30_NQ_H_CW_H_O_H1_2.34.172` (47 demo trades, 42 backtest
  Deploy trades in the window, 24 of which pair exactly)
- WHEN the trade-set breakdown is produced
- THEN `PairedCount` reflects the paired trades and `DemoOnlyCount`/`BacktestOnlyCount` reflect the
  remaining unpaired trades on each side, each with its own separately reported net P/L

#### Scenario: Set counts are never merged into a single residual number
- GIVEN a produced trade-set breakdown
- WHEN it is inspected
- THEN `PairedCount`, `DemoOnlyCount`, `BacktestOnlyCount` and their P/L values are exposed as
  distinct fields, and no single combined number replaces or summarizes them

### Requirement: Ranking Across Strategies Orders By Absolute Mean Offset, And Never Grades

When comparability readouts for more than one strategy are ranked together, the ranking MUST use
`|MeanOffsetPriceUnits|` (per applicable month, or however the caller scopes the ranking) purely as an
ordering key. The capability MUST NOT attach a pass/fail label, grade, or any threshold-based
classification to any strategy's position in the ranking.

#### Scenario: Ranking orders by absolute mean offset only
- GIVEN comparability readouts for two strategies with differing `MeanOffsetPriceUnits`
- WHEN they are ranked together
- THEN they are ordered by `|MeanOffsetPriceUnits|` and neither carries a pass/fail label or grade

#### Scenario: No threshold is applied anywhere in ranking or reporting
- GIVEN any comparability readout or ranked list of readouts
- WHEN it is inspected
- THEN no code-constant threshold, cutoff, or classification boundary is applied; any future
  threshold would have to be supplied as external configuration, not found here

### Requirement: Figures Publish At Any Paired Count Above Zero; Nulls Mean Undefined, Not Withheld

The capability MUST NOT invent a minimum paired-trade count below which figures are withheld. A
month's `MeanOffsetPriceUnits`, `MinOffsetPriceUnits`, `MaxOffsetPriceUnits`, and `SignConsistency`
MUST be published for any `PairedTradeCount >= 1`. Unlike `AnalyticsSeries.MinHistoryDays` or the
`portfolio-monthly-var` density gate — which encode a recorded user decision — no such decision exists
here, and a paired-trade sample of n = 1 offers no basis to calibrate one; inventing a minimum would
be exactly the fabricated domain assumption `INDEX.md` §5 forbids. `PairedTradeCount` MUST always
travel beside every published figure, so a mean computed over a small sample is visibly a mean over a
small sample rather than being presented as equivalent to one computed over a large sample.
`SignConsistency` is the discriminator that makes an invented minimum unnecessary: a small
`PairedTradeCount` with a `SignConsistency` far from 1.0 visibly signals nothing, while a high
`PairedTradeCount` with `SignConsistency` near 1.0 visibly signals agreement — both are arithmetic
over the observed data, not a domain threshold. A field MUST be reported as `null` only where the
underlying arithmetic is itself undefined — a range or mean computed over an empty set — which occurs
only when `PairedTradeCount == 0` for that scope.

#### Scenario: Zero paired trades reports undefined figures, not withheld ones
- GIVEN a strategy and window with zero exact-minute-paired trades for a given month
- WHEN the comparability readout is produced
- THEN `MeanOffsetPriceUnits`, `MinOffsetPriceUnits`, `MaxOffsetPriceUnits`, and `SignConsistency` are
  `null` for that month because the arithmetic is undefined over an empty set, and `PairedTradeCount`
  is reported as `0`

#### Scenario: A single paired trade still publishes its figure beside its count
- GIVEN a strategy and month with exactly one exact-minute-paired trade
- WHEN the comparability readout is produced
- THEN `MeanOffsetPriceUnits` (equal to that one pair's offset), `MinOffsetPriceUnits`,
  `MaxOffsetPriceUnits`, and `SignConsistency` are all published for that month, alongside
  `PairedTradeCount = 1`, with no invented minimum causing them to be withheld

### Requirement: The Calculator Is Deterministic

No computation in this capability MAY use a random number generator or a seed. Identical inputs
(the same demo trades and backtest trades) MUST produce byte-identical readouts across repeated
runs, matching the standard already required of `backtest-portfolio-analytics`
(`openspec/specs/backtest-portfolio-analytics/spec.md`, "The Slice Is Wholly Deterministic").

#### Scenario: Repeated calls are byte-identical
- GIVEN the same set of demo trades and backtest trades supplied twice
- WHEN the comparability calculator runs both times
- THEN the returned readout is byte-identical between the two runs

## Non-Goals (recorded, not designed for)

- **Cost decomposition** (swap, embedded backtest cost, execution residual) is `demo-backtest-cost-
  decomposition`, a separate future slice gated by this one. Not specified here.
- **`PlatformType? SourcePlatform` on `BacktestRun`** is a separate future slice (`sqx-backtest-
  import` modification). Not specified here.
- **Frontend readout.** This slice is backend-only; no Angular component, service, or i18n key is
  specified here.
- **Merging or combining the demo and backtest series for any purpose other than this pairwise
  offset measurement.** Exact-minute pairing is sound only for measuring the offset; using it to
  align or blend the two series for any other computation is unsound and out of scope (see the
  pairing requirement above).
- **The intrabar SL/TP order assumption.** The backtest runs at "1 minute data tick" precision (M1
  bars, not real ticks), so when a stop and a target are both reachable inside one bar, the order
  they are touched is an assumption, not an observation
  (`.agents/knowledge/imox/MEASURED_Demo_vs_Backtest_Divergence.md` §4). This is an independent
  modelling gap that produces the same TP-vs-SL symptom the offset check surfaces, and it would
  persist even after the price series is fixed. It is recorded here so a later reader does not
  conclude it was overlooked; it is not addressed by this capability.
