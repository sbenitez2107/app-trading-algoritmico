# Funding-Service Simulator — Roadmap

> **Type**: planning artifact spanning several SDD changes. Not a spec, not doctrine.
> **Written** 2026-09-09 from three parallel read-only explorations plus orchestrator verification.
> Every claim about existing code carries a `file:line`. Every claim about vendor rules cites the
> rulebook. Values the vendors do not publish are marked `NOT FOUND` and are never filled in.

## What the simulator is for

From ~**120 strategies** currently running on one Darwinex demo account (100k, MM 2 decimals,
0.2% risk per trade), produce **candidate groups** that qualify for a target
`(service, mode/stage, capital)`, respecting that target's rule engine and risk.

Targets in scope: **FTMO** (Phase 1 / Phase 2 / Funded, 2-Step **Swing**), **Darwinex Zero**
(Calibration / DarwinIA), **Axi Select** (Seed → Pro M). Own capital is deferred to ~2027 by
decision.

The knowledge base already recorded this requirement, verbatim at `SERVICE_FTMO.md:537`:

> "**The simulator needs a `mode` dimension**, not just a `service` one."

## What exists, verified

| Asset | Location | Note |
|---|---|---|
| Demo trades | `Domain/Entities/StrategyTrade.cs` | individual, **cost-inclusive** net = `Profit + Commission + Swap + Taxes` (`AnalyticsSeries.cs:47`) |
| Backtest trades | `Domain/Entities/BacktestTrade.cs` | individual, `SampleTypeRaw` IS/OOS, `RealizedRisk` = `|MAE|` on SL closes |
| Walk-forward | `StrategyWalkForwardExport` + `WalkForwardWindow` | **period aggregates only, no trades**; stores `DeployParameters` / `EvaluationParameters` |
| Risk normalization + resizing | `TradeRiskNormalizer`, `TradeResizer`, `LotGrid` | **implemented and wired** via `BacktestReadService.cs:231-244` |
| Ad-hoc group evaluation | `GET api/backtests/portfolio-risk` | evaluates a group named in query params with **no `Portfolio` row** |
| Guardrail vocabulary | `GuardrailKind`, `FtmoProduct`, `BreachBasis`, `RulebookMismatch`, `FundingStageLimit` | from the archived `funding-guardrail-shape` |

## The five verified blockers

1. **One guardrail row per broker string.** `BrokerRiskLimitsConfiguration.cs:35` —
   `HasIndex(x => x.Broker).IsUnique()`, resolved by name on write (`RiskLimitsService.cs:59-61`)
   and read (`PortfolioService.cs:375-381`). A `(service, stage, capital)` target **has no key**.
   This is why `FtmoProduct` became a column on that single row.
2. **Demo trades have no risk basis.** `StrategyTrade` carries no MAE and no realized risk, and the
   MT statement provides none. `TradeRiskNormalizer.Estimate` reads `RealizedRisk` and
   `CloseType == "SL"` off `BacktestTrade`. **The demo half cannot be rescaled by any existing path.**
3. **Exactly two run slots per strategy.** `BacktestRunConfiguration.cs:18` —
   `HasIndex(x => new { x.StrategyId, x.Kind }).IsUnique()` with `Kind ∈ {Deploy, Evaluation}`.
   "Last run and previous run" *are* those two slots. Re-import replaces; there is no history.
   And the two cover essentially **the same calendar** — they differ in parameters, not period.
4. **Backtest analytics are VaR and correlation only.** `PortfolioAnalyticsCalculator` exposes
   `BacktestNetSeries` overloads at `:528` (VaR) and `:582` (correlation). Max drawdown, monthly
   returns and the equity curve are reachable **only** through `PortfolioMemberInput`, which holds
   `IReadOnlyList<StrategyTrade>` — the live path. A backtest group yields no drawdown today.
5. **Worst-case simultaneous risk is computed nowhere.** The academy's group criterion is 1% total
   on the worst simultaneous case (`08_Servicios_de_Fondeo.md:183-186`), and nothing computes the
   quantity it constrains. `AnalyticsSeries.ComputeExposure` deliberately **merges** overlapping
   intervals, so it measures time-in-market breadth, not depth of concurrency.

## The simplification that sets the order

On a **2-Step Swing** account the breach rules **do not change by stage**:

| | Phase 1 | Phase 2 | Funded |
|---|---|---|---|
| Max daily loss | 5% | 5% | **5%** |
| Max total loss | 10% static | 10% static | **10% static** |
| Profit target | 10% | 5% | none |
| Weekend / news | free | free | **free (Swing)** |

