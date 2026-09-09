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

> **Sources — primary (Darwinex ZERO):** retrieved **2026-09-08** from
> `https://www.darwinexzero.com/docs/` — pages `risk-engine.md`, `var.md`, `d-leverage.md`,
> `drawdown-calculation.md`, `return-calculation.md`, `correlation-analysis.md`,
> `initial-training-phase.md`.
> Machine-readable index: `https://www.darwinexzero.com/docs/llms.txt` (**81 pages**, each served as
> clean `.md`).
> ⚠️ **`https://www.darwinexzero.com/llms.txt` returns 404** — the working index path is the one
> under `/docs/`. Recorded so the next retrieval does not repeat the mistake.
>
> **Sources — secondary (Darwinex CLASSIC, *not* Zero):** `https://help.darwinex.com/es/gestor_riesgo`
> — retrieved 2026-08-14. This page describes the **Darwinex Classic** risk manager.

> ### ⚠️ Classic vs Zero attribution caveats
>
> Every shared number between the Classic risk-manager page and the Zero risk pages **matches
> exactly**: the `Lev(investor)` formula, the 6.5% target, the 3.25–6.5% band, the 45-day window, the
> 6-month / 2:1 lookback, and all three D-Leverage caps.
>
> That agreement is **evidence, not a claim either source makes**:
>
> - **The 95% confidence level is stated ONLY on Zero's `var.md`.** The Classic page states no
>   confidence level. **Do not cite Classic for the 95% figure.**
> - **The regulatory-compliance framing** of the engine appears **only** on the Classic page. It is
>   not a Zero statement.
> - The **15% performance fee** appears on the Classic risk-manager page as a *rationale* for the
>   engine; on the Zero side the 15% is documented separately (`performance-fees.md`,
>   `darwinia-silver.md`).
> - **Neither page asserts the two engines are identical.** Treat the equivalence as **inference**,
>   not as a documented fact.

---

## 1. Why this model is different

Most funding services enforce **breach rules**: a max daily loss, a max total loss, and a profit
target. Cross a threshold and the account is terminated.

Darwinex Zero publishes **no such rule**. Across the risk pages and the whole 81-page documentation
index, **no maximum daily loss, no maximum total loss, and no account-termination-for-losses rule
appears at all**. This is recorded as an **absence in the documentation**, not as a quoted vendor
promise — see `SERVICE_Darwinex_Zero.md` § 2 for the precise wording and the only two sentences
Darwinex actually publishes on the subject.

Its single published risk constraint is a **target VaR**, and the platform does not terminate you for
missing it — it **rescales** you toward it. This is **normalization, not breach**. Any UI or
calculation that frames Darwinex Zero in terms of "headroom before you blow the account" is modelling
the wrong thing.

> ⚠️ **One forced-liquidation event does exist**, and it is a broker mechanic rather than a funding
> rule: **margin call at 100% of margin, stop-out at 50%**. See `SERVICE_Darwinex_Zero.md` § 2.1. A
> simulator that models "nothing can end the account" is wrong at the margin.

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

Verbatim from the Classic source:

> "los DARWINs cotizan con un VaR objetivo máximo de 6.5%, equivalente a un índice bursátil"

> "su % de VaR mensual siempre oscilará entre el 6.5% - 3.25%"

> "el algoritmo toma como periodo de referencia los últimos 45 días de operaciones abiertas del trader"

Verbatim from Zero's `var.md` — horizon and confidence:

> "At Darwinex Zero this horizon is 1 month."

> "6.5% monthly target VaR using a 95% confidence level. This means that DARWINs mighte be expected
> to lose 6.5% or more 1 month out of 20, or 5% of the time."

*(the typo "mighte" is verbatim in the source)*

### 2.1 VaR method — DOCUMENTED (no longer a known unknown)

Verbatim (`var.md`):

> "different scenarios get projected taking into account both historical data and thousands of Monte
> Carlo simulations matching both the risk and investment style."

The figure is read at the **95th percentile of the projected 1-month-ahead distribution**. So the
method is **hybrid historical + Monte Carlo simulation**, and it is **forward-looking by
construction** — not a historical percentile of realized returns.

**Documented VaR drivers** (`var.md`):

- trade frequency;
- leverage and duration of trades;
- market volatility and correlation of assets.

**Interpretation of the 95% / monthly pairing:** under normal market conditions, the strategy is
expected to lose more than the VaR figure in roughly 1 of every 20 months.

