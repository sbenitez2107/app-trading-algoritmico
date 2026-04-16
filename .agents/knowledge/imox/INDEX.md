# IMOX Trading Academy — Knowledge Base Index

This is the **entry point** for all domain knowledge from the IMOX Algorithmic Trading Academy (9-class program).

Agents MUST read this file first before making any domain-related decision. Then read only the documents relevant to the task — do NOT read all documents indiscriminately.

---

## Document Registry

| File | Type | Description |
|------|------|-------------|
| [01_Fundamentos_y_Data.md](01_Fundamentos_y_Data.md) | `concept` + `sqx-config` | IMOX philosophy (trend-only, swing trading, no grail), data management (10yr history, M1/Tick), instrument configuration in SQX v136 (Point Value, Pip Size, spread rules, timezone UTC+2), swap handling per phase, broker selection (Darwinex / Axi) |
| [02_Mineria_y_Genetica.md](02_Mineria_y_Genetica.md) | `workflow` + `selection-criteria` | Builder phase: IS/OOS strategy, genetic algorithm mechanics (islands, population, crossover/mutation), "What to Build" config, ATR-based SL/TP, key KPIs (Sharpe >1.2, Ret/DD >8), the 5 fatal errors, and criteria for selecting ~300-400 strategies for Retester |
| [02_sqx_metrics.md](02_sqx_metrics.md) | `concept` | Complete SQX metrics dictionary: profit/return (Total Profit, CAGR, Yearly AVG), risk/efficiency (Sharpe Ratio, Profit Factor, Return/DD, Drawdown), quality (SQN, SQN Score), statistics (Z-Score, Expectancy, Exposure), stagnation (Stability R²), symmetry metrics |
| [02_Manual_BuildingBlocks.pdf](02_Manual_BuildingBlocks.pdf) | `sqx-config` | Full SQX Building Blocks manual: Glossary, Signals (trend, momentum, volatility, volume, price action), Indicators (all categories with parameters and examples), Entry blocks (Stop/Limit/Market), Logical operators. Reference for understanding SQX strategy components |
| [02_BuilderTheory.odt](02_BuilderTheory.odt) | `sqx-config` | Builder theory document (ODT format — not directly readable by agents; ask user to convert to markdown if needed) |
| [03_Validacion_y_Stress_Test.md](03_Validacion_y_Stress_Test.md) | `workflow` + `selection-criteria` | Full pipeline: Builder config (What to Build parameters, genetic options, data split, ranking filters), Retester protocol (Monte Carlo Trades + Retest Methods, SPP anti-overfit, Sequential Optimization), Optimizer (Walk Forward WFO + Matrix WFM with specific thresholds) |
| [06_Money Management.md](06_Money Management.md) | `concept` + `sqx-config` | IMOX Money Management protocol: ATR-based sizing philosophy, Fixed Amount method ($200 risk / 0.20% on $100k), lot size decimals (1 decimal in Builder), Max Lots=10, no compound interest in Builder phase |
| [XAUUSD_Profile.md](XAUUSD_Profile.md) | `concept` | Asset profile for Gold (XAUUSD): bullish bias (Long-Only), key building blocks (BB, ATR, Session Levels), IMOX config (spread 1.5-2.5, H1+H4 filter, Trailing Stop mandatory), mining strategy |
| [GDAXI_Profile.md](GDAXI_Profile.md) | `concept` | Asset profile for DAX 40 (GDAXI): explosive European hours, gap-prone, key blocks (HMA, Opening Range, ADX, SMA200), IMOX config (spread 1-2, H1, 08:00-18:00 CET only), mining strategy |
| [US100_Profile.md](US100_Profile.md) | `concept` | Asset profile for Nasdaq (US100): prolonged trends, tech-correlated, key blocks (EMAs, LinReg, Donchian, MACD), IMOX config (spread 1-1.5, H1, WFM mandatory), mining strategy |

---

## Trigger Table — What to Read When

