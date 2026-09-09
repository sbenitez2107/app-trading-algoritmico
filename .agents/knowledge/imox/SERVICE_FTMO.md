# Service Rulebook — FTMO

> **Type**: `vendor-rulebook` + `provider-data`
> **Retrieved**: 2026-09-07 from FTMO official domains. **Re-verify before acting** — prop firm rules
> change often, and FTMO's have changed repeatedly across the programme's history.
> **Schema**: the common 8-field schema defined in [SERVICE_Own_Capital.md](SERVICE_Own_Capital.md).
> **Confidence**: HIGH = stated plainly in official FTMO documentation · MEDIUM = official but
> ambiguous, or secondary source · LOW = third-party aggregator.
>
> **Updated 2026-09-07 with the user's own research.** Two source classes were added:
> ✅ **FTMO's official Spanish comparison table**, which **independently confirms every headline
> figure** in this document — 2-Step static vs 1-Step trailing, unlimited trading period, refund
> only on 2-Step, Swing only on 2-Step, Best Day Rule only on 1-Step.
> ⚠️ **IMOX academy summary cards** (`ACADEMY-CARD`), which contain **five rules this research
> did not surface**. They are a well-made secondary digest, not FTMO's own page — treated as
> **MEDIUM** and flagged for confirmation.

---

## 🚨 Read this first — FTMO is now TWO products

The rules differ materially between them, and **the 2-Step additionally offers a `Swing` account
type**. For H1/H4 swing systems this choice is not cosmetic — it decides whether the strategy can run
at all.

| | **1-Step** | **2-Step** |
|---|---|---|
| Profit target | 10% | Phase 1: 10% · Phase 2: 5% |
| Max daily loss | **3%** | **5%** |
| Max total loss | **10% end-of-day TRAILING** | **10% STATIC** |
| Min trading days | none | **4 per phase** |
| Best Day Rule | **yes** (50%) | no |
| Account types | **Standard only** | **Standard or Swing** |
| Fee refund | ❌ **never refunded** | ✅ **100% refunded** on first reward |
| Profit split | **90%** | **80%**, → 90% after scaling |
| From | €79 | €89 |

> ⚠️ **For an H1/H4 swing portfolio, the 1-Step product is a poor fit** — see § *Fit assessment*.

---

## 1. Objective function

| Item | 1-Step | 2-Step | Conf. |
|---|---|---|---|
| Profit target | **10%** of Initial Simulated Capital | Challenge **10%** · Verification **5%** | HIGH |
| Basis | Of **initial** capital, not compounding | Same | HIGH |
| Passing condition | Target reached **with all positions closed** | Same | HIGH |
| On the funded account | **No profit target** | **No profit target** | HIGH |

Unlike Axi Select, **the objective is a single scalar** (reach the profit target without breaching).
There is no score and no trade-count race.

---

## 2. Hard constraints — the load-bearing section

| Rule | 1-Step | 2-Step | Conf. |
|---|---|---|---|
| **Max Daily Loss** | **3%** of Initial Capital | **5%** of Initial Capital | HIGH |
| Reset | **00:00 CE(S)T** daily | Same | HIGH |
| Reference point | Current day's **opening balance** − 3% | Previous midnight balance − 5%; day 1 uses Initial Capital | HIGH |
| Reference basis, confirmed | *"La pérdida diaria es calculada **en tiempo real** y se calcula con base en el **BALANCE** al final del día anterior (23:59 CET)"* — reference is the previous day-end **balance**; the trigger is **real-time equity** | HIGH `ACADEMY-CARD` + official |
| **Breach trigger** | **EQUITY** = balance + open P/L ± swaps − commissions | Same | HIGH |
| **Max Loss** | **10%, end-of-day TRAILING** | **10%, STATIC** | HIGH |
| Trailing mechanics | Recomputed 00:00 CE(S)T from the **highest preceding day-end balance** (or Initial Capital if higher) − 10%. **Ratchets up only, never down.** Resets to 90% of new Initial Capital after a reward withdrawal | Computed once: Initial − 10%. Never adjusts | HIGH |
| **Time limit** | **UNLIMITED** | **UNLIMITED**, both phases | HIGH |

### 🔴 Two consequences the simulator cannot ignore

**a) Both limits read EQUITY, not closed-trade balance.**

The breach is evaluated on **equity including floating P/L** — live, intra-day, not at end-of-day on
settled trades.

