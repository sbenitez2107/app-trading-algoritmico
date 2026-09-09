# Measured Baseline — FTMO Demo Account 1420754357

> **Type**: `measured-data` — not doctrine, not vendor rules. **This is what the deployed system
> actually did**, computed from the exported trade list, not estimated.
> **Source**: FTMO Account MetriX export + trading journal CSV, provided by the user 2026-09-07.
> **Window**: 2026-08-26 → 2026-09-07 · **48 closed trades** · **11 trading days**.
> **Account**: $10,000 · 2-Step · **Swing** · **MT4** · Free trial (start 26/08, end 08/09).

---

## 🔴 WHAT THIS ACCOUNT IS — read before drawing any conclusion

*Context provided by the user 2026-09-07, after the first analysis. It changes what several findings
below mean, and one of them was framed wrongly without it.*

| | |
|---|---|
| **Purpose** | A **14-day disposable test** of BTC EAs, to observe **the lot sizes they open with** and **which strategy types behave better** |
| **Provenance of the EAs** | 🔴 **Taken DIRECTLY from the Builder.** They did **not** pass the academy process — no Retester crosschecks, no Optimizer walk-forward, no 7-step selection |
| Lifespan | Expires 2026-09-08. A **more diverse portfolio** follows, to keep testing strategies and assumed risk |
| The 3-4 trades/month figure | ⚠️ Applies to the **FUNDED account**, not to the challenge phase |
| Challenge intent | **Pass both phases fast**, exploiting the current market state |

### ❌ Retraction: "RRR 0.29 is the structural problem" was the wrong frame

The RRR measurement in § 2 is correct **as a number**. Calling it a defect of the user's system was
**not**, because these strategies are **raw Builder output** — the academy pipeline exists precisely to
discard exactly this.

**What the measurement actually demonstrates, and it is more useful than the criticism was:**

> **The Builder's ranking filters are necessary but nowhere near sufficient.** These EAs clear the
> Builder-level bar — 76% win rate on BTCUSD passes the `Winning % > 40%` filter comfortably — and
> still produce **profit factor 0.42** and a **negative expectancy**. A win rate that passes, on a
> system that loses money.
>
> **This is a live, measured demonstration of why modules 3, 4 and 5 exist**, and of the doctrine in
> [02_SQX_Builder.md](02_SQX_Builder.md): *less strict in Genetic Options, more strict in Ranking* —
> with the Retester and Optimizer doing the real filtering afterwards. It is the best empirical
> argument in this knowledge base for not shortcutting the pipeline.

The **exit mechanics** finding in § 2 also stands and is worth keeping: **the trailing stop is too
tight relative to ATR**, cutting winners near hourly bar boundaries while losers run to the full stop.
That is a concrete, fixable parameter observation — and exactly the kind of thing a Retester What-If
or Sequential Optimization would surface before deployment.

### ⚠️ And the frequency mismatch in § 4 is not a mismatch

The declared **3-4 trades/month** describes the **funded account**. This demo is a **challenge-phase
speed test**. Measuring one against the other compares two different modes — see
[SERVICE_FTMO.md](SERVICE_FTMO.md) § *Two different problems: passing vs holding*.

> 🔑 **The user's own framing confirms the `mode` dimension**: *"la idea es pasar las 2 fases rápido
> aprovechando el estado del mercado"*, then 3-4 trades/month once funded. **Fast and opportunistic to
> pass; slow and defensive to hold.** Different frequency, different risk posture — possibly different
> strategies entirely.
>
> ⚠️ **And the 30-day inactivity risk relocates accordingly.** It is not a challenge-phase concern at
> 4.4 trades/day; it is a **funded-mode** concern at 3-4 trades/month. That is precisely where the
> account is worth the most and where losing it costs the most.

---

## ⚠️ Sample-size warning

**12 calendar days and 48 trades.** That is enough to characterise the *shape* of the system
(frequency, concurrency, win/loss geometry) and **not** enough to estimate anything about tails,
regimes, or rare events. Every conclusion below is scoped accordingly, and the one question the data
**cannot** answer is called out explicitly in § 5.

---

## 1. ✅ Correction — concurrency is NOT the problem

