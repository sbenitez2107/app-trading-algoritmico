# Measured Incident — Concurrent BTC Margin Exhaustion, FTMO Demo

> **Type**: `measured-data` — not doctrine, not vendor rules. **This is what the deployed system
> actually did**, read from the live MT4 console, not estimated.
> **Source**: MT4 console screenshot, FTMO demo account, provided by the user 2026-09-14.
> **When**: 2026.09.14 08:02:39, all three orders open at that exact second.

---

## 1. What happened

Three separate EAs opened BTCUSD longs **in the same second, at the same price**, and together
consumed more margin than the account had free:

| Order | Size | Open price | S/L | T/P | Commission | Profit | Comment |
|---|---|---|---|---|---|---|---|
| 113446513 | 0.05 | 77,594.38 | 78,725.80 | 79,522.02 | −2.52 | 60.24 | `Strategy_4_6_207` |
| 113447978 | 0.07 | 77,594.38 | 78,725.80 | 79,066.91 | −3.52 | 84.34 | `Strategy_6_61_378` |
| 113448459 | 0.07 | 77,594.38 | 78,725.70 | 78,963.25 | −3.52 | 84.34 | `Strategy_3_9_217` |

All three opened at **2026.09.14 08:02:39**. Current price at the reading: **78,799.21**. Combined
profit **+219.36**.

Account state at that moment:

```
Balance 10,030.69 · Equity 10,250.05 · Margin 14,742.93
Free margin −4,712.24 · Margin level 69.53%
```

---

## 2. The arithmetic that explains it

0.05 + 0.07 + 0.07 = **0.19 lots**. 0.19 × 77,594.38 = **14,742.93**, which matches the reported
Margin **exactly**.

So **margin equals full notional — leverage 1:1 on crypto**, the same treatment
[SERVICE_Darwinex_Zero.md](SERVICE_Darwinex_Zero.md) records for Darwinex Zero. An account with
10,250 of equity was holding 14,743 of notional.

Margin level 10,250.05 ÷ 14,742.93 = **69.53%**, confirming the reported figure.

---

## 3. The operational consequence

**Free margin is negative, so no new position can open.** Four pending orders sat in the same
console and would be **rejected on trigger** rather than filled:

| Type | Size | Symbol | Price | Comment |
|---|---|---|---|---|
| buy stop | 0.29 | us100.cash | 29,533.49 | `WF_7_24_NQ_H1_ATR_BB_ADX_4_2_2` |
| buy stop | 0.10 | xauusd | 4,420.33 | `ORIGINAL_XAUUSD_H4_C_LIR_7_69` |
| buy stop | 0.01 | xauusd | 4,456.85 | `WF_8_34_XAUUSD_H1_P_CCI_EQUITY` |
| buy stop | 0.06 | xauusd | 4,402.32 | `WF_6_22_XAUUSD_H1_H_CP_4_28_15` |

A pending order that silently fails to fill is worse than one that was never placed, because the
operator believes the entry is covered.

---

## 4. The cause is correlation by design, not coincidence

The three BTC EAs are different strategies with different profit targets, yet they fired on the
same bar at the same price. The pseudo-code of one of them, `BTC_H1_Highest_MACD_4.6.207`
(StrategyQuant X Build 136, MetaTrader4 engine, backtested on `BTCUSD_M1_UTC02` H1 over
2020.01.01–2026.08.21), shows why:

- Entry: `Open Long order at Highest(Main chart, 30, PRICE_OPEN)[1] Stop`, order valid for 26 bars.
- `Stop Loss = 2.7 × ATR(40)` · `Profit target = 5.8 × ATR(90)` · `Trailing Stop = 90 pips`.
- Signal: `MACD(60, 17, 9, PRICE_HIGH).Signal[1] is rising`. Short side disabled.
- **Every rule is evaluated On Bar Open.** `LimitSignalsTimeRange` 0100–2300.

A **breakout stop order placed at the highest open of the last N bars, evaluated on bar open**, is
a structural trigger. Several H1 crypto EAs built from the same family will converge on the same
bar even when their indicators and targets differ. The diversification is in the parameters, not
in the moment of entry.

---

## 5. Why this is the case study for a metric that does not exist

`08_Servicios_de_Fondeo.md:183-186` states the academy's group criterion: **1% total portfolio
risk on the worst simultaneous case**. `openspec/SIMULATOR_ROADMAP.md` records that **the quantity
that rule constrains is computed nowhere** — `AnalyticsSeries.ComputeExposure` deliberately merges
overlapping intervals, so it measures breadth of time in market, not depth of concurrency.

This episode is what that gap costs in practice: nothing warned that three EAs would take the same
position at the same instant until the margin went negative. Record it as the concrete test case
for the worst-case-simultaneous-risk metric planned in roadmap layer 2.

---

## 6. Two corrections to record honestly

1. **An earlier inference was wrong.** Looking only at the FTMO web panel, the assistant inferred
   that the stop was **virtual and EA-managed**, reasoning that MT4 rejects a stop-loss placed on
   the wrong side of price for a long. The MT4 console refutes it: the `S/L` column carries
   **78,725.80**, a real broker-side value. The stop is broker-side and functioning.
2. **The web panel's price display was inconsistent with MT4.** The panel showed a current bid of
   **78,717.62** against a stop at 78,725.80 — below the stop, with the position still open. MT4
   showed **78,799.21**. A broker-side stop would have fired had price genuinely traded there, so
   the panel reading, not the stop, was the anomaly. Treat the FTMO web panel's live price as
   indicative and MT4 as authoritative.

---

## 🎯 What to do with this

| Finding | Action |
|---|---|
| Three BTC EAs fired the same second at the same price, margin went negative | 🔴 **Concrete test case** for the missing worst-case-simultaneous-risk metric (roadmap layer 2) |
| Margin = full notional on crypto (1:1 leverage) | ⚠️ Matches Darwinex Zero's crypto treatment — do not assume forex-style leverage for BTCUSD sizing |
| Negative free margin left 4 pending orders unfillable | 🔴 A silently-rejected pending order is worse than none — the operator believes the entry is covered |
| Breakout-on-bar-open entry logic is a structural correlation trigger, not coincidence | ⚠️ Different parameters do not guarantee different entry moments for EAs built from the same family |
| FTMO web panel price disagreed with MT4 near the stop | ⚠️ Treat MT4 as authoritative; the web panel is indicative only |
| Decision taken | The user keeps **one** of the three BTC strategies and drops the other two |