> ⚠️ **This may exceed what our backtest data can answer.** A trade list gives **closed** trades. To
> know whether a group breached a 3% or 5% daily equity floor, you need the **intra-day price path
> while positions were open**, not just their entry and exit. Computing FTMO breach probability from
> a closed-trade series alone would **understate it** — an excursion that dipped below the floor and
> recovered before the close is invisible in the trade list but is a hard fail at FTMO.
>
> **This is a data-model question to settle before promising FTMO simulation.** It also explains why
> we deferred breach probability earlier in this project.

**b) The time limit is UNLIMITED — and that changes the fit.**

Older versions of FTMO had 30/60-day windows. **They are gone.** This matters enormously for the
declared strategy profile of *"3-4 trades a month"*: there is **no deadline to race**, so a
low-frequency portfolio is viable here in a way it is **not** at Axi Select, whose 60-day trade-count
window is the binding constraint.

**FTMO and Axi Select pull in opposite directions**, and this is the sharpest evidence yet that group
selection must differ per service.

---

## 3. Eligibility

| Item | Value | Conf. |
|---|---|---|
| Instruments | All available on the platform — Forex, Indices, Commodities, Stocks, Crypto | HIGH |
| Restricted symbols | None blanket-banned; per-symbol availability on FTMO's Symbols page | MEDIUM |
| Timeframe restrictions | **None** | HIGH |
| Platforms | MT4, MT5, cTrader, TradingView | HIGH |

### Weekend and overnight — the decisive rule for swing systems

| Stage / type | Rule | Conf. |
|---|---|---|
| **Evaluation** (1-Step Challenge, 2-Step Challenge + Verification) | ✅ **No restriction, either account type.** Hold overnight and over the weekend freely | HIGH |
| **Funded, STANDARD** | ❌ **Must close** before weekend market close, **and if any rollover/market break lasts > 2 hours** | HIGH |
| **Funded, SWING** (2-Step only) | ✅ **No restriction** | HIGH |
| Weekend execution | No restriction — if the market is open, trade | HIGH |

> 🚨 **The trap**: a swing portfolio can pass the evaluation comfortably and then be **structurally
> unable to run on the funded Standard account**, because it must be flat every weekend. The
> restriction appears **only after you pass.** The Swing account type is the only escape, and it costs
> leverage: **1:30 instead of 1:100**.

### News restriction

| Stage / type | Rule | Conf. |
|---|---|---|
| Funded, **Standard** | ❌ No opening or closing (including pending orders, SL and TP execution) on **targeted instruments** from **2 min before to 2 min after** selected releases. Breach *"may result in termination"* | HIGH |
| Evaluation, Standard | No restriction | MEDIUM |
| **Swing** | ✅ **No restriction at any stage** | HIGH |

Targeted events by currency: **USD** (Fed Funds Rate, NFP, CPI, FOMC Minutes, GDP) · **EUR** (Main
Refi Rate) · **GBP** (Official Bank Rate, CPI) · **CAD** (BOC Rate, CPI, Employment) · **AUD/NZD**
(rate, employment, CPI, GDP) · **CHF** (SNB Policy Rate) · **Crude Oil** (inventories). Marked
"Restricted event" in FTMO's Economic Calendar.

> ⚠️ **SL and TP execution counts.** An automated system cannot simply "not trade" during the window —
> a stop filling inside it is itself a breach. For an EA portfolio this is a real operational
> constraint, not a discretionary one.

### Swap — confirming the academy's claim

| Item | Value | Conf. |
|---|---|---|
| Swaps charged? | **Yes**, normal rollover (cost or income) | MEDIUM |
| Triple swap | **Wednesday → Thursday** | MEDIUM |
| Swap-free / Islamic account | **NOT FOUND** — assume swaps apply | — |

> ✅ **This confirms and sharpens [00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md)**,
> which says H4 is ruinous for props *"because the swap eats the result"*. The mechanism is now
> precise: **swaps land inside the equity figure that BOTH drawdown checks read.** Overnight financing
> does not merely erode profit — it can **push you toward a breach directly.** The academy's claim was
> correct and understated.

---

## 4. Activity requirements

| Item | 1-Step | 2-Step | Conf. |
|---|---|---|---|
| Minimum trading days | **None** | **4 per phase** | HIGH |
| "Trading day" | — | Any day 00:00–23:59 CE(S)T with **≥ 1 position opened** | HIGH |
| Minimum number of trades | **NOT FOUND** | **NOT FOUND** | — |
| Other consistency rules | *"No additional consistency requirements"* provided risk management is sustainable | HIGH |
| Single-trade profit cap | **NOT FOUND** | **NOT FOUND** | — |

