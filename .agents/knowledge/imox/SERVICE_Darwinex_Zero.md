# Service Rulebook — Darwinex Zero

> **Type**: `vendor-rulebook` + `provider-data`
> **Retrieved**: **2026-09-08** from the official Darwinex Zero documentation site
> `https://www.darwinexzero.com/docs/`, whose machine-readable index is
> `https://www.darwinexzero.com/docs/llms.txt` (**81 pages**, each served as clean `.md`).
> ⚠️ **`https://www.darwinexzero.com/llms.txt` returns 404** — record the working `/docs/` path so the
> next retrieval does not repeat the mistake.
> Earlier pass (2026-09-07) additionally used `darwinexzero.document360.io` and
> `help.darwinex.com` (the latter is **Darwinex CLASSIC**, not Zero). **Re-verify before acting.**
> **Schema**: the common 8-field schema defined in [SERVICE_Own_Capital.md](SERVICE_Own_Capital.md).
> **Confidence**: HIGH = stated plainly on an official page · MEDIUM = official but ambiguous, or
> secondary · LOW = third-party only.
>
> **Companion document**: [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) holds the VaR
> and Risk Engine model in depth and is **cited by path from production code** — do not move or
> restructure it. § 5 below cross-references rather than duplicates.

---

## 🚨 The finding that changes the simulator

**Darwinex Zero enforces correlation as a hard eligibility gate, with published numeric thresholds.**

| Correlation | Threshold | Applies to | Consequence | Conf. |
|---|---|---|---|---|
| **Against OTHER USERS' DARWINs** | **> 0.95** | **SILVER and GOLD** | The **lower-performing** DARWIN is **excluded from DarwinIA** | HIGH |
| **Against YOUR OWN other DARWINs** | **> 0.5** | **SILVER ONLY** | The **weaker** one is excluded — the higher cumulative return over *"the previous 5 months and current month"* wins | HIGH |

> ⚠️ **Correction (was wrong in an earlier version of this file).** The **0.5 same-user threshold is
> SILVER-only**. GOLD requires **only** the `< 0.95` versus other users; **no same-user threshold is
> documented for GOLD**.
>
> 🔴 **And the exclusion does not even apply across tiers.** Verbatim, on **both**
> `darwinia-silver.md` and `darwinia-gold.md`:
>
> > "Correlation between DARWINs from SILVER and GOLD are not taken into account."
>
> So **two of your own DARWINs do not penalise each other if one is in SILVER and the other in
> GOLD.** That is a structural escape hatch the simulator must model, not a footnote.
>
> Eligibility filter on the metric itself: correlation *"will be measured only for DARWIN with a
> minimum rating of 60"* — see [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) § 9a.

> Everywhere else in this knowledge base, correlation is a **quality heuristic** — something the
> academy recommends measuring to build a smoother equity curve. **At Darwinex Zero it is a funding
> gate with a number attached.** A group can be excellent and still earn nothing because it correlates
> above 0.5 with another SILVER account you already run.
>
> **This is the first hard, external, numeric definition of "decorrelated" the project has.**

**It also answers the user's own question directly.** The plan in
[10_Mentoria_1a1_PENDIENTE.md](10_Mentoria_1a1_PENDIENTE.md) is two Darwinex accounts — one with
ORO/NQ/DAX/divisa on MT4, another with BTC. **The 0.5 threshold is exactly the constraint that plan
must satisfy while both DARWINs sit in SILVER**, and the instinct to split by asset class is sound:
crypto against indices and gold is the natural way to stay under it. But it must be **measured**, not
assumed. (Note also that the crypto account is a **separate registration** — see § 3.)

---

## 1. Objective function

**Darwinex Zero has a fully specified, computable objective — the `Rating`.**

| Component | Weight | Conf. |
|---|---|---|
| **Current calendar-month return** | **22%** | HIGH |
| **Last 5 months + current month return** | **67%** | HIGH |
| **Max drawdown over current + preceding 5 calendar months** | **11%** | HIGH |

**Track-record bonus points**: +1 (6-12 months) · +2 (12-18 months) · **+3 (> 18 months)**. `HIGH`

| Fact | Value | Conf. |
|---|---|---|
| **Rating scale bounds ("0-100")** | **NOT FOUND** — see the correction below | — |
| **Mapping from return/drawdown to Rating points** | **NOT FOUND** | — |
| **Threshold for guaranteed allocation** | **Rating ≥ 75** — SILVER is **non-competitive**: everyone above it gets funded | HIGH |
| Rating floor for correlation to be measured at all | **60** | HIGH |
| GOLD ranking basis | Ranked **only on current month's return**, top-down until allocations are exhausted | HIGH |
| What actually earns money | **15% performance fee** on profits from **allocated** capital | HIGH |
| ⚠️ Critical | **A payout requires an ALLOCATION, not just profit.** Trading profit on the virtual account alone pays **nothing** | HIGH |

> ⚠️ **Correction — the "0-100 scale" was asserted at HIGH and is not documented.** The docs state
> only the three weights (22 / 67 / 11), the track-record bonus points, and the threshold of **75**.
> **Neither the scale bounds nor the function mapping return and drawdown to points is published.**
> Downgraded from HIGH to **NOT FOUND**. The number 75 is real; "out of 100" is our inference, and
> `INDEX.md` § 5 forbids inventing the rest.

