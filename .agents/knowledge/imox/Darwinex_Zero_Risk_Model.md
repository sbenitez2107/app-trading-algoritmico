# Darwinex Zero — Risk Model (VaR + Risk Engine)

> **Scope warning — read this first.**
> This document is **NOT IMOX Academy doctrine**. It describes the proprietary risk model of a
> single external platform: **Darwinex Zero / DARWIN**. It is vendor rulebook, not trading
> methodology.
>
> - Do **not** apply this VaR concept to any other broker, funding service, or pipeline stage.
> - Do **not** promote these numbers into the "Key IMOX Thresholds" table in `INDEX.md`.
> - These values are set by Darwinex and **change over time**. Always re-verify against the live
>   rulebook before relying on them.
>
> **Source:** https://help.darwinex.com/es/gestor_riesgo — retrieved 2026-08-14.

---

## 1. Why this model is different

Most funding services enforce **breach rules**: a max daily loss, a max total loss, and a profit
target. Cross a threshold and the account is terminated.

Darwinex Zero does **none of that**. It has:

- **No maximum daily loss.**
- **No maximum total loss.**
- **No profit target.**

Its single risk constraint is a **target VaR**, and the platform does not terminate you for missing
it — it **rescales** you toward it. This is **normalization, not breach**. Any UI or calculation
that frames Darwinex Zero in terms of "headroom before you blow the account" is modelling the
wrong thing.

---

## 2. The VaR standard

| Property | Value |
|---|---|
| Target VaR (maximum) | **6.5%** |
| Operating range | **3.25% – 6.5%** (may fall below 3.25% in exceptional cases) |
| Time horizon | **Monthly** (30 days) |
| Confidence level | **95%** |
| Calculation window | **Last 45 days** of the trader's open positions |
| Target-VaR determination window | Up to **6 months** of historical VaR, walking most-recent to oldest until the max/min ratio reaches **2:1** |

Verbatim from the source:

> "los DARWINs cotizan con un VaR objetivo máximo de 6.5%, equivalente a un índice bursátil"

> "su % de VaR mensual siempre oscilará entre el 6.5% - 3.25%"

> "el algoritmo toma como periodo de referencia los últimos 45 días de operaciones abiertas del trader"

**Interpretation of the 95% / monthly pairing:** under normal market conditions, the strategy is
expected to lose more than the VaR figure in roughly 1 of every 20 months.

**Purpose of standardization.** Fixing every DARWIN at the same target VaR lets investors compare
scalpers against swing traders on **skill rather than gross leverage**. The VaR standard is a
comparability device first, a risk cap second.

---

## 3. The Risk Engine

The engine monitors the trader's account and applies a dynamic multiplier so the investable index
(the DARWIN) tracks the target VaR, regardless of how much or how little leverage the trader uses.

```
Lev(investor) = Lev(trader) × (Target VaR / Strategy VaR) × f
```

Consequence worth internalizing: **running well below the target VaR does not make the DARWIN
safer.** It makes the engine scale the position sizes *up* to reach the target. Under-risking is
not conservatism at the DARWIN level — it is an instruction to the engine to multiply.

### Operating levels

| Level | Behavior |
|---|---|
| Level 1 | Computes the size to open for investors when the trader submits an order |
| Level 2 | Continuous monitoring against maximum D-Leverage thresholds |

### Level 2 — maximum D-Leverage by position duration

| Position held for | Max D-Leverage |
|---|---|
| < 30 minutes | 16.25 |
| 30 – 60 minutes | 13 |
| > 60 minutes | 9.75 |

These caps bound the multiplier: the scaling described above is **not unbounded**.

---

## 4. Known unknowns

Per `INDEX.md` §5 (*"Do NOT invent domain criteria"*), the following are **not documented** in the
cited source and must not be assumed:

- **Measurement methodology.** Whether the VaR is computed peak-to-trough, close-to-close, or
  intraday is not stated.
- **The `f` factor.** Its definition and range are not specified in the risk-manager page.
- **Rebalancing cadence.** How frequently the multiplier is recomputed is not stated.

If a feature depends on any of these, flag it to the user rather than filling the gap.

---

## 5. Contrast with this application's VaR

The platform computes its own VaR for portfolios (`PortfolioAnalyticsCalculator`). **It is not the
same metric** and the two numbers are not directly comparable.

| Dimension | This app | Darwinex Zero |
|---|---|---|
| Horizon | **Daily** | **Monthly** (30d) |
| Window | 250 days | 45 days |
| Input | **Realized** close-to-close daily net P&L | **Open-position** risk (volatility × position size) |
| Direction | Backward-looking (historical percentile) | Forward-looking |
| Confidence | 95% | 95% |
| Consequence of exceeding | Displayed as a breach | Leverage rescaled toward target |

Three traps when bridging the two:

1. **Horizon.** Converting a daily VaR to a monthly one with the √t rule assumes i.i.d. returns.
   Strategy returns are autocorrelated, so √t scaling is an approximation, not a conversion. Prefer
   aggregating the daily net series into rolling ~21-trading-day windows and taking the 5th
   percentile directly.
2. **Realized vs. prospective.** Even a correctly-computed monthly VaR from *realized* closes is a
   proxy. Darwinex measures the risk of positions currently open. The app can approximate the
   platform's number; it can never reproduce it.
3. **Capital base.** The app's VaR percentage is expressed over the portfolio's configured initial
   capital. If that differs from the Darwinex Zero account size, the percentage is not comparable
   to the 3.25–6.5% band at all.

---

## 6. Application to IMOX strategies

The IMOX money-management protocol sizes at **$200 risk per trade (0.20% of $100k)** — see
`06_Money Management.md`. That is a per-trade sizing rule under a *fixed-amount* model and is
**independent** of the Darwinex VaR standard.

The two interact only at the DARWIN level: a portfolio sized per IMOX rules will produce whatever
monthly VaR it produces, and the Darwinex engine will then scale investor exposure toward its
target. A portfolio running far under 3.25% monthly VaR will be scaled up substantially, subject to
the D-Leverage caps in §3.

**This is not an instruction to raise IMOX per-trade risk.** The sizing doctrine and the vendor's
normalization layer are separate concerns and should stay separate.
