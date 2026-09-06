# Proposal: Backtest Portfolio Risk Analysis

> Slice 2b of the strategy portfolio simulator. Named for what it does rather than its ordinal:
> it runs **already-shipped** portfolio risk analytics over **backtest-derived** series. It adds no
> new statistical estimator at all — the plumbing and the disclosure are the work.

## Intent

The simulator's purpose is to answer "how risky is this group of strategies?" from **backtest**
evidence, because live demo data is too thin to select on (117 strategies averaging 13 trades
against the academy's `Min # Trades > 200` — parent proposal, Intent).

The naive framing of this slice was "build correlation, portfolio VaR and a Darwinex Zero VaR
check". **That work already shipped**, in `194515f` / change `typed-funding-guardrails` (archived
`2026-09-03`):

| Already shipped | Where |
|---|---|
| Pearson correlation matrix over member daily NET series, aligned on the union of trading days | `PortfolioAnalyticsCalculator.ComputeCorrelation` (`:349-398`), `PortfolioCorrelationDto` |
| Portfolio VaR95/VaR99, Historical method, over the rolling daily NET-P/L series | `PortfolioAnalyticsCalculator.ComputeVaR` (`:260-312`), `PortfolioRiskDto` |
| Per-service standalone VaR contribution | `ServiceRiskDto`, computed unconditionally per broker (`:273-296`) |
| 30-calendar-day rolling monthly VaR95, no √t scaling | `ComputeMonthlyVar` (`:460-475`), `openspec/specs/portfolio-monthly-var/spec.md` |
| Darwinex Zero's band as configuration | `BrokerRiskLimits.TargetVarPct` / `VarFloorPct`, validated in `(0,1]` with floor ≤ target (`openspec/specs/funding-guardrails/spec.md`) |
| The band's values: target VaR max **6.5%**, operating range **3.25%–6.5%**, monthly horizon, 95% confidence | `.agents/knowledge/imox/Darwinex_Zero_Risk_Model.md` §2 |
| No-breach / no-headroom semantics for `VarTarget` (missing the target rescales leverage, KB §1) | `funding-guardrails` spec, `VarTargetReadoutDto` |

Every one of those consumes `PortfolioMemberInput.Trades`, hard-typed to
`IReadOnlyList<StrategyTrade>` (`PortfolioAnalyticsCalculator.cs:8-13`) — the **real/demo-account**
pipeline. The simulator has `ResizedTradeSeries`, which by design cannot bind to it (D9).

**So the actual problem this slice solves is a plumbing problem, not a statistics problem: get a
backtest-derived, already-sized net series into analytics that already exist, without duplicating
them and without breaking the guarantee that keeps the two pipelines apart.** Success is that an
operator can name a group of strategies, a risk-per-trade target and a sample segment, and read
back its correlation and its Darwinex Zero VaR band position — with **every number traceable to
already-shipped math**, and with every number the backtest data cannot support withheld rather than
rendered.

## Falsification Check — run before proposing, and it changed the proposal

**The check.** *If the shipped daily-VaR path is reused unchanged on a backtest run, does it return
a usable number?* `ComputeVaR` takes the 5th percentile of the **calendar-dense** daily net series
(`AnalyticsSeries.BuildDailyNetSeries` zero-fills every non-trading day; `WindowedDailyNets`
`:424-437`; `Percentile` `:478-488`). A backtest spanning a decade at ~30 trades a year is
overwhelmingly zeros. If fewer than 5% of the dense days are negative, the 5th percentile lands on
a zero-filled day and `VaR95` is exactly **0.00** — which would refute "reuse `ComputeVaR` as-is"
as the plan for the daily figure.