> **Note the shape**: returns carry **89%** of the Rating and drawdown only **11%**. Darwinex Zero
> rewards return far more than smoothness — the smoothness discipline comes from the **VaR engine**
> (§ 5) and the **correlation gate**, not from the Rating.
>
> 📌 The 67% component is a **cumulative** return, so it compounds geometrically rather than summing —
> see [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) § 8. And the 11% drawdown component
> is measured on a **30-second quote grid**, so a daily-close drawdown understates it (§ 5.1 there).

---

## 2. Hard constraints

| Constraint | Value | Conf. |
|---|---|---|
| **Daily loss limit** | **No such rule appears anywhere in the 81-page documentation** | MEDIUM ⚠️ |
| **Total / trailing drawdown limit** | **No breach rule appears.** Drawdown is **scored** (11% of Rating), not enforced | MEDIUM ⚠️ |
| **Account termination for losses** | **No such rule appears anywhere in the 81-page documentation** | MEDIUM ⚠️ |
| Profit target | **No return requirement documented for calibration** | HIGH |
| Time window | Calibration nominally **90 days**, but **extends indefinitely while actively trading** — the limit exists only to purge inactive accounts | HIGH |
| Permanent Allocation | *"No expiry date, no penalties, not even in drawdown"* | HIGH |
| High-water-mark loss cap | Losses beyond **−5%** on active DarwinIA allocations are capped at −5%; full reset to 0% when all allocations expire in loss | MEDIUM |
| **Margin call / stop-out** | **EXISTS** — see § 2.1 | HIGH |

### What Darwinex actually publishes on the subject

From `darwinex-zero-versus-funding-programs.md` (updated 2026-03-20), the two sentences that exist —
quoted exactly, and **nothing beyond them**:

> "Zero's model is scalable and transparent and is designed for the long-term. You trade your own
> way, without any restriction on the trading activity, nor any specific return & drawdown targets.
> We back you along the way to finally being able to manage investor capital."

and, under the heading **"No restrictions"**:

> "Instruments traded, volume, duration of the trades...your trade your own way."

> 🔴 **Precision required — do not overstate this.**
>
> - The phrase **"daily loss limit" does not appear** anywhere on that page, or on any other page in
>   the index. **Never attribute the words "no daily loss limit" to Darwinex in quotation marks.**
> - **"Maximum drawdown" does not appear.** The page says *"specific return & drawdown **targets**"* —
>   which covers **targets**, not a numeric max-drawdown **breach** rule.
> - **"Profit target" does not appear literally**; *"specific return … targets"* is the equivalent.
>
> What we have is an **absence**: a full sweep of the 81-page index found **no** maximum daily loss,
> **no** maximum total loss, and **no** account-termination-for-losses rule. An absence in the
> documentation is strong, but it is not a vendor guarantee — **and the legally binding T&C is not
> published under `/docs/` at all.** **MEDIUM, not HIGH.** If a hard limit matters to a decision,
> confirm it with Darwinex directly.

### 2.1 ⚠️ Margin call and stop-out — the one forced-liquidation event

| Event | Level | Conf. |
|---|---|---|
| **Margin call** | at **100%** of margin | HIGH |
| **Stop-out** | at **50%** | HIGH |

Stop-out behaviour: it *"automatically closes the losing trades with the highest losses to restore
equity above the 50% threshold."*

> 🔴 **This is load-bearing for the simulator.** It is **not** a funding breach rule — nobody
> "fails" — but it **is** an account-level forced liquidation, and it is the one mechanism by which
> a Darwinex Zero account can have positions closed against the trader's will. A model that assumes
> "nothing can end or truncate the account" is wrong at the margin.

**This is still the opposite shape from FTMO.** FTMO fails you on an equity floor; Darwinex Zero
**scores** you and withholds funding. Nothing blows up on a rule — you simply do not get allocated,
and the only involuntary event is the broker-level stop-out above.

---

## 3. Eligibility

| Item | Value | Conf. |
|---|---|---|
| Who may subscribe | *"any adult person can subscribe to Zero"* / *"without geographic restrictions"* | HIGH |
| Specific minimum age | **NOT FOUND** | — |
| Professional / retail status requirement | **NOT FOUND** | — |
| Payment restrictions | May apply in some countries; may require accepting **EUR, GBP or USD** | HIGH |
| **KYC** | **Not** a subscription prerequisite. Required for **withdrawals** (ID document + address) and to access a real **Darwinex Classic** account | HIGH |
| Account types | 5: CFD (MT4) · CFD (MT5) · Crypto CFD (MT5) · Futures (MT5) · Cash non-leveraged stocks & ETFs (MT5) | HIGH |
| Forex | **42 pairs** (individual pair names **NOT FOUND**) | HIGH |
| Commodities | **4**: XAGUSD, XAUUSD, XNGUSD, XTIUSD | HIGH |
| Indices | **10**: AUS200, FCHI40, GDAXI, J225, NDX, SPA35, SPX500, STOXX50E, UK100, WS30 | HIGH |
| US stocks / ETFs | ~800 / ~100 (MT5 only) | HIGH |
| **Crypto** | **7**: *"Bitcoin (BTCUSD), Ethereum (ETHUSD), XRP (XRPUSD), Solana (SOLUSD), Binance Coin (BNBUSD), Cardano (ADAUSD), Dogecoin (DOGEUSD)"* (MT5 only) | HIGH |
| Futures | CME + Eurex, multiple asset classes (full contract inventory **NOT FOUND**) | HIGH |
| Total | **1,500+ instruments** | HIGH |
| **Timeframe restrictions** | **NOT FOUND** — none documented on any page checked | — |
| **Minimum holding time** | **NOT FOUND** — none documented on any page checked | — |
| Tier-dependent restrictions | **NOT FOUND** — asset access follows the subscription type chosen, not a stage | MEDIUM |

