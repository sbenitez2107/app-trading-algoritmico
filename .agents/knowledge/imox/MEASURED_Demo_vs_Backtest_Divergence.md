# Measured Divergence — Demo vs Backtest, WF_7_30_NQ_H_CW_H_O_H1_2.34.172

> **Type**: `measured-data` — not doctrine, not vendor rules. **This is what the deployed system
> actually did**, computed from the project's own database, not estimated.
> **Source**: `AppTA` database, queried 2026-09-09 with the user's authorization, SELECT only.
> **Subject**: strategy `WF_7_30_NQ_H_CW_H_O_H1_2.34.172` on Darwinex demo account **SBDEMO2** — the
> only one of 123 strategies in the database with both demo trades and backtest runs loaded, which is
> what makes a demo-vs-backtest comparison possible at all right now.
> **Window**: demo 2026-04-21 → 2026-08-28.

---

## 1. The measured divergence

| Series | Trades | Net |
|---|---|---|
| **Demo (real)** | 47 | **+$897.51** |
| Backtest Deploy | 42 | +$300.90 |
| Backtest Evaluation | 39 | +$599.24 |

**Demo costs over the window**: gross +$937.13, commission −$18.05, swap −$21.57, net +$897.51. Cost
is **4.2% of gross**, and swap alone is more than half of that cost while touching only **6 of the 47
trades**.

---

## 2. Where the gap lives — period reconciliation

Splitting the window by which series was trading on which days, the arithmetic reconciles exactly to
both totals above:

| Period | Demo | Deploy |
|---|---|---|
| 21/04 → 12/06 (**both series trading the same days**) | **+$920.97** | **+$471.27** |
| 29/06 → 27/08 (demo-only days) | −$43.79 | — |
| 06/07 → 10/07 (Deploy-only days) | — | −$196.20 |
| 28/08 | +$20.33 | +$25.83 |

> **Most of the gap is in the period where both series trade the SAME days**, not in the disjoint
> ones. Whatever is causing the divergence is active even when both systems see the same signals at
> the same time — it is not a coverage or scheduling artifact.

---

## 3. The root cause — a systematic, drifting price offset

**24 trades open at the exact same minute** in both series — same signals, same timing, so the
strategy logic is identical in demo and backtest. But entry prices differ systematically:

| Month | N | Mean offset | Range |
|---|---|---|---|
| 2026-04 | 5 | **+22.06** | 16.2 – 25.5 |
| 2026-05 | 14 | **+22.55** | 11.4 – 31.9 |
| 2026-06 | 5 | **+40.90** | 38.5 – 44.3 |

Three properties of this offset: it is **always positive** (demo above backtest), it is
**systematic** (never near zero), and it **drifts** — nearly doubling between May and June.

### Confirmed cause, from the user's SQX screenshots

