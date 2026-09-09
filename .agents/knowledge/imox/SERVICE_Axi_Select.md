# Service Rulebook — Axi Select

> **Type**: `vendor-rulebook` + `provider-data`
> **Retrieved**: 2026-09-07. **Primary source**: the official **Axi Select Program Rules PDF**
> (Axi's own S3 host, `Last-Modified: 2026-03-26`), plus Axi's help centre.
> **Schema**: the common 8-field schema defined in [SERVICE_Own_Capital.md](SERVICE_Own_Capital.md).
> **Confidence**: HIGH = stated plainly in the official PDF · MEDIUM = help-centre only, or ambiguous ·
> LOW = third-party.
>
> **Updated 2026-09-08 with the user's own research.** Two source classes added:
> ✅ **`DASHBOARD`** — screenshots of the live Axi Select dashboard and its in-product knowledge
> base (General Rules, Edge Score, Quarantine, Definitions, Deposit and Withdraw, FAQ). This is
> **Axi's own product surface**, so it is treated as **HIGH**. It **confirms every headline figure**
> in this document and **resolves the leverage conflict**.
> ⚠️ **`IMOX-DECK`** — a Spanish community deck interpreting the Edge Score. Valuable, but it is
> **inference, not Axi's wording** — its own slide says *"lo que **probablemente** penaliza"*.
> Treated as **LOW-MEDIUM** and labelled wherever used.
>
> **Host note**: `support.axi.com` now redirects to `help.axi.com`, whose articles render tables via
> JavaScript. The same articles are served fetchably from Axi's help-centre vendor (DevRev). That is
> Axi's own content through its vendor — treated as **official-but-secondary**, hence MEDIUM where it
> is the only source.

---

# 🔴 Four corrections to [08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md)

The academy transcript is dated 04/2026 and the official rules PDF is dated **2026-03-26** — nearly
the same moment. Even so, **four of its claims do not survive contact with the published rules**, and
two of them change the simulator's design.

## ❌ Correction 1 — "60 days" is a MINIMUM to serve, not a deadline to beat

This is the most consequential error in the knowledge base.

| | Academy transcript | **Official rules** |
|---|---|---|
| Framing | *"Es una carrera entre conseguir la cantidad de trades pronto y conseguir el 7% de la fase"*, within 60 days | **`Stage Duration (Min Days)`: the MINIMUM number of days you must spend in the current stage before you can progress** |
| Seed | 60 days | **30 days** |
| Maximum time in a stage | implied | **NOT FOUND — no maximum is published** |

> **There is no race.** You cannot progress *before* the minimum days and minimum trades are served;
> you are not penalised for taking longer. The pressure runs in the **opposite direction** to what we
> recorded.

#### ✅ Axi states this outright — confirmed verbatim `DASHBOARD` `HIGH`

The in-product FAQ, *"Is there a time limit to qualify for funding?"*:

> **"No, there is no time limit. You can progress at your own pace. At Axi Select, we believe trading
> growth takes time. Clients must spend a minimum number of days at each stage to ease of any
> pressure."**

Axi not only sets no deadline — it states that the minimum days exist **to remove** pressure. The
academy transcript inverted the mechanism's purpose.

> ⚠️ **This invalidates the "dual-objective race under a 60-day deadline" model** described when
> transcribing module 8. Axi Select is a **floor to clear**, not a clock to beat. The objective
> function must be rewritten accordingly.

## ❌ Correction 2 — Gold is NOT banned

| Academy transcript | Official rules |
|---|---|
| *"Al día de hoy el oro está baneado en Axi Select"*, banned on Pro 500 and half-million tiers, allowed in pre-training / Phase 1 / Phase 2 | **Gold and Silver are the ONLY metals eligible** for copying to the Allocation Account. **No stage or account-size dependency appears anywhere in the published rules** |

The stage-dependent gold restriction is **NOT FOUND** in any official source. Gold is not merely
allowed — it is one of just two eligible metals.

> This also removes the phase-dependent eligibility model I flagged as a simulator requirement when
> writing module 8. **Eligibility appears to be programme-wide, not per stage.** If Aritz has
> first-hand evidence of a live gold restriction, it is either very recent, account-specific, or
> informal — worth asking in the 1-a-1, but it should not be encoded as a rule.

## ⚠️ Correction 3 — the restriction mechanism is not a ban

Ineligible symbols **still execute normally on your own Axi Select trading account**. They are simply
**not copied to the Allocation Account** — so they earn nothing and do not advance progression.

That is a softer and more useful mechanism than a ban: an ineligible instrument costs you
*progression*, not your account.

## ⚠️ Correction 4 — the 90-day reset is voluntary, not automatic

| Academy transcript | Official rules |
|---|---|
| *"cada 90 días tenés un reset score"* | The **Edge Score reset is opt-in**, available 90 days after opening the account and every 90 days thereafter, **unlimited times**. It **closes your Axi Select trading account**, wipes progress, and forces re-qualification from 20 unique trades and $500 equity |

**Two different resets exist and must not be confused:**

| Reset | What it does | Touches Edge score? |
|---|---|---|
| **Reset Stage** (Seed only) | Erases all trading statistics, closes active Allocation trades. Restartable any time **without penalty** | ❌ **No** |
| **Reset Score** (Profile) | Closes the trading account, wipes progress, re-qualification required. Every 90 days, unlimited | ✅ **Yes** |

The academy's "comodín" is the **first** one, and its description is accurate for that. Both are
**final and irreversible**.

---

## 1. Objective function

**The `Edge score`** — 0-100, a numerical representation of trading performance.

> *"A **weighted average of three measurable components** — **Skill**, **Risk**, **Consistency** —
> then multiplied by a **discount factor** depending on your **Experience**."*

| Component | Meaning | Conf. |
|---|---|---|
| **Skill** | Ability to generate profits while effectively managing drawdowns | HIGH |
| **Risk** | Proficiency in managing risk | HIGH |
| **Consistency** | Ability to achieve consistent gains | HIGH |
| **Experience** *(discount factor)* | Whether you meet minimum criteria for credible experience — number of trades, consecutive days of positive PnL. **Longer positive history → smaller discount** | MEDIUM |
| **Exact weights / functional form** | ❌ **STILL NOT PUBLISHED** — but the structure and one calibration point are now known (below) | — |

### 🧮 The structure, and what each component measures `DASHBOARD` `HIGH`

```
Edge Score = weighted_average(Skill, Risk, Consistency) × discount(Experience)
```

The dashboard renders **four** sub-scores, each on its own 0-100 dial. Axi's own definitions:

| Component | Axi's wording | What it is measuring, operationally |
|---|---|---|
| **Skill** | *"ability to generate profits while effectively managing drawdowns"* | A **return-vs-drawdown** quantity — closest KB analogue is **Ret/DD** |
| **Risk** | *"proficiency in managing risks effectively"* | **Predictability of losses** — see the deck reading below |
| **Consistency** | *"ability to achieve consistent gains, which showcases your understanding of market dynamics"* | **Reproducibility of behaviour**, not a smooth equity curve |
| **Experience** | *"whether you meet minimum criteria for displaying credible trading experience"* | A **sample-size discount**. It multiplies rather than adds |

> 🔑 **Experience is a multiplier, not a term.** A perfect Skill/Risk/Consistency profile on a thin
> sample is **discounted**, not merely unranked. That is the same idea as the academy's minimum trade
> counts, and it means **the score cannot be rushed** — which is consistent with the no-deadline design.

### 📍 One calibration point `IMOX-DECK` `LOW-MEDIUM`

A worked reference profile from the community deck:

| | Value |
|---|---|
| **Edge Score** | **95** |
| Skill / Risk / Consistency / Experience | 96 / 94 / 95 / **100%** |
| **Win rate** | **37.5%** |
| Average win | **$713.25** |
| Average loss | **$293.03** |
| **RR ratio** | **2.43** |
| Expectancy | `0.375 × 713.25 − 0.625 × 293.03 =` **+$84.33 per trade** |

*(Arithmetic verified: 267.47 − 183.14 = 84.33. The deck's figure is correct.)*

> 🔴 **The headline consequence, and it inverts an assumption:**
>
> **A 37.5% win rate produces an Edge Score of 95.** Losing 6 of every 10 trades and still scoring
> near-perfect.
>
> > *"La puntuación no premia acertar. Premia que el resultado esperado sea positivo y que las
> > pérdidas estén bien encapsuladas."*

**This matters because the academy's filters point the other way.** The KB's selection tier requires
**Winning % > 48%** ([05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) step 6). **Axi does
not reward win rate at all** — it rewards **expectancy and loss containment**. A strategy rejected by
the academy's win-rate filter could score highly here.

✅ **But the academy's RR requirement is exactly right for Axi.** `RR > 2 or 3`
([06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md)) vs the reference profile's **2.43**. The two
frameworks agree on ratio and disagree on hit rate.

### 🔎 Reading the components `IMOX-DECK` `LOW-MEDIUM`

**Risk = can I predict how much you lose when you are wrong?**

| ✅ Healthy | ❌ Dangerous |
|---|---|
| Small, repetitive, **similar** losses. The stop is respected | *"Normally you lose little… until you improvise and turn −1R into −4R"* |

**Operationally: the variance of loss sizes, and the tail.** Uniform losses score; occasional
outsized losses destroy the component.

**Consistency = statistical coherence, NOT winning every day:**

- Similar risk · stable position size · same entry/exit logic · no revenge trading · no emotional changes
- *"Trade 1 ≈ Trade 1000"*
- *"Consistency looks more like reproducible behaviour than a perfect profit curve"*

### ⭐ Why this programme structurally favours an EA portfolio

Read the Consistency checklist again against what an EA **is**:

| Consistency criterion | An EA, by construction |
|---|---|
| Similar risk | ✅ Fixed-amount MM sets it |
| Stable position size | ✅ Deterministic |
| Same entry/exit logic | ✅ **That is the definition of an EA** |
| No revenge trading | ✅ Impossible |
| No emotional changes | ✅ Impossible |

> **An algorithmic portfolio should approach the maximum on Consistency by construction**, and the
> academy's **Fixed Amount MM** ($200 risk, or $10 on a small account —
> [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md)) is **precisely engineered for the Risk
> component**: it makes every loss the same size in currency terms.
>
> **Two of the three base components are handed to you by the toolchain.** The competitive dimension
> is **Skill** — profit against drawdown — which is the one the Retester and Optimizer already filter for.

### ❌ What probably costs points `IMOX-DECK` `LOW` — inference, not Axi's wording

The deck is explicit that this list is *"lo que **probablemente** penaliza"*, framed as
*"no por moral: por riesgo de asignarte más capital"*:

- Moving the stop when price goes against you
- Averaging down to improve the mean price
- 🔴 **Taking profits too short**
- Increasing size after a losing streak
- A loss distribution with **huge tails**

> 🔴 **The third item is the exact defect measured on the FTMO demo.**
> [MEASURED_FTMO_Demo_Baseline.md](MEASURED_FTMO_Demo_Baseline.md) found **RRR 0.29** caused by a
> trailing stop cutting winners at the next H1 bar. If this inference is right, that configuration
> would be **penalised twice** at Axi — once on Skill (weak expectancy) and once on whatever component
> reads profit-taking behaviour.
>
> And note the first and last items are **already prohibited or discouraged elsewhere**: moving the
> stop against you and averaging down are martingale-adjacent, which **FTMO bans outright**
> ([SERVICE_FTMO.md](SERVICE_FTMO.md) § 8). The two services penalise the same behaviours by
> different mechanisms.

### 📝 The deck's playbook `IMOX-DECK` `LOW`

1. Do not chase win rate — **chase ratio**
2. Define the risk **before** entering
3. Make your losses **predictable**
4. Reduce operational discretion
5. Make trade 1 look like trade 1000

> *"¿Me podrían multiplicar el tamaño sin multiplicar mi caos?"* — a fair one-line summary of what a
> funding programme is actually assessing.

---

### 🔒 The weights are a PERMANENT gap — not a pending one

**Confirmed by the user 2026-09-08**: the exact weights are **part of Axi's internal algorithm and are
not published**. This is not something the 1-a-1 mentoring or further research will close.

> ⚠️ **Reclassified.** Every other gap in these rulebooks is *pending* — someone knows the answer and
> it can be obtained. **This one is closed by design.** Any approach that waits for the weights is
> waiting forever, and any simulator that claims to compute an Edge Score is lying.

#### What the one calibration point actually constrains — almost nothing

Worth doing the algebra rather than assuming the observation is informative. With
`w_Skill + w_Risk + w_Consistency = 1` and Experience at 100% (multiplier 1):

```
Edge = w1·96 + w2·94 + w3·95
     = 95·(w1+w2+w3) + w1·(+1) + w2·(−1) + w3·(0)
     = 95 + (w_Skill − w_Risk)
```

**Edge is exactly 95 whenever `w_Skill == w_Risk`.** And since the dashboard displays an integer, 95
means the true value lies in `[94.5, 95.5)`, so the only constraint extracted is:

```
−0.5 ≤ w_Skill − w_Risk < 0.5
```

Swept over a 0.05 grid on the simplex, **166 of 231 weightings (72%) reproduce Edge = 95.**

> 🔴 **The observation is nearly uninformative**, because the three sub-scores are clustered within
> two points of each other (96 / 94 / 95). It is tempting to read "equal thirds" out of
> `(96+94+95)/3 = 95` — that is a **coincidence of symmetry**, not evidence.
>
> ✅ **What a USEFUL calibration point looks like**: an observation where the sub-scores **diverge
> widely** — say Skill 80 with Risk 40. Only then does the weighting move the total enough to be
> identifiable. **Clustered sub-scores carry no information about weights, however precise they are.**

#### ✅ Why this does not actually block the simulator

**The dashboard exposes all four sub-scores separately, each on its own 0-100 dial.**

That is the resolution: **you never need the weights to know what to improve.** The score decomposes
into four observable quantities, and each maps to something the pipeline already controls:

| Sub-score | What moves it | Already handled by |
|---|---|---|
| **Skill** | Profit against drawdown | Retester + Optimizer filters (**Ret/DD**) |
| **Risk** | Uniformity of losses, no fat tails | **Fixed-Amount MM** — every loss the same size |
| **Consistency** | Reproducible behaviour | ✅ **An EA, by construction** |
| **Experience** | Sample size | Trade count · time in programme |

**So the workable design is:**

1. **Rank by a proxy**, built from the four components' *definitions* rather than from Axi's formula.
2. **Do not present the proxy as an Edge Score.** Name it something else so it is never confused
   with Axi's number.
3. **Treat the live dashboard as the calibration signal.** Each observation period yields a
   `(Skill, Risk, Consistency, Experience, Edge)` tuple. Enough tuples **with divergent sub-scores**
   would let the weights be estimated empirically — slowly, and only as a nice-to-have.
4. **Optimise the components directly**, since they are visible. That is strictly better than
   optimising a reconstructed total, and it sidesteps the gap entirely.

> 🔑 **The gap is real and permanent, and it is also mostly harmless** — because Axi tells you your
> score on each dimension, and the dimensions are what you can act on. What is lost is only the
> ability to *predict* the aggregate before trading.

### Progression is a conjunction, not a score

To advance a stage you need **all of these simultaneously**:

**Edge score threshold** · **7% profit on the Allocation Account** · **minimum days served** ·
**minimum trades** · **minimum equity**. `HIGH`

**Where each is measured** — this distinction is easy to get wrong:

> *"All progress is calculated based on the **Allocation Account**: profit and Drawdown. Only the
> **Edge Score and equity** requirements are based on the **Axi Select trading account**."* `HIGH`

### 🔑 The bootstrap rule, and why it matters for a portfolio

You need **20 unique closed trades** before you get an Edge score at all.

> **"Unique trade"** = opened and then closed **with no other trade in that symbol open at the same
> time**. **Multiple concurrent trades in the same symbol count as ONE.** Hedged trades do not count.
> `HIGH`

> 🚨 **This is a direct constraint on group composition.** Running several strategies on the same
> symbol concurrently — say three gold systems — collapses their overlapping trades into **a single
> unique trade** for counting purposes. A portfolio built for diversification within an asset
> **actively works against the trade-count requirement here.**
>
> Diversifying *across symbols* satisfies both goals. Diversifying *within* a symbol satisfies only
> one. No other service in this comparison has this property.

---

## 2. Hard constraints

| Fact | Value | Conf. |
|---|---|---|
| **Max loss** | **−7%** at Seed, Incubation, Acceleration, Pro, Pro 500 · **−10%** at Pro M | HIGH |
| Basis | Percentage of the **initial allocation funds** for the stage | HIGH |
| **Measurement** | **Real time, continuous** — counts **unrealised** losses on open positions, not just realised | MEDIUM |
| **Daily drawdown limit** | ❌ **NOT FOUND** — none published. Only the per-stage max loss | — |
| Breach consequence | **Quarantine**: all open Allocation positions closed, no further funding, Allocation Account paused ~1 week | HIGH |
| Quarantine scope | Does **not** apply to Seed, Pro, Pro 500, Pro M — so it bites at **Incubation and Acceleration**. Traders in Pro stages **drop back to Seed**. 1st/2nd quarantine → invited back to the previous stage **one week** later, provided criteria are met; **3rd → rejoin at Seed** | HIGH `DASHBOARD` |
| Quarantine exit condition | If, when the week ends, **equity and/or Edge score are below the required level**, you cannot start the stage or use the Allocation Account until they are met | HIGH `DASHBOARD` |
| During quarantine | You can still trade your own Axi Select account; only the Allocation Account is restricted | HIGH |

> ⚠️ **Same data problem as FTMO**: the −7% is evaluated on **unrealised** P/L in real time. A
> closed-trade series cannot tell you whether an open position dipped below the floor intraday and
> recovered. See [SERVICE_FTMO.md](SERVICE_FTMO.md) § 2 — the limitation is identical and the
> resolution must be shared.

---

## 3. Eligibility

**Mechanism**: ineligible symbols execute on your own account but are **not copied to the Allocation
Account**.

| Class | Copied | Not copied | Conf. |
|---|---|---|---|
| **Forex** | **All FX symbols** | — | MEDIUM |
| **Indices** | **All indices** | — | HIGH |
| **Metals** | **Gold and Silver ONLY** | All other metals, including Copper | MEDIUM |
| Commodities | UK and US oil only | Brent/WTI-other, Cocoa, Coffee, Nat Gas, Soybean, all others | MEDIUM |
| **Crypto** | **BTCUSD and ETHUSD only** | All other cryptocurrencies | HIGH |
| Equities | Alphabet, Amazon, Apple, Meta, Microsoft, Netflix, Tesla (and variants) | All other equities | HIGH |
| Futures | Index futures only | All other futures | MEDIUM |

| Item | Value |
|---|---|
| **Stage-dependent restrictions** | ❌ **NOT FOUND** — the list is presented as programme-wide |
| **Timeframe / holding-period restrictions** | ❌ **NOT FOUND**. The only holding-related rule is the **scalping prohibition** (§ 8), defined qualitatively as *"very short timeframes"* |
| **Overnight / weekend holding** | ❌ **NOT FOUND** — no restriction published |

> ✅ **Gold, indices and FX — the academy's whole target set — are all eligible.** The portfolio shape
> from [00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md) (gold / NQ / DAX / a currency)
> maps cleanly onto this programme.

---

## 4. Activity requirements

| Stage | Min days in stage | Min trades in stage |
|---|---|---|
| **Pre-Seed** | none | **20 unique** trades to obtain an Edge score |
| **Seed** | **30** | **20** |
| **Incubation** | **60** | **40** |
| **Acceleration** | **60** | **50** |
| **Pro** | **60** | **50** |
| **Pro 500** | **60** | **50** |

*Source: official PDF. `HIGH`.*

> **These are floors, not deadlines** — see Correction 1. There is **no maximum stage duration**.

### ⚠️ Fit check against the declared plan — this is the real constraint

The declared profile is **3-4 trades a month** with **1-2 strategies** for Axi Select.

| Stage | Requirement | At 4 trades/month, 2 strategies (~8/month) | At 4 trades/month, 1 strategy |
|---|---|---|---|
| Seed | 20 trades / ≥30 days | ~2.5 months | **~5 months** |
| Incubation | 40 trades / ≥60 days | **~5 months** | **~10 months** |
| Acceleration | 50 trades / ≥60 days | **~6 months** | **~12.5 months** |

> 🔴 **The trade count, not the time, is the binding constraint** — and it binds hard. Reaching Pro
> would take **years** at this frequency. The academy was right that Axi Select needs volume; it was
> wrong about *why* (not a deadline, but an absolute count) and about the remedy (see § 8).
>
> **Note the interaction with the "unique trade" rule**: adding more strategies *on the same symbols*
> does not help proportionally, because concurrent same-symbol trades collapse to one. **Breadth
> across symbols is what raises the count.**

---

## 5. Risk model

| Fact | Value | Conf. |
|---|---|---|
| **Funding multiplier** | **x10** (Seed, Incubation) · **x25** (Acceleration) · **x40** (Pro) · **x50** (Pro 500, Pro M) — applied to **your own equity**, capped by the stage's max funding | HIGH |
| Per-trade risk cap | ❌ **NOT FOUND** | — |
| Max lot size | ❌ **NOT FOUND** | — |
| Minimum trade volume | ❌ **NOT FOUND** | — |

### ✅ RESOLVED — the leverage conflict `DASHBOARD` `HIGH`

The live Axi Select Pathway table settles it:

| Stage | Leverage |
|---|---|
| **Seed** | **Up to 1:1000** |
| Incubation → Pro M | **1:100** |

The PDF was right and the help-centre article was describing the post-Seed stages. **Seed genuinely
offers up to 1:1000**, dropping to 1:100 from Incubation onward.

> ⚠️ **1:1000 at Seed is not an invitation.** With a **−7% max loss** and a **$500 minimum equity**,
> high leverage at the stage that **pays 0% profit share** is the fastest route to quarantine for no
> economic gain. Treat it as headroom for position sizing on a small balance, not as a target.

---

## 6. Stages and transitions

| Stage | Max funding | Profit share | Min equity | Edge score | Profit target | Min days | Min trades | Max loss | Multiplier |
|---|---|---|---|---|---|---|---|---|---|
| **Pre-Seed** | — | n/a | **$500** to exit | **50** to exit | — | — | 20 unique | — | — |
| **Seed** | $5,000 | **0%** | $500 | 50 | **7%** | 30 | 20 | −7% | x10 |
| **Incubation** | $20,000 | **40%** | $1,000 | 60 | 7% | 60 | 40 | −7% | x10 |
| **Acceleration** | $100,000 | **50%** | $2,000 | 70 | 7% | 60 | 50 | −7% | x25 |
| **Pro** | $200,000 | **60%** | $5,000 | **90** | 7% | 60 | 50 | −7% | x40 |
| **Pro 500** | $500,000 | **70%** | $10,000 | 90 | 7% | 60 | 50 | −7% | x50 |
| **Pro M** | **$1,000,000** | **80%** | $20,000 | 90 | — | — | — | **−10%** | x50 |

*Source: official PDF. `HIGH`.*

> ✅ **Confirms the academy**: Seed pays **0%**; earnings start at Incubation (40%).
>
> ⚠️ **Corrects the academy's tier labels.** The figures 500 / 1,000 / 2,000 / 5,000 / 10,000 / 20,000
> that module 8 records as "capital tiers" are the **minimum equity YOU must hold**, not the capital
> allocated. The allocation ladder is **$5k → $20k → $100k → $200k → $500k → $1M**.

**Other mechanics:**

- The profit target **changed from 5% to 7%** at some point; **7% is current.** `HIGH`
- **`Start Stage`** must be pressed to begin or resume Seed after a reset or a drop-back. `HIGH`
- **Deposits mid-stage do NOT increase the Allocation Account** — the multiplier is set at the start
  of the month / first trade. `HIGH`

---

## 7. Economics

| Fact | Value | Conf. |
|---|---|---|
| **Cost to join** | **Free** — *"no registration fees or ongoing membership fees"*. Standard spreads/commissions and a minimum deposit apply | HIGH |
| **Minimum capital** | **$500 USD** equity to enter Seed; scales to $20,000 at Pro M | HIGH |
| Profit split | 0 / 40 / 50 / 60 / 70 / **80%** | HIGH |
| **Payout cadence** | Automatic, **1st day of each calendar month**, credited to the Axi Select account | HIGH |
| **Payout conditions** | (a) Allocation Account balance > original Axi funding at month end, **and** (b) **NO OPEN POSITIONS** in the Allocation Account | HIGH |
| Missed payout | Open positions at month end → **no payment**, account unchanged | HIGH |
| Post-payout | Allocation Account **resets to the original funding amount** | HIGH |
| Deferring | You may deliberately keep a position open at month end to **defer** a payout | HIGH |
| **Withdrawal penalty** | A mid-month withdrawal **forfeits all profit gained that period**; the Allocation Account resets and the multiplier recalculates. Withdrawing below the stage minimum equity → **drop out, restart from Seed** | HIGH |
| Entity | Only for clients of **AxiTrader LLC**; not available in all regions | HIGH |

### 🔴 The withdrawal rules — there is a safe window, and it is narrow `DASHBOARD` `HIGH`

**Withdrawing at the wrong moment forfeits that month's profit.** The rules are specific enough to
plan around:

| Situation | Consequence |
|---|---|
| **Withdrawal at any point during the month** | **Forfeits all profit gained that period.** The Allocation Account resets and the multiplier is recalculated |
| Withdrawal while the Allocation Account has **negative PnL** | Allocation is **reduced immediately by `withdrawal × multiplier`**. Open positions are **not** auto-closed. If the reduction pushes you past max drawdown, **you may enter quarantine** |
| Withdrawal that drops you **below the stage minimum equity** | **Drop out of the stage and restart from Seed** |
| Any withdrawal **in Seed** | Allocation resets and profit is lost (there are no performance-fee payouts in Seed anyway) |

> ✅ **The safe window**: withdraw **after the Performance Fees have been paid out** (1st of the month)
> and **before opening the first position of the new month**. Once PFs are paid, you may withdraw and
> the multiplier is then set for the month. **Once a trade is placed, the period has commenced** and
> any later withdrawal counts as mid-month.
>
> Axi's own examples: PFs paid 1 August with no trades open by the 15th → a withdrawal simply adjusts
> Allocation and multiplier. But if a position is opened on the 2nd and a withdrawal is requested
> after, **the profit share owed so far that month is forfeited**.

**Operationally for an EA portfolio this is sharp**: the EAs must be **stopped** at month end until
the withdrawal is done, otherwise the first automated entry of the month closes the window. That is a
scheduling requirement, not a trading one.

### ⚠️ Deposits do NOT increase the Allocation `DASHBOARD` `HIGH`

> Depositing additional funds **during a stage** does not increase Allocation funding. Axi's example:
> at Acceleration with $2,000 in the Axi account, an x25 multiplier and a **$50,000** Allocation,
> depositing another $2,000 aiming for the $100,000 maximum **leaves the allocation at $50,000**.

**The multiplier is set at the start of the month / first trade.** Capital added mid-stage is dead
weight until the next cycle — so **top up in the same window as the withdrawal**, before the first
trade of the month.

---

> 🔴 **The month-end flat requirement is a real constraint for a swing portfolio.** An H4 system
> holding a position across the month boundary **forfeits that month's payout entirely**. With 2-3
> trades per month per strategy and multi-day holds, the probability of being flat at a specific
> instant is not high. This needs to be modelled, and it argues for **either** short-hold systems
> **or** deliberate flattening before month end.
>
> ✅ **The user's declared $1,000 starting capital comfortably clears the $500 Seed minimum**, and the
> programme is **free to join** — a materially different risk profile from FTMO's non-refundable fee.

---

## 8. Prohibited behaviours

| Behaviour | Rule | Conf. |
|---|---|---|
| **Scalping / artificially inflating trade count** | *"Scalping strategies are **strictly prohibited**… opening and closing trades within very short timeframes — with the primary intent to exploit minimal price movements **or artificially meet trade count requirements**… Participants who continue to engage in scalping may face **disqualification and permanent removal from the program**."* | **HIGH** |
| **Copy trading** | *"The use of Copy Trading in Axi Select is **strictly prohibited**"* | HIGH |
| **Expert Advisors** | ✅ **Your OWN EA is permitted.** Someone else's EA, or supplying an EA to others, is allowed **only for generating signals** — the trades must then be **placed manually**. EAs must not be used for copying trades | HIGH |
| Numeric definition of "very short timeframe" | ❌ **NOT FOUND** — the rule is qualitative and discretionary | — |
| PAMM / MAM, demo accounts | Dedicated help articles exist; contents not retrieved | NOT FOUND |

### ✅ EAs are fine — this was worth confirming

An algorithmic H1/H4 portfolio of **your own** EAs is explicitly permitted. The copy-trading ban
targets mirroring someone else's account, not automation.

### 🚨 But the academy's trade-count remedy may violate the rule

[08_Servicios_de_Fondeo.md](08_Servicios_de_Fondeo.md) records this as the *legitimate* way to meet
the trade quota:

> *"Un portfolio donde introducís divisas… operan a un micro y sacan 1 o 2 dólares y **rellenan
> trades**."*

And the official rule prohibits:

> *"opening and closing trades within very short timeframes — with the primary intent to exploit
> **minimal price movements** or **artificially meet trade count requirements**"*

> ⚠️ **Those two descriptions overlap almost exactly.** Micro-lot positions taking 1-2 dollars, run
> for the stated purpose of filling the trade count, matches both halves of the prohibited definition.
>
> **I am not asserting the academy's tactic is banned** — "very short timeframes" is undefined, and a
> genuine H1 currency strategy that happens to produce small wins is not the same as a scalp. But the
> distinction rests entirely on **intent and holding period**, both of which Axi judges at its
> discretion, with **permanent removal** as the stated penalty.
>
> **This belongs on the 1-a-1 question list.** It is exactly the kind of thing where a mentor's
> first-hand experience beats a document — and exactly the kind of thing where being wrong is
> expensive.

---

## 🎯 What the simulator gets from this service

| Need | Status |
|---|---|
| **Objective function** | 🔒 **Structure known, weights PERMANENTLY unavailable.** `weighted_avg(Skill, Risk, Consistency) × discount(Experience)`. Build a **named proxy** and rank by it — never call it an Edge Score. **Optimise the four sub-scores directly**: the dashboard exposes each one, which sidesteps the missing weights entirely |
| **Win rate as a criterion** | ❌ **Do NOT apply the academy's `Win % > 48%` here.** Axi rewards **expectancy and loss containment**, not hit rate — 37.5% scored 95 |
| **Consistency / Risk components** | ✅ **Structurally favourable to EAs.** Same logic, stable size, no discretion. The academy's Fixed-Amount MM directly serves the Risk component by making every loss the same size |
| Time pressure | ✅ **None** — minimums to serve, no maximum |
| **Trade count** | 🔴 **The binding constraint**: 20 / 40 / 50 unique trades per stage |
| **"Unique trade" rule** | 🔴 **Changes group composition**: concurrent same-symbol trades count as ONE. Breadth across symbols raises the count; depth within a symbol does not |
| Breach modelling | ⚠️ −7% on **unrealised** real-time equity — same intraday-path problem as FTMO |
| Balance to evaluate against | ✅ Allocation = your equity × multiplier, capped per stage |
| Eligibility | ✅ Programme-wide list; gold, indices and FX all eligible |
| **Month-end flat requirement** | 🔴 Must be modelled — open positions at month end forfeit the payout |

---

## ❌ Explicit gaps — NOT FOUND, not inferred

- 🔒 **Edge score component weights or functional form** — **PERMANENT.** Confirmed 2026-09-08 as
  part of Axi's internal algorithm, not published and not obtainable. See § 1 for why this does
  not block the simulator, and for the algebra showing the single known calibration point
  constrains the weights almost not at all.
- Daily drawdown limit · per-trade risk cap · max lot size · minimum trade volume.
- Numeric definition of "scalping".
- Maximum time allowed in a stage.
- Any stage-dependent instrument restriction.
- **Unresolved**: PDF says 1000:1 leverage at Seed, help centre says a fixed 100:1. Both are Axi's own.