**Purpose of standardization.** Fixing every DARWIN at the same target VaR lets investors compare
scalpers against swing traders on **skill rather than gross leverage**. The VaR standard is a
comparability device first, a risk cap second.

Darwinex's own framing of VaR against drawdown (`drawdown-calculation.md`):

> "Drawdown shows what has happened in the past while VaR shows what could happen in the future with
> a given probability."

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

### 3.1 How the target VaR is determined — full procedure

Verbatim (`risk-engine.md`):

> "To determine the DARWIN's target VaR, historical VaR data is taken into account, starting with the
> most recent data with a look-back window of 6 months max, until the ratio between the maximum and
> minimum VaR is 2:1. Should the DARWIN never exceed this 2:1 ratio in the last 6 months, Darwinex
> Zero will take into account the last 6 months. Then, the current VaR of the DARWIN is divided by
> the maximum VaR calculated before. Lastly, this ratio gets multiplied by 6.5%, resulting in a VaR
> that will move between 3.25% - 6.5%."

Worked examples published on the same page:

| Current VaR | Max VaR in window | Target VaR |
|---|---|---|
| 8% | 12% | (8 / 12) × 6.5% = **4.33%** |
| 9% | 14% | (9 / 14) × 6.5% = **4.17%** |

> "the Risk Engine tolerates changes in VaR up to factor of 2 (up or down)"

**Consequence for the simulator:** the target VaR is **not a constant 6.5%**. It is a function of the
strategy's own recent VaR dispersion. A strategy whose risk has been *stable* sits near the top of
the band; one whose current VaR is far below its own 6-month maximum is assigned a **lower** target.

### 3.2 VaR Ratio — an observable, checkable quantity

- **`VaR Ratio = (Target VaR / Strategy VaR)`**
- Verbatim: *"'VaR Ratio' is the leverage ratio between a DARWIN and an underlying strategy."*
- Ratio **2** = the DARWIN trades at **2×** the underlying strategy; **0.5** = half.
- Verbatim: *"VaR Ratio can be checked at Darwin Live trades."*

> 📌 **This is the validation hook.** The VaR Ratio is visible in the Darwinex UI, so any model this
> app builds of the Risk Engine can be checked against a real published number rather than assumed.

### Operating levels

| Level | Behavior |
|---|---|
| Level 1 | Computes the size to open for investors when the trader submits an order |
| Level 2 | Continuous monitoring against maximum D-Leverage thresholds |

### Level 2 — maximum D-Leverage by position duration

*Source: `risk-engine.md` (**not** `d-leverage.md`).*

| Position held for | Max D-Leverage |
|---|---|
| < 30 minutes | 16.25 |
| 30 – 60 minutes | 13 |
| > 60 minutes | 9.75 |

Verbatim (`risk-engine.md`):

> "Maximum D-Leverage of 16.25 for positions of less than 30 minutes."

These caps bound the multiplier: the scaling described above is **not unbounded**.

⚠️ The **inclusive/exclusive treatment of the exact 30-minute and 60-minute boundaries** is
**NOT FOUND** — do not assume one.

---

## 4. Known unknowns

Per `INDEX.md` §5 (*"Do NOT invent domain criteria"*), the following are **not documented** in the
cited sources and must not be assumed:

- **The `f` factor.** It appears in the `Lev(investor)` formula on **both** the Classic and the Zero
  risk pages, and is **defined on neither**. Its definition and range remain unspecified.
- **Rebalancing cadence.** How frequently the multiplier is recomputed is not stated. The only
  published statement is that *"the Risk Engine can act at any moment in time"*.
- **Inclusive/exclusive treatment** of the 30-minute and 60-minute D-Leverage boundaries.
- **The intra-position asset correlation** used inside D-Leverage: its window, its series, and its
  numeric threshold are all unstated (see § 9b).
- **Sampling frequency of the return series** used in the 3-month DARWIN-vs-DARWIN correlation
  (see § 9a).

*Previously listed here and now **closed**: the **drawdown measurement methodology** (documented —
see § 7) and the **VaR method** (documented — see § 2.1).*

If a feature depends on any of the remaining items, flag it to the user rather than filling the gap.

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
| Method | Historical percentile of the realized series | **Historical data + thousands of Monte Carlo simulations** |
| Confidence | 95% | 95% |
| Consequence of exceeding | Displayed as a breach | Leverage rescaled toward target |

Three traps when bridging the two:

1. **Horizon.** Converting a daily VaR to a monthly one with the √t rule assumes i.i.d. returns.
   Strategy returns are autocorrelated, so √t scaling is an approximation, not a conversion. Prefer
   aggregating the daily net series into rolling ~21-trading-day windows and taking the 5th
   percentile directly.