> ✅ **GDAXI, NDX and XAUUSD are all present** — the three instruments the existing asset profiles
> cover ([GDAXI_Profile.md](GDAXI_Profile.md), [US100_Profile.md](US100_Profile.md),
> [XAUUSD_Profile.md](XAUUSD_Profile.md)). The declared plan's instrument set is fully tradable here.

### 3.1 Starting balances by account type

| Account | Platform | Starting balance | Conf. |
|---|---|---|---|
| **CFD standard** | MT4 or MT5 | **$100K** | HIGH |
| **Futures** | MT5 | **$1M** | HIGH |
| **Crypto CFD** | MT5 | **EUR100,000** | HIGH |
| Cash US stocks & ETFs | MT5 | **NOT FOUND** | — |

Verbatim (`darwinex-zero-leverage.md`): *"The standard CFD account comes with $100K or $1M (futures)
in initial balance"*.

> ✅ **Upgraded MEDIUM → HIGH.** The $100,000 virtual balance is now directly confirmed for the
> standard CFD account. Note the currency inconsistency in the source itself: the CFD and futures
> balances are stated in **USD**, the crypto balance in **EUR**. Not converted here.

### 3.2 Leverage by asset class

*Source: `darwinex-zero-leverage.md`.*

| Asset class | Leverage | Conf. |
|---|---|---|
| Forex | up to **30:1** | HIGH |
| Commodities | up to **20:1** | HIGH |
| Indices | up to **20:1** | HIGH |
| US stocks & US ETFs (CFD) | **5:1** | HIGH |
| **Cryptocurrencies** | **1:1** | HIGH |
| Cash US stocks & ETFs account | **no leverage** | HIGH |
| Futures | Exchange margin, no fixed ratio (corn example ≈ **16:1**) | HIGH |

Margin requirements range **3.33% to 20%** of trade value depending on asset class.

> 🔴 **Crypto is 1:1 — this is material for the BTC plan.** The second-account idea in
> [10_Mentoria_1a1_PENDIENTE.md](10_Mentoria_1a1_PENDIENTE.md) assumed nothing about leverage; at
> **1:1** on a **EUR100,000** balance, position sizing and therefore achievable VaR are radically
> different from the 30:1 FX account. Availability is not equivalence.
>
> ⚠️ And the knowledge base still has **no academy doctrine for crypto** — no instrument
> configuration, no thresholds, no asset profile.

### 3.3 The crypto account is a SEPARATE account

- Requires registration with **different credentials**.
- Verbatim: *"Cannot currently migrate to main Darwinex platform."*
- Crypto is tradable **24h Monday through Friday** — **not** 24/7.

### 3.4 Platform mechanics

| Item | Value | Conf. |
|---|---|---|
| **Hedging mode is FORCED** | *"MetaTrader accounts are set on 'hedging mode', not being possible change it to 'netting mode'."* | HIGH |
| MT4 | 9 timeframes, 4 pending order types | HIGH |
| MT5 | 21 timeframes, 6 pending order types, DOM | HIGH |
| MT5-only asset classes | US stocks/ETFs, crypto, futures | HIGH |
| TradingView | Selectable as the platform **at subscription time**; MT accounts **cannot be converted** | HIGH |
| **Futures rollover** | **NOT automatic**: *"Rollover is not automatically placed."* Three dates per contract: trading-available (~10 days before the previous expiry closes), close-only, and date-of-close (open positions **automatically terminate**). Failure to roll = position closed, **no reinstatement** | HIGH |

### 3.5 Server time

*Source: `time-in-darwinex-metatrader-terminals.md`.*

- **"New York Close"** convention. **GMT+3** during US DST, **GMT+2** during US standard time.
- **DST follows the US calendar, not the European one.**
- Set centrally; users cannot change it locally.
- **Explicit daily-candle close clock time: NOT FOUND** (implied by NY Close plus the 17:00 NY swap
  settlement in § 7.3, but not stated).

> ⚠️ This is a **backtest-alignment gotcha**: a strategy developed on a GMT+2/GMT+3 *European* DST
> calendar will have its daily candles cut on different days for several weeks a year.

### 3.6 Execution

*Source: `trading-order-execution.md`.*

| Item | Value | Conf. |
|---|---|---|
| **Execution model (market maker / STP / ECN)** | **NEVER NAMED — NOT FOUND** | — |
| Instrument treatment | *"Currencies and contracts for differences (CFDs) are -for the time being- traded over the counter."* | HIGH |
| Route | Platform → Darwinex central server at **Equinix LD4 London** → Prime Broker → LPs | HIGH |
| Liquidity providers | **20+**; LPs have up to **200ms** to accept | HIGH |
| Platform-to-server latency | **100-200ms** | HIGH |
| Slippage | Explicitly **symmetric**: *"the asset price may have moved, in your favour or against you."* | HIGH |
| Toxic order flow / requotes / rejections | **NOT FOUND** | — |

### 3.7 Coexistence with Darwinex Classic

- Same credentials work on both platforms. Verbatim: *"You can have simultaneous active accounts at
  Darwinex and Zero... it is not necessary to create a new account or associate a different email."*
- Zero users need **Darwinex verification** to access a real Classic account.

---

## 4. Activity requirements