The Data Manager binds data symbol **`USATECHIDXUSD_M1_UTC02`** (Dukascopy's USA100 CFD) to
instrument **`NDX_DARWINEX`**. So the **price series comes from Dukascopy while the point value and
costs come from Darwinex**. The instrument specification is correct; the price series is a different
broker's product.

### Worked example — 2026-06-11, same signal, both open 16:50

| Series | Entry | Exit | Result |
|---|---|---|---|
| Demo | 28928.2 | closed 29224.0 | **TP, +$147.90** |
| Deploy | 28888.8 | closed 28563.1 | **SL, −$195.75** |

Demo SL was 28575.4. The backtest entered **39 points lower**, the dip reached its stop, and demo's
stop was never touched.

### What must NOT be claimed

This does **not** establish that demo has an edge over the backtest. With **24 paired signals** the
direction cannot be established statistically; the measured fact is that the paths differ
systematically, and that on this sample the difference happened to favour demo. A larger sample could
show the opposite sign just as easily — the offset's *sign* is not what is being asserted here, only
its *existence and drift*.

---

## 4. A second, independent modelling gap — bar-based intrabar path

The AlgoWizard backtest runs at precision **"1 minute data tick"** — M1 bars, not real ticks. The
intrabar path is inferred from the minute's OHLC, so when SL and TP are both reachable inside one bar,
**the order in which they are touched is an assumption**, not an observation. This is an **independent
cause** of the same TP-versus-SL symptom seen in the worked example above, and it would persist even
if the price series matched the instrument's own broker exactly.

---

## 5. A hypothesis ruled out by the data — timezone / DST

The Data Manager records timezone **UTC+02 Jerusalem, DST: Yes**. Israeli DST rules differ from the US
rules Darwinex follows, so a seasonal mismatch is real in principle. **It is not the cause of this
finding**: the offset is systematically **positive** across all 24 paired trades, whereas a timezone
error would pair different market moments and produce a **random** sign, not a consistent one.

Record it instead as a **separate seasonal risk** at the DST transition boundaries (late March and
late October/November) — a distinct concern from the price-series mismatch above, not its
explanation.

---

## 6. Independent confirmation of the cost model

The SQX instrument config is **Size-based commission at 5.5 $/lot, swap OFF**. Computing the implied
point value per trade as `Profit / (|ClosePrice − OpenPrice| × Size)`:

| Series | Implied point value |
|---|---|
| **Demo** | exactly **10.0000** on every trade |
| Backtest | **9.81 to 12.5**, deviating *above* 10 on losses and *below* 10 on wins |

Demo's `Profit` field is the **pure price move** — costs live in separate columns. The backtest's
spread away from 10 is **the signature of cost embedded inside `Profit`**. A 0.06-lot trade implied an
embedded cost of **$0.33**, and `5.5 × 0.06 = 0.33` — an exact match against the configured commission
model.

**Consequence worth stating plainly**: the backtest's embedded cost is **measurable**, not merely
estimable, from price arithmetic alone.

---

## 7. Instrument configuration, for the record

**`NDX_DARWINEX`** — description "NASDAQ IMOX", data type Index, point value $10, pip/tick size 1,
pip/tick step 0.1, default spread 1, slippage 0, order size multiplier 1, commission Size-based 5.5,
swap **OFF** with triple-swap Wednesday.

**AlgoWizard**: engine MetaTrader4, symbol USATECHIDXUSD_M1, timeframe H1, range 2015.12.31 →
2026.09.06 (data available from 2012.01.19), precision **"1 minute data tick"**, spread 1, slippage 0.

**Money management**: initial capital 100000, **Fixed amount**, RiskedMoney 200, **Size Decimals 2**,
Size if no MM 0.1, Maximum lots 10.

---

## 8. A third, independent cause — gaps in the backtest source data delete trades, they don't distort them

Sections 3 and 4 both change the **outcome** of a trade that exists on both sides: a different entry
price, or an assumed intrabar path. This cause is categorically different: **when the backtest source
data has a gap, the strategy could not trade at all during the missing period.** No entry price to
compare, no intrabar order to assume — the trade is simply absent from the backtest.

> **Causes 1 and 2 distort a trade. This one deletes it.**

### Measured evidence — DAX, from the user's SQX Data Manager

Symbol `DEUIDXEUR_M1_UTC02`, instrument `GDAXI_DARWINEX`, range 2013.09.30 → 2026.09.04,
**3,867,271 imported records**:

| Issue | Count | % of data |
|---|---|---|
| **Gaps** | **6,980** | **9.647%** |
| Spikes | 497 | 0.686% |
| Bad OHLC | 0 | 0 |

The graphical quality view shows a solid red block across **August 2026**, and the detailed list
names `2026.08.10 09:00` and `10:00` as missing — open, high, low, close and volume all absent.

**Consequence in the backtest trade list**: it jumps from **2026.07.22 straight to 2026.09.04** — **no
August trade exists**. Meanwhile the demo account took **five DAX trades in August**: 06/08, 18/08,
26/08 (twice) and 27/08.

### The same pattern, measured in NQ from the project database

Strategy `WF_7_30_NQ_H_CW_H_O_H1_2.34.172` — the same strategy this document is built around — trades
opened per month in 2026:

| Month | Demo | Backtest Deploy |
|---|---|---|
| April | 9 | 10 |
| May | 17 | 19 |
| June | 9 | 7 |
| July | 4 | 6 |
| **August** | **8** | **2** |

The two August backtest trades open on the **28th and 31st** — so **1 to 27 August is empty** on the
backtest side.

> This lines up with the demo-only period already recorded in §2 (**29/06 → 27/08**, −$43.79, no
> Deploy counterpart) and with row **28/08**, where both series resume trading together. §2 did not
> assign a cause to that gap; this section supplies one for its final five weeks. It does not explain
> the earlier part of that period (29/06 → 31/07), which remains unattributed here.

### The implication

With roughly **one bar in ten missing across thirteen years** on DAX, this is not an incident — it is
a **property of this data source**. It bounds how much any DAX backtest figure can be trusted while
the price series comes from this provider, independently of the offset in §3 and the intrabar
assumption in §4.

### Correction check

§2 lists a demo-only period (29/06 → 27/08, −$43.79) without stating a cause, and its surrounding text
(the "same signals at the same time" remark) refers only to the **shared-days** period, not this one.
**No attribution of the demo-only period to "different signals firing" or to strategy divergence was
found in this document** — so no correction to §2's wording is needed. The cross-reference above adds
the missing cause for the tail of that period without changing §2's own text.

### What must NOT be claimed

This does not mean the DAX or NQ strategies are worse than measured — it means the **backtest side of
the comparison is incomplete** for the missing windows. A gap-affected backtest figure understates
what the strategy would have done, it does not indicate the strategy performed poorly during that
window.

---

## 🎯 What to do with this

| Finding | Action |
|---|---|
| Data Manager bound Dukascopy price data to a Darwinex instrument | 🔴 **Rebind the data symbol to a Darwinex-sourced series**, or accept the offset as a known, drifting bias when reading this backtest against demo |
| 22-41 point drifting offset on 24 paired trades | ⚠️ Do not treat this backtest's individual TP/SL outcomes as predictive of demo on the same signal — the entry price itself differs |
| "1 minute data tick" precision | ⚠️ **Independent gap.** Re-running at real tick precision (Optimizer stage, per [01_SQX_Data.md](01_SQX_Data.md)) would remove this cause even if the price series stayed mismatched |
| DST mismatch (Jerusalem vs US rules) | ⏳ **Not the cause here** — track separately as a seasonal risk at the March / Oct-Nov transitions |
| Backtest cost is embedded and measurable from price arithmetic | ✅ Confirms the cost model is correctly configured; useful as a cross-check method for other strategies |
| Sample is 24 paired trades | 🚫 **Cannot support a demo-outperforms-backtest claim** — only that the paths diverge systematically |
| DAX source data has 9.647% gaps (6,980 of 3,867,271 records), a full August 2026 outage | 🔴 **Trades are missing, not distorted** — bounds trust in any DAX backtest figure while the data comes from this provider; check the gap % for any instrument before trusting a backtest-vs-demo comparison on it |
| NQ August 2026: 2 backtest trades vs 8 demo trades, backtest empty 01-27/08 | ⚠️ Confirms the same source-data-gap pattern outside DAX — a high `DemoOnlyCount` for a period can mean missing data, not strategy divergence |