2. **Realized vs. prospective.** Even a correctly-computed monthly VaR from *realized* closes is a
   proxy. Darwinex measures the risk of positions currently open, and projects it by simulation. The
   app can approximate the platform's number; it can never reproduce it.
3. **Capital base.** The app's VaR percentage is expressed over the portfolio's configured initial
   capital. If that differs from the Darwinex Zero account size, the percentage is not comparable
   to the 3.25–6.5% band at all — and the target itself is not fixed at 6.5% (see § 3.1).

### 5.1 Contrast with this application's DRAWDOWN

Now that the Darwinex methodology is documented (§ 7), the same contrast can be drawn — and it has a
**directional bias**, which the VaR contrast does not.

| Dimension | This app (typical backtest analytics) | Darwinex Zero |
|---|---|---|
| Series | Equity/return curve sampled at **daily closes** (or per closed trade) | The **DARWIN's historic quote data** |
| Sampling | Daily | **Every 30 seconds** |
| Definition | Peak-to-trough on the sampled curve | **Peak-to-trough on the return curve** |
| Open positions | Usually excluded until the trade closes | Included — quotes are continuous |
| Episode end | Typically ends at the trough | **Does not end until the return curve exceeds the prior peak** |

> 🔴 **The implication is one-directional: a drawdown computed on daily closes will systematically
> UNDERSTATE the figure Darwinex reports.** A 30-second sampling grid can catch an intraday trough
> that a daily close never records, and it never misses one that a daily grid does catch. The
> app's number is therefore a **lower bound** on the Darwinex number, not an estimate of it.
>
> This matters directly because drawdown is **11% of the Rating** (see `SERVICE_Darwinex_Zero.md`
> § 1). Any group ranked here on a daily-close drawdown is being ranked on a flattering figure.
>
> ⚠️ Do **not** "correct" the app's drawdown with an assumed inflation factor. No factor is
> published, and inventing one is exactly the failure mode `INDEX.md` §5 prohibits.

---

## 6. Application to IMOX strategies

The IMOX money-management protocol sizes at **$200 risk per trade (0.20% of $100k)** — see
`06_Gestion_de_Riesgo.md`. That is a per-trade sizing rule under a *fixed-amount* model and is
**independent** of the Darwinex VaR standard.

The two interact only at the DARWIN level: a portfolio sized per IMOX rules will produce whatever
monthly VaR it produces, and the Darwinex engine will then scale investor exposure toward its
target. A portfolio running far under 3.25% monthly VaR will be scaled up substantially, subject to
the D-Leverage caps in §3.

**This is not an instruction to raise IMOX per-trade risk.** The sizing doctrine and the vendor's
normalization layer are separate concerns and should stay separate.

---

## 7. Drawdown measurement — DOCUMENTED

*Source: `drawdown-calculation.md`. This closes a former "known unknown" in § 4.*

**Method: peak-to-trough on the return curve.** Verbatim:

> "a peak-to-trough decline in the return curve"

**Sampling — the load-bearing detail.** Verbatim:

> "The source data used to calculate the drawdown is the DARWIN's historic quote data, which is
> updated every 30 seconds. Therefore, the calculated drawdown is measured based on the DARWIN quote
> points with a 30-second update."

> ⚠️ **It is neither close-to-close nor daily-bar.** It is a **30-second quote grid**. See § 5.1 for
> what that does to any comparison with this app's own figure.

**Episode termination.** Verbatim:

> "The maximum drawdown period will not finish until the total return is once again above the peak."

**Official worked example:**

| Step | Value |
|---|---|
| Peak return | **47.54%** (9 Aug 2017) |
| Trough return | **26.84%** (17 Nov 2017) |
| Computation | `[((1 + 0.2684) − (1 + 0.4754)) / (1 + 0.4754)] × 100` |
| Result | **−14.03%** |
| Return / Drawdown | **3.05** |

Note the formula divides by `(1 + peak)`, i.e. the decline is expressed relative to the **equity
level at the peak**, not relative to the peak *return* figure.

---

## 8. Return calculation

*Source: `return-calculation.md`.*

- **Basis**, verbatim: *"a percentage of the equity used for a given position or at the start of a
  given timeframe"*.
- **Inputs:** closed trades (realized), open trades (unrealized), and deposits & withdrawals.
- **Compounding formula:**

```
[(1+(R1/100)) * (1+(R2/100)) * (1+(R3/100)) * (1+(Rn/100))... -1] * 100
```

