# Service Rulebook — Own Capital

> **Type**: `decision-framework` — **not** a vendor rulebook. There is no external party imposing
> these rules.
> **Compiled**: 2026-09-07. **Sources**: internal to this knowledge base (see citations per field).
> **Schema**: this file establishes the **common 8-field schema** every service rulebook in this
> series follows, so the simulator can treat all destinations polymorphically rather than as four
> special cases.

---

## 🧭 The defining property

Own capital is the only destination where **the objective function is a free choice**.

Every other service hands you a success criterion: FTMO gives a profit target, Axi Select gives a
score and a trade count, Darwinex Zero gives a VaR standard and a track record to build. Here, nobody
does.

> ⚠️ **This makes own capital the HARDEST case to simulate, not the easiest.** A group ranker needs
> something to rank by. For the other three services that something is externally given and
> verifiable. Here it must be **elicited from the user and written down**, or the simulator will
> silently invent one — almost certainly "highest return", which is the exact failure mode module 9
> documents in QA4.

---

## 1. Objective function

**Not externally defined. Must be chosen and recorded.**

What the knowledge base says about the *intent* of this destination:

| Statement | Source |
|---|---|
| Long-term trading is "structuring a portfolio, controlling drawdowns, trusting the work done, living a fall and then recovering" | [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) |
| **Profits earned on prop firms are moved here.** Funding services are a liquidity instrument, not the goal | [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) |
| "Working a trajectory, trying to decorrelate the strategies, running several mixed accounts where some win and others lose" | [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) |

**Implied objective**: survival and compounding over a long horizon, with a smooth equity curve —
not maximum return over a window. **Confirm with the user before encoding it.**

---

## 2. Hard constraints

**None imposed externally.** No daily loss limit, no total loss limit, no time window, no phase gate.

The only hard constraint is the account balance itself, and the fact that **a drawdown percentage is
a property of the pair (strategy, balance)**, not of the strategy:

| Balance | Same strategy's DD |
|---|---|
| 1,000 | **92%** |
| 10,000 | **12%** |

*Source: [09_AlgoWizard_y_QA4.md](09_AlgoWizard_y_QA4.md).* Never evaluate a group's drawdown here
without naming the balance.

---

## 3. Eligibility

**No restrictions.** Any asset, any timeframe.

This is the destination where the timeframe matrix is most favourable:

| Timeframe | Verdict for own capital | Why |
|---|---|---|
| **H4** | ✅ **The best of all destinations** | Swing trading, no noise, **2-3 trades per month**, extremely robust and stable long term. The swap that ruins H4 for prop firms is not a constraint here |
| H1 | ✅ | Standard working timeframe |

*Source: [00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md).*

**Broker note** *(provider data)*: Axi can also serve own capital because **it charges no swap on NQ
or DAX**. Re-verify before relying on it.

---

## 4. Activity requirements

**None.** No minimum trade count, no minimum active days, no deadline.

⚠️ **This inverts the Axi Select constraint entirely.** Where Axi Select *requires* a trade count and
therefore favours high-frequency strategies, own capital has no such pressure and the doctrine points
the opposite way: **low `Avg. Trades Month` is preferred**
([05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) step 7,
[09_AlgoWizard_y_QA4.md](09_AlgoWizard_y_QA4.md)). H4 at 2-3 trades a month is the ideal profile here
and would be a liability on a funded challenge.

---

## 5. Risk model

**Chosen, not imposed.** This is the field where the knowledge base has the most to say.

| Parameter | Value | Source |
|---|---|---|
| **VaR target** | **A decision.** Own accounts can run **10% or 15%**, earn 30-40%, and **readjust down to 6.5% after a good run** | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **Risk per trade — large account** | $200 (0.20% of $100k) | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **Risk per trade — small account** | $10 (**1.0%** of $1k), decimals 2, no-MM lot 0.01 | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **Total portfolio risk, worst simultaneous case** | **1%** — e.g. 0.2 each across gold / NQ / DAX / a currency | [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) |
| **RR (Average Win / Average Loss)** | > 2 or 3 preferred | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **Monte Carlo tolerance** | Max %DD ≤ **2× original** at 95% confidence; **3-4×** acceptable at 100% | [09_AlgoWizard_y_QA4.md](09_AlgoWizard_y_QA4.md) |