| Requirement | Value | Conf. |
|---|---|---|
| **Calibration** | **25 risk-equivalent trading decisions** over a minimum of **15 trading days** | HIGH |
| "Risk-equivalent decision" | See § 4.1 — now fully computable | HIGH |
| DARWIN creation | Automatic the **following Monday** after calibration; the calibration track record **carries over** | HIGH |
| DarwinIA SILVER activity | **≥ 1 trade in the current or previous month** | HIGH |
| DarwinIA GOLD track record | **> 8 months** of signal history, plus one of five return/drawdown gates (§ 6) | HIGH |
| Minimum trading days, ongoing | **Does NOT exist** after calibration | MEDIUM |

### 4.1 🔴 The risk-equivalent decision formula — load-bearing for the simulator

*Source: `initial-training-phase.md`. Full detail and the official worked example are in*
*[Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) § 11.*

```
Exposure  = D-Leverage × √(Time)                    [Time = position duration]
weight_i  = √(exposure_i) / √(exposure_max)         [the largest-exposure position counts as 1]
```

Fractional weights sum until **25** is reached. Official example: exposures 109.5 / 141.9 / **201.2**
/ 69.57 give weights 0.73 / 0.84 / **1** / 0.58 = **3.15 decisions**.

**Two properties that decide everything downstream:**

1. **The normalization is relative to your own book**, not to an absolute scale.
2. **`weight_i ≤ 1` always.** Longer holds raise a position's exposure — but they raise
   `exposure_max` too. **Duration cannot buy a weight above 1.**

### 4.2 ⚠️ Fit check against the declared plan — the "6-8 months" estimate is SUPERSEDED

The declared profile is **3-4 trades a month**. An earlier version of this file estimated that
calibration *"would take 6-8 months"* at that rate. With the formula now known, here is the honest
re-derivation, arithmetic shown so it can be audited:

**Best case — a uniform book (all positions similar exposure ⇒ every weight ≈ 1):**

```
positions needed = 25          (the hard floor: weight ≤ 1)
25 ÷ 4 trades/month = 6.25 months
25 ÷ 3 trades/month = 8.33 months
⇒ 6.3 – 8.3 months
```

**Dispersed book — one outsized position sets `exposure_max`.** If a typical position runs at, say,
a quarter of the account's largest exposure:

```
weight = √(0.25) = 0.50
positions needed = 25 ÷ 0.50 = 50
50 ÷ 3.5 trades/month ≈ 14.3 months
```

**Portfolio of 5 strategies, uniform, 3-4 trades/month each = 15-20 positions/month:**

```
25 ÷ 20 = 1.25 months   …   25 ÷ 15 = 1.67 months
⇒ ~1.3 – 1.7 months, subject to the 15-trading-day floor
```

> 🔴 **Verdict: "6-8 months" was not pessimistic — it was the OPTIMISTIC floor.** The intuition that
> `√(time)` "rewards long holds" and would therefore shorten calibration **does not survive the
> formula**: the square root is applied to a ratio against the trader's own maximum exposure, so
> absolute duration cancels. A swing position does not reach weight 1 *sooner*; it reaches weight 1
> **only if nothing else in the book is bigger**. Dispersion makes calibration **longer**, never
> shorter.
>
> ⚠️ **Marked as unquantified for a single strategy.** The single-strategy figure depends on an
> undocumented question — **whether `exposure_max` is scoped to the whole calibration period, a
> rolling window, or a batch of simultaneous positions (NOT FOUND)** — which moves the answer between
> the 6.3-8.3 and the ~14 month branches. Do not quote a single number without stating the scoping
> assumption.

- The **only robust lever is portfolio breadth**: more strategies running concurrently at similar
  exposure. This is a concrete argument for starting Darwinex with a **group** rather than one
  system, and it is a much stronger argument than the earlier estimate implied.
- The 15-**trading-day** minimum is a separate axis: trades must fall on ≥ 15 distinct days, so
  clustering does not help.
- **There is no deadline pressure** — calibration extends indefinitely while active.

**And the payoff for patience is explicit**: the track-record bonus reaches **+3 Rating points after
18 months**, and **GOLD requires > 8 months of history**. Darwinex Zero structurally rewards exactly
the long-horizon profile the academy prefers — but calibration itself rewards **breadth**, not
patience.

---

## 5. Risk model

**Authoritative document: [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md)** — it is cited
from production code. Summary only:

| Item | Value | Conf. |
|---|---|---|
| Target VaR (maximum) | **6.5% monthly**, **95% confidence** *(the 95% figure is Zero-only — see the attribution caveats in the companion doc)* | HIGH |
| VaR band | **3.25% – 6.5%** monthly | HIGH |
| **Target VaR is NOT constant** | Computed as `(current VaR ÷ max VaR in window) × 6.5%`. Official examples: 8/12 → **4.33%**; 9/14 → **4.17%** | HIGH |
| Target-VaR lookback | Up to **6 months**, walking most-recent to oldest until the max:min VaR ratio reaches **2:1** | HIGH |
| Engine tolerance | *"the Risk Engine tolerates changes in VaR up to factor of 2 (up or down)"* | HIGH |
| Strategy-VaR estimation window | Last **45 days of market exposure** | HIGH |
| Leverage multiplier | `Lev(investor) = Lev(trader) × (target VaR / strategy VaR) × f` — **`f` is undefined on every source page** | HIGH |
| **VaR Ratio** | `Target VaR / Strategy VaR`; *"VaR Ratio can be checked at Darwin Live trades"* — **observable in the UI** | HIGH |
| **D-Leverage caps by duration** | **< 30 min → 16.25** · **30-60 min → 13** · **> 60 min → 9.75** — *source: `risk-engine.md`* | HIGH |
| D-Leverage unit | **Per POSITION**, standardized in **EURUSD terms**; visible in the Trading Journal tab | HIGH |
| D-Leverage inputs | Nominal leverage, asset volatility vs EURUSD, correlation of assets opened simultaneously, duration | HIGH |
| VaR method | *"historical data and thousands of Monte Carlo simulations"*, read at the 95th percentile of the 1-month-ahead distribution | HIGH |
| **Drawdown method** | **Peak-to-trough on the return curve, sampled from quote data updated every 30 seconds** | HIGH |

