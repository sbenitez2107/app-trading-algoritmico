# Exploration — Cost reconciliation and divergence

> **Layer 0 of `openspec/SIMULATOR_ROADMAP.md`.**
>
> ⚠️ **Revised 2026-09-09 after measuring against the database.** The first pass was desk analysis
> over source and specs. Measurement then closed one open risk, confirmed one unconfirmed
> assumption, refuted one recommendation, and surfaced a root cause the desk pass could not see.
> Superseded conclusions are marked rather than deleted, so the reasoning stays auditable.
>
> Measured evidence lives in `.agents/knowledge/imox/MEASURED_Demo_vs_Backtest_Divergence.md`.
> Engram topic: `sdd/cost-reconciliation-divergence/explore`.

## What this change is

Separate the difference between a strategy's **demo** results and its **backtest** results into
components, so later layers can cross the two series honestly. The decomposition is itself
immediately useful as a **strategy filter**.

The academy prescribes this comparison — `07_Backtest_y_Puesta_en_Marcha.md:127-134`, *"la
comparación diferida — herramienta vs mercado"* — and reads a large divergence as a **modelling**
problem rather than a logic one (`07:16`).

## 🔴 The finding that reorders the change

Measured on the one strategy with complete evidence, `WF_7_30_NQ_H_CW_H_O_H1_2.34.172`:

**24 trades open at the exact same minute in both series** — so the strategy logic, signals and
timing are identical. But entry prices differ systematically:

| Month | N | Mean offset | Range |
|---|---|---|---|
| 2026-04 | 5 | **+22.06** | 16.2 – 25.5 |
| 2026-05 | 14 | **+22.55** | 11.4 – 31.9 |
| 2026-06 | 5 | **+40.90** | 38.5 – 44.3 |

Always positive, never near zero, and it **drifts** — nearly doubling between May and June. That
rules out random slippage (which is signed randomly and small) and rules out a fixed configuration
error (which would be constant).

**Root cause, confirmed from the user's SQX configuration**: the Data Manager binds data symbol
`USATECHIDXUSD_M1_UTC02` — Dukascopy's USA100 CFD — to instrument `NDX_DARWINEX`. **The price series
is one broker's product while the point value and costs are another's.** The instrument
specification is correct; the prices are not the ones the account trades.

Consequence, 2026-06-11, one signal, both open 16:50:

| | Entry | Close | Result |
|---|---|---|---|
| Demo | 28928.2 | 29224.0 | **TP, +$147.90** |
| Backtest Deploy | 28888.8 | 28563.1 | **SL, −$195.75** |

The backtest entered 39 points lower, the dip reached its stop, and demo's stop was never touched.

**So layer 0's first job is not cost decomposition. It is detecting whether the two series are
comparable at all.** If the price offset is material, a cost decomposition is separating swap and
commission between two different assets — arithmetic dressed as signal.

## Revised answers

### A — Is `BacktestTrade.Profit` gross or net of commission? **Net. Now settled by measurement.**

Implied point value per trade, `Profit / (|ClosePrice − OpenPrice| × Size)`:

- **Demo: exactly 10.0000** on every sampled trade → demo `Profit` is the pure price move, with all
  costs in the separate `Commission` / `Swap` / `Taxes` columns.
- **Backtest: 9.81 to 12.5**, deviating **above** 10 on losses and **below** on wins — the signature
  of cost embedded inside Profit.

Independently confirmed against the SQX configuration: commission is **Size-based at 5.5 $/lot**. A
0.06-lot trade implied an embedded cost of **$0.33**, and `5.5 × 0.06 = 0.33`. Exact match.

> ⚠️ **Two corrections to the first pass.**
>
> 1. It offered an arithmetic proof — `Balance[i] − Balance[i-1] == Profit[i]` across the fixture.
>    **That check does not discriminate**: the identity holds equally whether commission was folded
>    into Profit or never applied at all. The conclusion was right; that argument for it was not.
> 2. It recommended deferring a three-way split to a KB-rate **estimate**. **Superseded** — the
>    embedded cost is directly **measurable** from price arithmetic, and now verified against the
>    configured model. No estimate is needed.

### B — Aligning the two series

Per-trade matching remains wrong for **composition**: no principled tolerance or tie-break, and an
unmatched trade is indistinguishable from a matcher failure.

> **Revised.** Per-trade pairing is nonetheless **correct for the comparability check**, and it is
> what produced the offset finding. The distinction is the unit of the claim: pairing by exact
> minute to *measure a price offset* is sound because it needs no tolerance rule at all — either the
> minute matches or it does not. Pairing to *merge series* is what fails.

