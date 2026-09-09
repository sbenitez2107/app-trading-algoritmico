# IMOX Trading Academy — Knowledge Base Index

This is the **entry point** for all domain knowledge from the IMOX Algorithmic Trading Academy (9-class program).

Agents MUST read this file first before making any domain-related decision. Then read only the documents relevant to the task — do NOT read all documents indiscriminately.

---

## Document Registry

| File | Type | Description |
|------|------|-------------|
| [00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md) | `concept` + `service-rules` | Module 0: the four capital destinations (own capital, Axi Select, Darwinex Zero, prop firms), target assets and their timeframes, portfolio composition, the **timeframe x service eligibility matrix** (H4 is best for own capital and Darwinex, impossible for Axi by score, ruinous for props by swap), and the central doctrine: **one logic per asset, re-parameterised per service through Money Management and risk — never a strategy per broker** |
| [01_SQX_Data.md](01_SQX_Data.md) | `sqx-config` + `provider-data` | Module 1 (mentoria 11/02/2026): market doctrine (no counter-trend, CFDs decentralised so no M1/M15, brokers inflate swap precisely because swing is the easier edge), Darwinex vs Axi, **Darwinex is the reference instrument configuration and every system is adapted to other brokers afterwards**, data rules (10 years, M1 enough to mine, tick only for M5/M15 and added last in the Optimizer, UTC+2), the SQX instrument fields with the long explanation of **Point Value in $** and the spread comma shift, the **10-instrument Darwinex configuration table (dated, provider data)**, and why **swap is not configured** — bugged in v136 AND applying today inflated swaps to past data injects noise; use a Retester WhatIf crosscheck instead |
| [01_Fundamentos_y_Data.md](01_Fundamentos_y_Data.md) | `concept` + `sqx-config` | IMOX philosophy (trend-only, swing trading, no grail), data management (10yr history, M1/Tick), instrument configuration in SQX v136 (Point Value, Pip Size, spread rules, timezone UTC+2), swap handling per phase, broker selection (Darwinex / Axi) |
| [02_SQX_Builder.md](02_SQX_Builder.md) | `sqx-config` + `selection-criteria` | Module 2 (mentorias 18-19/02/2026): the **four Building Block sets** (BB1 generalist, BB2 trend, BB3 volatility, BB4 exhaustion) with their signals, indicators, entry blocks and operators; the five errors that kill strategies; the **complete Builder walkthrough** — What to Build, ATR SL/PT ranges, genetic options, trading options, IS/ISV/OOS split, Money Management, ranking filters and the databank selection procedure; and the **timeframe / asset-class threshold variants** (H4 uses 150 trades not 200; currencies lower WF Ret/DD to 5 and raise stagnation to 500; Axi Select small accounts use a pips model, not ATR). Core principle: **less strict in Genetic Options, more strict in Ranking — high precision IS overfitting** |
| [02_Mineria_y_Genetica.md](02_Mineria_y_Genetica.md) | `workflow` + `selection-criteria` | Builder phase: IS/OOS strategy, genetic algorithm mechanics (islands, population, crossover/mutation), "What to Build" config, ATR-based SL/TP, key KPIs (Sharpe >1.2, Ret/DD >8), the 5 fatal errors, and criteria for selecting ~300-400 strategies for Retester |
| [02_sqx_metrics.md](02_sqx_metrics.md) | `concept` | Complete SQX metrics dictionary: profit/return (Total Profit, CAGR, Yearly AVG), risk/efficiency (Sharpe Ratio, Profit Factor, Return/DD, Drawdown), quality (SQN, SQN Score), statistics (Z-Score, Expectancy, Exposure), stagnation (Stability R²), symmetry metrics |
| [02_Manual_BuildingBlocks.pdf](02_Manual_BuildingBlocks.pdf) | `sqx-config` | Full SQX Building Blocks manual: Glossary, Signals (trend, momentum, volatility, volume, price action), Indicators (all categories with parameters and examples), Entry blocks (Stop/Limit/Market), Logical operators. Reference for understanding SQX strategy components |
| [02_BuilderTheory.odt](02_BuilderTheory.odt) | `sqx-config` | Builder theory document (ODT format — not directly readable by agents; ask user to convert to markdown if needed) |
| [03_SQX_Retester.md](03_SQX_Retester.md) | `workflow` + `selection-criteria` | Module 3 (mentoria 25/02/2026): the **anti-overfitting arsenal**. Preconditions (tick precision, Builder custom filters OFF — the crosschecks are already strict enough), the three crosscheck tiers and which ones IMOX runs, What-If (the only place swap enters the analysis), both Monte Carlos (trades manipulation: 1000 sims, no Full Sample + Exact, 10% skip; retest methods: tick randomisation, spread 1-3, parameters 10%/20%) with their shared filters, **Higher Backtest Precision** (deliberately looser filters — Win% > 35% not 40% — because raising precision LOWERS strategy quality, plus per-asset Ret/DD: XAUUSD 10, NQ 8, DAX/SP500/currencies 5), Sequential Optimization (30/30/40, 100% stability, 15% stable area, 5% fitness range), and **SPP / System Parameter Permutation** — the overfitting detector: 15000 permutations, > 95% profitable optimizations, < 5 sign changes, Median within 70-130% of Higher |
| [03_Validacion_y_Stress_Test.md](03_Validacion_y_Stress_Test.md) | ⚠️ **SUPERSEDED** | Condensed earlier version of modules 2, 3 and 4. **Do not read it to decide anything** — use `02_SQX_Builder.md`, `03_SQX_Retester.md` and `04_SQX_Optimizer.md` instead. Kept only for traceability: an archived design artifact cites it |
| [04_SQX_Optimizer.md](04_SQX_Optimizer.md) | `workflow` + `selection-criteria` | Module 4: the **last filtering stage** and the strongest evidence of stability. Base config (All strategies in databank, Recommended parameters, 15000 max optimizations, 20/20/**step 8** — a high step is overfitting), Simple Optimization, **Walk-Forward theory** (Floating vs Fixed — Floating is chosen precisely because it produces **harder** windows; the hidden last year is the OOS and its Net Profit must be read against what the market actually did that year), **WFO config** (Exact IS / Exact OOS, Floating, OOS 30%, 10 runs) with the PASSED gate **Robustness score > 80%** plus six ranking conditions, **WF Matrix** (OOS 20-36 step 2, runs 5-10 step 1; passes on a contiguous **3x3 area with 7 of 9 cells >= 80%** — a stability zone, not a scattered set of luck), and the **WF Special metrics dictionary**. Doctrine: **never keep the best strategy, keep the most stable and robust one** |
| [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) | `workflow` + `selection-criteria` | Module 5 (+ mentoria 11/03/2026): **the most operational document in this KB** — modules 2-4 produce candidates, this one **chooses** them. Contains the ordered **7-step decision procedure** for taking a strategy to Backtest or Demo (SPP → WF Matrix green zones → stable area cross-checked on the 3D Surface, **explicitly NOT the highest Ret/DD** → all years positive → **group by identical price/Entry parameters and keep 2** → KPI gate → low Avg. Trades Month). Also: WFM result reading and the reoptimisation cadence it implies, Overview / trade list / Equity Chart analysis (stagnation and max drawdown should sit **before 2022**), the **50% rule** (original vs optimised, and between matrix blocks), why STR correlates with trade count and why that is a trap, and the Demo exit condition: **not a duration — an event**, seeing the strategy fall into drawdown and beat itself. ⚠️ Documents **three unresolved discrepancies** in its own final section |
| [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) | `concept` + `sqx-config` | Module 6 (+ mentoria 18/03/2026): the **MM of the service** that module 0's doctrine calls for. Both models (Fixed Size for pips/% builds, **Fixed Amount/ATR** for everything else — best performance), the **per-account-size configuration table** (large: capital 100k, risked 200, decimals 1, no-MM lot 0.1 · small: capital 1k, risked 10, decimals 2, no-MM lot 0.01), the **decimals trap** (1 decimal leaves the SL floating at 158/200/300 and cannot express a microlot; 2 decimals nails it — Builder uses 1 as an anti-false-precision filter, final AlgoWizard testing uses 2), `Maximum lots` with its worked example, the **`Size if no MM` silent fallback** (the one configuration error the module names), MM backtesting in AlgoWizard, and the risk doctrine: **start ultra conservative and raise later**, 1-2 strategies max on small capital, RR > 2-3, diversify across accounts / strategy types / **RR**. ⚠️ Corrects a naive reading: **the 6.5% VaR is Darwinex's standard, not an academy limit** — own capital can run 10-15% and readjust after a good run |
| [06_Money Management.md](06_Money%20Management.md) | ⚠️ **SUPERSEDED** | Earlier summary covering **only the large-account column**. Use `06_Gestion_de_Riesgo.md`. Filename kept because two archived designs and the Darwinex rulebook cite it; its claims remain correct for large accounts — the defect was scope |
| [07_Backtest_y_Puesta_en_Marcha.md](07_Backtest_y_Puesta_en_Marcha.md) | `workflow` + `provider-data` | Module 7 (+ mentorias 04/12/2025 and 01/04/2026): the first **infrastructure** module. The MT4 confirmation backtest — **optional, and it validates the tool, not the strategy**: it checks that SQX and MT4 agree on the same logic (MT4 has no tick data built in, MT5 does; download the **last 2 years** of only the needed timeframe, locally; Darwinex tick data first, else Dukascopy). **VPS capacity rules**: ~6-7 MT4 instances per box, **max 20 EAs per instance**, one instance per asset, many instances may share one demo account (the transcript's *"Darwinex allows up to 10"* is ⚠️ **UNVERIFIED** — the 2026-09-08 documentation sweep found **no published demo-account quota**; see `SERVICE_Darwinex_Zero.md`, and do not plan VPS capacity around the figure 10). **MM in demo/live is always 2 decimals** — confirms module 6. The **Demo filter**: leave EAs running and wait for a bad, lateral or bearish period to see if they recover — **a strategy must have had a fall before going live**. Live hygiene: save EA presets, deploy while the market is closed. And the deferred loop: after **1 year 3 months**, compare live results against what SQX predicted |
| [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) | `service-rules` + **`provider-data`** | Module 8 (+ mentoria 02/04/2026): **the per-service rulebook** and the direct input for any per-service simulator. ⚠️ **Almost entirely provider data dated 04/2026 and the most volatile document in this KB — re-verify before using any number.** The academy's critical framing (funding firms are a **liquidity instrument, not the goal**; profits go to own capital; the economics assume losing 3-4 challenges to win one). **Axi Select**: gold is banned and **eligibility depends on the phase** (allowed in pre-training/F1/F2, banned on large accounts), filling trades gets you banned so **currencies at microlots are the legitimate way to meet the trade count**, Phase 1 is a wildcard with a stats reset and a 90-day reset score, and it is a **dual-objective race — trade count AND 7% within 60 days**, not a return maximisation. **Portfolio risk rule**: 1% total, e.g. 0.2 each across gold/NQ/DAX/a currency — sized on the **worst simultaneous case**. FTMO: 20k-50k challenges. Plus the per-destination strategy profile split (few trades for own capital and Darwinex, many trades for challenges) |
| [09_AlgoWizard_y_QA4.md](09_AlgoWizard_y_QA4.md) | `workflow` + `concept` | Module 9 (+ mentoria 08/04/2026), the last group module. **The one that overlaps most with this application's portfolio simulator — read it before designing any group ranking.** Carries the academy's own critique of the tool that does this job: **QA4 overfits portfolios and they "always win", a false impression**; *"the only thing QA4 is good for is the Monte Carlo analysis"*. **AlgoWizard**: edit and fast-backtest (SL/TP via long-entry pips, MM configs, any timeframe including the hidden year; a known **SQX v136 bug** where Source Code does not reflect edited params) and the fact that **%DD is a property of (strategy, balance)** — the same strategy shows 92% DD on 1k and 12% on 10k. **QA4**: Analyze / Monte Carlo / Portfolio Master / Money Management, MM is edited in AlgoWizard and only then imported, Portfolio Master builds by **decorrelation of profit/loss by day**, and Monte Carlo tolerances are **2x the original DD at 95% and 3-4x at 100%**. ⚠️ **Group sizes here are 5-15, never 20-30**, and the two sections the material flags as critical (Portfolio Master detail, Correlation) are **not transcribed** |
| [10_Mentoria_1a1_PENDIENTE.md](10_Mentoria_1a1_PENDIENTE.md) | ⏳ **`open-questions`** + `user-plan` | 🚨 **CONTAINS NO ANSWERS — the 1-a-1 mentoring has not happened yet.** Never treat its content as validated knowledge. It holds (a) the user's **open questions**, which map exactly onto this KB's gaps — the **4 correlation types**, **how VaR is measured**, the **demo-vs-backtest comparison method**, and **portfolio lifecycle / strategy replacement**, which is absent from the entire KB; and (b) the user's **declared starting structure** (1 FTMO 10k challenge monthly · 1 Darwinex Zero on MT4 with mixed ORO/NQ/DAX H1-H4 · a second DZ on MT5 later · Axi Select with 1000) plus the selection criteria **"EAs that open little and safe"** and **"H1/H4 opening 3-4 trades a month"**. The plan's tensions with academy doctrine are listed in the document, chief among them that **low-trade strategies may not meet Axi Select's trade count** and that only **USDJPY** is available as a currency |
| [SERVICE_Darwinex_Zero.md](SERVICE_Darwinex_Zero.md) | `vendor-rulebook` + `provider-data` | **Service rulebook, re-verified 2026-09-08 against the official documentation site** — source of record is now `https://www.darwinexzero.com/docs/`, whose machine-readable index lives at `https://www.darwinexzero.com/docs/llms.txt` (81 pages, each served as clean `.md`). ⚠️ `https://www.darwinexzero.com/llms.txt` returns **404** — use the `/docs/` path. ⭐ Carries the most consequential finding of the service research: **Darwinex enforces correlation as a hard funding gate with published numbers** — **> 0.95 vs other users' DARWINs** excludes the weaker one, and **> 0.5 vs your own DARWINs applies in DarwinIA SILVER only** (GOLD states only the 0.95 figure, and **SILVER-vs-GOLD pairs are explicitly not counted**). That is still the project's first external, numeric definition of "decorrelated". Also: a **computable objective function** (Rating = 22% current-month return + 67% six-month return + 11% max drawdown, plus a track-record bonus reaching +3 after 18 months; **≥ 75 earns a guaranteed, non-competitive allocation** — but the **rating scale and the points mapping are NOT FOUND**), calibration of **25 risk-equivalent decisions over ≥ 15 trading days** under the now-documented formula `Exposure = D-Leverage × √duration` (**§ 4.2** — 25 positions is a hard floor; breadth, not hold time, is the lever), **margin call at 100% / stop-out at 50%**, unrestricted weekend/overnight/EA/news trading, and a **15% performance split** on allocated capital. **No daily or total loss limit appears anywhere in the 81-page index** — an audited absence, not a quoted denial. Price **45€/month** for CFD/Cash/Crypto, **50€** Futures — except `crypto-cfds.md` states **38€** for the crypto account, contradicting `pricing.md`. 🚨 See its **§ 9 source-risk register**: eight unresolved contradictions in Darwinex's own documentation |
| [SERVICE_FTMO.md](SERVICE_FTMO.md) | `vendor-rulebook` + `provider-data` | **Service rulebook, retrieved 2026-09-07 from ftmo.com.** FTMO now runs **two products with materially different rules** — 1-Step (10% target, 3% daily, **trailing** 10% max loss, Best Day Rule, no fee refund, Standard only) and 2-Step (10%+5%, 5% daily, **static** 10% max loss, 4 min trading days per phase, **fee fully refunded**, and a **Swing account type**). ⚠️ **The time limit is now UNLIMITED**, which makes a low-frequency portfolio viable here — the opposite of Axi Select. 🔴 **The weekend rule is decisive for swing systems**: unrestricted during evaluation, but funded **Standard** accounts must be flat before every weekend and any market break > 2h; only the **2-Step Swing** type (at 1:30 leverage instead of 1:100) removes that and the news restriction. Both drawdown limits read **equity including floating P/L**. Reusing a strategy across challenges is **not banned but capped** at **$400k combined allocation per strategy** |
| [SERVICE_Axi_Select.md](SERVICE_Axi_Select.md) | `vendor-rulebook` + `provider-data` | **Service rulebook, retrieved 2026-09-07 from the official Axi Select Program Rules PDF (dated 2026-03-26).** 🔴 **Carries four corrections to module 8**, two of which change the simulator: (1) **the "60 days" is a MINIMUM stage duration to serve, not a deadline to beat** — there is no race and no published maximum; Seed's minimum is 30 days, not 60. (2) **Gold is NOT banned** — gold and silver are the only eligible metals, with no stage dependency found. (3) Ineligible symbols are not banned, they simply are not copied to the Allocation Account. (4) The 90-day score reset is **voluntary**, and is a different mechanism from the Seed stage reset. The binding constraint is the **absolute trade count** (20/40/50 unique trades per stage), sharpened by the **"unique trade" rule**: concurrent trades in the same symbol count as **ONE**, so breadth across symbols raises the count while depth within one does not. Free to join, $500 minimum, Seed pays 0%. ⚠️ The **Edge score weights are unpublished**, and the academy's micro-lot trade-filling tactic **may fall under the scalping prohibition**, whose penalty is permanent removal |
| [SERVICE_Own_Capital.md](SERVICE_Own_Capital.md) | `decision-framework` | **Not a vendor rulebook — there is no external party imposing rules.** Establishes the **common 8-field schema** the other three follow, so the simulator can treat destinations polymorphically. Its defining property: own capital is the **only destination whose objective function is a free choice**, which makes it the **hardest to simulate, not the easiest** — a group ranker needs something to rank by, and here it must be **elicited from the user and written down** or the simulator will silently default to "highest return", the exact failure mode module 9 documents in QA4. Everything else is well grounded: no constraints, no activity requirements, H4 is the best fit of any destination, risk is 1% total on the worst simultaneous case, and **the 6.5% VaR is Darwinex's, never to be inherited here** |
| [MEASURED_FTMO_Demo_Baseline.md](MEASURED_FTMO_Demo_Baseline.md) | **`measured-data`** | 📊 **What the deployed system actually did**, computed from the exported trade list of FTMO demo 1420754357 ($10k, 2-Step, Swing, MT4) over 2026-08-26 to 09-07 — 48 trades, 11 trading days. Not doctrine and not vendor rules: **measurements**, with an explicit sample-size warning. Retracts an earlier estimate (**concurrency is not the problem**: max 4 concurrent positions, 1.42% worst-case simultaneous loss against a 5% daily limit, worst observed day 1.59%) and identifies the real one (**RRR 0.29 against the academy's required > 2-3**, profit factor 0.42, driven by H1 bar-close exits that cut winners while losers run to stop). Also documents that **BTCUSD carries 85% of activity** while having no academy doctrine, that the deployed frequency (**4.4 trades/day**) is ~30x the declared plan (3-4/month) with a **1.9 h median hold**, and **10 clusters of duplicate simultaneous entries** — the structural-duplication problem visible in live data. States plainly the one question it **cannot** answer: a 12-day window cannot rule out a 30-day inactivity gap |
| [XAUUSD_Profile.md](XAUUSD_Profile.md) | `concept` | Asset profile for Gold (XAUUSD): bullish bias (Long-Only), key building blocks (BB, ATR, Session Levels), IMOX config (spread 1.5-2.5, H1+H4 filter, Trailing Stop mandatory), mining strategy |
| [GDAXI_Profile.md](GDAXI_Profile.md) | `concept` | Asset profile for DAX 40 (GDAXI): explosive European hours, gap-prone, key blocks (HMA, Opening Range, ADX, SMA200), IMOX config (spread 1-2, H1, 08:00-18:00 CET only), mining strategy |
| [US100_Profile.md](US100_Profile.md) | `concept` | Asset profile for Nasdaq (US100): prolonged trends, tech-correlated, key blocks (EMAs, LinReg, Donchian, MACD), IMOX config (spread 1-1.5, H1, WFM mandatory), mining strategy |
| [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) | `vendor-rulebook` | **NOT academy doctrine.** Darwinex Zero / DARWIN proprietary risk model, re-verified 2026-09-08 against the Zero `/docs/` pages (now primary; `help.darwinex.com` is secondary and describes **Darwinex Classic**). Monthly VaR standard (6.5% target, 3.25-6.5% range, 95% confidence, 45-day window), the **VaR method** (historical data + Monte Carlo, read at the 95th percentile of the 1-month distribution), the full **target-VaR procedure** with worked examples, Risk Engine leverage multiplier, **D-Leverage caps `< 30 min → 16.25` · `30-60 min → 13` · `> 60 min → 9.75`**, absence of daily/total loss limits, and how it contrasts with this app's own metrics. ⚠️ **Drawdown methodology is now documented**: peak-to-trough on the DARWIN return curve sampled from **quote data updated every 30 seconds** — so a drawdown computed on daily closes is a **lower bound** on the figure Darwinex reports (**§ 5.1**). The factor `f` in the leverage formula remains **undefined by any source** |

---

## Trigger Table — What to Read When

| When the agent is... | Read these documents |
|----------------------|----------------------|
| Analyzing a new pipeline feature (any stage) | `02_SQX_Builder.md` + `03_SQX_Retester.md` + `04_SQX_Optimizer.md` |
| Designing or updating Analyzer Rules (strategy checklist) | `02_sqx_metrics.md` + `02_Mineria_y_Genetica.md` |
| Deciding which KPIs to display or track | `02_sqx_metrics.md` |
| Working on Builder stage configuration | `02_SQX_Builder.md` (primary) + `02_Mineria_y_Genetica.md` (theory) + `01_SQX_Data.md` |
| Working on Retester stage configuration | `03_SQX_Retester.md` |
| Working on Optimizer stage configuration | `04_SQX_Optimizer.md` |
| **Selecting which strategies go to Backtest or Demo** | **`05_Analisis_de_Resultados.md`** (the 7-step decision procedure) |
| Analysing WFM results, equity curves, trade lists, or drawdown | `05_Analisis_de_Resultados.md` |
| Deciding when a strategy leaves Demo | `05_Analisis_de_Resultados.md` + `07_Backtest_y_Puesta_en_Marcha.md` — the exit is an **event**, not a duration: **a strategy must have had a fall and recovered before going live** |
| Designing per-stage settings, date tracking, or stage transitions | `02_SQX_Builder.md` + `03_SQX_Retester.md` + `04_SQX_Optimizer.md` |
| Working on instrument/asset configuration | `01_SQX_Data.md` + `01_Fundamentos_y_Data.md` + relevant asset profile |
| Working on XAUUSD (Oro) features | `XAUUSD_Profile.md` + `01_SQX_Data.md` |
| Working on GDAXI (DAX) features | `GDAXI_Profile.md` + `01_SQX_Data.md` |
| Working on US100 (Nasdaq) features | `US100_Profile.md` + `01_SQX_Data.md` |
| Working on money management, risk, lot sizing | `06_Gestion_de_Riesgo.md` |
| Choosing lot size / decimals for a given account size | `06_Gestion_de_Riesgo.md` — **decimals 1 in the Builder, 2 for final testing and for any small account** |
| Setting a VaR target for an account | `06_Gestion_de_Riesgo.md` first (**6.5% is Darwinex's, not the academy's**), then `Darwinex_Zero_Risk_Model.md` if the destination is Darwinex Zero |
| Deploying EAs, sizing a VPS, or organising MT4 instances | `07_Backtest_y_Puesta_en_Marcha.md` — **max 20 EAs per instance, ~6-7 instances per VPS** |
| Running an MT4/MT5 confirmation backtest or sourcing tick data | `07_Backtest_y_Puesta_en_Marcha.md` |
| Working on **any funding service's rules** | **the `SERVICE_*.md` rulebooks** (retrieved 2026-09-07, with per-fact confidence levels) — they **supersede** `08_Servicios_de_Fondeo.md` wherever they disagree |
| Modelling a service's objective function | `SERVICE_Darwinex_Zero.md` (**fully computable**) · `SERVICE_FTMO.md` (**single scalar target**) · `SERVICE_Axi_Select.md` (**conjunction, weights unpublished**) · `SERVICE_Own_Capital.md` (**must be elicited**) |
| Deciding what "decorrelated" means numerically | `SERVICE_Darwinex_Zero.md` — **< 0.95 vs other users** (both programmes) and **< 0.5 vs your own DARWINs in SILVER only**; GOLD publishes no same-user threshold and **SILVER-vs-GOLD pairs are not counted**. Measured on **DARWIN returns over the last 3 months**, and **only for DARWINs rated ≥ 60**. Still the only external numeric definition the project has. ⚠️ Do not confuse it with the **intra-position asset correlation** that feeds D-Leverage — different object, and its window and threshold are **NOT FOUND** |
| Auditing whether a vendor number can be trusted | `SERVICE_Darwinex_Zero.md` **§ 9 source-risk register** — eight places where Darwinex's own documentation contradicts itself (crypto price 38€ vs 45€, booster cap 500k vs 400k **within one page**, SILVER max 375k vs 250k, and five more). The KB's first contradiction register: consult it before treating any single Darwinex figure as settled |
| Checking whether an instrument is allowed on a service | the relevant `SERVICE_*.md` § 3 — **not** module 8, whose gold claim is contradicted |
| Modelling a per-service objective or eligibility constraint | `08_Servicios_de_Fondeo.md` — the objective is **not return**, and asset eligibility can depend on the **phase** |
| Allocating risk across a group of strategies | `08_Servicios_de_Fondeo.md` (**1% total on the worst simultaneous case**) + `06_Gestion_de_Riesgo.md` |
| **Building, ranking or correlating portfolios of strategies** | **`09_AlgoWizard_y_QA4.md`** — read the overfitting warning FIRST |
| Deciding how many strategies belong in a group | `09_AlgoWizard_y_QA4.md` (**5-15 depending on destination; 1-2 for props and Axi**) |
| Configuring the simulator for the user's actual accounts | `10_Mentoria_1a1_PENDIENTE.md` — the **declared** target structure; confirm before building on it |
| Asked something the KB does not answer | check `10_Mentoria_1a1_PENDIENTE.md` § *Índice de gaps* — if it is listed there, **say it is unanswered**; do not infer one |
| Reasoning about concurrency, simultaneous risk, or live-vs-declared behaviour | **`MEASURED_FTMO_Demo_Baseline.md`** — measured, not estimated. **Never infer simultaneous exposure by multiplying per-strategy risk by strategy count**; that estimate was made in this project and the data refuted it |
| Editing SL/TP, MM configs, or running a fast backtest | `09_AlgoWizard_y_QA4.md` (AlgoWizard) |
| Working on Darwinex Zero / DARWIN risk, VaR guardrails, or funding-service limits | `Darwinex_Zero_Risk_Model.md` + `08_Servicios_de_Fondeo.md` |
| Working on SQX building blocks, indicators, or signals | `02_Manual_BuildingBlocks.pdf` |
| Understanding domain terminology (drawdown, Sharpe, IS/OOS, etc.) | `02_sqx_metrics.md` + `01_Fundamentos_y_Data.md` |
| Making Demo → Live deployment decisions | `05_Analisis_de_Resultados.md` + `04_SQX_Optimizer.md` + `06_Gestion_de_Riesgo.md` |

---

## Key IMOX Thresholds (Quick Reference)

These are the criteria for **H1 on indices and gold**, which is the default case. They are
non-negotiable in the sense that you must never *invent* an alternative — but several have
**documented variants by timeframe and asset class**, and using the default where a variant applies is
just as wrong as inventing one. See [02_SQX_Builder.md](02_SQX_Builder.md) § *Variantes*:

- **H4**: `# of trades > 150`, not 200. `Exit on Friday` off.
- **Divisas**: Optimizer `WF Ret/DD > 5` (not 8-10) and `WF Stagnation 500` (not 365); MonteCarlo
  Ret/DD filter disabled in the Retester.
- **Axi Select, small accounts**: pips model (SL 40-80, PT 100-250) instead of ATR, with Fixed size MM.
- **Higher Backtest Precision** ([03_SQX_Retester.md](03_SQX_Retester.md)): `Ret/DD Full > 5` for DAX,
  SP500 and currencies; **> 8 for NQ**; **> 10 for XAUUSD**. And `Winning % > 35%`, deliberately looser
  than the Builder's 40% — raising precision lowers strategy quality, so filtering twice at full
  strictness discards good strategies as noise.

| Metric | Threshold | Phase | Source |
|--------|-----------|-------|--------|
| Sharpe Ratio | > 1.2 | All | `02_Mineria_y_Genetica.md` |
| Ret/DD Ratio (Oro) | > 10 | Retester+ | `03_SQX_Retester.md` |
| Ret/DD Ratio (Nasdaq / DAX-SP500-divisas) | > 8 / > 5 | Retester+ | `03_SQX_Retester.md` |
| Ret/DD Ratio (Builder filter) | > 8 | Builder | `02_SQX_Builder.md` |
| Net Profit OOS | > 0 (mandatory) | All | `02_Mineria_y_Genetica.md` |
| Winning % (Builder) | > 40% | Builder | `02_SQX_Builder.md` |
| Winning % (Higher Precision) | > 35% — **deliberately looser** | Retester | `03_SQX_Retester.md` |
| Min # Trades | > 200 (H4: > 150) | Builder/Retester | `02_SQX_Builder.md` |
| Profit Factor | > 1.3 | Reference | `02_sqx_metrics.md` |
| **Robustness Score** — the PASSED gate, *not* `WF Score` (a separate SQX composite index) | > 80% | Optimizer | `04_SQX_Optimizer.md` |
| Profitable WF Runs | > 70% | Optimizer | `04_SQX_Optimizer.md` |
| WF Winning % (OOS) | >= 70% of WF Winning % (IS) | Optimizer | `04_SQX_Optimizer.md` |
| WF Stability of Net Profit | >= 60% | Optimizer | `04_SQX_Optimizer.md` |
| WF Max profit in one run as % of total | < 50% | Optimizer | `04_SQX_Optimizer.md` |
| WF Ret/DD Ratio | >= 10 (divisas: 5) | Optimizer | `04_SQX_Optimizer.md` |
| Max Stagnation | < 365 days (divisas: 500) | Optimizer | `04_SQX_Optimizer.md` |
| Risk per trade — **large accounts** | $200 (0.20% of $100k) | All | `06_Gestion_de_Riesgo.md` |
| Risk per trade — **small accounts** | $10 (1.0% of $1k), decimals 2, no-MM lot 0.01 | All | `06_Gestion_de_Riesgo.md` |
| RR (Average Win / Average Loss) | > 2 or 3 | Selection | `06_Gestion_de_Riesgo.md` |

### Final selection tier — taking a strategy to Demo

The table above is the **pipeline** tier: it filters so that good strategies are not lost.
[05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) § *procedimiento de decisión* step 6 is
the **final selection** tier: it filters so that bad strategies do not reach production. It is
**stricter on every shared metric**, and applying the pipeline default where the final gate belongs is
just as wrong as inventing a number.

| KPI | Pipeline default | **Final selection (step 6)** |
|---|---|---|
| Sharpe Ratio | > 1.2 | **> 1.3** |
| Profit Factor | > 1.3 | **> 1.6** ⚠️ |
| Ret/DD Ratio | > 8-10 | **> 12** |
| Winning % | > 40% | **> 48%** |
| %DD | — | **< 2%** |
| Stagnation | < 365 | **< 365** |
| max avg loses | — | **< 10** |
| STR (SQN) | — | **> 1.6** |
| Avg. Trades Month | — | **low** — but **inverted for funding challenges**, where a high count helps |

⚠️ **Three discrepancies are open in the source material** and are recorded unresolved in
`05_Analisis_de_Resultados.md` § *Discrepancias*: the Profit Factor (`> 1.5/1.4` in the narrative vs
`> 1.6` in the decision step), the SPP tolerance (**±30%** as the Retester filter vs **±50%** as the
analyst rule), and the tier relationship above. **Do not silently pick one** — ask.

### Portfolio-level thresholds

| Metric | Threshold | Source |
|---|---|---|
| Total portfolio risk if every position triggers at once | **1%** (e.g. 0.2 each across gold/NQ/DAX/a currency) | `08_Servicios_de_Fondeo.md` |
| Monte Carlo Max %DD at 95% confidence | **at most 2x** the original DD | `09_AlgoWizard_y_QA4.md` |
| Monte Carlo Max %DD at 100% confidence | **3x-4x** is acceptable | `09_AlgoWizard_y_QA4.md` |
| Strategies per group | **5-15** by destination; **1-2** for props and Axi Select | `09_AlgoWizard_y_QA4.md` |
| Correlation basis | **profit/loss by day** (Portfolio Master) | `09_AlgoWizard_y_QA4.md` |

> ⚠️ **%DD is a property of the pair (strategy, balance), not of the strategy.** The same strategy
> shows 92% DD on a 1,000 account and 12% on a 10,000 one. Never evaluate a group's drawdown without
> naming the balance.

---

> This table holds **academy criteria only**. Broker- and platform-specific risk numbers (e.g. the
> Darwinex Zero VaR band) are **vendor rulebook**, are set externally, and change over time — they
> live in their own document and must never be promoted into this table. See
> `Darwinex_Zero_Risk_Model.md`.
>
> Named explicitly because the 2026-09-08 re-verification added them and they read like thresholds:
> the **30-second drawdown sampling**, the worked target-VaR values (**4.33%**, **4.17%**), the
> **margin call 100% / stop-out 50%** levels, the **D-Leverage caps**, the **Rating threshold of 75**
> and the **0.95 / 0.5 correlation gates** are all **Darwinex's numbers, not the academy's**. None of
> them belongs in this table, and none of them constrains any other destination by default.

---

## Usage Protocol for Agents

1. **Read this INDEX.md first** — always, before any domain decision
2. **Identify the trigger** — match the current task to a row in the Trigger Table
3. **Check the Quick Reference** — for numeric thresholds, use the table above
4. **Read only the relevant documents** — do not read all docs, only what the trigger says
5. **Do NOT invent domain criteria** — if a decision requires domain knowledge not found here, flag it to the user
6. **Treat `10_Mentoria_1a1_PENDIENTE.md` as questions, never as answers.** Its gap index is the authoritative list of what this KB does **not** know. A topic listed there has no validated answer — report that plainly instead of reasoning one out

---

## About This Knowledge Base

Source: IMOX Algorithmic Trading Academy — 9-class program by Aritz.
**Modules 0-9 are fully transcribed** (2026-09-07). What remains is the **mentoría 1 a 1**, plus four
flagged gaps inside the transcripts: the `OOS1/OOS2/OOS3` builder setup for difficult markets
(module 6), the MT4 backtest methods (module 7), the Axi gold workaround (module 8), and — most
consequential for this application — the **Portfolio Master detail and the Correlation section**
(module 9), which the material itself marks as critical and which are **not transcribed**.

Last updated: 2026-09-07
Platform: StrategyQuant X v136
Primary broker: Darwinex (DMA, low spread); secondary: Axi (swing on indices)

Documents typed `vendor-rulebook` are **not** academy material. They capture external platform
rules, carry their own source URL and retrieval date, and must be re-verified before use.

Documents prefixed `SERVICE_` are the **per-service rulebooks** (retrieved 2026-09-07), written to a **common 8-field schema** so the simulator can treat every destination polymorphically. Each fact carries a confidence level and every gap is recorded as an explicit **NOT FOUND** rather than inferred. Where a rulebook and an academy transcript disagree, **the rulebook wins and says so** — see the correction banners in `08_Servicios_de_Fondeo.md`.

Documents typed `open-questions` are **not** knowledge. They record what has been asked and not yet
answered, and exist so that a gap is reported as a gap instead of being filled by inference.