> ✅ **Correction — the D-Leverage duration bucket.** An earlier version of this file said
> **"< 15 min → 16.25"**. That was **wrong** and it contradicted the companion document. The
> verbatim source (`risk-engine.md`) reads: *"Maximum D-Leverage of 16.25 for positions of less than
> 30 minutes."* The table above is now **< 30 min**, matching
> [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) § 3. Also corrected: this table lives on
> **`risk-engine.md`**, not on `d-leverage.md`.
> ⚠️ The **inclusive/exclusive treatment at exactly 30 and 60 minutes is NOT FOUND**.

> 🔁 **Reminder from [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md)**: the 6.5% is **Darwinex's
> standard, not an academy limit**. It applies here and nowhere else by default.
>
> 📌 **D-Leverage penalises long holding periods** (9.75 above 60 minutes vs 16.25 under 30). A swing
> portfolio runs at the most constrained end of the leverage ladder — consistent with, and a second
> mechanism behind, the academy's preference for lower risk on H4.
>
> 📌 **The drawdown methodology has a directional consequence for this app**: a drawdown computed on
> daily closes will **systematically understate** what Darwinex reports. See
> [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) § 5.1.

---

## 6. Stages and transitions

| Stage | Rules | Conf. |
|---|---|---|
| **Calibration** | 25 risk-equivalent decisions / ≥15 trading days. No return target, no pass-fail beyond activity | HIGH |
| **DarwinIA SILVER** | Requires: calibration done · active subscription · **correlation < 0.95 vs other users** and **< 0.5 vs your own DARWINs** · ≥1 trade current-or-prior month. **Rating ≥ 75 → allocation.** €30,000 min → €375,000 max · **3-month** duration ⇒ **up to 3 simultaneous allocations** · 15% fee, quarterly, HWM. Ranking updated **hourly**; final ranking at market close on the **last trading day of the month** | HIGH |
| **DarwinIA GOLD** | Requires **> 8 months** history + one of the five gates below + correlation **< 0.95 vs other users** (**no same-user threshold documented**) + divergence **> −0.4** if managing investor capital. **€50,000 → €500,000** · **6-month** duration · ranked **only on current-month return** | HIGH |
| **Zero Boosters** *(purchasable)* | €25k / €50k / €100k / €200k notional · durations per source (see D5) · time-based or 5%/10%-target pricing. Cap **€500,000** active+pending (see D4) · independent HWM per booster · **no HWM reset** | HIGH |
| **Permanent Allocation** *(purchasable)* | Instant: €25k = €455/$505 · €50k = €900/$1,000 · €100k = €1,795/$1,995. Target-based: €50k@5% = €500 · €50k@10% = €270 · €100k@5% = €995 · €100k@10% = €535. **Max €100k total.** No expiry; lost only on subscription restart. Requires the DARWIN to exist, no open trades in closed markets, and **not** between **17:00-18:00 New York time**. **HWM reset behaviour: NOT FOUND** | HIGH |
| **Investor capital** | Unlocked on **qualifying for GOLD**; the DARWIN **stays open to investors even if it later stops meeting GOLD criteria**. Reported AUM — see the caveat below | MEDIUM |

### 6.1 SILVER ⇄ GOLD are mutually exclusive

- Verbatim: *"in no case could a DARWIN participate simultaneously in both."* Changes apply the
  **following month**.
- **GOLD demotion**: a DARWIN falls back to **SILVER automatically the following month** if the
  criteria lapse, and **returns to GOLD** when they are met again.
- **Concurrent GOLD allocation count: NOT FOUND.**

### 6.2 GOLD entry — five alternative gates (any ONE suffices)

**All five additionally require `Return / Drawdown > 2.5`:**

| Track record | Return gate |
|---|---|
| 1-year | **> 20%** |
| 2-year | **> 25%** |
| 3-year | **> 30%** |
| 4-year | **> 35%** |
| 5-year | **> 40%** |

*"Or since inception if DARWIN lifetime is shorter than 1 year"*.

**GOLD futures restriction**: *"In order to open a DARWIN investment whose underlying asset is a Zero
futures account, the MBT asset (BTC micro futures) must be deactivated."*

### 6.3 Correlation-exclusion credit — applies to BOTH tiers

> ⚠️ **Correction**: an earlier version of this file recorded this as GOLD-only. A DARWIN with
> **Rating > 75** excluded for **correlation** receives a **credit equal to the next monthly
> subscription**, usable against a restart — **in both SILVER and GOLD**.

### 6.4 6-Month Guaranteed Allocation *(was entirely absent from this knowledge base)*