Source: `SERVICE_FTMO.md:17`, `:31`, `:36-37`, `:81-83`, `:207-208`.

Only the **profit target** varies. The funded-Standard restrictions — weekend-flat and the news
window, which would structurally disqualify an H4 swing portfolio — **do not apply to Swing**.

**Therefore an FTMO breach simulation can be built on the current model, before the target-key
refactor.** The stage axis becomes load-bearing when Axi arrives (~November 2026), because Axi has
six stages with genuinely different limits and a per-stage *consequence* axis (quarantine applies at
Incubation and Acceleration, not at Seed or Pro; a third quarantine returns you to Seed —
`SERVICE_Axi_Select.md:344-347`).

## Series composition — superseded approach

An earlier framing offered three options: OOS tail only (defensible but ~30 active days, under the
90-day floor), Evaluation full span (long but contaminated before `OosFromDate`), or a splice.

**That framing is superseded.** The chosen approach is the one the academy already prescribes
(`07_Backtest_y_Puesta_en_Marcha.md:127-134`):

> "**La comparación diferida — herramienta vs mercado.** Cuando pasa 1 año y 3 meses, vas a comparar
> las estrategias que pusiste en el mercado contra lo que saca SQX."

- **Precedence is temporal, not per-trade.** Demo wins over the period it covers; the backtest fills
  the period before. Per-trade matching is fragile: the same signal opens at a different time and
  price under slippage and latency, a tolerance window and tie-break are needed, and a demo trade
  with no counterpart is indistinguishable from a matcher failure.
- **The overlap is used to MEASURE, not to merge.** A weighted average of a real and a simulated
  series describes neither. Where the two cover the same period, the comparison *is* the calibration
  of how much the backtest overstates; that measurement is then applied to the unobservable part.
- **Divergence is a strategy filter, not only a correction.** `07:16` — *"divergen mucho sobre la
  misma estrategia, el problema está en el modelado, no en la lógica."*

### Why cost reconciliation must come first

The SQX trade-list export has **exactly 16 columns and none for commission or swap**
(`sqx-backtest-import/spec.md:75`). The instrument configuration does carry both
(`01_SQX_Data.md:139`) — but **swap is deliberately not configured**, per `01_SQX_Data.md:181-191`:

1. Technical — in v136 it is **bugged**, added with a positive sign.
2. Methodological, and the one that matters — applying today's inflated swaps to past data injects
   noise, and the broker benefits from that. The prescribed treatment is a **Retester WhatIf
   crosscheck**, not a simulator-side adjustment.

So divergence between demo and backtest carries a **systematic, known, one-directional component:
swap.** Demo pays it; the backtest does not model it. For an H4 swing book that is not background
noise — `SERVICE_FTMO.md:112-120` records that swap sits inside the equity both drawdown checks
read, with triple swap Wednesday to Thursday, and that overnight financing "can push you toward a
breach directly."

Crossing the series without separating swap first measures **our own missing column**, not the
market. The decomposition has three parts, each separately measurable:

| Component | Origin | How it is isolated |
|---|---|---|
| Swap | not modelled by the backtest, by decision | `StrategyTrade.Swap`, already held |
| Commission | applied internally by the backtest | compare against `StrategyTrade.Commission` |
| Execution | slippage, real spread, latency | **the residual** after the two above |

The residual is the signal worth having.

## The real bottleneck is data loading, not code

Established with the user 2026-09-09:

- **Lot grid is 2 decimals, always**, matching real/live. This is the good case: the codebase
  measured 0.3% of trades pinned at the minimum lot on a 2-decimal export against **33.8%** on a
  1-decimal one, so rescaling down to a 10k or 1k account is reliable rather than systematically
  optimistic. It also settles the KB's internal contradiction in practice — the table's
  `Size decimals = 1` (`06:51`) describes the **Builder** phase, not AlgoWizard testing, per the
  phase rule at `06:88-91`.
- **Only ONE strategy has its backtest trade list loaded.** Not 120.

Import surface, verified: backtest runs import **per strategy** through
`StrategyBacktestsController`; demo trades import **per trading account** through
`TradingAccountsController:130`, attributed by `(TradingAccountId, MagicNumber)`. **There is no bulk
import anywhere** — one `IFormFile` per request across the whole WebAPI.

