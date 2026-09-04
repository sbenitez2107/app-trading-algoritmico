# Exploration: Import Darwinex MT4 Statement HTML

## Trigger

User wants to import a broker-side HTML statement of their Darwinex MT4 demo account to:
1. See real trade list (closed + open) attributed to each EA (Strategy)
2. Compute real KPIs per EA (DD%, Ret/DD, win rate, ...)
3. Track real spread / swap / commission
4. Compare SQX backtest vs real performance

Scope of Sprint 1 is only (1) — the trade list. KPIs/comparisons in Sprint 2+.

## Source file inspected

`strategies/Report Trades DW DEMO2.htm` (62 KB, 222 lines). Live report dated 2026-04-21, account 2089130867, currency USD.

## Key findings

### 1. The ticket cell's `title` attribute carries magic number + strategy name + close reason

```html
<td title="#2333376 WF_8_34_NQ_SH_LIR_H1_2_33_3[sl]">263463718</td>
```

- `#{N}` — EA magic number (integer)
- `{name}` — SQX filename sanitized (dots → underscores), often truncated to ~30 chars
- `[sl|tp|...]` — close reason suffix, OPTIONAL. Absent for open trades. `cancelled` used for pending orders that expired.

Regex: `^#(\d+)\s+(.+?)(?:\[(\w+)\])?$` with a special case for "cancelled".

This eliminates any need to infer close reason from price comparisons. Darwinex flags it at the broker level.

### 2. Four sections in the report

| Section | Marker | Lines | In scope v1 |
|---------|--------|-------|-------------|
| Closed Transactions | `<b>Closed Transactions:</b>` | 35-155 | Yes (executed trades only; cancelled rows skipped) |
| Open Trades | `<b>Open Trades:</b>` | 156-181 | Yes (CloseTime null, ClosePrice from "Market Price" column — but NOT stored as ClosePrice since trade is open) |
| Working Orders | `<b>Working Orders:</b>` | 183-195 | **No** — pending orders, not trades |
| Summary | `<b>Summary:</b>` | 197-219 | Yes — Balance, Equity, Floating P/L, Margin, Free Margin |

### 3. Row structure (14 columns, Closed Transactions)

`Ticket | OpenTime | Type | Size | Item | Price | S/L | T/P | CloseTime | ClosePrice | Commission | Taxes | Swap | Profit`

- Types observed: `buy`, `sell`, `buy stop`, `sell stop`, `buy limit`, `sell limit`
- Times format: `2026.04.20 14:47:17` (broker local time)
- Item is the broker symbol, lowercase: `ndx`, `xauusd`, `gdaxi`
- Cancelled rows replace the last 4 cells with `<td colspan=4>cancelled</td>`

### 4. Summary section (account-level snapshot)

```
Balance: 102,730.18 | Equity: 102,918.16 | Floating P/L: 187.98
Margin: 9,054.14    | Free Margin: 93,864.02
Deposit/Withdrawal: 0.00 | Credit Facility: 0.00 | Closed Trade P/L: 2,897.15
```

Report datetime is in the header (line 33): `2026 April 21, 07:06` — parseable but Darwinex uses a custom format (`YYYY Month DD, HH:MM`).

## Strategy → Trade attribution

- **Primary key for matching**: `MagicNumber` scoped to `TradingAccountId`.
- User must set `Strategy.MagicNumber` manually when adding the strategy (confirmed in user Q&A).
- Trades whose magic number has no matching Strategy in the account → "orphans". Reported back to the user with the strategy name hint from the title, so they can link quickly.
- Secondary name match (fuzzy) is NOT used to avoid false positives — magic number is exact.

## Things deliberately out of scope (Sprint 1)

- Cancelled orders (visible in Closed Transactions with "cancelled" marker) — skipped, they're pending orders that expired without executing, not trades.
- Working Orders section — pending orders that never filled.
- Equity chart over time (line chart of Balance/Equity per import) — data is captured via `AccountEquitySnapshot`, but chart is Sprint 2.
- Per-EA equity curve — computed on-demand from cumulative P&L of closed trades, Sprint 2.
- MT5 statement format — wait until user shares a real MT5 report. Parser is Darwinex-specific for now.
- Multi-broker support — YAGNI. If another broker appears, factor out a parser interface.

## Risks flagged

1. **Duplicate ticket numbers across re-imports** → solved by `(StrategyId, Ticket)` unique index + upsert semantics.
2. **Strategy name truncation** in the title makes name-matching unreliable — rely only on magic number.
3. **Darwinex "Tradeslide Trading Tech" branding** — if they re-brand, header parsing needs to be lenient. We match by section markers (`Closed Transactions:` etc.) which are stable.
4. **Time zone** — report times are in broker local time (UTC+2/+3 for Darwinex). Store as given; do NOT try to normalize to UTC in v1.
5. **Currency assumption** — one account = one currency (USD here). Store currency at the snapshot level.

## Artifacts

- engram: `sdd/import-mt-trades/explore`
- file: `openspec/changes/import-mt-trades/explore.md`