| Item | Value | Conf. |
|---|---|---|
| Booster | **EUR25,000**, **3 months**, **at no cost** | HIGH |
| Trigger | You receive **NO DarwinIA allocation in the first 6 months** after registration | HIGH |
| Correlation | *"Received even if the DARWIN is excluded due to correlation criteria."* | HIGH |
| Form | Delivered as a **credit** usable toward other boosters | HIGH |
| Reset behaviour | **Restarting the subscription restarts the 6-month clock** | HIGH |
| Blocker | Not granted if you already hold the **simultaneous booster cap** | HIGH |

### 6.5 Allocation caps and stacking

- **All allocation types combine simultaneously** (DarwinIA + Boosters + Permanent).
- Each type has an **independent HWM** and an **independent fee calculation**.
- Caps: **Boosters €500k** (see contradiction **D4**) · **Permanent €100k** · **combined ceiling
  €600k**.
- Verbatim: *"Restarting the subscription results in the loss of any type of allocation."*
- **DarwinIA HWM resets** (`performance-fees.md`): all allocations expiring at a loss → P&L resets to
  **0%**; losses beyond **−5%** on active allocations are **capped at −5%**.
- **Booster allocations have no HWM reset. Permanent allocation HWM reset: NOT FOUND.**
- **Whether an allocation is terminated or reduced while in drawdown: NOT FOUND.**

---

## 7. Economics

| Item | Value | Conf. |
|---|---|---|
| **Monthly — CFDs / Cash** | **45€** (Europe & UK) · **$50** (rest of world) | HIGH |
| **Monthly — Crypto CFDs** | ⚠️ **CONTRADICTORY IN THE SOURCE**: `pricing.md` lists **45€ / $50**; `crypto-cfds.md` states *"The Crypto CFD Account is available for a flat fee of EUR38 (or $43) per month"*. See **D1** | MEDIUM ⚠️ |
| **Monthly — Futures** | **50€** · **$56** | HIGH |
| 1-Year pack | 420€/$480 (CFD/Cash/Crypto) · 480€/$552 (Futures) | HIGH |
| 3-Year pack | 1,080€/$1,260 · 1,260€/$1,476 (Futures) — includes a **190€/$210 booster credit** | HIGH |
| **Performance split** | **15% to the trader** on profits over HWM. Quarterly by default | HIGH |

### 7.1 ⚠️ Where the academy's 38€/40€ figure actually comes from — CORRECTED

> 🔴 **The previous explanation in this file was wrong and has been deleted.** It claimed the 40€ in
> [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) traced to *"a stale €38 in the meta
> description"* of the pricing page. **It does not.**
>
> **EUR38 is a live, current price in the body of an official page.** Verbatim (`crypto-cfds.md`):
>
> > "The Crypto CFD Account is available for a flat fee of EUR38 (or $43) per month"
>
> …while `pricing.md` lists Crypto CFDs at **45 EUR / $50**.
>
> **This is an unresolved contradiction between two official pages** (logged as **D1** in § 9), not a
> stale artifact. Do not present the 45€ crypto price as settled.

The **~50€** in [00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md) is right *only* for
the rest-of-world USD price ($50), for Futures in the EU (50€), or for a CFD account carrying the
monthly-HWM add-on.

### 7.2 Monthly HWM add-on

| Item | Value | Conf. |
|---|---|---|
| Price | **+EUR12 / $13 per month** | HIGH |
| Election window | **Only within 14 days** of account activation or reset | HIGH |
| Applies to | DarwinIA / Booster / Permanent allocations | HIGH |
| **Does NOT apply to** | **Investor capital** — that stays **quarterly** | HIGH |
| Official worked example | **EUR450** over three months under monthly HWM vs **EUR375** under quarterly | HIGH |

### 7.3 Costs, swaps and platform accounting

- Zero clients are **never charged commissions or swaps to their card**; costs affect the **signal
  account and the DARWIN performance only**.
- **Swap settlement at 17:00 New York** daily. Friday settlements occur **Sunday 17:00 NY**.
  **TRIPLE SWAP ON WEDNESDAY** (covers Friday, Saturday and Sunday).
- **Futures have no daily swap.**
- 🔴 **MT4 vs MT5 commission booking — a backtest reconciliation gotcha.** Verbatim:

  > "MT4 => Roundtrip (100% applied at the entry)  MT5 => 50% applied at the entry and 50% at the exit"

  The same strategy reconciles differently between platforms on any intra-trade equity measurement —
  including the 30-second drawdown grid.

### 7.4 Account lifecycle traps

| Event | Rule | Conf. |
|---|---|---|
| **Payment failure** | *"Your account will be cancelled if the payment cannot be processed within 7 days from the date established for the monthly subscription."* | HIGH |
| **Cancelling** | Cancel **before the 25th** to avoid the next charge | HIGH |
| 🔴 **Cancelling DURING CALIBRATION** | **Cannot be reactivated at all.** Restart is the only option | HIGH |
| Reactivation otherwise | Before the next payment date, **keeping the same track record** — but see contradiction **D3** | MEDIUM ⚠️ |
| **Reset / restart cost** | Equivalent to **1 monthly subscription** | HIGH |
| **Reset consequence** | Closes the current track record **and the DARWIN**; calibration must be redone; **all allocations lost**. Verbatim: *"this action cannot be reversed."* | HIGH |
| **Multiple accounts** | **Allowed — but only 1 subscription per account** | HIGH |

### 7.5 Withdrawals