So the two sides of the evidence are asymmetric: one account import covers all 120 strategies on the
demo side, while the backtest side is 1 of 120 and loading the rest is up to 238 manual uploads
(two run slots each) plus the walk-forward exports.

**Consequence for sequencing.** Layer 0 needs both sides *for the same strategy*, so it is gated at
n=1 today — and that is the right place to build it. Measuring divergence on one strategy tells you
whether the measurement is worth scaling **before** spending hours loading 238 files. It is also the
ideal strict-TDD position: one real measured case to pin behaviour against, which is how
`MEASURED_FTMO_Demo_Baseline.md` was produced.

A **bulk import** becomes a strong candidate for its own small change once layer 0 proves the
measurement matters. Design constraint if it is built: the current importer validates hard per file
(single sample type per file, whole-file rejection on a missing column). A bulk path MUST preserve
per-file refusal and report it, never silently skip a failure.

## Layers, in dependency order

| # | Layer | Contents | Blocks |
|---|---|---|---|
| **0** | **Cost reconciliation and divergence** | Separate swap / commission / execution residual per strategy. Emit a per-strategy divergence decomposition. Immediately usable as a **strategy filter** per `07:16`. | 1, and every later crossing |
| **1** | **FTMO 2-Step Swing breach simulation** | Daily realized loss vs 5%, cumulative vs 10% static, on the rescaled series. Buildable on the current one-row-per-broker model thanks to Swing's stage-invariance. Claim boundary: *"would not have breached on closed-trade daily aggregates"*, **never** *"would have passed"* — FTMO reads equity including unrealised P&L continuously. | — |
| **2** | **Analytics parity + the missing quantity** | `BacktestNetSeries` overloads for max drawdown, monthly returns and equity curve. Worst-case simultaneous risk and max concurrent positions via a sweep line over `(OpenTime, CloseTime)`. | 4, 6 |
| **3** | **The target key** | `(service, mode/stage, capital)` replacing one-row-per-broker; real per-stage tables. Axi's six stages with their consequence axis. **Deadline: before Axi entry, ~November 2026.** | Axi, 4 |
| **4** | **Objective functions** | The parked `funding-objective-functions`, **re-keyed from `Guid portfolioId` to a member list** — a candidate group has no row. Darwinex Rating proxy (relative ranking only; scale and points mapping are `NOT FOUND`), FTMO pass/breach, Axi named proxy. | 6 |
| **5** | **Eligibility rule class** | Predicates over strategy structure, not thresholds on a number: flat-by-deadline, blackout windows, instrument eligibility, archetype bans (FTMO prohibits martingale and grid by name), capacity caps, and counting rules that redefine what a trade *is* (Axi's unique-trade rule; Darwinex's risk-equivalent decision). A group is ineligible if **any** member violates — it does not aggregate like a loss limit. Already named as a future capability at `funding-guardrails/spec.md:12-14`. | 6 |
| **6** | **Generation and search** | Incremental additive accumulator (member daily series are additive — `EffectiveWeights` returns raw weights verbatim, `PortfolioAnalyticsCalculator.cs:1004-1010`), then beam search over a monotone conflict-graph prefilter, with **exhaustive enumeration whenever k ≤ 4**. | — |

## Why the parked change is not step one

`funding-objective-functions` was drafted as the first step. Three verified reasons it is not:

1. Its contract is `ScoreAsync(Guid portfolioId, …)`. A candidate group has no `Portfolio` row, so a
   search cannot call it without persisting every candidate.
2. It needs to know which stage it scores against, and that key does not exist until layer 3.
3. Its Darwinex component needs a windowed max drawdown, and **no drawdown exists over
   `BacktestNetSeries`** (blocker 4).

Its analysis is sound and its three resolved decisions stand. It is parked, not discarded.

## The search space, and the asymmetry that rescues it

Pool 120, academy group size 5-15: **Σ ≈ 5.5 × 10¹⁸** subsets, dominated by k=14 and k=15 at 98%.
At an optimistic 1 µs per candidate that is ~174,000 years; real cost is milliseconds.

But the academy prescribes **1-2 strategies per group for props and Axi Select**, not 15
(`09_AlgoWizard_y_QA4.md:60-62`). `C(120,1..4) ≈ 8.5 × 10⁶` — **exhaustive enumeration is genuinely
viable** there. Only the Darwinex-at-15 case is intractable.

**These are two different problems, and the tractable one is the one needed first.**

Two findings that make the inner loop affordable:
- Member daily series are **additive** (raw weights, never renormalized), so a maintained
  accumulator turns an evaluation from ~10⁵-10⁶ operations into ~10³-10⁴.