- Verbatim consequence: *"cumulative returns don't equal the simple sum of period returns"*.

> 📌 **Simulator note.** Any multi-period Darwinex return this app displays must be **geometrically
> chained**, not summed. This also governs the Rating's "last 5 months + current month" component
> (`SERVICE_Darwinex_Zero.md` § 1) and the SILVER correlation tie-break, both of which are cumulative
> returns.

---

## 9. Correlation — TWO DISTINCT METRICS

> 🔴 **Do not conflate these.** They share a name and nothing else: different series, different
> windows, different consumers. Carrying figures from one to the other is a modelling error.

### 9a. DARWIN vs DARWIN — the funding eligibility gate

*Source: `correlation-analysis.md`.*

| Property | Value |
|---|---|
| Series | **DARWIN returns** |
| Window | *"We use the standard correlation formula taking into account last 3 months."* |
| Scale | −1 … +1 |
| "Highly correlated" | *"Values beyond +0.7 or -0.7 are considered highly correlated."* |
| **Eligibility filter** | *"Correlation will be measured only for DARWIN with a minimum rating of 60. It will not be measured for DARWINs with a lower rating."* |
| Sampling frequency of the return series | **NOT FOUND** |

This is the metric behind the DarwinIA correlation thresholds (0.95 vs other users; 0.5 vs your own,
**SILVER only**) documented in `SERVICE_Darwinex_Zero.md`.

> 📌 The **rating ≥ 60 filter** is new and material: below rating 60 a DARWIN is not measured for
> correlation at all.

### 9b. Intra-position asset correlation — an input to D-Leverage

*Source: `d-leverage.md`.*

- Verbatim: *"we measure the correlation of all the assets opened simultaneously"*.
- **Window: NOT FOUND. Numeric threshold: NOT FOUND.**

> ⚠️ **Do NOT carry the 3-month window or the ±0.7 threshold across from § 9a.** They belong to a
> different metric on a different series.

---

## 10. D-Leverage — definition and inputs

*Source: `d-leverage.md`.*

> "D-leverage is the proprietary tool we use at Darwinex Zero that measures the risk of a trading
> decision."

| Property | Value |
|---|---|
| Unit of measurement | **Per POSITION**, not per trade |
| Normalization | Standardized in **EURUSD terms** |
| Visibility | **Trading Journal** tab |

**Inputs:**

1. nominal leverage;
2. asset volatility (or the combination of assets);
3. correlation of the assets opened **simultaneously** (§ 9b);
4. duration.

**Official example of the EURUSD normalization:** if GBPJPY is **20% more volatile** than EURUSD,
then **EURUSD at 5:1 ≡ GBPJPY at 6:1**.

The duration caps that bound D-Leverage are in § 3, Level 2.

---

## 11. The risk-equivalent trading decision — computable

*Source: `initial-training-phase.md`. This is the arithmetic behind the 25-decision calibration gate.*

```
Exposure = D-Leverage × √(Time)          [Time = position duration]
```

**Weighting rule:** the position with the **greatest exposure counts as 1 decision**; every other
position weighs

```
weight_i = √(exposure_i) / √(exposure_max)
```

Fractions sum until **25** is reached.

**Official worked example** *(cell values indicative, reproduced from the page)*:

| Position | D-Leverage | Duration | Exposure | Weight |
|---|---|---|---|---|
| P1 | 20 | 30 min | 109.5 | 0.73 |
| P2 | 24 | 35 min | 141.9 | 0.84 |
| P3 | 30 | 45 min | **201.2** (max) | **1** |
| P4 | 22 | 10 min | 69.57 | 0.58 |
| | | | | **Σ = 3.15 decisions** |

> 🔴 **Read the normalization carefully — it is *relative*, and it caps at 1.**
> Because every weight is divided by the account's own maximum exposure, `weight_i ≤ 1` **always**.
> Longer holds raise a position's exposure, but they raise the denominator too. Absolute duration
> therefore **cannot** buy weight above 1, and **25 positions is a hard floor** on calibration —
> never fewer.
>
> Where duration *does* matter is **dispersion**: a book with one very large exposure drags every
> other position's weight **down**. A uniform book is the fastest path through calibration.
>
> See `SERVICE_Darwinex_Zero.md` § 4 for the re-derived calibration timeline that follows from this.

⚠️ **NOT FOUND:** whether `exposure_max` is scoped to the whole calibration period, to a rolling
window, or to a batch of simultaneous positions. The published example shows only a four-row table.
This ambiguity changes the answer materially and must not be guessed.