| Item | Value | Conf. |
|---|---|---|
| Minimum | **EUR100** (`withdrawals.md`) — but see contradiction **D7** | MEDIUM ⚠️ |
| Speed | *"typically processed within 24 hours"* | HIGH |
| Requirements | Identity verification **+ a verified bank account** (~24h to verify) | HIGH |
| FX | **No Darwinex FX commission**; the bank applies the rate | HIGH |

> 🔴 **The 15% split is the sharpest economic contrast in this whole comparison.** FTMO pays the
> trader **80-90%**. Darwinex Zero pays **15%** — but on **allocated third-party capital** you never
> had to fund or risk, with **no documented breach rule that can end the account**, and a track
> record that compounds in value. They are not the same trade at all.

> ⚠️ **The reset is not a wildcard here.** At Axi Select a Phase-1 reset is a *tactic*. At Darwinex
> Zero a restart **destroys the track record**, which is the entire asset being built, **and every
> allocation with it**. Costs one month of subscription and is irreversible.

> 💡 **Two Darwinex accounts = two subscriptions = 90€/month.** Budget it explicitly. (And note the
> crypto account requires a **separate registration** — § 3.3.)

---

## 8. Prohibited behaviours

> 🔴 **Sourcing caveat that governs this entire section.** The complete **81-page** docs index
> contains **no page** for trading rules, prohibited practices, or terms of use. Direct probes of
> `prohibited-trading-practices.md` and `trading-rules.md` both returned **404**.
> **The legally binding T&C is not published under `/docs/`. Absence in the documentation is not
> absence in the contract.**

| Behaviour | Status | Conf. |
|---|---|---|
| **Correlation > 0.95 vs other users** | Excludes the lower-performing DARWIN from DarwinIA | HIGH |
| **Correlation > 0.5 vs your own DARWINs** | **SILVER only.** Excludes the weaker one (higher *"previous 5 months and current month"* cumulative return wins) | HIGH |
| Copying between your own accounts | **Allowed to trade** — but governed by the correlation rules above | HIGH |
| **News trading · weekend holding · EAs · scalping** | **Allowed, no restriction** — now supported by a verbatim positive statement (§ 2): *"Instruments traded, volume, duration of the trades...your trade your own way."* | **HIGH** ⬆️ |
| Account / password sharing | **NOT FOUND in the Zero documentation.** Previously recorded here at HIGH with the quote *"each registration is for a single you only"* — that quote is not sourced to any page in the index | **LOW** ⬇️ |
| Reverse-engineering, hacking, spam | Generic website ToU only; **not found in the Zero docs index** | LOW ⬇️ |
| Latency arbitrage | Reported as the one practice causing an immediate ban — **zero official corroboration; third-party only, and the official docs are silent on it entirely** | **LOW** ⚠️ |
| Trade-filling abuse as a named offence | **NOT FOUND.** Related but distinct: divergence and the Capacity/toxicity attribute penalise poor order flow **economically**, not by ban | MEDIUM |

> ⬆️ **Upgraded MEDIUM → HIGH**: news trading, weekend holding, EAs and scalping. There is now a
> verbatim positive statement covering *instruments, volume and duration of trades*, rather than an
> inference from silence.
>
> ⬇️ **Downgraded HIGH → LOW**: account/password sharing and the reverse-engineering clause. Neither
> appears in the Zero documentation index. They may well be in the trader agreement behind login —
> but this knowledge base cannot cite one.

> ✅ **Weekend and overnight holding are unrestricted** — the exact opposite of FTMO's funded Standard
> account. This is a strong structural argument for Darwinex Zero as the home of the H4 swing
> portfolio, and it independently corroborates
> [00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md), which already rates H4 on Darwinex
> as *"muy bueno"*.

---

## 9. ⚠️ Source-risk register — unresolved contradictions in Darwinex's own documentation

> These are **not resolved**. Each is a place where two official pages — sometimes two sections of
> **the same** page — disagree. Recorded so that no downstream feature silently picks a side.
> **Do not treat any of these figures as settled without contacting Darwinex.**

| # | Subject | Source A | Source B | Status |
|---|---|---|---|---|
| **D1** | **Crypto CFD monthly price** | **EUR38 / $43** (`crypto-cfds.md`) | **45 EUR / $50** (`pricing.md`) | Unresolved |
| **D2** | **SILVER max allocation** | **EUR375,000** (page body + `type-of-allocations.md`) | **EUR250k** (`darwinia-silver.md` metadata description) | Source B appears **stale**; still unresolved |
| **D3** | **Reactivation after cancellation** | `subscription-payments.md` and `account-cancellation.md` both state a cancelled account **cannot be reactivated** | …yet **both also describe a reactivation window** before the next payment date | Unresolved. The reconciliation ("possible only within the current billing period") is **INFERRED across pages** and is **not stated on any one of them** |
| **D4** | **Booster simultaneous cap** | **EUR500k** (`zero-boosters-1.md` + Boosters section of `type-of-allocations.md`) | **EUR400,000** (6-Month Guaranteed Allocation section of `type-of-allocations.md`) | Unresolved — a contradiction **within a single page** |
| **D5** | **Booster durations** | **3 / 6 / 12 months** (`type-of-allocations.md`) | **3M / 6M / 1Y / 3Y**, with a fully priced **3Y** column (`zero-boosters-1.md`) | Unresolved |
| **D6** | **Booster price tables** | USD table gives **EUR50k and EUR100k identical prices** for "10% 1Y" (**$380**) and "10% 3Y" (**$595**) — implausible for double the capital | EUR table leaves 50k "10% 1Y" **blank** while the USD table fills it | Probable **source table errors**. **Do not treat those cells as load-bearing** |
| **D7** | **Withdrawal minimum** | **EUR100** (`withdrawals.md`) | **"$100"** (`darwinex-zero-versus-funding-programs.md`) | Unresolved (currency mismatch, not converted here) |
| **D8** | **Assets under management** | **"We currently hold $150M+ (and growing) in assets under management."** (`darwinex-zero-versus-funding-programs.md`) | This knowledge base previously carried **"$367.8M invested to date / INDX $48M"** | Unresolved — **possibly different metrics** (cumulative invested vs AUM). See § 9.1 |