> 🚨 **The correction that matters most here.** The **6.5% VaR is Darwinex Zero's standard, not an
> academy limit.** Treating it as a universal ceiling on own capital is a misreading. The doctrine is:
>
> **"Start ultra conservative and raise later if necessary"** — not "start at the ceiling".

---

## 6. Stages and transitions

**No formal stages.** But the doctrine defines a de facto progression:

| Stage | Guidance | Source |
|---|---|---|
| **Start** | **1, at most 2 strategies.** Small capital means low exposure and conservative RR | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **Grow** | Raise risk gradually — "de menos a más" | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **Mature** | Up to **15 strategies** on a 100k account produces a normal, stable equity curve | [09_AlgoWizard_y_QA4.md](09_AlgoWizard_y_QA4.md) |

> **"When a trader has more capital, they manage downward"** — a higher probability of surviving
> market swings. *Source: [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md).*

**Entry condition into live** *(consistent across three modules — this is firm doctrine)*:
a strategy must have **fallen into drawdown and recovered above it** before going live. Not a
duration — an event. And: *"it is better to start trading LIVE when the market is hitting you than to
start with a lot of money."*

---

## 7. Economics

| Item | Value |
|---|---|
| Entry fee | **None** |
| Profit split | **100%** — it is your capital |
| Recurring cost | Infrastructure only: **VPS ~14 USD/month** for ~6-7 MT4 instances *(provider data, 11/2025)* |
| Capital source | Includes **profits withdrawn from prop firms**, per module 8 doctrine |

*Source: [07_Backtest_y_Puesta_en_Marcha.md](07_Backtest_y_Puesta_en_Marcha.md),
[08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md).*

---

## 8. Prohibited behaviours

**None imposed.** No rule can ban you from your own account.

The real constraints here are **psychological and structural**, and the knowledge base treats them as
seriously as any external rule:

| Constraint | Detail | Source |
|---|---|---|
| **Psychology is 60% of trading** | Van K. Tharp, cited by the academy. High-trade-count strategies are *"the worst to live with"* | [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) |
| **Do not trade against the trend out of fear of blowing the account** | Explicit warning | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **"VaR catches you overnight"** | Explicit warning | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| **Short-term thinking from funding firms clouds judgement** | *"They make you think you are doing badly when you are doing well"* | [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) |

---

## 🔗 What the simulator needs from this service

| Need | Status |
|---|---|
| **An objective function** | ❌ **MISSING — must be elicited from the user.** Everything else in this file is well grounded; this is not |
| Risk allocation rule | ✅ 1% total on the worst simultaneous case |
| Group size | ✅ 1-2 starting, up to 15 mature |
| Strategy profile | ✅ Low `Avg. Trades Month`, H4 preferred |
| Balance to evaluate against | ⚠️ Must be a required input — DD is meaningless without it |
| Drawdown tolerance | ✅ Monte Carlo: 2× at 95%, 3-4× at 100% |
| VaR target | ⚠️ A user decision, defaulting to conservative — **never inherit Darwinex's 6.5%** |

---

## 📋 The common schema (template for the other rulebooks)

1. **Objective function** — what counts as success
2. **Hard constraints** — drawdown limits, loss limits, time windows, and their calculation basis
3. **Eligibility** — assets, instruments, timeframes; whether restrictions depend on stage
4. **Activity requirements** — minimum trades, active days, deadlines
5. **Risk model** — per-trade caps, leverage, VaR, lot limits
6. **Stages and transitions** — the progression ladder and what changes at each step
7. **Economics** — cost, split, payout, reset rules
8. **Prohibited behaviours** — banned conduct, especially anything that changes the *algorithm*
   rather than a parameter (e.g. a ban on reusing a strategy across accounts turns group generation
   into allocation **without replacement**)