### 🚨 Inactivity rule — the finding that most affects this plan

> **Periods of inactivity (not trading) longer than 30 DAYS are grounds for account closure.**
> `MEDIUM` — `ACADEMY-CARD`; **not found on FTMO's own pages during research. Confirm before relying
> on the exact figure**, but treat the rule as real: an inactivity clause is standard at FTMO.

**Why this matters more here than anywhere else in the knowledge base.**

The declared profile is **3-4 trades a month**, and Jaime's documented pattern (below) is
**one strategy per challenge**. Those two facts combine badly with a 30-day inactivity clause: a
single H4 strategy on a single account does not trade continuously — it waits.

**A back-of-envelope check.** If a strategy averages 3 trades a month and arrivals were memoryless,
the chance that any given gap exceeds 30 days is `e^-3 ≈ 5%`. Across ~36 gaps in a year, the chance of
hitting **at least one** 30-day gap is high — on the order of 80%.

> ⚠️ **That number is an estimate under an assumption real strategies violate** (trades cluster
> around regimes rather than arriving independently). **Do not act on it.** Treat it as a reason to
> run the real check.
>
> ✅ **And this one IS answerable with the data we already have.** Unlike breach probability — which
> needs intraday equity paths we do not hold — **the longest gap between trades is directly
> computable from a closed-trade series.** It should become a first-class metric on every strategy and
> every group: `max days between consecutive trades`.

**Mitigations available**: run **more than one strategy per account** (which conflicts with Jaime's
1:1 pattern), or **span several assets** so their idle periods do not coincide.

---

### Best Day Rule — 1-Step only

> **Best single day must not exceed 50% of Positive Days' Profit.**

- Computed from **closed trades**, evaluated daily at 00:00 CE(S)T.
- "Positive Days' Profit" = the sum of profits/losses of all profitable days.
- ⚠️ **Exceeding it is NOT a breach.** You simply keep trading until the ratio dilutes to ≤ 50%.
- Applies to the 1-Step Challenge **and** the funded 1-Step account.

**Simulator note**: this is a *soft* constraint — it delays passing rather than failing you. It should
be modelled as **time-to-pass**, not as a pass/fail gate. It penalises exactly the profile the
academy warns about: a result concentrated in one period (compare
[04_SQX_Optimizer.md](04_SQX_Optimizer.md), *max profit in one run < 50% of total* — the same 50%,
the same idea, at a different scale).

---

## 5. Risk model

| Item | Value | Conf. |
|---|---|---|
| Per-trade risk cap | **None stated.** A stop-loss is explicitly **not required** | HIGH |
| Implicit cap | Forbidden practices ban position sizes or counts *"substantially larger/smaller than typical activity"* and *"repeated activity creating disproportionate risk per trade"* — discretionary and unquantified | MEDIUM |
| Leverage — Standard | up to **1:100** (cannot be increased) | HIGH |
| Leverage — **Swing** | up to **1:30** | HIGH |
| Lot size limits | NOT FOUND (per-symbol max volume in platform contract specs) | — |
| Concurrent orders | **200 open at one time** | MEDIUM |
| Daily positions | **2,000 per day** | MEDIUM |
| **Max capital per trader OR per strategy** | **$400,000** across all accounts, pre-scaling | HIGH |
| **FTMO's own recommendation** | Use up to **20% of account margin for one trade**, and risk up to **1.5% per trade** | MEDIUM `ACADEMY-CARD` |
| Minimum trade duration | **No minimum rule exists** — but HFT and tick scalping are prohibited (§ 8) | MEDIUM `ACADEMY-CARD` |
| Commissions | **$0 to $3 USD** (Forex) plus raw spreads | MEDIUM `ACADEMY-CARD` |

> 📌 **Calibration check against the declared plan.** The user risks **$50 on a $10,000 account =
> 0.5% per trade**. That is **one third of FTMO's own 1.5% ceiling**, and sits between the academy's
> 0.20% (large accounts) and 1.0% (small accounts) from
> [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md). **Consistent with the doctrine of starting
> ultra-conservative** — no conflict to resolve.

---

## 6. Stages and transitions

**2-Step**: Challenge (10%, ≥4 days) → Verification (5%, ≥4 days) → FTMO Account (no target; 5% MDL
and static 10% max loss persist). Account type **Standard or Swing**.