Daily bucketing stays hazardous for the value comparison: `AnalyticsSeries.BuildDailyNetSeries`
buckets on raw `.Date` with **no timezone conversion**, and with 43-47 trades over ~130 calendar days
most days are empty, so a day-boundary error moves a whole trade with nothing to average it out.

**Recommendation: exact-minute pairing for the offset check; whole-window aggregation for the value
comparison. Daily buckets only as optional visualization, never as a measured figure.**

### C — The decomposition: four components, not two

> **Superseded.** The first pass proposed a two-way split (swap exact, residual everything else).
> Measurement showed the residual would have hidden two distinct and much larger effects.

| Component | How it is obtained |
|---|---|
| **Price-series offset** | exact-minute pairing; **runs first and gates the rest** |
| **Swap** | `Σ demo(Swap)` — exact. The backtest does not model it, by academy decision |
| **Embedded backtest cost** | measurable via the implied point-value deviation, verified against the configured model |
| **Trade-set difference** | the two series did not take the same trades — 47 demo against 42 Deploy and 39 Evaluation in the same window. Conceptually distinct from cost; a residual mixing "traded better" with "traded more often" informs nothing |
| **Execution residual** | what remains after the above |

### D — The threshold problem — unchanged

The KB says a large divergence indicates a modelling problem and publishes **no number**;
`INDEX.md` forbids inventing domain criteria.

**Report-only, ranked, no pass/fail.** Nothing has to be invented. Any future threshold must be
**elicited from the user and persisted as configuration**, never a code constant — the same
discipline already applied to every guardrail number.

The offset check strengthens this: it yields an **actionable cause** rather than a symptom score, so
it is useful without any threshold at all.

### E — Is the walk-forward export needed? **No, unchanged.**

`StrategyWalkForwardExport` carries no trades and no cost data, only `OosFromDate` and verbatim
parameter text. Useful here only as passive confirmation that a window is genuinely out-of-sample.

### F — Placement and sizing

A new Application DTO mirroring the sealed/factory pattern of `BacktestNetSeries`, an interface, a
static stateless calculator in `Infrastructure/Services` beside `PortfolioAnalyticsCalculator`, and
one read endpoint.

> **Revised upward.** The first pass estimated ~300-450 lines for a two-way split. Four components
> plus the offset check will exceed that. Sizing is a proposal-phase question; the offset check is
> the piece that must ship first if the change is split.

## Risks now closed by measurement

| First-pass risk | Outcome |
|---|---|
| Sizing mismatch between demo and backtest, unmeasured | **Closed.** Demo avg .0694, Evaluation avg .0692, Deploy avg .0769 — same scale, no normalization step needed |
| `NQ` → KB commission-rate symbol mapping unconfirmed | **Confirmed by data**: demo `ndx` ↔ backtest `USATECHIDXUSD_M1_UTC02` |
| Backtest commission is an estimate, not a measurement | **Superseded** — it is measurable, and verified |

## Risks that remain

- **A second, independent modelling gap**: the backtest runs at precision *"1 minute data tick"* —
  M1 bars, not real ticks. The intrabar path is inferred from the minute's OHLC, so when SL and TP
  are both reachable inside one bar the order they are touched is an **assumption**. This produces
  the same TP-versus-SL symptom and would persist even if the price series matched.
- **A seasonal timezone risk, ruled out as this finding's cause.** The Data Manager records
  timezone **UTC+02 Jerusalem, DST: Yes**, and Israeli DST rules differ from the US rules Darwinex
  follows. It is not the cause here — the offset is systematically positive across all 24 trades,
  whereas a timezone error would pair different market moments and produce random signs. It remains
  a risk at the DST transition boundaries in late March and late October/November.
- **n = 1 strategy.** Directionally useful, not generalizable, and **no threshold may be inferred
  from it**.
- **Nothing here establishes that demo outperforms the backtest.** 24 paired signals cannot support
  a direction. The measured fact is that the paths differ systematically and that on this sample the
  difference favoured demo.
- **A mixed MT4/MT5 population is coming** (October 2026, SQX v144). MT4 books commission 100% at
  entry, MT5 splits it 50/50 entry and exit — so the evidence must carry the platform from the
  start, or a daily-resolution measurement will attribute cost to the wrong days.

## Carry into the proposal

1. The offset check ships first and gates the rest. Decide whether it is its own slice.
2. The trade-set difference needs its own treatment, not absorption into a residual.
3. Carry the source platform (MT4/MT5) on the evidence now, ahead of the October migration.
4. The change name still says "cost reconciliation", but the primary deliverable is now
   **comparability detection**. Keeping the name is fine; the proposal should lead with the change
   of emphasis so a later reader is not misled by the folder.