An earlier estimate in this project reasoned *"14 EAs × 0.5% risk = 7% worst-case simultaneous
exposure, against a 5% daily limit"*. **The measured data does not support that.**

| Measure | Value |
|---|---|
| **Max concurrent open positions** | **4** (2026-08-27 11:08, all BTCUSD) |
| Max concurrent, per symbol | BTCUSD 4 · US100 2 · XAUUSD 2 |
| Mean "full stop" loss | **−$35.53 = 0.36%** of account |
| Worst single loss | −$40.25 = **0.40%** |
| **Worst-case simultaneous loss at the peak** | 4 × −35.53 = **−$142 = 1.42%** |
| **Worst day actually observed** | **−$159.40 = 1.59%** (2026-08-28) |
| FTMO daily limit | −$500 = **5.00%** |

> **The 14 EAs do not fire together.** They are spread across time; only four were ever open at once.
> Headroom against the daily limit was roughly **3×** at the worst observed moment.
>
> **The estimate was wrong because it multiplied a per-EA risk by the EA count.** That is only valid
> if every EA holds a position simultaneously, which this system never does. **Concurrency must be
> measured, never inferred from strategy count.**

**Measured risk per trade vs declared**: the user targets **$50 (0.5%)**; the realised full stop
averages **$35.53 (0.36%)** and never exceeded **$40.25 (0.40%)**. Sizing is **on target, slightly
conservative**.

### 🔁 The lesson for the simulator

`max concurrent positions` and `worst-case simultaneous risk` are **computable from a closed-trade
series** — entry and exit timestamps are all that is needed. They belong in the group metrics
alongside correlation. **A group of 15 strategies that never overlaps carries less simultaneous risk
than a group of 3 that always does**, and no correlation coefficient captures that directly.

---

## 2. 🔴 The real problem: the win/loss geometry

| Metric | Measured | Academy requirement | Source |
|---|---|---|---|
| **RRR (avg win / avg loss)** | **0.29** | **> 2 or 3** | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| Avg win | **$8.06** | — | |
| Avg loss | **−$28.23** | — | |
| Profit factor | **0.42** | > 1.6 (final selection) | [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) |
| Sharpe | **−0.43** | > 1.3 | idem |
| Expectancy | **−$5.83** | positive | |
| Win rate | 50% overall, **76% on BTCUSD** | > 48% | idem |

> **A 76% win rate on BTCUSD that still loses money is the diagnostic.** Many small wins, few large
> losses — the exact inverse of the academy's RR > 2-3 requirement, and off by roughly **an order of
> magnitude**.

**Where it comes from**: exits cluster on **round hour boundaries** (03:00:06, 16:00:00, 21:00:01,
12:00:13…), i.e. **bar-close exits on H1**. Winners are cut at the next bar; losers run to the stop.
The trailing stop is not letting winners develop.

**Net result over the window**:

```
gross P/L      −193.66
commissions     −78.34      ← 40% again on top of the gross loss
swap             −7.72
NET            −279.72     (−2.80% of account)
```

**Against the trial's objectives**: profit target **$500 (5%)**, achieved **−$278.10 (−2.7%)**. ❌
*(Note: the free trial's target is 5% and its minimum is 2 trading days — the real 2-Step Phase 1 is
**10%** and **4** days, so a paid challenge is a harder bar than this.)*

---

## 3. ⚠️ The portfolio is not the portfolio that was described

| Symbol | Trades | Share | Net P/L | Win% |
|---|---|---|---|---|
| **BTCUSD** | **41** | **85%** | −$92.39 | 76% |
| US100.cash | 5 | 10% | −$47.17 | 20% |
| XAUUSD | 2 | 4% | −$54.10 | 0% |
| **GDAXI** | **0** | — | — | — |

**This is a BTCUSD system with occasional index and gold trades**, not a four-asset portfolio. GDAXI
did not trade at all in the window.

⚠️ **And BTCUSD is the asset with no academy doctrine** — no instrument configuration, no thresholds,
no asset profile. It is carrying 85% of the activity.

---

## 4. ⚠️ Frequency and holding period do not match the declared plan