**1-Step**: Challenge (10%, no minimum days) → FTMO Account. **MDL 3%, trailing 10% max loss and the
Best Day Rule all persist onto the funded account.** **Standard only — no Swing option exists.**

**Account sizes**: $10,000 · $25,000 · $50,000 · $100,000 · $200,000. `HIGH`

### Scaling Plan `HIGH`

Requires: ≥ 4 months as an FTMO Trader (or since the last scale-up) · ≥ 10% net profit above starting
balance in that window · ≥ 2 processed rewards in the same window · positive balance at scale-up.

Reward: **+25% account size every 4 months**, up to **$2,000,000** total across all FTMO accounts, and
qualifies the trader for the **90% split** on 2-Step.

> ⚠️ On 1-Step, **the trailing max loss resets to 90% of the new Initial Capital upon a reward
> withdrawal** — withdrawing re-baselines your drawdown floor.

### Account merging `MEDIUM` `ACADEMY-CARD`

Funded accounts can be **merged**, raising the working limit from **$400K** toward the **$2M**
scaling ceiling. All of these must hold:

- The FTMO Account is **not in loss**
- **No profits pending** under the 80% split
- **All accounts share the same risk configuration**
- **All accounts share the same base currency**

> 🔑 This changes how the $400k per-strategy cap should be read. It is not only a ceiling — it is
> a **consolidation path**. Several passed challenges running the same configuration can be combined
> rather than run in parallel forever.

### 🔴 No resets `MEDIUM` `ACADEMY-CARD`

> **Resets are NOT available. If you fail, you start a new challenge — and pay again.**

**This is the sharpest structural contrast with Axi Select**, where the Seed stage has a free,
unlimited reset button and the Edge score can be reset every 90 days. At FTMO, **failure costs money
every time**. It is also exactly why the academy's economics (§ *La economía real del fondeo* in
[08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md)) assume losing 3-4 challenges to win one.

### Premium Programme `MEDIUM` `ACADEMY-CARD`

The best traders may join the **Premium Programme** and become professionals at **QuantLane**, a
proprietary trading firm managing its own real capital.

---

## 7. Economics

| Item | Value | Conf. |
|---|---|---|
| Starting price, 1-Step | **from €79** | HIGH |
| Starting price, 2-Step | **from €89** | HIGH |
| Official fee table by size | **NOT FOUND** — `ftmo.com/en/pricing/` returns 404 | — |
| Fee by size, 1-Step | 10k €79 · 25k €199 · 50k €319 · 100k €499 | **LOW** — third-party aggregators |
| Fee by size, 2-Step | 10k ~€155 · 25k €250 · 50k €345 · 100k €540 · 200k €1,080 | **LOW** — third-party aggregators |
| **Profit split, 1-Step** | **90%** | HIGH |
| **Profit split, 2-Step** | **80%**, → **90%** after Scaling Plan or Premium | HIGH |
| **Fee refund, 2-Step** | **100%** with the first reward withdrawal after passing both phases | HIGH |
| **Fee refund, 1-Step** | ❌ **Not refunded** — the fee applies to a single step only | HIGH |
| First payout | Claimable on the **14th day or later** after the first placed trade, all positions closed | HIGH |
| Payout processing | Review + notification 1-2 business days; payment 1-2 business days after invoice approval | HIGH |
| Payout minimums | $20 bank wire · $50 crypto — **no minimum beyond covering processor fees** | MEDIUM |
| Payout methods | Bank transfer, Skrill, cryptocurrencies | MEDIUM `ACADEMY-CARD` |
| **On-demand payout** | You may request payment on a **fixed monthly date**, provided it is **every 14 days** | MEDIUM `ACADEMY-CARD` |
| **Profit cushion** | **None** | MEDIUM `ACADEMY-CARD` |
| **Extra refund condition** | Also refunded if you have **not used the account and fewer than 14 days have passed since purchase** | MEDIUM `ACADEMY-CARD` |
| 🔴 **Country restrictions** | **Australia, Cuba, USA, Venezuela, and others** | MEDIUM `ACADEMY-CARD` |

> ⚠️ **Marketing says "up to 90%"** — that is the 2-Step *ceiling* after scaling, not its default. The
> default on 2-Step is **80%**.

---

## 8. Prohibited behaviours

Source: FTMO Forbidden Trading Practices. `HIGH` unless noted.

