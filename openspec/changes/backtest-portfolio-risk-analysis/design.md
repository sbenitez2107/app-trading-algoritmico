# Design: Backtest Portfolio Risk Analysis (slice 2b)

## Technical Approach

No new statistical estimator. One new value object whose *possession is proof* (`OosWindow`
precedent), two new adapters over the already-shipped math (`Pearson`, `Percentile`,
`RollingWindowSums`, `ComputeMonthlyVar`), and one new DTO family that can express *withheld*.
The whole slice is arithmetic over already-persisted rows plus one read endpoint: deterministic,
single-group, no seed, no candidate iteration (proposal decision 2's two tripwires).

**Corrections carried below**, each with its evidence. Against the proposal: the VaR99 claim (E1),
the shape of the generalised entry point (D2), the reusability of `PortfolioRiskDto` (D5), the
`ComputeMonthlyVar` "reused unchanged" claim (D4b), and the whole `OosWindow.Filter` pipeline
(D8 — the segment cannot partition a run). Against earlier revisions of *this* document: the
positional zip (D1/C2), the invisible `windowDays` trim (D4a), the non-existent `BacktestRun.Segment`
(D8a), and the false claim that `Unscalable` rows shorten `resized.Trades` (D1/P1).

### Capabilities — this change promotes TWO

Split, per the `typed-funding-guardrails` precedent (which promoted `funding-guardrails` **and**
`portfolio-monthly-var`, so one-capability-per-slice is not this project's convention):

| Capability | Owns |
|---|---|
| `backtest-net-series-bridge` | Pairing by `RowIndex` lookup (D1), throw-not-status for a pairing failure, the non-unit-weight refusal (D3), `ExcludedUnscalableCount` (D2/P3) |
| `backtest-portfolio-analytics` | The density gates (D4/D4a/D4b), the band-position boundary (D4c), correlation alignment and cell withholding (D6), segment/run provenance (D8/D8a/D8b), and the reporting surface |

The bridge's guarantees are consumer-independent — they hold for any future consumer of an
already-sized series, which is exactly the D9 obligation's scope — while the analytics requirements
are about what this slice publishes. Splitting is cheap now and expensive after archive.

## Architecture Decisions

### D1 — The dated bridge: pair by `RowIndex` lookup, refuse on mismatch

**Choice.** `BacktestNetSeries` in `Application/DTOs/Backtests/`, a **sealed class with a private
constructor**, obtainable only from its nested `public static class Bridge`. `Bridge.TryBuild`
receives the `IReadOnlyList<BacktestTrade>` the caller already holds plus the `ResizedTradeSeries`
produced from it, and pairs them by **`RowIndex` lookup — not by position**:

    sourceByRowIndex = source.ToDictionary(t => t.RowIndex)      // duplicate ⇒ throw
    ∀ r ∈ resized.Trades : sourceByRowIndex.ContainsKey(r.RowIndex)   // unmatched ⇒ throw

**Corrected from a positional zip (C2).** An earlier revision specified
`resized.Count == source.Count AND ∀i: resized[i].RowIndex == source[i].RowIndex`, which is an
equal-length positional zip — and it **throws on exactly the strict-subset case the rationale
describes**. That was an internal contradiction: the rationale, the delta spec's lookup semantics,
and the surviving cause of subsetting all point at lookup, and only the invariant disagreed.

**P1 — there is NO in-pipeline subset cause today, and the earlier claim that `Unscalable` rows
create one was false.** Verified end to end: `TradeRiskNormalizer` adds one `NormalizedTrade` per
trade unconditionally (`TradeRiskNormalizer.cs:147`), and `TradeResizer`'s `rows.Add` sits inside
the `foreach` but **outside** the outcome `switch` (`TradeResizer.cs:96`, with `Unscalable` handled
at `:79` as a counter only). So `resized.Trades.Count == profile.Trades.Count == source.Count`
**always**, and two shipped tests already pin it (`HaveCount(329)`; and
`raised + onTarget + capped + unscalable == series.Trades.Count`). The exclusion I was thinking of
happens **later and elsewhere** — the bridge drops `Unscalable` rows when building `Nets` (D2),
which changes `Nets.Count`, not `resized.Trades.Count`.

**So the subset case is a DEFENSIVE GUARD, not a pipeline case.** With date filtering out of scope
(D8) there is no production producer of a strict-subset `resized.Trades`. The guard exists for two
things that are not reachable today: an OOS or segment filter returning in a later slice, and a
caller that concatenated two runs' rows. **The lookup decision is unchanged and still correct** —
only its stated cause was wrong.

**Consequence for tasks and tests, which is the part that matters.** Because every real series has
equal counts, **a positional zip would pass CI**: every fixture-driven test would be green. The
subset scenario must therefore be built as a **hand-constructed guard test** — a `ResizedTradeSeries`
assembled in the test with a non-contiguous `RowIndex` subset — and labelled as defensive, not
sourced from a fixture. A fixture-driven test here would prove nothing about a real code path,
which is a failure mode this project has already hit twice.

**Uniqueness is not assumed** — it is checked. Slice 1 created a unique `(BacktestRunId, RowIndex)`
index, so duplicates cannot come from one run's rows; they *can* come from a caller that
concatenated two runs, which is precisely the wiring error worth catching.

**On violation: throw `ArgumentException`**, naming the offending `RowIndex` and whether it was
unmatched or duplicated. Not a status. Precedent is D8's non-positive target: a pairing failure is
never a legitimate data condition — it means the caller wired two unrelated lists — and the
alternative is "a complete, plausible-looking series that is entirely wrong". A status would invite
a caller to log it and carry on; nothing downstream can do anything useful with a misaligned pair.

**Alternatives rejected.** *Positional zip* — see C2 above. *Timestamp on `ResizedTrade`* (proposal
option A) — narrows D9's moat to one fact, for a benefit only the consumer D9 exists to prevent
would want. *Re-read from the database on `(BacktestRunId, RowIndex)`* (option B) — a round trip on
a path whose caller already holds the rows, and it *accepts* the ordering-drift risk the checked
lookup closes. *Status instead of throw* — see above.

### D2 — The net of a resized trade, and where the shared math actually forks

`ResizedTrade` carries **no P/L** — only sizes and an `AchievedRisk` interval. Verified by reading
the record. So the bridge must compute the net itself, from the source row it paired by `RowIndex`:

    net_i = source[i].Profit × (resized[i].ResizedSize / resized[i].OriginalSize)

Linear-in-volume, which is D7's own stated basis. `BacktestTrade.Profit` is the only P/L column;
D2 of slice 2a forbids it as a **calibration** source, not as a P/L source. There are no
`Commission`/`Swap`/`Taxes` columns, so this net is gross of every unmodelled cost — see *Cannot
Claim*.

`Unscalable` rows (`OriginalSize ≤ 0`) are **excluded and counted** (`ExcludedUnscalableCount`),
never contributed as `0`: a zero net is a breakeven trade, which is a different claim. This changes
`Nets.Count`, **not** `resized.Trades.Count` — see P1 in D1.

**P3 — where the count is reported.** It belongs on `SeriesDensityDto`, not only on
`BacktestNetSeries`. The spec's "reported alongside every figure" is **right**, and my earlier
design was wrong to give the count no route to the payload: it is a **denominator disclosure** —
the operator is told `TradeCount` and must be able to tell that `TradeCount − ExcludedUnscalableCount`
rows actually reached the series. `SeriesDensityDto` already accompanies both the risk and
correlation payloads, so one field there satisfies "every figure" without duplicating it into
three DTOs. **Rejected**: a field on each of `BacktestPortfolioRiskDto` and `BacktestCorrelationDto`
(three copies of one number, which is how two of them drift); leaving it only on the internal
series (the spec requirement would then be unmet, and the operator could not reconcile the counts).

**Correction to the proposal.** It plans a *public* overload over `(label, broker, dated nets)`.
Rejected: a public raw-tuple entry point lets a hand-scaled projection into the shipped analytics
and re-opens exactly the hole D9 closes. Instead:

| Layer | Shape | Visibility |
|---|---|---|
| Math core | `Pearson`, `Percentile`, `RollingWindowSums`, `ComputeMonthlyVar` | unchanged, private/internal |
| Alignment core | `CorrelationMatrixCore(labels, dayMaps, AlignmentMode)` | **private** |
| Live adapter | `ComputeCorrelation(IReadOnlyList<PortfolioMemberInput>)` → `AlignmentMode.Union`, projects `w * NetOf(t)` | public, **signature unchanged** |
| Backtest adapter | `ComputeCorrelation(IReadOnlyList<BacktestNetSeries>)` | public, new |

Same split for VaR. One copy of the math, two typed doors, no raw door. Shipped output
bit-identical — asserted, not hoped.

### D3 — How much of D9 survives: partly structural, partly convention. Plainly.

| D9 fact | After this slice |
|---|---|
| (1) `ResizedTradeSeries` is not `IReadOnlyList<StrategyTrade>` | **Intact.** Untouched. |
| (2) `ResizedTrade` has no cost fields / no `BaseEntity`, so `NetOf` cannot bind | **Intact.** Untouched. |
| (3) Convention: a consumer that accepts the series MUST refuse `Weight != 1` | **Upgraded to structural**, then partly re-degraded — see below. |

**Upgraded.** `BacktestNetSeries` has a private constructor and its only factory is nested in the
same program text (`OosWindow`'s inverted-nesting trick, which D9 rejected for `ResizedTradeSeries`
only because that type and its producer sit in different assemblies — here they do not).
`Bridge.TryBuild` takes `decimal memberWeight` as a **required** parameter and refuses `!= 1`.
Therefore *"every `BacktestNetSeries` in existence had its weight checked"* is a fact about the type
system, not a convention. It is a **class, not a struct**, for `OosWindow`'s exact reason: a struct
has a `default` instance no matter how private the constructor.

**Re-degraded, and this is the honest cost.** D9 fact (2) made `w * NetOf(t)` a **compile error**.
`w * series.Nets[i].Net` compiles — the projection is a list of `decimal`. So immunity to
*post-hoc* multiplication drops from structural to convention. Two mitigations, neither a
type-system guarantee: the analytics adapters take the **sealed series type**, not a bare tuple
list (D2), so a hand-scaled projection cannot be passed *into* the analytics; and a reflection test
asserts `BacktestNetSeries` exposes no scaling member, mirroring slice 2a's absence-of-conversion
test.

**Verdict: the refusal is structural; the immunity is convention.** Say it that way in the spec
rather than claiming the whole guarantee survived.

**Refusal shape.** `bool TryBuild(..., out BacktestNetSeries? series)` gates consumption (nullable
`out` makes an ignored `bool` a CS8602 warning — D4's mechanism), alongside
`BacktestNetSeriesResult Build(...)` carrying `Status ∈ {Built, NonUnitWeight}` plus the offending
weight and member identity, with `Series` null unless `Built`. Not a silent skip, not a coerced 1,
not a flag on a returned-and-aggregable series. `0` and `0.5` and `1.5` are the same error.
The endpoint maps `NonUnitWeight` to **422** naming the member, so it cannot be dropped from a
group unnoticed.

### D4 — The density gate is derived, not chosen

**What `Measure` measures**, per series and for the merged group series: `DenseDayCount`
(first-to-last calendar days inclusive), `NegativeDayCount`, `NonZeroDayCount`, and — for the
monthly path — `NegativeWindowCount` over the rolling 30-day sums. **`NegativeDayCount` and
`NegativeWindowCount` are the only ones that gate**; `DenseDayCount` and `NonZeroDayCount` are
reported for the operator and must never enter the predicate (see the rejected alternative below).

**One derivation, one number — the reported gating count IS the gating count.** The bridge does
**not** measure density and `BacktestNetSeries` does **not** carry it. `AnalyticsSeries` is
`internal` to Infrastructure, so an Application-side measurement would have to re-derive the dense
day counts on a second code path — and then the payload could report 164 while the gate used
something else. Instead a single
`internal static SeriesDensity Measure(IReadOnlyList<decimal> denseDailyNets)` lives beside
`SupportedPercentile` in `PortfolioAnalyticsCalculator`, both consuming the one dense series built
by `AnalyticsSeries.BuildDailyNetSeries`. The gate reads the same `SeriesDensity` instance that is
projected into the payload. **Rejected**: duplicating the day bucketing in Application (two code
paths, and the divergence would be invisible precisely because both numbers would look plausible);
making `AnalyticsSeries` public to share it (widens a deliberately `internal` surface for a
reporting convenience). This supersedes an earlier revision that placed `Density` on
`BacktestNetSeries`.

**0.1 — `SeriesDensityDto` is MIXED-PROVENANCE, and that is a deliberate choice with a cost.**
`Measure` takes a dense **daily** series and therefore **cannot** recover a trade count: the IST
fixture's 329 trades collapse into 318 non-zero days (measured). It certainly cannot recover
`ExcludedUnscalableCount`. So after P3 routed that field onto this DTO, the six counts have two
origins:

| Field | Source |
|---|---|
| `DenseDayCount`, `NonZeroDayCount`, `NegativeDayCount`, `NegativeWindowCount` | `Measure` (Infrastructure) — **the gating counts** |
| `TradeCount`, `ExcludedUnscalableCount` | `BacktestNetSeries` (the bridge, Application) — trade-level |

Composed at the **read-service boundary**, which is the one place that holds both.

**What this does and does not break.** The single-derivation **rule** governs the *gating* counts
and **still holds** — the gate and the payload read one `SeriesDensity` instance. What breaks is
the single-derivation **test**: "assert the payload carries the same instance the gate consumed"
cannot cover two fields `Measure` never sees. So that assertion is **scoped explicitly to the four
gating counts**, and the two trade-level counts get their own assertion
(`TradeCount − ExcludedUnscalableCount == Nets.Count`, P3).

**Rejected — widening `Measure`'s inputs to take the trades too.** Its job is day-level density;
passing trades in to recover a count it does not need is the wrong coupling, and it would make the
**gating function** know about `Unscalable`, which is a bridge concern. There is a second reason:
`Measure` is also on the live path's future, where "trade count" means `StrategyTrade` rows — a
parameter that means two different things per caller is how a shared helper starts drifting.

**The cost is legibility, so the DTO must carry its provenance in its own doc comment**, per field.
Without that a reader assumes all six counts share one origin and will, reasonably, write the
single-derivation assertion over all six and watch it fail for the wrong reason.

**The threshold is not a threshold, and it is not a share.** On an ascending sort the negatives
occupy indices `0 .. neg−1`, then the zero-filled days, then the positives — and **positives sort
above the zeros, so they cannot help a low percentile**. What a low percentile needs is *negative*
mass, not *non-zero* mass. `Percentile(sorted, p)` does **not** read one index: it INTERPOLATES
between `sorted[⌊p(N−1)⌋]` and `sorted[⌈p(N−1)⌉]`, so the figure it publishes is supported only
when **both** endpoints are losses:

    supported(p)  ⇔  NegativeObservationCount ≥ ⌈p · (N − 1)⌉ + 1

Exact, per confidence level, and derived from the percentile actually being computed. Measured on
both fixtures at **day level**:

| | IST | OOST |
|---|---|---|
| Dense span `N` | 3,860 | 3,804 |
| Negative **days** | 164 (4.25%) | 172 (4.52%) |
| Non-zero days | 318 (8.24%) | 320 (8.41%) |
| `p = 0.05` requires | ≥ 194 | ≥ 192 |
| Gate verdict | **withhold** | **withhold** |
| Shipped daily VaR95 | `0.00` | `0.00` |

**D4-C (correction, RELIABILITY-001) — the gate must defend the value that is PUBLISHED, not one
index of it.** The relation above was first written as `≥ ⌊p(N−1)⌋ + 1`, from the premise that
`Percentile` reads a single index. It does not. Exactly on that older threshold `sorted[⌈p(N−1)⌉]`
is, by construction, the **first non-negative observation**, so the figure the gate authorised was
partly determined by a zero-fill or a win. Measured on the slice's own committed constructions:

| Construction | Old verdict | Published figure | Contamination |
|---|---|---|---|
| `Population(3860, 193)`, `p = 0.05` | report | `0.0500` | `sorted[193] = 0`, weight `0.95` |
| `TailNegativeSeries(120, 5)`, monthly | report | `4,970.50` | `sorted[5] = +30` supplies `5,000.50` — **101%** of it |
| 91 obs, 5 losses, `sorted[5] = +900` | report | `−447.50` | a **negative** loss magnitude in a field documented as positive |

**Why `⌈·⌉ + 1` and not `⌊·⌋ + 2`.** They are the same number for every rank that is not a whole
number, which is every case above and both committed fixtures. They differ when `p(N−1)` **is** a
whole number: there `lo == hi`, `Percentile` returns `sorted[lo]` verbatim, no interpolation happens
and there is no second endpoint to defend. `⌊·⌋ + 2` would withhold a figure that is entirely
composed of a single genuine loss — the same error as the original, mirrored: reasoning about the
published value as if its composition were fixed rather than reading it. The synthetic daily
boundary at `N = 101` (`0.05 × 100 = 5` exactly) is that case, and its pivot is unchanged at 6.

**No fixture-published figure moves.** IST monthly stays `400.19` (1,148 negative windows vs 193
required), OOST monthly stays `378.62` (1,203 vs 190), and both daily VaR95 figures stay withheld.
Only the boundary constructions move, by exactly one negative observation each.

**Rejected — a non-zero-day share gate (e.g. "< 5% ⇒ withhold").** It is not conservative-but-crude,
it is **wrong in the reporting direction on both motivating fixtures**: 8.24% and 8.41% both clear
5%, so it would publish two figures that are measured to be exactly `0.00`. Also rejected: any
hard-coded `5%` constant — the relation must survive a change of percentile, and a bare constant is
precisely what produced the error. Also rejected: D11's "report, never gate" — that stance was
defended for a metric with *zero* measured failures; this one has a measured, certain failure.

**Placement follows from the predicate.** Because the gate is a function of the `p` being computed
and of the sorted list's negative count, it lives **beside `Percentile` in
`PortfolioAnalyticsCalculator`** — a private `SupportedPercentile(sorted, p) → decimal?` returning
null when unsupported — not in a caller that would have to re-derive or hard-code the relation.
`Percentile` itself is untouched, so no shipped number moves.

**E1 — the proposal's VaR99 claim is wrong, measured.** For IST, `⌈0.01 × 3859⌉ = 39`, so VaR99
needs ≥ 40 negative days and there are **164**. `VaR99` is therefore a real non-zero number while
`VaR95` is exactly `0.00`. The proposal states both are `0.00`. The gate reports VaR99 and
withholds VaR95 on the same run — which is the whole argument for computing per level rather than
declaring "daily VaR is unusable".

**Two anchor refinements applied throughout.** Day-level counts are used for every day-level claim:
the earlier `173` is negative *trades*, and a day holding both a win and a loss can net positive, so
the day-level count is `164`. The one-sided bound is now measured twice (164 < 173, 172 < 186).

**The monthly figure is measured, and it reports.** 30-calendar-day rolling windows, `p = 0.05`:

| | windows `M` | negative windows | threshold | outcome | monthly VaR95 |
|---|---|---|---|---|---|
| IST | 3,831 | **1,148** (30.0%) | ≥ 192 | **reports** | **−400.19** |
| OOST | 3,775 | **1,203** (31.9%) | ≥ 189 | **reports** | **−378.62** |

Both clear by roughly six times the required margin, so the Darwinex-relevant figure exists and the
slice's headline output is not at risk. The proposal's "~2.6 trades per 30-day window, so it
survives" turns out to be right, but the margin — 30% negative windows against a 5% requirement —
is the actual reason, and it is much larger than that reasoning implied. The test asserts the
figures, not merely the code path.

**But a gate that never fires on the available data is untested by that data.** Both fixtures
report, which means neither exercises the monthly withholding branch at all. "Both fixtures report"
is evidence about the fixtures, not evidence the gate works. What makes it trustworthy:

1. A **synthetic population** constructed to sit one window either side of the threshold — `M`
   windows with exactly `⌊p(M−1)⌋` negative windows (withholds) and with one more (reports). This
   pins the boundary, which no real fixture can.
2. An **injected-defect** check: take the IST window sums and zero out all but 191 of the negative
   windows; the figure must flip to withheld with the count reported. It proves the gate is reading
   the quantity it claims to.

The daily gate gets the mirror image of this: it fires on both fixtures, so its *reporting* branch
needs the same synthetic treatment.

**D4a — the backtest adapter passes NO window trim, and this is not a detail.** Shipped
`ComputeVaR(initialCapital, members, int windowDays = 250)` trims the dense series to its most
recent 250 observations (`PortfolioAnalyticsCalculator.cs:268-270`), and passes the same
`windowDays` into the per-service monthly path (`:278`). An earlier revision of this document
reasoned from `BuildDailyNetSeries` rather than from the caller, so the trim was invisible to it.
Both paths withhold, so the gate's verdict is unchanged — but the thresholds and the sample are
completely different:

| | untrimmed | trimmed (shipped default 250) |
|---|---|---|
| IST | `N = 3,860`, neg = 164, needs 193 | `N = 250`, neg = **5**, needs **13** |
| OOST | `N = 3,804`, neg = 172, needs 191 | `N = 250`, neg = **7**, needs **13** |

**Choice: `windowDays = 0` (no trim).** A trailing-250-day window answers *what is my risk now*,
which is the right question for a live account and the wrong one for a backtest: every figure here
is a descriptive statistic **of a stated sample**, and trimming would silently answer a different
question over the last ~8 months of a decade-long run. **Rejected**: inheriting the shipped default
(it discards ~93% of the sample and leaves 5–7 negative observations to estimate a tail from);
exposing `windowDays` as an operator input (a knob whose only effect is to change which sample the
figure describes, with no rule available for choosing it). Recorded explicitly because a reader who
assumed the shipped default would derive the wrong thresholds and write the wrong scenarios.

### D4b — Both gates live beside their own percentile. The policy is a parameter, not a layer.

**The tension.** The gate must sit where the percentile is computed (D4's placement argument), but
`ComputeMonthlyVar` is on the **live** path too, and gating inside it unconditionally would flip a
real account with sparse negative windows from a number to a withheld figure — a shipped-behaviour
change this slice is not scoped to make. Put the monthly gate in the adapter instead and the two
gates sit at different layers, which reopens the reported-vs-gating drift hazard D4 just closed.

**The premise that dissolves it: `ComputeMonthlyVar` (`:460`), `VarFromDaily` (`:440`) and
`Percentile` (`:478`) are all `private`.** So adding a parameter is **not** a shipped-signature
change — it is invisible outside the class and cannot break a caller. The only thing that must not
move is the live path's *output*.

**Choice.** A `PercentilePolicy { Unconditional, RequireSupport }` threaded through the private
helpers, **required, not defaulted**:

| Helper | Live adapter passes | Backtest adapter passes |
|---|---|---|
| `VarFromDaily(nets, policy)` | `Unconditional` → `Percentile` verbatim | `RequireSupport` → `SupportedPercentile` |
| `ComputeMonthlyVar(dailyNets, capital, policy)` | `Unconditional` | `RequireSupport` |

`Percentile`, `RollingWindowSums`, `Pearson` and the `MinHistoryDays` gate are untouched. The
asymmetry disappears: **both** gates sit next to their own percentile, inside the calculator, and
the live/backtest difference is one value stated explicitly at each public adapter.

**Rejected — the monthly gate in the adapter** (the spec agent's inference). It preserves a
"reused unchanged" label at the cost of the two gates living at different layers, and it re-creates
exactly the drift hazard closed for `Density`: the adapter would re-derive `NegativeWindowCount`
from its own pass over the window sums while the figure came from `ComputeMonthlyVar`'s pass.

**Rejected — a defaulted parameter.** A default lets a future call site inherit live behaviour
silently. With a private method and two call sites, requiring the argument costs nothing and makes
the choice visible at the call site rather than hidden in a signature.

**Rejected — a `bool`.** `ComputeMonthlyVar(nets, capital, true)` does not say what is true. The
enum names *why* the paths differ.

**Say it plainly: `ComputeMonthlyVar` is NOT "reused unchanged".** The proposal's *Approach*
section lists it under "Reused unchanged (no new math)" and that claim is now **false as written**
and must be corrected. What survives, precisely: no new statistical estimator; the **math** is
unchanged; the live path's output is **bit-identical**. What does not survive: the literal claim
that the function is untouched. `VarFromDaily` moves the same way and was never in that list.
`RollingWindowSums`, `Percentile`, `Pearson`, `BuildDailyNetSeries`, `MonthlyVarHorizonDays` and
`MinHistoryDays` remain genuinely unchanged.

**And confirming the daily gate one level down**: `SupportedPercentile` is a **new private helper**;
`Percentile`'s own body is not touched, so the live path never reaches the gate even though the
gate lives beside it. That is by design, not by luck — and it is why the policy parameter is needed
at all: `VarFromDaily` is *shared*, so the choice has to be passed in rather than inferred.

**What the caller receives.** `decimal?` = **null**, plus a `VarWithholdReason` and the density
evidence that caused it. Model: slice 2a's `RunRiskEstimate` (status + evidence surviving
rejection), **not** `OosWindow`'s "no window at all". They answer different questions —
`OosWindow` answers *may this claim be made at all* (provenance, must be unforgeable);
the density gate answers *does the data support this number* (sufficiency, and the operator must
see the counts). A withheld figure is **never** `0m`.

### D4c — A currency figure is not a band position. The conversion has a cited dependency.

`−400.19` and `−378.62` are **currency**. The 3.25%–6.5% band is a **percentage of a capital base**,
and this design must not let the first become the second by division alone.

**Two dependencies, both from the KB and neither satisfiable by this slice's arithmetic.**
`Darwinex_Zero_Risk_Model.md` §2 records the target-VaR determination window as *"up to 6 months of
historical VaR, walking most-recent to oldest until the max/min ratio reaches 2:1"* — a
methodology this slice does not implement — and the calculation window as *"the last 45 days of the
trader's open positions"*, which is forward-looking open-position risk, not realized closes.

**Choice.** The slice emits `MonthlyVar95` (currency) and `MonthlyVar95Percent` **only** through the
existing shipped `Var95Percent` basis (`monthlyVar95 / initialCapital`, `:471`), reuses
`VarTargetReadoutDto` unchanged, and **never computes a band position itself**. Where the readout
carries one it is the shipped `VarTarget` comparison against the operator's configured
`BrokerRiskLimits`, labelled with the capital-base denominator that requirement already mandates —
not a new determination. **Rejected**: deriving a band position from the currency figure and a
capital base (it would silently substitute this slice's whole-sample percentile for the KB's 6-month
2:1 determination window); implementing the determination window here (no data source for open
positions exists, and it is a different estimator — out of scope by the slice's own "no new
estimator" rule).

This is recorded because the project has already shipped three figures whose provenance could not
be reproduced — the `Profit`-derived point value, 2a's assumed round-half rounding, and the daily
VaR of `0.00`. A band position asserted from `−400.19` would be the fourth of the same kind.

### D5 — A new risk DTO, because the shipped one cannot express "withheld"

`PortfolioRiskDto.Var95` / `Var99` / `Var95Percent` are non-nullable `decimal`
(`PortfolioAnalyticsDto.cs:157-160`). Reusing it would **force a `0`** — the exact failure the
falsification found. Making them nullable would move a shipped contract and put the "no shipped
number changes" property at risk. So: new `BacktestPortfolioRiskDto` / `BacktestCorrelationDto` /
`SeriesDensityDto` in `Application/DTOs/Backtests/`, reusing `VarTargetReadoutDto` unchanged for
the band readout (its `MonthlyVar95` is already `decimal?`). **Correction to the proposal's**
*Affected Areas*, which lists `PortfolioAnalyticsDto.cs` as gaining density fields.

### D6 — Correlation: gate per cell, and remove the defect rather than caveat it

The D4-vs-D11 precedent conflict, resolved. **What each precedent protected**: `OosWindow`/D4
protected against *a number that reads as evidence of nothing* — refusal is right when the figure
would mean something other than what it is labelled. D11 protected against *gating on a metric that
does not discriminate the failure* — its point was granularity and attribution, not "never gate".

A coefficient over a union-aligned series that is **91.8% zeros** (IST: 318 non-zero days of 3,860;
OOST 91.6%) is not a weakly-supported measure of co-movement; it measures **co-absence**. That is
D4's case. But refusing the whole matrix because
one pair is thin discards well-supported pairs — that is D11's error, committed at the wrong
granularity. So:

1. **The backtest adapter aligns on the pairwise INTERSECTION** of trading days (days on which both
   members closed a trade), not the union. This removes the bias instead of disclosing it. `Pearson`
   is untouched; the live `PortfolioMemberInput` adapter keeps `Union` and stays bit-identical.
2. **Matrix cells become `decimal?`.** A cell is withheld (null) when pairwise `CoActiveDays < 2`
   (Pearson's own domain) or the pair's series is constant. The shipped `Pearson` returns `0` for a
   constant series; that mapping to `null` lives in the **backtest adapter**, so no shipped
   behaviour moves.
3. `CoActiveDays` and `CoActiveShare` are **reported per cell** with no invented minimum — D11's
   surviving stance, correctly applied to the metric that has no measured cliff.
4. `AverageCorrelation` is `decimal?` over reported cells only, with `WithheldCellCount`. All cells
   withheld ⇒ null, never `0`.

### D7 — Placement

| Component | Location | Precedent / constraint |
|---|---|---|
| `BacktestNetSeries` + nested `Bridge` | `Application/DTOs/Backtests/` | `OosWindow` inverted nesting needs one program text; `ResizedTradeSeries` lives in Application and Domain cannot reference it |
| `BacktestPortfolioRiskDto`, `BacktestCorrelationDto`, `SeriesDensityDto` | `Application/DTOs/Backtests/` | slice 2a DTO placement |
| `BacktestNetSeriesStatus`, `VarWithholdReason` | `Domain/Enums/` | `CalibrationStatus` precedent |
| Dated adapters, `CorrelationMatrixCore`, `SupportedPercentile`, **`SeriesDensity Measure`**, **both density gates** | `Infrastructure/Services/PortfolioAnalyticsCalculator.cs` | `AnalyticsSeries` is `internal` to Infrastructure — forced (D10). Density measurement and BOTH gates are here, not split across layers, so the reported count and the gating count are one derivation (D4, D4b) |
| Group orchestration | `Infrastructure/Services/BacktestReadService.cs` | slice 2a read-service precedent |

The bridge is **not** a calculator, so D10's "calculators in Infrastructure/Services" does not
bind it; the constraint that *does* bind it is single-program-text for the private constructor.
Deviation stated deliberately.

### D8 — The segment is a property OF the run. This slice does no date filtering at all.

**Decided (phase 0): `BacktestSegment` is authoritative, and it cannot partition a run.** Verified
in code: `SqxTradeListParserService` rejects any file carrying more than one `Sample type`
(`:261`, `distinctSampleTypes.Count > 1` ⇒ `Rejected`), and its own comment records that "a file
that is wholly IS, wholly OOSn or wholly IST still imports". So `BacktestTrade.Segment` (`:45`) is
**constant across every trade of a run**: it is stored per-trade but carries run-level information.

**Consequence — the whole filtering pipeline comes out.** No `SegmentFilterResult`, no
`OosWindow.Filter`, no `OosWindow.Resolver` call, no date comparison anywhere in the slice.
Selection happens at **run granularity**, and the segment then travels with the series as
**metadata** so every figure states which sample it was computed over.

**Correction — there is no `BacktestRun.Segment` to read.** An earlier revision of this document
said run selection reads `BacktestRun.Segment`. That property **does not exist**: `BacktestRun`
(`:23-46`) carries only `SourceFileName`, `ContentHash`, `StrategyId`, `Kind`, `Symbol` and
`Trades`. The segment lives solely on `BacktestTrade.Segment` (`:45`), and migrations are out of
scope for this slice, so adding a column is not available. **The segment must be read off the run's
trades.**

### D8a — How a member's run is selected, and it is a bounded two-row question

**The constraint.** `BacktestRunConfiguration:18` declares
`HasIndex(x => new { x.StrategyId, x.Kind }).IsUnique()` — "Identity is the SLOT. A strategy has at
most one Deploy run and one Evaluation run". So this is a choice among **at most two rows**, not a
search.

**`Kind` and `Segment` are different axes and no mapping between them is invented.** `Kind`
(`Deploy` / `Evaluation`) says which parameter set backs the run; `Segment` says what SQX labelled
its trades. A `Deploy` run's trades can be `InSampleTest` — that is the AlgoWizard full-period
export, and it is the IST fixture. An `Evaluation` run's trades can be `InSample` or `OutOfSample`.
Anything that derives one enum from the other is a bug.

**Deriving the segment, in one server-side query for the whole group** (the `ReadinessRows`
precedent — one query per page, not one per member):

```csharp
runs.Where(r => strategyIds.Contains(r.StrategyId))
    .Select(r => new {
        r.Id, r.StrategyId, r.Kind,
        MinSegment = r.Trades.Min(t => (int?)t.Segment),
        MaxSegment = r.Trades.Max(t => (int?)t.Segment) })
```

- **`MinSegment is null`** ⇒ the run has no trades ⇒ **no segment, and no evidence**. Never coerced
  to `Unknown`. Slice 1's own words: "A run ROW is not evidence — its trades are."
- **`MinSegment != MaxSegment`** ⇒ the parser invariant has been violated (only reachable by a
  hand-edited database) ⇒ that run is **refused**, naming it. Costs one extra SQL aggregate and
  converts an invariant this design *depends on* into one it *checks* — the same stance as D1's
  `RowIndex` uniqueness. **Rejected**: `FirstOrDefault()` over the trades, which silently picks a
  row and would also need an explicit `OrderBy` to stay deterministic.

**Selecting, given a requested segment `S`:**

| Runs whose segment is `S` | Outcome |
|---|---|
| Exactly one | That run is the member's input |
| **None** | Explicit *no evidence for this segment* for that member — no series, not an empty one |
| **Both** | **Refused**, naming the strategy and both `Kind`s |

**Why both-match is a refusal, not a preference.** Two runs carrying the same segment are two
different parameter sets over the same sample; picking either silently makes the published figure
depend on an arbitrary choice, and preferring `Evaluation` would be exactly the `Kind`→`Segment`
inference ruled out above. The request may therefore carry an **optional `BacktestRunKind`** to
disambiguate; absent it, ambiguity is refused rather than guessed. **Rejected**: a hardcoded
preference order; returning both (the slice evaluates one group, and a member cannot be two series).

**Both survive — confirmed for the spec.** The segment input is **required** (without it every
figure is silently in-sample, which the proposal names as the number most likely to be optimistic),
and the explicit *no evidence for this segment* state is **retained**, now at run granularity
rather than trade granularity. If the delta spec dropped either, it needs them back.

### D8b — `Unknown` is not a selectable segment, and the request field is nullable

`BacktestSegment.Unknown = 0` is the enum's **default**, and its own doc comment says it exists "so
an unrecognised future label degrades safely instead of pointing at a meaningful segment"
(`BacktestSegment.cs:5-7`). Two problems follow, and D8a was silent on both.

**(a) The request field must be `BacktestSegment?`.** A non-nullable field cannot express "no
segment specified": an omitted JSON property binds to `0` = `Unknown`, so "required input" would be
**unsatisfiable as typed** — the caller who forgot it and the caller who asked for `Unknown` are
indistinguishable. Nullable + an explicit null check makes the required-input requirement real.
**Rejected**: adding a `NotSpecified = -1` sentinel (a second way to say nothing, and it pollutes a
shipped enum); relying on model-state `[Required]` alone on a non-nullable enum (it does not fire
on `0`).

**(b) `Unknown` is REFUSED as a requested segment.** A run whose trades are genuinely `Unknown`
carries a label the parser could not classify — its `SampleTypeRaw` is preserved verbatim but its
meaning is unestablished. Publishing a risk figure labelled "computed over the Unknown sample" is
the same act as publishing a `0.00` VaR: a figure whose label asserts something the data does not
support. So a request for `Unknown` is refused, and a run whose trades are `Unknown` is never
selected. **Rejected**: allowing it with a caveat (D4's stance — a number whose provenance is
unestablished is not published); silently treating `Unknown` as full-sample (it invents a
classification the parser explicitly declined to make).

Note this is a *different* rule from D8a's trade-less-run case. There, a run with no trades yields
**no segment** and must never be *coerced* to `Unknown`. Here, a run with a genuine `Unknown` is
not *selectable*. Both roads lead to no series, by different reasoning, and the spec needs both.

**`OosWindow` is out of scope, and for a reason, not by omission.** It answers a different and
later question — which trades of an `Evaluation` run are genuinely unseen by the optimiser — which
is not the same as which sample a run belongs to. The practical objection also bites: measured
earlier in this project, a Deploy run's last-window parameters leave only **3 of 329 trades** past
the boundary, so filtering would carve away essentially the whole sample.

**New obligation created by the simplification.** Because the sample label now varies *per member*,
a group can be **heterogeneous** — member A's run `InSampleTest`, member B's `OutOfSample`. A
correlation or VaR over that group has no single sample label, so **the analysis is refused**,
naming the disagreeing members and their segments. Precedent: the D4 stance that a figure whose
label would be false is not published. Rejected: computing it with a "mixed" label (the operator
would then be reading a number that means nothing in particular); silently taking the majority
segment.

**The grep guarantee gets stronger, not weaker.** `CloseTime >=` is now absent from this slice by
construction rather than by convention — there is no date comparison to keep in one place.

**Removed from scope by this decision** (re-scope PR3 accordingly): `SegmentFilterResult`,
`SegmentSelection`/`SegmentSource` enums (reuse `BacktestSegment`), the `OosWindow` wiring and its
no-window state machine, and the `CloseTime >=` grep assertion. **Added**: the run-selection
projection (D8a) and the heterogeneous-group refusal — together materially smaller than the
filtering pipeline they replace.

## Data Flow

    strategyIds[] + REQUIRED BacktestSegment + optional BacktestRunKind
        │
        ▼  ONE query: per run, Min/Max Segment over its trades   (no date filter — D8/D8a)
        │       ├─ no trades          ⇒ no segment, no evidence
        │       ├─ Min != Max         ⇒ run REFUSED (parser invariant violated)
        │       ├─ no run matches S   ⇒ "no evidence for this segment" (no series)
        │       ├─ both runs match S  ⇒ member REFUSED unless runKind disambiguates
        │       └─ members disagree on Segment ⇒ group REFUSED, naming them
        ▼
    BacktestTrade[] (one run, wholly one sample)
        │
    TryNormalize ──→ RunRiskProfile ──→ TradeResizer.Resize ──→ ResizedTradeSeries
        │                                                             │
        └──────────── pair by RowIndex LOOKUP (D1) ───────────────────┘
                                   │  weight != 1 ⇒ 422, no series (D3)
                                   │  unmatched / duplicate RowIndex ⇒ ArgumentException (D1)
                                   ▼
                          BacktestNetSeries  (private ctor; possession = weight checked)
                                   │
                    ┌──────────────┴───────────────┐
                    ▼                              ▼
        ComputeCorrelation(series[])    ComputeVaR(series[], windowDays: 0)
        intersection-aligned, D6        density gate per p, D4/D4a
                    │                              │
                    ▼                              ▼
        BacktestCorrelationDto          BacktestPortfolioRiskDto + VarTargetReadoutDto
                    └──────────────┬───────────────┘
                                   ▼
        GET api/backtests/portfolio-risk  ──→  group risk panel (withheld ≠ 0)

## File Changes

| File | Action | Description |
|---|---|---|
| `Application/DTOs/Backtests/BacktestNetSeries.cs` | Create | Sealed class, private ctor, nested `Bridge`; **`RowIndex` lookup pairing** (not a zip), weight refusal, net rescaling (D1–D3) |
| `Application/DTOs/Backtests/BacktestNetSeriesResult.cs` | Create | Status + evidence + nullable series (D3) |
| `Application/DTOs/Backtests/SeriesDensityDto.cs` | Create | **Mixed-provenance** (D4/0.1), composed at the read-service boundary: four day/window counts from the Infrastructure `SeriesDensity` (the gating counts) **plus** `TradeCount` and `ExcludedUnscalableCount` from the bridge. Per-field provenance MUST be in the doc comment |
| `Application/DTOs/Backtests/GroupRiskAnalysisRequest.cs` | Create | Endpoint request: `strategyIds[]`, `targetRiskPerTrade`, grid, **`BacktestSegment?` (nullable — D8b)**, optional `BacktestRunKind`, funding service |
| `Application/DTOs/Backtests/BacktestServiceRiskDto.cs` | Create | Per-funding-service breakdown with `decimal?` VaR fields — the backtest counterpart of `ServiceRiskDto`, which cannot express withheld (D5) |
| `Infrastructure/Services/PortfolioAnalyticsCalculator.cs` (nested) | Create | `internal readonly record struct SeriesDensity` — declared beside `Measure` and the gates so the reported and gating counts are one derivation (D4); projected to `SeriesDensityDto` at the read-service boundary |
| `Application/DTOs/Backtests/BacktestPortfolioRiskDto.cs` | Create | `decimal?` VaR fields + `VarWithholdReason` + density + `WindowDays = 0` (D5, D4a) |
| `Application/DTOs/Backtests/BacktestCorrelationDto.cs` | Create | `decimal?` cells, `CoActiveDays`, `WithheldCellCount`, `Alignment` (D6) |
| `Domain/Enums/BacktestNetSeriesStatus.cs`, `VarWithholdReason.cs` | Create | Two enums. **No `SegmentSelection`/`SegmentSource`** — `BacktestSegment` is reused (D8) |
| `Infrastructure/Services/PortfolioAnalyticsCalculator.cs` | Modify | Private nested enums `AlignmentMode`, `PercentilePolicy` (no new files) + `CorrelationMatrixCore`; new `SupportedPercentile` and `SeriesDensity Measure(..)` beside `Percentile`; **private** `VarFromDaily`/`ComputeMonthlyVar` gain a required `PercentilePolicy` (D4b); live signatures become adapters; new `BacktestNetSeries[]` adapters passing `windowDays: 0`. `Percentile`/`Pearson`/`RollingWindowSums` bodies untouched. **Shipped output bit-identical** |
| `Application/Interfaces/IBacktestReadService.cs` + `Infrastructure/Services/BacktestReadService.cs` | Modify | `GetGroupRiskAnalysisAsync(strategyIds, targetRisk, grid, segment, runKind?, service, ct)`; one server-side projection deriving each run's segment from `Min`/`Max` over its trades (D8a); refuses an ambiguous member and a heterogeneous group (D8) |
| `WebAPI/Controllers/BacktestsController.cs` | Modify | One read endpoint; 422 on `NonUnitWeight` |
| `web/features/sqx/...` + `assets/i18n/{en,es}.json` | Modify | Group risk panel: withheld states, density, disclaimers, denominator label, "simulated closes" |
| Migrations · `StrategyTrade` · `PortfolioStrategy.Weight` · `Portfolio` · `BrokerRiskLimits` · `PortfolioAnalyticsDto.cs` | **Untouched** | Nothing persisted; no shipped DTO widened (D5) |

## Interfaces / Contracts

```csharp
// Application/DTOs/Backtests — possession is proof the weight was checked (D3).
public sealed class BacktestNetSeries
{
    private BacktestNetSeries(...) { }

    public Guid StrategyId { get; }
    public string Label { get; }
    public string? FundingService { get; }
    public BacktestSegment Segment { get; }           // run-level metadata; never a filter (D8)
    public decimal TargetRiskPerTrade { get; }
    public IReadOnlyList<DatedNet> Nets { get; }       // chronological, one per SCALABLE row
    public int ExcludedUnscalableCount { get; }        // Nets.Count == resized.Trades.Count - this
    // NO Density: measured once, in Infrastructure, by the code that gates (D4).

    public static class Bridge
    {
        /// <exception cref="ArgumentException">
        /// A resized RowIndex with no source match, or a duplicated source RowIndex (D1).
        /// </exception>
        public static BacktestNetSeriesResult Build(
            IReadOnlyList<BacktestTrade> source,   // paired by RowIndex LOOKUP, not by position
            ResizedTradeSeries resized,
            Guid strategyId, string label, string? fundingService,
            BacktestSegment segment,
            decimal memberWeight);                 // REQUIRED; != 1 is refused, never applied

        /// <summary>false ⇒ Status != Built. No series exists for a refused member.</summary>
        public static bool TryBuild(..., out BacktestNetSeries? series);
    }
}

public readonly record struct DatedNet(DateTime When, decimal Net);

// The withheld figure is null, NEVER 0 (D4/D5). WindowDays is 0 — no trim (D4a).
public sealed record BacktestPortfolioRiskDto(
    decimal InitialCapital, string Method, int WindowDays, BacktestSegment Segment,
    decimal? DailyVar95, decimal? DailyVar95Percent, VarWithholdReason DailyVar95Withheld,
    decimal? DailyVar99, decimal? DailyVar99Percent, VarWithholdReason DailyVar99Withheld,
    decimal? MonthlyVar95, decimal? MonthlyVar95Percent, VarWithholdReason MonthlyVar95Withheld,
    SeriesDensityDto Density,
    IReadOnlyList<BacktestServiceRiskDto> ByService,
    VarTargetReadoutDto? VarTarget);
```

## Testing Strategy

Strict TDD — every row is RED first.

| Layer | What to test | Approach |
|---|---|---|
| Unit — falsification | IST: dense series **3,860** elements, **164** negative days (4.25%), 318 non-zero (8.24%); shipped `Percentile(.,0.05)` path returns exactly `0.00`; the gate withholds it. OOST: 3,804 / **172** / 320, same verdict | Fixture-driven; the measurement *is* the assertion, so the defect can never return as a feature |
| Unit — wrong predicate | A **non-zero-day** gate at 5% would REPORT both fixtures (8.24%, 8.41%) while the true figure is `0.00` | Pins why the predicate is negative-count, not share. Guards the exact error the parallel spec draft made |
| Unit — E1 | Same fixture: `VaR99` **is reported** (164 ≥ ⌊0.01·3859⌋+1 = 39) while `VaR95` is withheld — one run, two verdicts | Pins the per-level gate and corrects the proposal's "both 0.00" |
| Unit — monthly gate | IST: `M = 3,831`, **1,148** negative windows (30.0%) ≥ 192 ⇒ reports **−400.19**; OOST: `M = 3,775`, **1,203** (31.9%) ≥ 189 ⇒ reports **−378.62**; both clear `MinHistoryDays = 90` | Asserts the measured figures, not merely the path |
| Unit — **gate boundary, synthetic** | A constructed population with exactly `⌊p(M−1)⌋` negative windows withholds; one more reports. Mirror case for the daily gate's *reporting* branch | **Both fixtures report monthly and both withhold daily, so neither fixture exercises the other branch.** No real fixture can pin a boundary |
| Unit — **injected defect** | Zero out all but 191 of IST's negative window sums ⇒ the monthly figure flips to withheld with the count reported | Proves the gate reads the quantity it claims to, not something correlated with it |
| Unit — no band position | The slice never derives a band position from a currency figure; `MonthlyVar95Percent` uses only the shipped `monthlyVar95 / initialCapital` basis and carries the denominator label | Pins D4c against the KB §2 determination-window dependency |
| Unit — withheld ≠ 0 | Every withheld figure serialises as `null`; a JSON assertion, not a C# one | The exact failure mode being guarded |
| Unit — bridge pairing | A resized `RowIndex` with no source match throws naming it; a duplicated source `RowIndex` throws naming it | Pins D1's lookup semantics |
| Unit — **defensive guard, hand-built** | A **hand-constructed** `ResizedTradeSeries` with a non-contiguous strict-subset `RowIndex` set pairs correctly. **Must not be fixture-driven**: every real series has equal counts (P1), so a fixture version would be green under a positional zip and would prove nothing | Pins D1 against C2. Labelled defensive — there is no production producer of a subset today |
| Unit — no trim | The backtest adapter passes `windowDays = 0`: `ObservationDays == 3,860` on IST, not 250, and the gate needs 193 rather than 13 | Pins D4a. A reader inheriting the shipped default would compute the wrong threshold |
| Unit — one derivation, **scoped to the four gating counts** | `DenseDayCount`, `NonZeroDayCount`, `NegativeDayCount`, `NegativeWindowCount` in the payload are the same values the gates consumed (assert on the same `SeriesDensity` / `ComputeMonthlyVar` result, not on recomputed numbers). **Do NOT extend this to `TradeCount`/`ExcludedUnscalableCount`** — `Measure` never sees them (0.1) | Pins D4/D4b's single-derivation rule; without it the payload can disagree with its own verdict |
| Regression — `PercentilePolicy` | The live path passes `Unconditional` and every shipped monthly and daily VaR is **bit-identical**; a live series that WOULD fail the support test still returns its number | Pins D4b. This is the assertion that proves the shared helpers were parameterised, not re-behaved |
| Unit — segment | A group whose members' runs disagree on `Segment` is **refused**, naming them; the payload states the segment it was computed over | Pins D8. Also assert `OosWindow` is not referenced by this slice |
| Unit — run selection | Requested segment matches: exactly one run ⇒ used; **none** ⇒ no-evidence state; **both** ⇒ refused naming the strategy and both `Kind`s, unless `runKind` disambiguates. A `Deploy` run whose trades are `InSampleTest` (the IST fixture) is selected for `InSampleTest` — no `Kind`→`Segment` inference | Pins D8a. The `Deploy`+`InSampleTest` row is the one a `Kind`-based shortcut gets wrong |
| Unit — `Unknown` | An omitted segment field is refused as *not specified* (not silently `Unknown`); an explicit request for `Unknown` is refused; a run whose trades are `Unknown` is never selected | Pins D8b. The first assertion fails if the request field is non-nullable |
| Unit — excluded count | `ExcludedUnscalableCount` appears on the payload's density block and `TradeCount − ExcludedUnscalableCount == Nets.Count` | Pins P3's denominator disclosure |
| Unit — run segment derivation | A run with **no trades** yields no segment and no evidence, never `Unknown`; a run whose trades disagree (`Min != Max`) is refused naming it | Pins D8a's checked invariant; slice 1's "a run ROW is not evidence — its trades are" |
| Unit — weight refusal | `1.5`, `0.5`, `0` each refused naming member + weight, `Series is null`; `1` converts with every net unscaled | The three D9 scenarios verbatim from the spec |
| Compile/reflection | `BacktestNetSeries` has no public ctor and no scaling member; `PortfolioMemberInput(Trades: resizedSeries)` still does not compile | Pins D3's structural half and the surviving D9 facts |
| Unit — net rescaling | `net = Profit × ResizedSize/OriginalSize`; at `target = Â` the nets reproduce `Profit` exactly; `Unscalable` rows excluded and counted, never `0` | Pins D2; the round-trip mirrors 2a's D7 test |
| Unit — correlation | Intersection alignment: a pair with disjoint trading days yields a **withheld cell**, not `0`; `CoActiveDays` reported; all-withheld ⇒ `AverageCorrelation is null` | Pins D6 |
| Regression | Live `ComputeCorrelation`/`ComputeVaR` **bit-identical** after the core extraction; 419/419 -> 448/448 backend + 371/371 frontend | Assert before the new adapters are wired |
| Determinism | Repeated calls on unchanged inputs return byte-identical payloads; grep finds no `Random`/seed in the slice | Tripwire 1 |
| Grep | No `CloseTime >=` and no `OosWindow` reference in this slice (stronger than slice 1's one-file convention — the comparison is absent, not localised); no iteration over candidate groups | D8 + tripwire 2 |
| Integration | Endpoint returns the analysis; a `NonUnitWeight` member yields **422** naming the member; a heterogeneous group is refused naming the disagreeing members | Existing controller test pattern |
| Frontend | Withheld VaR renders its state label and never `0`; density and "simulated closes" always present | Vitest |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or
process-integration boundary. Arithmetic over already-persisted rows plus one read endpoint.

## Migration / Rollout

No migration; nothing persisted. `PortfolioAnalyticsCalculator` is touched additively only, with
bit-identical output asserted, so a revert restores it exactly. Three chained PRs:
(1) core extraction + `SupportedPercentile`/`Measure` + bit-identical regression assertions;
(2) bridge + `RowIndex` lookup + weight refusal + density gate; (3) endpoint + UI + i18n.
Frontend-only rollback leaves the endpoint unused.

**PR3 re-scope.** D8's decision removes `SegmentFilterResult`, two enums, the `OosWindow` wiring
and its no-window state machine from PR3, and replaces them with run-level segment selection plus
the heterogeneous-group refusal — a materially smaller surface than the ~650 lines forecast against
the filtering pipeline.

## What This Slice Cannot Claim

- **Not that it recommends a group.** It measures the one group it is handed. No ranking, no
  scoring, no search, no group-size decision.
- **Not that any figure is a forecast.** Every number is a descriptive statistic over one fixed set
  of **simulated** closes. Deterministic by construction, which is also why it cannot present a
  distribution — there is none.
- **Not that costs are modelled.** `BacktestTrade` has no commission/swap/tax column. The net is
  `Profit × ResizedSize/OriginalSize`: gross of slippage, spread, swap, commission and real broker
  minimums, and the rescaling is exact only for volume-proportional costs. A resized series is not
  broker-executable.
- **Not that any figure is more precise than `Â`.** Every net inherits slice 2a's estimation error
  (IST `Â = 199.98`, band `[199.98, 200.16)`, 90/90; OOST `Â = 200.00`, 88/95). A re-run or a
  recalibration moves `Â` and every net with it. Nothing here detects an `Â` that is wrong.
- **Not that the weight guard is exercised in production.** This slice's only caller works from a
  request-scoped group whose API contract has **no weight field**, so the value passed is always
  `1`. The refusal is proven by unit tests and by the required parameter; its purpose is that the
  *next* caller cannot obtain a series without passing a weight through the check.
- **Not that immunity to double-sizing is structural.** The refusal is (private ctor). The immunity
  is not: `w * series.Nets[i].Net` compiles. D3 states this plainly.
- **Not that its VaR is Darwinex's VaR.** Realized close-to-close from a backtest against
  forward-looking open-position risk over a 45-day window (KB §5, traps 2 and 3). The shipped
  approximation disclaimer applies unchanged, plus "simulated closes".
- **Not that the 3.25%–6.5% band (KB §2) applies to a backtest capital base.** KB §5 trap 3; the
  denominator label requirement is inherited, not relaxed.
- **Not that `−400.19` is a band position.** It is a whole-sample currency percentile. KB §2's
  target-VaR determination walks up to 6 months of historical VaR most-recent-to-oldest until the
  max/min ratio reaches 2:1, and its calculation window is the last 45 days of **open** positions —
  neither is implemented here (D4c). The percentage this slice reports is
  `monthlyVar95 / initialCapital` and nothing more.
- **Not that the density gates are validated by the fixtures.** The monthly gate reports on both and
  the daily gate withholds on both, so each has one branch that no available data exercises. Only
  the synthetic-boundary and injected-defect tests speak to those branches.
- **Not that intersection-aligned correlation is unbiased** — only that it is not measuring
  co-absence. A pair with 3 co-active days gets a reported coefficient with `CoActiveDays = 3`
  beside it; no minimum-count threshold is defended, because nothing measured supports one (D11).
- **Not that a passing density gate makes a figure adequate.** The gate proves the percentile index
  landed on a genuinely negative observation. It says nothing about how many, and nothing about
  whether 164 negative days out of 3,860 is a sample anyone should size on. A reported VaR99 on the
  IST fixture rests on `sorted[38]` of 3,860 mostly-zero days.
- **Not that the reported non-zero-day share means anything about the gate.** It is disclosure only.
  Both fixtures clear 8% non-zero and still return `0.00` at the 5th percentile.
- **Not that a figure labelled `OutOfSample` is out-of-sample in the walk-forward sense.** It is
  computed over a run whose imported `Sample type` was wholly `OOSn` (D8). Whether those trades were
  unseen by the optimiser is `OosWindow`'s question and this slice does not ask it. The two can
  disagree.
- **Not that the sample was chosen.** The slice measures whichever run carries the requested
  segment, whole. It does no date filtering and cannot analyse part of a run — nor a period
  spanning two runs.
- **Not that the segment says anything about the parameter set.** `Segment` and `Kind` are
  independent (D8a): an `InSampleTest` figure may come from a `Deploy` run, and nothing here reports
  whether the parameters were fitted on the sample being measured. That is the question `Kind` and
  the walk-forward export answer, and this slice does not ask it.
- **Not that the figure is current.** With `windowDays = 0` (D4a) it describes the entire sample,
  including a decade-old regime. It is deliberately not the trailing-250-day "risk now" the live
  path reports, and the two numbers are not comparable.
- **Not anything about breach probability, FTMO, the group selector, a random benchmark,
  portfolio-level walk-forward, or cross-service pooled analytics.**

## Spec Dependencies

Requirements this design depends on, with their status as of round 5. **Items 1 and 5–8 are
DISCHARGED — the specs now cover them; do not re-spec them.** Items 2–4 and 9 remain open or are
wording corrections the spec owns. Kept rather than deleted so the reasoning survives archive.

1. ✅ **DISCHARGED — `ExcludedUnscalableCount`** — that `Unscalable` rows (`OriginalSize ≤ 0`) are
   excluded from the dated series and counted, and are **never** contributed as a `0` net. Not an
   internal detail: it changes which trades a published figure was computed over.
2. ⬜ **Heterogeneous-group refusal** (D8) — confirmed as intended. A group whose members' runs
   disagree on `Segment` is refused, naming the disagreeing members; no partial figure is produced.
3. ⬜ **Required segment input and the *no evidence for this segment* state — both survive; do not
   trim D8.** The input is required because without it every figure is silently in-sample, which
   the proposal names as the number most likely to be optimistic. The no-evidence state moves from
   trade granularity to **run** granularity but is not dropped.
4. ⬜ **Run selection (D8a)** — the four outcomes in that table, including that a run with no trades
   yields no segment rather than `Unknown`, that `Min != Max` over a run's trades is refused, and
   that `Kind` never implies `Segment`.
5. ✅ **DISCHARGED — E1 as a scenario** — on the IST fixture VaR99 **reports** while VaR95 is
   **withheld**. It is the whole argument for a per-level gate.
6. ✅ **DISCHARGED — `Unknown` refused, request field nullable** (D8b) — a request of `Unknown` is
   refused, and a run whose trades are `Unknown` is never selected. Distinct from D8a's trade-less
   run, which yields *no segment* and must not be coerced to `Unknown`.
7. ✅ **DISCHARGED — `ExcludedUnscalableCount` reported on `SeriesDensityDto`** (P3). See 9 below:
   this fix disturbed that DTO's provenance and the follow-up is still open.
8. ✅ **DISCHARGED — the subset-pairing scenario is DEFENSIVE** (P1) — marked as a hand-built
   guard, not a pipeline case, because no production path produces a strict subset today and a
   fixture-driven version would pass under a positional zip.
9. ⬜ **`SeriesDensityDto` provenance and the scoped single-derivation assertion** (0.1, opened by
   item 7's fix) — the DTO is mixed-provenance: four gating counts from `Measure`, two trade-level
   counts from the bridge. The single-derivation assertion MUST be scoped to the four; per-field
   provenance MUST be legible in the DTO's doc comment.

**Monthly-gate placement — the sentence to quote:** *the monthly density gate lives INSIDE
`ComputeMonthlyVar`, selected by the required private `PercentilePolicy` parameter (D4b), not in
the backtest adapter; and the spec must assert that the live path's output is bit-identical, NOT
that `ComputeMonthlyVar` is "reused unchanged", because it is not.*

**Residual wording to fix, same class as the above:** `portfolio-monthly-var:32-33` still says the
monthly estimator "MUST be computed … exactly as for real-account ones … regardless of source"
two sentences after specifying the `RequireSupport`/`Unconditional` split. That is true of the
horizon, the history floor and the percentile method, and **false of the gate**. Scope the sentence
to those three rather than to the whole computation.

## Process Note

**Standing instruction for whoever implements this.** Every load-bearing defect found across five
review rounds — the `windowDays: 250` trim, the non-existent `BacktestRun.Segment`, the `rows.Add`
placement, and `Measure`'s inputs — was found by **reading source**. **None was found by comparing
the design and the spec to each other.** Comparing the documents found real *divergences* that had
to be resolved, and that work was not wasted; but every underlying *fact* came from a file.

So: **when a claim about existing code looks obviously true, that is the moment to open the file.**
Agreement between artifacts is not a substitute for reading the code, and neither is agreement
between this document and a reviewer. Each of these read as obviously true before someone looked:
`ResizedTrade` surely carries a P/L; `PortfolioRiskDto` surely accepts a null VaR; `BacktestRun`
surely knows its own segment; a resizer surely skips rows it cannot resize; a density measurement
surely knows how many trades it summarised.

Sequencing the artifacts instead of writing them in parallel removed cross-artifact drift and
replaced it with **faithful copying of a single-artifact error**: P1's false premise originated
here and the spec reproduced it exactly. The file-and-line citation discipline in both documents
exists for that reason and should not be relaxed once they agree.

**And one on the shape of fixes.** P3 was a correct fix that **disturbed a neighbouring invariant**:
routing one field onto a shared DTO changed what that DTO could honestly claim about its own
provenance (0.1). A fix that moves data across a layer boundary should be followed by asking what
the receiving type now asserts that it cannot support.

## Open Questions

- [x] ~~Segment authority~~ — **decided in phase 0: `BacktestSegment`, which is run-level and
      cannot partition a run.** D8 reworked; the filtering pipeline is out of scope.
- [ ] Group identity: is a request-scoped `strategyIds[]` sufficient, or is a saved group needed
      before the UI is usable? (Proposal Q3. The design assumes request-scoped and persists nothing.)
- [ ] Is the band **point estimate** enough without dispersion? (Proposal Q4. It is what a
      deterministic slice can produce; confirm deliberately.)
- [x] ~~Does the monthly VaR95 survive the D4 gate?~~ **Measured: yes, on both fixtures, by ~6×
      the required margin** (IST 1,148/192 ⇒ −400.19; OOST 1,203/189 ⇒ −378.62). No longer a
      failure mode. It became a *testing* problem instead — the withholding branch is unexercised
      by both fixtures, hence the synthetic and injected-defect rows in the test plan.