- The **Intersection** correlation door (`:604`, `:480-488`) yields a pair-independent coefficient,
  so all C(120,2) = 7,140 pairs precompute once. The live **Union** door (`:432`, `:441-452`) makes
  the coefficient group-dependent and uncacheable. Worth ~three orders of magnitude.

## Constraints that must be respected, not worked around

- `backtest-portfolio-analytics/spec.md:253-255` forbids **that capability** from iterating or
  ranking candidate groups, and `:251-252` forbids any RNG or seed. The generator is a **new**
  capability that composes single-group evaluations; it must never loop inside the one-group
  contract. This also rules out a genetic search as the primary driver — it needs an RNG.
- `GroupRiskAnalysisRequest.cs:8-12` keeps multi-group ranking deliberately inexpressible, pinned by
  `BacktestPortfolioRiskTripwireTests`. **The generator crosses that tripwire deliberately or not at
  all.**
- Any ranked output must be scored against a **null model**. `09:12-33` is explicit that ranking N
  candidates measures how much you searched; a ranked list without a shuffled-returns benchmark
  ships the exact overoptimization defect QA4 documents.
- `Portfolio` contradicts itself in adjacent comments: the class doc says strategies may be drawn
  from **any** account or broker (`Portfolio.cs:7`), the `Broker` property says members **must** be
  on this broker (`:18-22`). A generator proposing "these strategies for Darwinex Zero" may produce
  something the entity refuses to hold. Resolve before layer 3.

## The strategy universe — measured, not estimated

Queried against the local dev DB `AppTA` (Docker `mssql-db`, localhost:1433) on 2026-09-09 with the
user's explicit authorization. SELECT only.

| Account | Broker | Strategies |
|---|---|---|
| **SBDEMO2** | Darwinex | **123** ← the source of truth |
| FTMO-Demo2 | FTMO | 17 (expired trial, discarded by the user) |

`Strategies` carries `TradingAccountId` and `MagicNumber` **directly**, so a strategy belongs to
exactly one account and the same logical strategy on two accounts is two rows. There is no
`AccountStrategies` link table despite the spec of that name.

**The pool is 123, not 140.** An earlier "duplicate strategy names" observation was mostly an
artifact of not filtering by account.

One genuine duplicate survives, the only one across all 140:
`WF_7_30_XAUUSD_H1_EMA_CW_O_SO_TEMA_3.89.219` appears twice inside FTMO-Demo2, once with
`MagicNumber 3892193` and once with **`MagicNumber NULL`**. Demo trades are attributed by
`(TradingAccountId, MagicNumber)`, so the NULL row can never receive trades — an orphan. It sits in
the discarded account and contaminates nothing. Flagged, not deleted.

### The layer-0 case, measured

`WF_7_30_NQ_H_CW_H_O_H1_2.34.172` (single underscores) on SBDEMO2 is the one strategy with complete
evidence:

| | Period | Trades |
|---|---|---|
| Demo | 2026-04-21 → 2026-08-28 | 47 |
| Backtest Deploy | 2016-01-12 → 2026-08-31 | 726 |
| Backtest Evaluation | 2016-01-12 → 2026-08-31 | 680 |
| **Overlap inside the demo period** | | **43 Deploy / 40 Evaluation** |

Both slots span the **same calendar**, confirming they differ in parameters rather than period —
concatenating them would double-count a decade. Walk-forward `OosFromDate = 2025-03-19`, so the
genuine OOS tail is ~17 months.

**Cost decomposition of the 47 demo trades** — the layer-0 input:

| | |
|---|---|
| Gross profit | +$937.13 |
| Commission | −$18.05 |
| Swap | −$21.57 |
| **Real net** | **+$897.51** |

Total cost is **4.2% of gross**, and **swap is more than half of it while touching only 6 of 47
trades**. Since the backtest does not model swap by academy decision, uncorrected divergence between
the two series would be dominated by it — now measured rather than assumed.

## Planned change of inputs — October 2026

Stated by the user 2026-09-09. Not immediate, but it changes the simulator's inputs, so it is
recorded before it lands rather than discovered afterwards.

After the 1-a-1 mentoring in **October 2026** the user will:
1. Move to **SQX v144**, and
2. Build **all new strategies for MetaTrader 5**.

The existing 123 strategies on SBDEMO2 are MT4. Nothing says the old ones are rebuilt, so **plan for
a mixed MT4/MT5 population**, not a clean cutover.