- **Exploiting system errors** — price display errors, quote-update delays.
- **Manipulative / coordinated trading** — simulated trades or combinations for manipulative purposes,
  e.g. simultaneously opening opposite positions **across connected accounts or with multiple
  operators**. *Carve-out: opposite positions on a **single** account are permitted.*
- **Unfair-advantage tooling** — software, AI, ultra-high-speed tools, or mass data entry.
- **Gap trading** — opening trades before major news/macro events, or **≤ 2 hours before a relevant
  market closes for at least 2 hours**.
- **Artificial profit distribution** — *"strategies that artificially distribute profit across multiple
  days without proportionally distributing market risk, such as hedging or holding opposing positions
  on the same or highly correlated instruments."* ← **this targets Best-Day-Rule gaming.**
- **Unrealistic risk** — sizes or counts out of line with typical activity.
- **Personal use only** — no third-party access, no coordinated trading.

### 🔑 Answering the open question: reusing a strategy across challenges

The user's question in [10_Mentoria_1a1_PENDIENTE.md](10_Mentoria_1a1_PENDIENTE.md) was: *"En FTMO no
deberíamos repetir estrategias en distintos challenges, ¿cierto? Por temas de copy."*

**The answer is more nuanced than a flat ban:**

| Fact | Value | Conf. |
|---|---|---|
| Limit on number of accounts | **None** | HIGH |
| **The binding cap** | **$400,000 total allocation per trader OR PER STRATEGY**, pre-scaling, spanning 1-Step and 2-Step together | HIGH |
| Enforcement | *"If the same trading strategy appears across multiple FTMO accounts and the combined fictional capital surpasses the allocation limit, FTMO reserves the right to **suspend the affected accounts**."* | HIGH |
| Duplicate registrations | *"Holding multiple accounts through different registrations is not permitted"* | HIGH |
| **Third-party / commercial EAs** | If you buy an EA, **other traders may run the identical strategy**, and the per-strategy cap is measured **across those traders** — you can be caught by someone else's usage | HIGH |
| Copy trading between your own accounts | Not named as a standalone prohibition; governed by the per-strategy cap + coordinated-trading + duplicate-registration bans | MEDIUM |

> **So**: repeating a strategy across challenges is **not forbidden — it is capped**, at $400k of
> combined allocation *for that strategy*. On 10k accounts that is a large number of challenges before
> it binds. **The rule that actually bites first is the $400k per-trader cap.**
>
> **Simulator consequence**: this is **not** allocation without replacement, as we assumed when
> reading the academy note. It is a **capacity constraint per strategy** — a strategy can appear in
> several groups until its cumulative allocation reaches the cap. That is a different and softer
> model, and it changes the generator design.

### 🔴 The explicit permitted / prohibited list `MEDIUM` `ACADEMY-CARD`

| ✅ **PERMITTED** | ❌ **PROHIBITED** |
|---|---|
| **EAs** | HFT |
| **Bots** | **Martingale** |
| **Algorithms** | **Grid trading** |
| **Copytrading — own and local only** | Managing or commercialising accounts |
| **VPN / VPS — no restriction** | **Hedging between accounts** |
| | Slow data feed |
| | Latency and reverse arbitrage |
| | Gap trading |
| | Toxic order flow |
| | **Tick scalping** and server exec |

**Three of these are new and load-bearing:**

1. ❌ **Martingale and grid trading are named prohibitions.** SQX-built systems with ATR stops do not
   use either, so the current pipeline is unaffected — but it becomes a **hard filter** on any future
   strategy archetype.
2. ✅ **Copytrading is allowed when it is your own and local.** This is the **opposite of Axi Select**,
   where copy trading is *"strictly prohibited"*. On FTMO you may mirror your own accounts locally;
   the binding limit remains the **$400k per-strategy cap**, not the act of copying.
3. ✅ **VPN and VPS carry no restriction** — keep the IP as constant as possible. This clears the VPS
   deployment plan in [07_Backtest_y_Puesta_en_Marcha.md](07_Backtest_y_Puesta_en_Marcha.md).

### Automated trading / EAs

| Fact | Value | Conf. |
|---|---|---|
| **Are EAs allowed?** | ✅ **Yes, explicitly** — *"whether it's discretionary trading, algorithmic trading, EAs, etc."* — provided trading is legitimate, uses proper risk management, conforms to real market conditions, and does not resemble forbidden practices | HIGH |
| Server-request cap | Banned: EAs causing **> 2,000 server requests per day** | HIGH |
| Latency / HFT tooling | Banned as unfair advantage | HIGH |
| Replicability | Trading must be replicable on live accounts and show genuine edge and consistency | MEDIUM |
| Penalty for repeat violation | Remove positions, rebalance accounts, reduce leverage, or terminate | HIGH |