| | Declared *(module 10)* | **Measured** |
|---|---|---|
| Trades | *"H1 or H4 opening **3-4 trades a month**"* | **48 in 11 trading days ≈ 4.4/day** |
| Profile | *"EAs that open little and safe"* | ~**30× more frequent** than declared |
| Median holding time | swing (H4 ≈ 2-3 trades/month) | **1.9 hours** |
| Max holding time | — | 94.6 h (3.9 days) — **1 trade** |
| Trades held > 24h | — | **1 of 48** |

> **This is not a swing portfolio.** Median 1.9 hours with hourly bar-close exits is an intraday H1
> system. That is not a criticism of the system — but the declared plan and the deployed reality are
> **different things**, and the rulebooks give opposite advice for each.

**Consequences, and they cut both ways:**

| | Effect |
|---|---|
| ✅ Swap exposure | Only **−$7.72** total. The academy's *"swap kills H4 on props"* warning barely applies here |
| ✅ Axi Select trade count | **4.4 trades/day comfortably clears** 20/40/50 per stage — the constraint I flagged as binding **dissolves at this frequency** |
| ✅ Weekend rule | Almost nothing is held overnight, so even a funded **Standard** account would work |
| ⚠️ Axi scalping rule | Several trades close in **23-30 minutes**. Not scalping by any normal reading, but Axi's prohibition is **qualitative and discretionary**, and this frequency is closer to its edge than an H4 portfolio would be |
| ⚠️ Commission drag | **$1.63/trade × high frequency** compounds. At 4.4 trades/day that is ~$7/day on a $10k account |

---

## 5. ❌ What this data CANNOT answer

**The 30-day inactivity question.**

| Measure | Value |
|---|---|
| Longest gap between any two trades | **1.88 days** |
| Longest gap on BTCUSD | 1.88 days |
| Longest gap on US100.cash | 3.24 days |
| Longest gap on XAUUSD | 0.00 days (2 trades, same second) |

> 🚫 **A 12-day window cannot demonstrate the absence of a 30-day gap.** The observed maximum is
> reassuring about *this* configuration at *this* frequency, and it is **not evidence** that a 30-day
> gap will not occur — the sample is shorter than the event being tested.
>
> **To answer it properly**: run `max days between consecutive trades` over the **full backtest
> series** of each strategy, which spans years. That is the correct instrument, and the data for it
> already exists.

---

## 6. Simultaneous entries — EAs firing together

**10 clusters** of two or more entries within 60 seconds, almost all on BTCUSD:

```
2026-08-27 11:02:57  x2 BTCUSD   2026-08-30 15:05:30  x2 BTCUSD
2026-08-27 18:12:58  x2 BTCUSD   2026-08-30 19:04:41  x2 BTCUSD
2026-08-28 03:01:38  x2 XAUUSD   2026-09-01 03:05:27  x2 BTCUSD
2026-08-28 18:00:13  x2 BTCUSD   2026-09-02 06:19:14  x2 BTCUSD
2026-09-03 05:34:42  x2 BTCUSD   2026-09-07 02:03:15  x2 BTCUSD
```

Several pairs open at the **identical second, same symbol, same entry price**, with different volumes
and different stops — different EAs reacting to the same signal.

> 📌 **This is the structural-duplication problem from
> [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) step 5**, visible in live data:
> *"group strategies with identical price/Entry parameters and keep 2"*. Two EAs entering the same
> symbol at the same second at the same price **are the same bet**, whatever their return series
> correlation says. **Deduplicate by structure before measuring correlation of returns.**
>
> ⚠️ **It also interacts with Axi Select's "unique trade" rule**: concurrent same-symbol trades count
> as **ONE**. Ten clusters means ten pairs that would collapse to a single countable trade there.

---

## 🎯 What to do with this

| Finding | Action |
|---|---|
| Concurrency is fine (1.42% worst case vs 5% limit) | ✅ **No sizing change needed.** Estimate withdrawn |
| **RRR 0.29 vs required > 2** | 🔴 **The structural problem.** Exits cut winners at the next H1 bar |
| BTCUSD is 85% of activity, and has no doctrine | ⚠️ Either build BTC doctrine or rebalance |
| Deployed ≠ declared (4.4/day vs 3-4/month) | ⚠️ **Decide which one is the plan**, then pick the service to match |
| Inactivity risk | ⏳ **Unanswerable here** — measure on the full backtest series |
| 10 duplicate-entry clusters | 🔴 Structural deduplication before correlation |