| When the agent is... | Read these documents |
|----------------------|----------------------|
| Analyzing a new pipeline feature (any stage) | `03_Validacion_y_Stress_Test.md` + `02_Mineria_y_Genetica.md` |
| Designing or updating Analyzer Rules (strategy checklist) | `02_sqx_metrics.md` + `02_Mineria_y_Genetica.md` |
| Deciding which KPIs to display or track | `02_sqx_metrics.md` |
| Working on Builder stage configuration | `02_Mineria_y_Genetica.md` + `03_Validacion_y_Stress_Test.md` + `01_Fundamentos_y_Data.md` |
| Working on Retester stage configuration | `03_Validacion_y_Stress_Test.md` (Module 3 section) |
| Working on Optimizer stage configuration | `03_Validacion_y_Stress_Test.md` (Module 4 section) |
| Designing per-stage settings, date tracking, or stage transitions | `03_Validacion_y_Stress_Test.md` + `02_Mineria_y_Genetica.md` |
| Working on instrument/asset configuration | `01_Fundamentos_y_Data.md` + relevant asset profile |
| Working on XAUUSD (Oro) features | `XAUUSD_Profile.md` + `01_Fundamentos_y_Data.md` |
| Working on GDAXI (DAX) features | `GDAXI_Profile.md` + `01_Fundamentos_y_Data.md` |
| Working on US100 (Nasdaq) features | `US100_Profile.md` + `01_Fundamentos_y_Data.md` |
| Working on money management, risk, lot sizing | `06_Money Management.md` |
| Working on SQX building blocks, indicators, or signals | `02_Manual_BuildingBlocks.pdf` |
| Understanding domain terminology (drawdown, Sharpe, IS/OOS, etc.) | `02_sqx_metrics.md` + `01_Fundamentos_y_Data.md` |
| Making Demo → Live deployment decisions | `02_Mineria_y_Genetica.md` + `03_Validacion_y_Stress_Test.md` |

---

## Key IMOX Thresholds (Quick Reference)

These are non-negotiable criteria from the academy. Never invent alternatives.

| Metric | Threshold | Phase | Source |
|--------|-----------|-------|--------|
| Sharpe Ratio | > 1.2 | All | `02_Mineria_y_Genetica.md` |
| Ret/DD Ratio (Oro) | > 10 | Retester+ | `03_Validacion_y_Stress_Test.md` |
| Ret/DD Ratio (Nasdaq/DAX) | > 8 / > 5 | Retester+ | `03_Validacion_y_Stress_Test.md` |
| Ret/DD Ratio (Builder filter) | > 8 | Builder | `03_Validacion_y_Stress_Test.md` |
| Net Profit OOS | > 0 (mandatory) | All | `02_Mineria_y_Genetica.md` |
| Winning % (Builder) | > 40% | Builder | `03_Validacion_y_Stress_Test.md` |
| Min # Trades | > 200 | Builder/Retester | `03_Validacion_y_Stress_Test.md` |
| Profit Factor | > 1.3 | Reference | `02_sqx_metrics.md` |
| WF Score | > 80% | Optimizer | `03_Validacion_y_Stress_Test.md` |
| Profitable WF Runs | > 70% | Optimizer | `03_Validacion_y_Stress_Test.md` |
| Max Stagnation | < 365 days | Optimizer | `03_Validacion_y_Stress_Test.md` |
| Risk per trade | $200 (0.20% of $100k) | All | `06_Money Management.md` |

---

## Usage Protocol for Agents

1. **Read this INDEX.md first** — always, before any domain decision
2. **Identify the trigger** — match the current task to a row in the Trigger Table
3. **Check the Quick Reference** — for numeric thresholds, use the table above
4. **Read only the relevant documents** — do not read all docs, only what the trigger says
5. **Do NOT invent domain criteria** — if a decision requires domain knowledge not found here, flag it to the user

---

## About This Knowledge Base

Source: IMOX Algorithmic Trading Academy — 9-class program by Aritz.
Last updated: 2026-04-15
Platform: StrategyQuant X v136
Primary broker: Darwinex (DMA, low spread); secondary: Axi (swing on indices)