---

## 📋 The user's declared FTMO configuration

*Recorded 2026-09-07 from the user's own decisions. This is **intent**, not a rule.*

| Decision | Value |
|---|---|
| **Product** | **2-Step, Swing account type** |
| **Assets** | **XAUUSD · NDX · GDAXI · BTCUSD** — with the user's own short/medium/long-term analysis of each |
| **Risk per trade** | **$50 on a $10,000 account = 0.5%** *(measured on FTMO demo accounts)* |
| Cadence | 1 challenge per month *(from [10_Mentoria_1a1_PENDIENTE.md](10_Mentoria_1a1_PENDIENTE.md))* |

### ✅ The product choice is right

**2-Step Swing** is exactly the configuration this rulebook independently arrived at. It is the only
one that permits **unrestricted weekend and overnight holding on the funded account**, removes the
**news window**, and **refunds the fee**. The cost is leverage: **1:30 instead of 1:100**.

### ✅ The asset set is universally portable — worth knowing

All four chosen assets are eligible on **all three** external services:

| Asset | FTMO | Axi Select | Darwinex Zero |
|---|---|---|---|
| **XAUUSD** | ✅ | ✅ *(gold and silver are the only eligible metals)* | ✅ |
| **NDX** | ✅ | ✅ *(all indices)* | ✅ |
| **GDAXI** | ✅ | ✅ *(all indices)* | ✅ |
| **BTCUSD** | ✅ | ✅ *(BTCUSD and ETHUSD are the only eligible cryptos)* | ✅ *(Crypto CFD account, MT5)* |

**The same four systems can be pointed at any destination without an eligibility rewrite.** That is a
real simplification for the simulator — asset eligibility stops being a per-service variable for this
particular set.

### ✅ Currencies are deliberately excluded here — and that is coherent

[00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md) defines the target portfolio as
**gold + Nasdaq/SP500 + DAX + a currency**. This set replaces the currency with **BTCUSD**.

**The user's stated allocation resolves the concern I raised earlier:**

> *"En FTMO no voy a usar divisas, solamente las usaré en Axi Select y quizás en Darwinex,
> principalmente para sumar trades."*

That is **exactly the right split**, and it follows from the rulebooks rather than from habit:

| Service | Currencies | Why |
|---|---|---|
| **FTMO** | ❌ Not used | No trade-count requirement. The objective is a profit target, so frequency buys nothing |
| **Axi Select** | ✅ Used | The **trade count is the binding constraint** — currencies are the highest-frequency component available |
| **Darwinex Zero** | ✅ Maybe | Calibration needs **25 risk-equivalent decisions over ≥ 15 trading days**; currencies shorten that |

**This is the per-service thesis working as intended**: the same pool, allocated differently because
the objective functions differ. ✅ Concern withdrawn.

⚠️ **BTCUSD still has no academy doctrine** — no instrument configuration, no thresholds, no asset
profile. The user reports having their own analysis; that analysis is currently its **only** basis.

---

## 📈 Empirical pattern — Jaime (academy student)

> **Second-hand testimony reported by the user, not a rule and not verified.** Recorded because it is
> the only observed end-to-end result in this knowledge base, and because its strategy pattern
> **contradicts an assumption the simulator was about to make.**

**The trajectory** *(reported)*:

| Step | Detail |
|---|---|
| After the mentoring | Opened **2 Darwinex Zero** accounts and bought **3-4 FTMO accounts** |
| Outcome | **Only 1 passed** |
| Reinvestment | Took **1 withdrawal but did not cash it out** — bought points, and used the points to **re-buy more challenges** |
| Today | **€1,500 invested → €1,100,000 in active challenges** |
| Withdrawn | **€48,000 in a little over a year** |

**The stated philosophy:**

> The key to prop firms is to **pass few challenges and make many withdrawals on the ones that pass**,
> because statistically that is more likely. It is oriented to the **short term**: pass fast, withdraw
> often. **Learn to lose challenges.**

This is the same economics [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) describes — lose 3-4
to win 1 — with an added compounding mechanism: **rewards are recycled into more challenges rather
than withdrawn.**

### ⚠️ One strategy per challenge — **an option, not doctrine**

> **Jaime's strategy: 1 strategy – 1 challenge.** A high Ret/DD strategy, **no take profit**, with a
> **trailing stop**.