### 9.1 ⬇️ The AUM figure is downgraded

The **"$367.8M third-party capital invested to date; Darwinex INDX holds $48M"** figure carried in
§ 6 is **not supported by the Zero documentation**. The only AUM statement found in the 81-page index
is **"$150M+"**. The two may measure different things (cumulative capital ever invested vs. current
assets under management), but that reconciliation is **our inference, not a vendor statement**.
**Confidence on the $367.8M / $48M pair: downgraded to LOW.**

---

## 📊 Demo accounts

> ⚠️ **UNVERIFIED — downgraded from HIGH.** The claim *"You may open as many demo trading accounts as
> you need"* **does not appear** in any of the instrument or execution pages retrieved from the Zero
> docs. It is **neither confirmed nor refuted** by the Zero documentation; the original citation is
> flagged here as **non-Zero-docs**.

| Thing | Limit | Conf. |
|---|---|---|
| **Demo MetaTrader trading accounts** | Previously recorded as **unlimited** | **UNVERIFIED** ⬇️ |
| Demo *investment portfolios* (for investing in DARWINs) | 1 demo + 5 live | MEDIUM |
| **Darwinex Zero virtual trading accounts** | **One per subscription** | HIGH |

> The claim in [07_Backtest_y_Puesta_en_Marcha.md](07_Backtest_y_Puesta_en_Marcha.md) that
> *"Darwinex tiene hasta 10 cuentas demo"* still does not match any published limit. But this file can
> no longer assert "unlimited" against it either — **the demo MetaTrader quota is now NOT FOUND in the
> Zero docs**, so the VPS instance planning in module 7 is **unconstrained-but-unconfirmed** on this
> axis.

---

## 🎯 What the simulator gets from this service

| Need | Status |
|---|---|
| **Objective function** | ⚠️ **Weights specified, scale not**: Rating = 22% current-month return + 67% 6-month cumulative return + 11% max drawdown, + track-record bonus, threshold **≥ 75**. **The scale bounds and the points mapping are NOT FOUND** |
| **Correlation definition** | ✅ **The first external numeric one**: < 0.95 vs others (both tiers); **< 0.5 vs your own — SILVER only**; not measured at all below Rating 60; **SILVER↔GOLD pairs exempt** |
| Breach modelling | ✅ **No daily or total loss limit documented** — but ⚠️ **margin call at 100% / stop-out at 50% must be modelled** |
| Balance to evaluate against | ✅ **$100,000** virtual (standard CFD) · $1M futures · EUR100,000 crypto |
| Risk ceiling | ✅ VaR band 3.25-6.5% monthly at 95% — **target is computed, not fixed at 6.5%** |
| Activity gate | ✅ 25 risk-equivalent decisions over ≥ 15 trading days, **formula now computable** (§ 4.1) |
| Drawdown comparability | ⚠️ **Darwinex samples every 30 seconds**; a daily-close drawdown **understates** it |
| Return chaining | ✅ **Geometric compounding formula documented** — never sum period returns |
| Time pressure | ✅ **None** — and long track records are actively rewarded |
| Crypto doctrine | ❌ Instrument available at **1:1 leverage** on a **separate account**, but **no academy doctrine exists** |

---

## ❌ Explicit gaps — NOT FOUND, not inferred

- **No official page states *"there is no daily loss limit / no maximum drawdown"*** in those words.
  The absence is real across the 81-page index, but it is an absence, not a vendor guarantee — and
  **the binding T&C is not published under `/docs/`**.
- **The Rating scale bounds** and the arithmetic mapping return/drawdown to Rating points.
- **The `f` factor** in `Lev(investor) = Lev(trader) × (Target VaR / Strategy VaR) × f` — present in
  the formula on both the Classic and Zero pages, **defined on neither**.
- **Risk-engine recomputation cadence** — only *"the Risk Engine can act at any moment in time"*.
- **Inclusive/exclusive treatment** of the exact 30-min and 60-min D-Leverage boundaries.
- **Window, series and numeric threshold** for the intra-position asset correlation inside D-Leverage.
- **Sampling frequency of the return series** used in the 3-month DARWIN-vs-DARWIN correlation.
- **Scoping of `exposure_max`** in the risk-equivalent decision count (§ 4.1).
- **Maximum number of concurrent GOLD allocations** — officially *"varies monthly based on
  participant count"*.
- **Whether an allocation is terminated or reduced while in drawdown.**
- **HWM reset behaviour for Permanent Allocations.**
- **Cash US stocks & ETFs account starting balance.**
- **Demo MetaTrader account quota**; live/virtual Zero accounts per subscription.
- **Timeframe restrictions / minimum holding time** — none documented anywhere checked.
- **Execution model designation** (market maker / STP / ECN) — never named.
- **Individual names of the 42 FX pairs**; full futures contract inventory.
- **Latency arbitrage as a bannable offence**: third-party only; the official docs are silent.