### Consequences the simulator must absorb

**The swap doctrine may be reopened.** `01_SQX_Data.md:181-191` gives two reasons swap is not
configured: a **v136-specific bug** (it is added with a positive sign) and the methodological
objection to applying today's swaps to past data. The first reason is a version defect and **may be
fixed in v144**; the second stands regardless. If v144 fixes it, that is a doctrine decision for the
academy, not a code change — but the layer-0 swap component would then have a modelled counterpart
on the backtest side for new strategies and none for old ones.

**MT4 and MT5 book commission differently, and the codebase already records it.** `MT4 => Roundtrip
(100% applied at the entry)` versus `MT5 => 50% applied at the entry and 50% at the exit`. Same
round-trip cost, different timing. Any daily-resolution measurement — the FTMO daily-loss
simulation, the divergence decomposition — will attribute cost to different days depending on the
platform. A mixed population means the evidence layer must know which platform produced each run.

**MT5 unlocks destinations that are currently impossible.** On Darwinex Zero, crypto, futures and US
stocks/ETFs are **MT5-only**. The user's deferred second Darwinex account with BTC becomes possible
only on MT5. So the October migration is a prerequisite for part of the plan already recorded.

**Other platform differences already documented**: MT5 offers 21 timeframes against MT4's 9, and 6
pending-order types against 4. Hedging mode is forced on both — netting is unavailable — so that axis
does not change.

### What this does NOT change

The layer ordering stands. Layer 0 measures divergence between whatever evidence exists, and a
platform column is an attribute of a run rather than a new layer. The correct response is to **carry
the platform on the evidence** from the start, so a mixed population is expressible on the day it
appears rather than retrofitted.

### Immediate need, separate from October

The user needs **Darwinex price data for backtests now**. The measurement in
`.agents/knowledge/imox/MEASURED_Demo_vs_Backtest_Divergence.md` shows what the current Dukascopy
fallback costs: a systematic 22-41 point offset that drifts, enough to turn a take-profit into a
stop-loss on the same signal. `01_SQX_Data.md:72` records that Darwinex tick data requires the paid
SQX version, and `07:34` records the academy's own preference — *"Mayor precisión: tick data de
Darwinex. Si no, Dukascopy."*

This is not a blocker for layer 0 — measuring the offset is precisely what layer 0 does, and it works
on the data already loaded. It is a blocker for **trusting** any backtest-derived number about an
instrument the strategy will actually trade.

## Open items needing a human answer

1. **Which lot grid produced the 120 AlgoWizard exports** — 1 or 2 decimals. The KB is internally
   inconsistent: its table says `Size decimals = 1` for the large account (`06:51`), its own MT4
   snippet says 2 (`:61`), and its text says use 2 to fix the SL value (`:84`). The phase rule at
   `:88-91` (Builder 1 / final AlgoWizard 2) is the likely reconciliation but is not stated as such.
   **This changes every rescaling number** — Â, the min-lot pinned fraction, and every resize outcome.
2. **Whether the FTMO no-shared-strategies constraint is real** (`10_Mentoria_1a1_PENDIENTE.md:118-122`,
   flagged unconfirmed). If it is, simultaneous targets become set packing over one shared pool
   rather than independent searches — it changes the algorithm choice at layer 6.
3. **Portfolio lifecycle** (`10:110-113`): every module covers group *construction*, none its
   *maintenance*. A generator answers construction only; re-generation cadence has no doctrine.
4. **Should the strategy pool be parameterizable by account?** Raised by the user 2026-09-09 and
   deliberately deferred. Today the universe would be hardcoded to SBDEMO2's 123 strategies. The
   question is whether the simulator should let the user scope the pool — choose an account, or
   combine accounts — rather than assuming one. It bears directly on the search layer, whose input
   *is* the pool definition, so it must be settled before layer 6. It also interacts with the
   `Portfolio.Broker` contradiction above: if the pool can span accounts, a proposed group may hold
   members from several brokers, which the entity's `Broker` property currently forbids.

## Measured precedent

`MEASURED_FTMO_Demo_Baseline.md` is the model for how layer 0 should report: real measured data, 48
closed trades over 11 trading days, with an **explicit retraction** where the first framing was wrong.

⚠️ But its EAs came **straight from the Builder** with no Retester or Optimizer. It calibrates
modelling divergence usefully; it must **not** be used to infer how much a strategy that did pass the
pipeline diverges — that bias runs against the user.