**Inputs, recorded.** `app.trading.algoritmico.api/tests/Fixtures/ListOfTrades_XAUUSD_H1_IST.csv`:
330 lines = 1 header + **329** trades (matches slice 1's committed count). Rows with a negative
`Profit/Loss` (field 9, regex `^("[^"]*";){8}"-`): **173**. Rows with an empty `Close time`
(field 7): **0** — close time is total, so the dense series is well-defined. First close
`2016.01.04`, last close `2026.07.29` (line 330).

**The arithmetic.** `2016-01-04 → 2026-07-29` = 3,859 days elapsed → **3,860** dense calendar
elements. `Percentile(sorted, 0.05)` computes `rank = 0.05 × (3860 − 1) = 192.95`, so it reads
`sorted[192]` and `sorted[193]`. At most 173 elements are negative (day collisions can only reduce
that), and 173 < 192, so both indices sit in the zero-filled block.

**Result: `VaR95 = 0.00`, `VaR99 = 0.00`.** Negative-day share is **≤ 4.48%** (173/3,860), below
the 5% the percentile needs. The `_OOST` fixture reproduces it: 337 trades, **186** negative, 0
empty close times, `2016.01.04 → 2026.06.03` = 3,804 dense elements, `rank = 190.15`, and
186 < 190. Both fixtures return a hard zero. The bound is one-sided and therefore robust — no
assumption about how trades distribute across days can rescue it.

**What it changed.** Two things. (1) The daily VaR95 is **not** reusable on backtest runs and this
slice must not present it as if it were; the Darwinex-relevant figure is the **30-calendar-day
rolling-window** monthly VaR, which does not have this failure mode (~329 trades over 3,860 days is
~2.6 trades per 30-day window, so the window sums are mostly non-zero). (2) The same density
defect biases **correlation** toward 0, because `ComputeCorrelation` aligns on the union of trading
days and scores a no-trade day as `0` (`:365-378`) — two strategies that never trade on the same
day get a near-zero correlation that reads as diversification and is really absence of data. Both
now appear below as requirements rather than as things a reader has to notice.

This is the same defect the `portfolio-monthly-var` spec already records as known and out of scope
on the real-account side (spec note, `PortfolioAnalyticsCalculator.cs:394-407`). It is **not** out
of scope here: on backtest data it is not a bias, it is a zero.

## Scope

### In Scope

- **A dated-net bridge** from `ResizedTradeSeries` back to a dated series the shipped primitives
  accept, built at the call site by zipping the already-in-hand `IReadOnlyList<BacktestTrade>`
  with the resized rows on `RowIndex` (decision 1 below). Placement is **forced**, not chosen:
  `AnalyticsSeries` is `internal static` in Infrastructure, so any consumer of its primitives must
  live in `Infrastructure/Services` (D10's precedent becomes a hard constraint here).
- **Generalising the two shipped entry points** to a member-agnostic dated projection.
  `ComputeCorrelation` and `ComputeVaR` gain an overload over `(label, broker, dated nets)`; the
  existing `PortfolioMemberInput` signatures become thin adapters that project
  `w * AnalyticsSeries.NetOf(t)`. **Shipped numbers must be bit-identical** — that is a regression
  assertion, not a hope. This answers 2a's deferred open question ("generalise
  `PortfolioMemberInput.Trades` to a projection, or map at the boundary?") with **both**: generalise
  the entry point, map at the boundary.
- **Refusing a non-unit weight** — the D9 obligation, discharged here (requirement below).
- **Segment selection as a required input**, wired through `OosWindow.Filter` upstream of the
  normalizer (see *The OOS wiring*).
- **Density disclosure**: trade count, dense-day count, non-zero-day share and correlation
  observation days reported alongside every figure, and the daily VaR95 withheld rather than shown
  as 0 when the density gate fails.
- One read endpoint plus a UI panel for a **caller-specified** group.

### Out of Scope

- **Breach probability, in any form — deferred to a future FTMO slice.** Not an oversight, and the
  reason is structural rather than a matter of effort. `Darwinex_Zero_Risk_Model.md` **§1** states
  that Darwinex Zero has **no maximum daily loss, no maximum total loss and no profit target**; its
  single constraint is a target VaR, and the platform **rescales rather than terminates**. The KB
  goes further and says outright that framing Darwinex Zero as "headroom before you blow the
  account" is *modelling the wrong thing*. Darwinex demo is the agreed baseline, so **on the
  baseline account there is structurally nothing to breach.** A breach estimator is a
  `GuardrailKind.LossLimits` concept and belongs with the FTMO rulebook — which is **not in this
  repository**. Building it now would mean coding an estimator against thresholds whose source
  cannot be cited, and this project has already been bitten three times by figures whose sources
  could not be reproduced (parent proposal's `Profit`-derived point value; 2a's assumed round-half
  rounding; and the daily VaR of 0.00 measured below). It returns when the FTMO rulebook does.
- **The group selector (Slice 3), in full.** See decision 2 for where the line falls.
- **The shuffled-returns random benchmark and the anti-overfitting apparatus** (parent proposal,
  Out of Scope). With breach probability gone this slice contains **no stochastic method at all**
  — see decision 2, where that becomes the boundary rule rather than a coincidence.
- **Portfolio-level walk-forward.** This slice selects one segment; it does not select on one
  window and hold on the next.
- **Cross-account / cross-broker pooled analytics.** Darwinex demo is the baseline (see
  *Multi-account reality*).
- **AXI Select.** No rulebook exists (parent proposal). `Darwinex_Zero_Risk_Model.md`'s scope
  warning independently forbids reusing its VaR concept for another service.
- **Persisting simulated portfolios.** No new entity, no migration. `Portfolio` is scoped to one
  broker + one account type and is not a home for a backtest group; inventing one is a later
  decision.
- **Any change to `StrategyTrade`, `PortfolioStrategy.Weight`, or the live-trade pipeline.**
- **Fixing the real-account calendar-dense daily-VaR bias.** Separately tracked
  (`portfolio-monthly-var` spec note). This slice must not silently change a shipped number.

### What This Slice Explicitly Cannot Claim

- **Not that it recommends a group.** It measures the group it is given.
- **Not that its VaR is Darwinex's VaR.** Realized close-to-close from a backtest against
  forward-looking open-position risk over a 45-day window (KB §5, traps 2 and 3). The existing
  disclaimer requirement applies unchanged, plus one more: these are **simulated** closes.
- **Not that any figure here is a forecast.** Every number is a descriptive statistic of one fixed
  set of simulated closes. It inherits every error in `Â` (2a: "not that R is stable") and every
  unmodelled cost (slippage, spread, swap, commission, real broker minimums).
- **Not that correlation over backtest days measures co-movement.** With ≤4.5% non-zero days
  measured above, it substantially measures co-*absence*. Disclosed, not hidden.
- **Not that the 3.25%–6.5% band applies to a backtest capital base.** KB §5 trap 3; the
  denominator label requirement already exists and is inherited.

## Decision 1 — The timestamp blocker

**The fact.** `NormalizedTrade` and `ResizedTrade` carry no timestamp — verified by reading both
files end to end: `NormalizedTrade(TradeId, RowIndex, Ticket, CloseType, Size, Profit, Basis, Risk,
RLow, RHigh)`, `ResizedTrade(RowIndex, Ticket, OriginalSize, ResizedSize, AchievedRisk, Outcome,
Basis)`. Correlation and every VaR path need a clock. This is **not** an oversight: D9 impoverished
`ResizedTrade` deliberately so `AnalyticsSeries.NetOf(StrategyTrade)` could not bind to it, and the
timestamps went out with the cost fields.

**Option A — put timestamps back on `ResizedTrade`.**
How much of D9 survives, precisely: D9 fact (1) is that `ResizedTradeSeries` is not
`IReadOnlyList<StrategyTrade>` and `PortfolioMemberInput.Trades` is hard-typed — a `DateTime`
member does not touch that. D9 fact (2) is that `ResizedTrade` has no `Commission`/`Swap`/`Taxes`
and no `BaseEntity`, so `NetOf` cannot bind — a `DateTime` member does not touch that either. **So
the structural guarantee survives intact; both facts are about type identity and the absence of the
three cost fields, not about dates.** What it does cost is defence in depth: today the only route to
a date is an explicit, auditable join, and the permanently-rejected `ToStrategyTrades()` synthesis
would need to invent one. With a timestamp on the row, the cost fields become the *only* remaining
barrier. That is a narrowing of the moat, not a breach of it — and it is a real cost, because D9's
strength was that it had two independent structural facts and a stated convention.

**Option B — join back to `BacktestTrade`.** Keeps the type poor. The join key exists and is sound:
slice 1 created a unique `(BacktestRunId, RowIndex)` index, and `ResizedTrade.RowIndex` is "0-based
ordinal within the source file, carried through unchanged". Costs: a second database round trip on
a path whose caller **already had the trades in memory** (it passed them to
`TradeRiskNormalizer.TryNormalize`), and a mismatch risk if the resizer's ordering assumption ever
drifts from the DB read's ordering.

**Option C — RECOMMENDED. Date the series at the boundary, not the trade.** The service that calls
`TryNormalize` and `Resize` is holding both the `IReadOnlyList<BacktestTrade>` and the resulting
`ResizedTradeSeries`. It zips them positionally into a new, purpose-built dated projection, guarded
by an explicit invariant: counts must be equal and `resized[i].RowIndex` must equal
`source[i].RowIndex`, or the whole conversion is refused. `ResizedTradeSeries` documents "one row
per trade of the source profile, **in the same order**", so the zip is checking a documented
contract rather than trusting an implicit one.

**Why C.** It is strictly better than B — same "keep the type poor" property, no round trip, and
the mismatch risk B *accepts* is closed by a checked invariant. It is better than A because D9's
defence in depth stays whole, and A's benefit (self-sufficient rows) is a benefit only to the
consumer D9 exists to prevent. The cost of C is honest and worth naming: the dated projection it
produces **is** bindable to the analytics primitives, so it becomes the first type in the simulator
that can be weighted. That is exactly why the D9 requirement below attaches to the bridge, and it
is the right home for it — the obligation lands on the one place capable of violating it, rather
than being spread as a caution over a type that cannot.

## Decision 2 — The boundary against Slice 3

No document partitions them, so this proposal draws the line. The parent proposal's Slice 3 is
"the group selector and its anti-overfitting apparatus (structural constraints, shuffled-returns
random benchmark, portfolio-level walk-forward)".

**The line: this slice answers "how risky is *this* group?". Slice 3 answers "*which* group?".**

| Question | Slice |
|---|---|
| Given a named group, a risk target and a segment: correlation and Darwinex Zero VaR band position | **this slice** |
| Which strategies, and how many, form the recommended group | Slice 3 |
| Is this group's performance distinguishable from a random group's (shuffled-returns benchmark) | Slice 3 |
| Structural constraints (max per symbol, per timeframe, per direction) | Slice 3 |
| Select on window *n*, hold on window *n+1* | Slice 3 |
| Ranking or scoring strategies against each other | Slice 3 |

**Two mechanical tripwires, both checkable by grep rather than by judgement.** An earlier draft of
this proposal drew the line on a subtlety — two kinds of resampling, one estimating a statistic and
one building a null — which asked a reader to accept a distinction on faith. Removing breach
probability makes that argument unnecessary and replaces it with something stronger:

1. **This slice is entirely deterministic.** It contains no stochastic method whatsoever: every
   figure is a descriptive statistic over one fixed set of trades, and the same inputs return the
   same numbers with no seed involved. Randomness is Slice 3's, and it arrives with the
   shuffled-returns null. **If this slice ever needs a random number generator or a seed, it has
   crossed into Slice 3.**
2. **This slice evaluates exactly one group — the one it was given.** Slice 3 evaluates many
   candidate groups in order to choose between them. **If this slice ever iterates over candidate
   groups, it has crossed into Slice 3.**

The second tripwire is the one that matters for scope creep, because a "just rank these three
groups" request looks small and is the whole of Slice 3's problem. The first is the one that
matters for honesty: a deterministic slice cannot accidentally present a simulated distribution as
evidence.

Deliberately NOT in this slice, restated so the boundary is not re-litigated in design: no ranking,
no scoring, no group search, no recommendation, no group-size decision, no random benchmark, no
structural constraint engine, no portfolio-level walk-forward.

## Carrying D9's obligation forward

`openspec/specs/trade-risk-normalization/spec.md` ("Already-Sized Output Refuses A Non-Unit
Weight") records the companion obligation as belonging to **this slice**, "because the guarantee
decays if it does not survive into that slice's spec". This slice introduces the first such
consumer. Wording, to be promoted into the delta spec verbatim:

> ### Requirement: The Bridge Refuses A Non-Unit Weight
>
> The bridge that converts a `ResizedTradeSeries` into a dated net series for portfolio analytics
> is the first consumer of an already-sized series. It MUST refuse the conversion when the member
> carries a `PortfolioStrategy.Weight != 1`, and MUST NOT multiply that weight into the
> already-sized nets. The refusal MUST identify the member and the offending weight. It MUST NOT be
> a silent skip, a weight coerced to 1, or a flag attached to a series that is nonetheless returned
> and aggregable — for the same reason a refused run yields no per-trade output at all rather than a
> list of `Unavailable` rows.
>
> The refusal is unconditional on the value: `1.5` double-sizes and `0.5` half-sizes, and both are
> the same error, because the series' `TargetRiskPerTrade` is the sizing decision and there is no
> second one to make. Excluding a strategy from a group means not passing its series.
>
> #### Scenario: Non-unit weight is refused, not applied
> - GIVEN a `ResizedTradeSeries` for a member whose `Weight` is `1.5`
> - WHEN the bridge is asked to convert it
> - THEN the conversion is refused, naming the member and `1.5`; no dated series is produced and
>   `Weight` is never multiplied into an already-sized net
>
> #### Scenario: Unit weight converts
> - GIVEN a `ResizedTradeSeries` for a member whose `Weight` is exactly `1`
> - WHEN the bridge is asked to convert it
> - THEN the dated series is produced and every net equals the resized trade's own net, unscaled
>
> #### Scenario: A zero weight is an error, not an exclusion
> - GIVEN a `ResizedTradeSeries` for a member whose `Weight` is `0`
> - WHEN the bridge is asked to convert it
> - THEN the conversion is refused; a member is excluded by not being passed, never by a weight

## Approach

Reuse first, and be explicit about the split.

**Reused unchanged (no new math):** `AnalyticsSeries.BuildDailyNetSeries`,
`AnalyticsSeries.RollingWindowSums`,
`PortfolioAnalyticsCalculator.Pearson`, `Percentile`, `ComputeMonthlyVar`, `MonthlyVarHorizonDays`
(30), `MinHistoryDays` (90), `BrokerRiskLimits.TargetVarPct`/`VarFloorPct`,
`VarTargetReadoutDto`, `PortfolioCorrelationDto`, and every disclaimer/denominator-label
requirement in `funding-guardrails` and `portfolio-monthly-var`.

**Refactored, with a bit-identical regression assertion:** `ComputeCorrelation` and `ComputeVaR`
gain a dated-projection overload; the `PortfolioMemberInput` signatures become adapters.

**Genuinely new:** the dated bridge and its `RowIndex` invariant; the weight refusal; the density
metrics and the daily-VaR withholding gate; segment selection through `OosWindow.Filter`; one read
endpoint and one UI panel. **No new statistical estimator** — which is why the slice's whole risk
profile is a plumbing-and-disclosure profile, not a modelling one.

### What the removal of breach probability leaves behind

Two pieces of the removed reasoning survive on their own merits and are recorded here so design
does not have to rediscover them when the FTMO slice arrives:

- **The serial-dependence evidence is already in the codebase, and it is measured, not assumed.**
  `AnalyticsSeries.ComputeZScore` (`:207-238`) exists specifically to quantify how far a strategy's
  win/loss runs depart from independence, and `ComputeStreaks` (`:159-196`) returns the observed
  max consecutive-loss run. Whenever a breach estimator is built, that is the evidence that an
  i.i.d. resample would be wrong — breach is a **path** property crossed by a *run* of losses, not
  by an average, so destroying the serial dependence systematically understates it.
- **The density measurement constrains any future resampler too.** Whatever that slice does, it
  must draw on the **trade sequence** and not the calendar-dense daily series: resampling a series
  measured above at ≥95.5% zeros would mostly resample nothing. This is the same finding that
  forces the daily-VaR withholding gate here, and it generalises past this slice.

Neither is scope. Both are why the deferral is a deferral rather than a gap.

### Multi-account reality — what correlating across accounts collides with

Two demo accounts (Darwinex Zero and FTMO), potentially different symbols and different money
management. Darwinex demo stays the baseline. Pooling them collides with four things:

1. **No home for the group.** `Portfolio` is scoped to one broker + one account type
   (comment-enforced; no DB constraint found), and no equivalent grouping entity exists for
   backtests. A cross-account group has nowhere to live and this slice adds nothing.
2. **No single capital base.** `funding-guardrails` requires the VaR percentage to be labelled with
   the portfolio's configured initial capital, and KB §5 trap 3 says a mismatched denominator makes
   the number incomparable to the 3.25%–6.5% band. A pooled two-account stream has two capital
   bases, so the pooled VaR% is comparable to nothing. The shipped code already answers this
   correctly by computing VaR **per service** (`:273-296`); this slice keeps that.
3. **Different money management breaks the risk figures but not the correlation.** Pearson is
   scale-invariant, so a correlation between two differently-sized accounts' daily nets survives.
   VaR and monthly VaR do not — they are absolute-currency statistics and
   mixing two sizing regimes makes them uninterpretable. (This slice's resizing to a common
   risk-per-trade target is what would eventually make a pooled figure meaningful; that is a
   later decision, not a claim here.)
4. **Different symbol sets amplify the density defect.** `ComputeCorrelation` aligns on the *union*
   of trading days, so two accounts trading different instruments produce a union dominated by days
   on which one of them did nothing — and each of those scores as a `0` net. The measured ≤4.5%
   non-zero-day share is the same defect; across accounts it gets worse, and it biases the answer
   toward "diversified".

Consequence for the slice: correlation and every risk figure are computed **within one funding
service's group**. The cross-service pooled number is out of scope, and the reason is recorded
above so a later slice does not rediscover it.

### The OOS wiring

Slice 1 built `OosWindow` with a private constructor reachable only from its nested `Resolver`, so
holding one is proof the run is an `Evaluation` run whose strategy has a walk-forward export. Only
`Resolver.ReadinessRows` is wired (`StrategyService.cs:98-99`); `TryGetOosWindow`, `Includes` and
`Filter` have **zero** production callers.

**What wiring means here, concretely.** `OosWindow.Includes` takes a `BacktestTrade` and compares
`trade.CloseTime >= FromInclusive`. After normalization and resizing the date is gone (decision 1),
so the filter **must** sit upstream of `TradeRiskNormalizer` — the pipeline becomes
`BacktestTrade[] → OosWindow.Filter → TryNormalize → Resize → bridge → analytics`. It must not
grow a second date comparison anywhere: `OosWindow`'s own stated guarantee is that a repository grep
for `CloseTime >=` finds nothing outside that one file, and that convention is this slice's to keep.
Note the ordering consequence for decision 1's invariant: filtering upstream means the resized
series' `RowIndex` values are a *subset* of the run's, so the zip invariant must be stated as
element-wise `RowIndex` equality over the filtered list, not as "`RowIndex == i`".

**Does this slice need it? Yes — minimally.** Without a segment choice every figure is computed on
in-sample data, which is the number most likely to be optimistic, and this slice's entire output is
risk figures. The minimal version: the segment is a **required** input (full-sample or
out-of-sample), the slice refuses to produce figures without one, and out-of-sample goes through
`OosWindow.Filter` and nothing else. A run with no window yields the explicit "no out-of-sample
evidence" state the type was built to force, not an empty result set. Portfolio-level walk-forward
stays in Slice 3.

## Capabilities

### New Capabilities

- `backtest-portfolio-analytics`: bridge already-sized backtest series into the shipped portfolio
  analytics primitives, per funding service, with density disclosure — and refuse a non-unit
  weight.

### Modified Capabilities

- `portfolio-monthly-var`: scope extended from the real-account daily-net series to a
  backtest-derived one; adds the density gate and the daily-VaR withholding rule the falsification
  check forced.
- `trade-risk-normalization`: discharges the deferred non-unit-weight obligation (the requirement
  above), moving it from "recorded for a later slice" to "asserted by tests".

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `Infrastructure/Services/BacktestSeriesBridge.cs` (name TBD in design) | New | `ResizedTradeSeries` + source `BacktestTrade[]` → dated net projection; `RowIndex` invariant; weight refusal. Placement forced by `AnalyticsSeries` being `internal` to Infrastructure |
| `Infrastructure/Services/PortfolioAnalyticsCalculator.cs` | Modified | Dated-projection overloads of `ComputeCorrelation` / `ComputeVaR`; existing signatures become adapters. **Shipped output bit-identical** |
| `Application/DTOs/Backtests/` | New | Dated projection, density metrics |
| `Application/DTOs/Portfolios/PortfolioAnalyticsDto.cs` | Modified | Density fields; reuse `PortfolioCorrelationDto` / `VarTargetReadoutDto` unchanged |
| `Application/Interfaces/` + read service | Modified | Group risk-analysis query (group + target + grid + segment + service) |
| `WebAPI/Controllers/BacktestsController.cs` | Modified | One read endpoint |
| `web/features/sqx/...` + `assets/i18n/{en,es}.json` | Modified | Group risk panel; disclaimers, denominator label, density, withheld-VaR state |
| Migrations · `StrategyTrade` · `PortfolioStrategy.Weight` · `Portfolio` · `BrokerRiskLimits` | **Untouched** | Nothing persisted, no schema change |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| A daily VaR of 0.00 is displayed as a real number | **Measured, certain if unguarded** | Density gate; the figure is withheld with an explicit state, never rendered as 0 |
| Correlation near 0 is read as diversification when it is absence of overlapping trading days | **High** | Report observation days and non-zero-day share adjacent to every coefficient; design must decide whether a minimum-overlap gate refuses the matrix outright |
| Refactoring `ComputeCorrelation`/`ComputeVaR` regresses shipped portfolio numbers | Med | Bit-identical regression assertions on the existing 365/371 suites before the overload is used |
| The weight refusal is written as a warning and D9 decays silently | **High if not specced** | The requirement above, with three scenarios, promoted verbatim into the delta spec |
| The breach-probability deferral is later read as an oversight and rebuilt against uncited FTMO thresholds | Med | Out of Scope carries the KB §1 reason, not just the exclusion; the reasoning that survives is recorded in *What the removal of breach probability leaves behind* |
| A figure is read as a forecast | Med | The slice is wholly deterministic and says so; every rendering carries the simulated-closes qualifier |
| A second `CloseTime >=` comparison appears outside `OosWindow` | Med | The OOS path goes through `Filter` only; a grep assertion, as slice 1 specified |
| The zip invariant silently passes on a filtered list | Med | Element-wise `RowIndex` equality, not positional index equality; a RED test with an OOS-filtered run |
| Slice creep into the Slice 3 selector | **High** — the boundary is not documented elsewhere | Decision 2's table plus its two grep-checkable tripwires: no seed, no iteration over candidate groups |
| Line budget | Med — one PR less than before | Chained PRs: (1) calculator generalisation + regression assertions; (2) bridge + weight refusal + density; (3) endpoint + UI + i18n |

## Rollback Plan

1. Revert the frontend and backend commits.
2. **No migration to reverse.** Nothing is persisted; no entity, column, index or existing service
   contract changes.
3. The only touched shipped file is `PortfolioAnalyticsCalculator`, and only additively (new
   overloads plus adapter bodies), with bit-identical output asserted — so a revert restores it
   exactly and no shipped number ever moved.
4. Frontend-only rollback is safe: the new endpoint simply goes unused.

## Dependencies

- Committed fixtures `ListOfTrades_XAUUSD_H1_{IST,OOST}.csv` — the TDD substrate, and the source of
  the falsification measurement above.
- Shipped and archived: `trade-risk-normalization` (slice 2a) and `typed-funding-guardrails`.
- `.agents/knowledge/imox/Darwinex_Zero_Risk_Model.md` as the only rulebook in scope — **§1** (no
  daily loss, no max loss, no profit target; rescaling not termination), **§2** (the band) and
  **§3** (the multiplier and D-Leverage caps). Its scope warning forbids extending its VaR concept
  to another service.
- **No FTMO rulebook exists in this repository**, which is why breach probability is deferred
  rather than scoped. That slice depends on the rulebook arriving first.
- An answer to open question 2 below before `sdd-design`.

## Success Criteria

- [ ] The falsification measurement is a committed RED-first test: the `_IST` fixture's dense daily
      series is 3,860 elements with ≤173 negative days, and the shipped daily-VaR path returns
      exactly `0.00` on it — asserted, so the defect can never be reintroduced as a feature.
- [ ] The same run's daily VaR95 is **withheld** with an explicit density state, not rendered as 0.
- [ ] The 30-calendar-day monthly VaR95 for the same run produces a non-zero estimate and clears
      the 90-day `MinHistoryDays` gate.
- [ ] Existing portfolio and live-trade suites are green and every shipped analytics number is
      **bit-identical** after the calculator generalisation (419/419 before PR1, 448/448 after; 371/371 frontend).
- [ ] A `Weight` of `1.5`, `0.5` and `0` are each refused by the bridge, naming the member and the
      weight; `1` converts and every net is unscaled.
- [ ] The zip invariant refuses a mismatched `RowIndex` pair and accepts an OOS-filtered subset.
- [ ] An out-of-sample analysis is reachable only through `OosWindow.Filter`; a grep for
      `CloseTime >=` outside `OosWindow.cs` finds nothing.
- [ ] A run with no walk-forward export yields the explicit "no out-of-sample evidence" state, not
      an empty series.
- [ ] The whole slice is deterministic: no seed, no random number generator, and repeated calls on
      unchanged inputs return byte-identical payloads.
- [ ] Every rendered VaR percentage carries the existing approximation disclaimer, the capital-base
      denominator label, and a new "simulated closes" qualifier.
- [ ] `dotnet format` and `pnpm format` pass clean.

## Proposal Question Round

Interactive questioning was unavailable in this phase. Question 1 of the first round — which
guardrail breach probability would score against — has since been **answered by the user: breach
probability leaves this slice entirely and is deferred to a future FTMO slice** (see Out of Scope
for the KB §1 reasoning). Of what remains, question 2 below is load-bearing enough to change the
slice.

1. **Is there a minimum overlapping-trading-day count below which the correlation matrix should be
   refused outright rather than shown with a density caveat?** The measurement above says a
   coefficient computed over ≤4.5% non-zero days is largely measuring co-absence. Refusing it is
   consistent with how this codebase treats weak evidence (`OosWindow`: "no window, not an empty
   one"; 2a's D4: no per-trade output for a rejected run). Showing it with a caveat is consistent
   with D11 ("report, never gate"). The two precedents genuinely conflict here and the choice is a
   product call about how much the operator is trusted to read a footnote.
2. **Which segment source is authoritative — the imported `BacktestSegment` or the export-derived
   `OosWindow`?** These are two different mechanisms and they can disagree. Slice 1 preserved the
   CSV's `Sample type` verbatim (`IST` / `IS` / `OOS1`), while `OosWindow` derives its boundary from
   `StrategyWalkForwardExport.OosFromDate` and only for `Evaluation` runs. Both committed fixtures
   carry a `Sample type` and neither implies a walk-forward export exists. If `Sample type` is
   authoritative, `OosWindow` is not the wiring this slice needs and the *Filter* plan changes.
3. **Group size and identity: how does an operator name a group today?** No entity holds a backtest
   group. Is a request-scoped list of strategy IDs sufficient for this slice, or is a saved group
   needed before the UI is usable?
4. **Does the band position alone answer the Darwinex-shaped question?** With breach probability
   gone, the slice's Darwinex output is the existing band-position readout (below floor / within
   band / above target) over backtest-derived monthly VaR. That is a point estimate with no
   dispersion around it. If you want to know how *reliably* a group sits inside 3.25%–6.5% rather
   than merely where its single estimate lands, that needs a distribution — which is the machinery
   this slice just removed. Worth confirming the point estimate is enough for now, so the question
   is answered deliberately rather than by omission.

Assumptions taken meanwhile: correlation and VaR are reused, never rebuilt; the bridge dates the
series at the boundary and `ResizedTrade` gains no timestamp; the weight refusal is unconditional
on the value; figures are computed per funding service with Darwinex Zero demo as the baseline; the
segment is a required input; the daily VaR95 is withheld rather than shown as zero; nothing is
persisted; the slice is wholly deterministic; no breach estimator, selector, ranking or null
benchmark appears in this slice.