**Three consequences:**

**1. The unit for FTMO is an open question, not a settled 1:1.** ⚠️ **Corrected by the user
2026-09-07**: this is *one student's choice*, not academy doctrine. What the user has actually
observed in the community is splitting **by asset** — gold, NQ, DAX, one per challenge.

**Why people might split by asset** — the user's hypothesis, and it checks out with a caveat: to
avoid strategy duplication across accounts. **But that constraint binds only once funded.** The
`ACADEMY-CARD` states the limit as *"un máximo de **$400K en cuentas financiadas**"* — funded
accounts. During evaluation the cap is not the binding concern, so **splitting by asset during
challenges is not required by the rules.** If people do it, the reason is something else — most
plausibly that a single-asset account has a cleaner, more attributable drawdown profile.

So the real question stands open: **one strategy, one asset, or a portfolio per challenge?** The
simulator should be able to evaluate all three rather than assume one.

**2. It is a different strategy archetype from the academy pipeline.** No TP with a trailing stop is
not the ATR SL/PT model of [02_SQX_Builder.md](02_SQX_Builder.md). It maximises the tail of a winning
run — which serves a fixed **profit target** far better than it serves a smooth equity curve. The
archetype **follows the objective function**, exactly as the per-service thesis predicts.

**3. ⚠️ It collides with the inactivity rule.** One strategy alone on one account is the
**maximum-exposure case** for a 30-day inactivity clause (§ 4). And *no take profit with a trailing
stop* means positions **run long**, which compounds swap cost — charged inside the equity that both
drawdown checks read — and makes the **Swing account type** not merely preferable but necessary.

> **This tension is not resolved.** Jaime's pattern and the inactivity rule can both be true only if
> his strategies trade more often than 3-4 times a month, or if the inactivity figure differs from the
> card. **Worth asking him directly** — it is a cheap question with an expensive wrong answer.

---

## 🔴 Two different problems: passing vs holding

> **Requirement stated by the user, 2026-09-07.** *"Hay que separar simulaciones para pasar challenges
> step 1 y 2, y luego gestionar una cuenta que ya está fondeada, porque ahí el riesgo de perderla o
> quemarla es más importante."*

**This is a structural requirement, not a preference.** The same service, the same strategies and the
same account size present **two different optimisation problems**, because the cost of failure is not
the same:

| | **Challenge mode** (Phase 1 + 2) | **Funded mode** |
|---|---|---|
| Cost of failure | **A fee.** €89-540, budgeted and expected | **The account** — and every future payout from it |
| Correct posture | **Aggressive.** Failure is priced in | **Defensive.** Survival dominates |
| Objective | **Reach +10% / +5%** without breaching | **Maximise cumulative withdrawals over the account's life** |
| Time | Unlimited — but faster passing compounds faster | Indefinite — longevity IS the return |
| Extra constraints | none beyond the limits | Weekend-flat (Standard) · news window (Standard) · 30-day inactivity |
| Academy analogue | *"Aprender a perder desafíos"* | *"Vivir una caída y luego recuperarse"* |

> 🔑 **The same strategy can be right for one mode and wrong for the other.** A high-variance system
> that reaches +10% quickly is excellent in challenge mode and dangerous in funded mode. This is the
> **per-service objective-function thesis applied one level deeper: per service AND per stage** — the
> same granularity Axi Select already forced with its phases.
>
> **The simulator needs a `mode` dimension**, not just a `service` one.

### The economics of challenge mode — and a number to measure

The IMOX philosophy the user reports:

> **Buy 4 challenges and try to pass them. Do not fear losing or burning them.**

| Source | Observed pass rate |
|---|---|
| IMOX community | **~1 in 4 (25%)** |
| Other traders the user has seen | **~30%** |

> ⚠️ **Both figures are reported, not measured here.** The user's own note: *"debemos medirlo a
> futuro."* **The pass rate is a first-class simulator parameter** — it converts a strategy's
> probability of passing into an expected value against a known fee, and it is the number that decides
> whether 10k-at-volume beats 50k-at-quality.

---

## 🚨 Aggregate simultaneous risk — check this before sizing

The user runs **~14 EAs on demo at 0.5% risk each**, and states the daily exposure average is not yet
known.

**The arithmetic that matters:**

```
14 EAs × 0.5% = 7.0% worst-case simultaneous risk
FTMO 2-Step max daily loss = 5.0%
FTMO max total loss        = 10.0%
```

> 🔴 **A simultaneous stop-out across all 14 positions breaches the daily limit and fails the
> challenge outright.** It does not produce a bad day — it ends the account.

**And the positions are not independent.** The declared asset set is four instruments
(XAUUSD, NDX, GDAXI, BTCUSD), so multiple EAs share an asset, a direction and a regime. Same-asset
positions tend to stop out together, which pushes effective exposure **toward** the worst case rather
than toward the square-root-of-N intuition.

**Against the academy's own rule** ([08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md)):

> *"1% el portfolio total… si se te activan todas las operaciones a la vez, no arriesgás más del 1%."*

**7% is seven times that guideline.**

> ✅ **The per-EA number is not the risk. The aggregate simultaneous number is** — and it is
> measurable. FTMO's own challenge metrics report exposure; the user has offered them. **Measure
> before sizing.** Options if the measured figure is high: fewer concurrent EAs per account, lower
> per-EA risk, or caps on concurrent positions per asset.

---

## 🔧 Platform and version migration

| Item | Current | Planned |
|---|---|---|
| **Platform** | **MT4** — all current strategies | **MT5**, like the rest of the community |
| **SQX version** | **v136** — what this entire knowledge base documents | **v144 or v145** |
| Trigger | — | **After the 1-a-1 mentoring** |

✅ **MT4 is supported by FTMO challenges**, so the current strategies are usable today — no blocker.

⚠️ **But the migration has knowledge-base consequences worth flagging early:**

1. **Every SQX procedure in modules 1-9 is written against v136**, including two documented v136 bugs
   (the swap bug in [01_SQX_Data.md](01_SQX_Data.md) and the Source Code / AlgoWizard parameter bug in
   [09_AlgoWizard_y_QA4.md](09_AlgoWizard_y_QA4.md)). Those bugs **may be fixed in 144/145** — in which
   case the workarounds become wrong rather than merely unnecessary. **Re-verify both after migrating.**
2. **MT5 carries tick data natively** ([07_Backtest_y_Puesta_en_Marcha.md](07_Backtest_y_Puesta_en_Marcha.md)),
   which removes the MT4 tick-data export step entirely.
3. **The swap question may reopen.** Swap is not configured in the Builder partly *because it is bugged
   in v136*. If 144/145 fixes it, that decision deserves revisiting — and it matters most for FTMO,
   where swap sits inside the drawdown equity.

---

## 🎯 Fit assessment

| Factor | Assessment |
|---|---|
| **Unlimited time limit** | ✅ **Strongly favourable.** A low-frequency portfolio has no deadline to race — the opposite of Axi Select |
| **2-Step Swing chosen** | ✅ **Correct.** The only configuration that survives weekend holding on a funded account |
| **0.5% risk per trade** | ✅ Conservative — one third of FTMO's own 1.5% guideline |
| **Asset set** | ✅ Eligible everywhere — but **no currency**, which costs at Axi, and **BTCUSD has no doctrine** |
| **4 minimum trading days per phase** | ⚠️ Tight at 3-4 trades/month, but with no deadline it resolves over time |
| 🔴 **30-day inactivity rule** | 🔴 **The main open risk.** Directly computable from existing trade data — **measure `max days between trades` before committing** |
| 🔴 **No resets** | 🔴 Every failure costs a new fee. Budget for a pass rate near **1 in 3-4** |
| **Swing leverage 1:30** | ⚠️ Materially lower than 1:100. Affects sizing on a 10k account |
| **10k vs the academy's 20k-50k** | ⚠️ The academy recommends 20k-50k so a pass is worth 200-300 USD. Jaime's pattern favours **volume of challenges** over size, which supports 10k — **two defensible strategies, pick one deliberately** |
| **Swap on H4** | 🔴 Charged, triple on Wednesday, **counted inside the drawdown equity** |
| **Country restrictions** | ⚠️ Australia, Cuba, USA, Venezuela and others — confirm your jurisdiction |

---

## ❌ Explicit gaps — NOT FOUND, not inferred

- Official per-account-size fee table (page 404s; only *"from €79 / €89"* is officially confirmed).
- Minimum number of **trades** (only minimum trading **days**).
- Any per-trade risk percentage cap or numeric lot-size cap.
- Any swap-free / Islamic account offering.
- An explicit standalone copy-trading policy separate from the $400k per-strategy cap.
- Whether the news restriction formally applies during Evaluation on Standard accounts (stated as an
  exception in the FAQ, but absent from the numbered Trading Objectives).
