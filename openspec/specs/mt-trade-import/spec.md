# MT Trade Import Specification

## Purpose

Parse a Darwinex MT4 HTML "Detailed Statement", attribute closed and open trades to Strategies
by `(TradingAccountId, MagicNumber)`, persist `StrategyTrade` rows plus one `AccountEquitySnapshot`
per upload, and return a structured result with orphan aggregation.

Fixture reference: `strategies/Report Trades DW DEMO2.htm` (account 2089130867, USD, 2026-04-21).

---

## Requirements

### Requirement: R1 — Upload Endpoint

The system MUST expose `POST /api/trading-accounts/{id}/trades/import` accepting a multipart
`IFormFile`. On success it MUST return HTTP 200 with `TradeImportResultDto`. On non-existent
`TradingAccountId` it MUST return HTTP 404. On null/malformed HTML it MUST return HTTP 400.

#### Scenario: Happy path upload

- GIVEN a valid `TradingAccountId` and the fixture file `Report Trades DW DEMO2.htm`
- WHEN `POST /api/trading-accounts/{id}/trades/import` is called
- THEN HTTP 200 is returned with `imported + updated = total non-skipped trades`, `snapshot` populated, and `orphans` list

#### Scenario: Non-existent account

- GIVEN a `TradingAccountId` that does not exist in the database
- WHEN the upload endpoint is called
- THEN HTTP 404 is returned

#### Scenario: Empty or malformed HTML

- GIVEN a file with empty content or HTML that has no recognised section markers
- WHEN the upload endpoint is called
- THEN the parser returns null and the endpoint returns HTTP 400

---

### Requirement: R2 — Closed Transactions Parsing

The parser MUST locate the `<b>Closed Transactions:</b>` marker and extract all subsequent `<tr>` rows
until the next section marker. Each row MUST be mapped to 14 columns:
`Ticket | OpenTime | Type | Size | Item | Price | S/L | T/P | CloseTime | ClosePrice | Commission | Taxes | Swap | Profit`.

#### Scenario: Normal closed trade row

- GIVEN the fixture row `<td title="#2333376 WF_8_34_NQ_SH_LIR_H1_2_33_3[sl]">263463718</td>…`
- WHEN the parser processes it
- THEN `Ticket=263463718`, `MagicNumber=2333376`, `StrategyNameHint="WF_8_34_NQ_SH_LIR_H1_2_33_3"`, `CloseReason=SL`, `OpenTime=2026-04-20 14:47:17`, `CloseTime=2026-04-20 17:03:33`, `ClosePrice=26615.1`, `Profit=20.96`

#### Scenario: TP close reason

- GIVEN a row with title suffix `[tp]` (e.g. ticket 263004851 in fixture)
- WHEN the parser processes it
- THEN `CloseReason=TP`

#### Scenario: Close reason absent (no bracket suffix)

- GIVEN a closed row with title `"#4533187 WF_9_26_XAUUSD_H1_KAMA_BB_4_53"` (no bracket)
- WHEN the parser processes it
- THEN `CloseReason=null`

#### Scenario: Unrecognised close reason suffix is preserved, not bucketed

- GIVEN a row whose title ends with `[other_value]`
- WHEN the parser processes it
- THEN `CloseReason="OTHER_VALUE"` — the suffix is trimmed and upper-cased, never collapsed into an `Other` bucket

> Corrected after the fact. This scenario previously specified `CloseReason=Other`. Commit
> `04cd462` deliberately changed `MtStatementParserService.MapCloseReason` to preserve the raw
> upper-cased suffix and updated the tests; the spec was not updated with it and described a
> system that had stopped existing. The shipped behaviour is the better one — a bucket discards
> the only information the suffix carried — so the spec was corrected to the code, not the
> reverse.

---

### Requirement: R3 — Open Trades Parsing

The parser MUST locate the `<b>Open Trades:</b>` marker and extract its rows. For open trades
`CloseTime` MUST be null and `ClosePrice` MUST NOT be stored (the "Price" column in Open Trades
is market price, not a close price). `IsOpen` MUST be true.

#### Scenario: Open trade row

- GIVEN the fixture row with ticket 263502096 (XAUUSD, magic 15418111, no close time)
- WHEN the parser processes it
- THEN `CloseTime=null`, `IsOpen=true`, `ClosePrice=null`, `OpenTime=2026-04-21 01:01:17`, `OpenPrice=4833.84`

---

### Requirement: R4 — Cancelled Rows Skipped

The parser MUST detect rows where the last cell contains `colspan=4` with text `cancelled` inside
Closed Transactions and MUST skip them. Skipped rows MUST NOT be counted as `imported`, `updated`,
or `orphans`.

#### Scenario: Cancelled row in Closed Transactions

- GIVEN the fixture row ticket 263492812 (`title="#62218610 cancelled"`, `colspan=4>cancelled</td>`)
- WHEN the parser processes it
- THEN the row is excluded from the parsed result entirely

#### Scenario: Cancelled title with magic number — no orphan created

- GIVEN a cancelled row with `title="#2483342 cancelled"`
- WHEN the service processes the result
- THEN no orphan entry is created for magic number 2483342

---

### Requirement: R5 — Working Orders Section Skipped

The parser MUST stop collecting rows when it encounters `<b>Working Orders:</b>` and MUST NOT
parse any rows under that section.

#### Scenario: Working Orders rows are not in parse result

- GIVEN the fixture file which contains 6 Working Orders rows (tickets 263455666–263535623)
- WHEN the parser runs
- THEN none of those ticket numbers appear in the parsed trade list

---

### Requirement: R6 — Title Regex and Malformed Title Handling

The parser MUST apply regex `^#(\d+)\s+(.+?)(?:\[(\w+)\])?$` to each ticket cell's `title` attribute.
A row whose title does not match (and is not the literal `cancelled`) MUST be skipped with a log
entry. Such rows MUST NOT contribute to counts or orphans.

#### Scenario: Valid title with magic number, name, close reason

- GIVEN `title="#2333376 WF_8_34_NQ_SH_LIR_H1_2_33_3[sl]"`
- WHEN regex is applied
- THEN `MagicNumber=2333376`, `StrategyNameHint="WF_8_34_NQ_SH_LIR_H1_2_33_3"`, raw suffix `"sl"`

#### Scenario: Malformed title (no leading `#`)

- GIVEN a `<td title="NO_HASH_VALUE">123456</td>`
- WHEN the parser encounters it
- THEN the row is skipped and a warning is logged; no exception is thrown

---

### Requirement: R7 — CloseReason Mapping

The parser MUST map the bracket suffix (case-insensitive) as: `sl` → `SL`, `tp` → `TP`,
absent → `null` (used for open trades and no-bracket closed trades), anything else → `Other`.

| Input suffix | Mapped value |
|---|---|
| `sl` | `SL` |
| `tp` | `TP` |
| absent | `null` |
| any other | `Other` |

_(Scenarios covered under R2 and R3.)_

---

### Requirement: R8 — Trade Attribution

The service MUST match each parsed trade's `MagicNumber` against `Strategy.MagicNumber` scoped to
`TradingAccountId`. A match assigns `StrategyId`. A non-match produces an orphan. Attribution MUST
use an exact integer match — no fuzzy name matching.

#### Scenario: Magic number matches a strategy

- GIVEN a strategy with `MagicNumber=2333376` on the same `TradingAccountId`
- WHEN trades with `MagicNumber=2333376` are processed
- THEN all those trades are assigned `StrategyId` of that strategy

#### Scenario: Magic number has no matching strategy

- GIVEN no strategy has `MagicNumber=7532499` on the account
- WHEN a trade with `MagicNumber=7532499` is processed
- THEN an orphan entry `{ magicNumber: 7532499, strategyNameHint: "Strategy_7_53_249", tradeCount: N }` is added to the response

#### Scenario: All strategies have null MagicNumber

- GIVEN all strategies on the account have `MagicNumber=null`
- WHEN the file is imported
- THEN all trades become orphans; `imported=0`, `updated=0`, `orphans` contains one entry per distinct magic number

---

### Requirement: R8b — Orphan Resolution By Exact Name

After attribution (R8) has produced its orphans, the service MUST attempt to resolve each orphan
bucket by name before reporting it. For a bucket carrying a non-empty `strategyNameHint`, the
service MUST look for strategies on the same account whose `MagicNumber` is `null` and whose
trimmed `Name` equals the hint case-insensitively. **Exactly one** candidate resolves the bucket:
the service back-fills that strategy's `MagicNumber`, attributes the bucket's trades to it, and
reports it under `autoAssigned`. Zero or more than one candidate leaves the bucket an orphan.

The service MUST NEVER overwrite a `MagicNumber` that is already set.

This does not weaken R8. Attribution is still an exact integer match; this is a separate recovery
step for trades attribution could not place, and the name comparison is exact — case and
surrounding whitespace are normalised, nothing is fuzzy-matched.

#### Scenario: A single unmagicked strategy matching the hint is adopted

- GIVEN an orphan bucket with `magicNumber=7532499` and `strategyNameHint="WF_9_26_XAUUSD_H1_KAMA_BB_4_53"`
- AND exactly one strategy on the account named `"WF_9_26_XAUUSD_H1_KAMA_BB_4_53"` with `MagicNumber=null`
- WHEN the file is imported
- THEN that strategy's `MagicNumber` becomes 7532499, the bucket's trades are attributed to it, and it appears in `autoAssigned` with its `tradeCount`

#### Scenario: An ambiguous hint is left as an orphan

- GIVEN an orphan bucket whose hint matches two strategies with `MagicNumber=null`
- WHEN the file is imported
- THEN neither is modified and the bucket remains in `orphans`

#### Scenario: A strategy that already carries a magic number is never re-pointed

- GIVEN a strategy whose name matches the hint but whose `MagicNumber` is already set to a different value
- WHEN the file is imported
- THEN its `MagicNumber` is left untouched and the bucket remains in `orphans`

---

### Requirement: R9 — Idempotent Upsert

The service MUST upsert each trade on `(StrategyId, Ticket)`: if the pair exists it MUST UPDATE
all fields; if not it MUST INSERT. The service MUST NEVER delete existing trade rows.

#### Scenario: First import inserts new rows

- GIVEN no existing `StrategyTrade` rows for the account
- WHEN the fixture is imported for the first time
- THEN `imported = N` (number of attributed trades), `updated = 0`

#### Scenario: Re-import same file is idempotent

- GIVEN the fixture was already imported once
- WHEN the same file is uploaded again
- THEN `imported = 0`, `updated = N`, and the total row count in `StrategyTrade` is unchanged

---

### Requirement: R10 — Equity Snapshot

The service MUST persist one `AccountEquitySnapshot` per successful call, parsed from the Summary
section: `Balance`, `Equity`, `FloatingPnL`, `Margin`, `FreeMargin`, `ClosedTradePnL`. `ReportTime`
MUST be parsed from the header format `YYYY Month DD, HH:MM` (e.g. `2026 April 21, 07:06`).
Snapshots are immutable history — existing rows are never overwritten.

#### Scenario: Summary section parsed correctly

- GIVEN the fixture Summary with Balance=102730.18, Equity=102918.16, FloatingPnL=187.98, Margin=9054.14, FreeMargin=93864.02, ClosedTradePnL=2897.15
- WHEN the file is imported
- THEN one `AccountEquitySnapshot` row is inserted with those exact values and `ReportTime=2026-04-21T07:06:00`

#### Scenario: Upload with zero trades but Summary present

- GIVEN a file that has no attributed trades but a valid Summary section
- WHEN the file is imported
- THEN `imported=0`, `updated=0`, one snapshot row is created, no error returned

---

### Requirement: R11 — Response DTO Shape

The endpoint MUST return `TradeImportResultDto`:
```
{
  imported: int,
  updated: int,
  skipped: int,
  orphans: [{ magicNumber: int, strategyNameHint: string, tradeCount: int }],
  autoAssigned: [{ strategyId: guid, strategyName: string, magicNumber: int, tradeCount: int }],
  availableStrategies: [{ id: guid, name: string }],
  snapshot: { balance, equity, floatingPnL, margin, freeMargin, closedTradePnL, reportTime }
}
```
Orphans MUST be aggregated by magic number (one entry per distinct unmatched magic number).

`autoAssigned` reports the buckets R8b resolved, so a caller can see which strategies had a
`MagicNumber` written for them rather than discovering it later. `availableStrategies` lists the
account's strategies so the caller can offer a manual assignment for the orphans that remain. Both
collections are present and possibly empty; neither was in the original contract, and both were
added additively.

#### Scenario: Orphan list is aggregated

- GIVEN 5 trades with `MagicNumber=7532499` that have no matching strategy
- WHEN the file is imported
- THEN `orphans` contains exactly one entry with `magicNumber=7532499` and `tradeCount=5`

---

### Requirement: R12 — GET Trades Endpoint

The system MUST expose `GET /api/strategies/{id}/trades?status=open|closed|all` returning a
paginated list ordered by `CloseTime DESC` (for open trades: `OpenTime DESC` as secondary sort).

| `status` value | Filter |
|---|---|
| `open` | `CloseTime IS NULL` |
| `closed` | `CloseTime IS NOT NULL` |
| `all` (default) | no filter |

#### Scenario: Status=open returns only open trades

- GIVEN a strategy has both open and closed trades
- WHEN `GET /api/strategies/{id}/trades?status=open` is called
- THEN only trades with `CloseTime=null` are returned

#### Scenario: Status=closed returns only closed trades

- GIVEN the same strategy
- WHEN `GET /api/strategies/{id}/trades?status=closed` is called
- THEN only trades with `CloseTime IS NOT NULL` are returned

---

### Requirement: R13 — Frontend Import Trades Modal

The system MUST provide an `ImportTradesModal` Angular component (standalone, OnPush, Signals) with:
a file picker (MUST accept only `.htm`/`.html`), a submit button, and a result panel showing
`imported`, `updated`, `skipped` counts plus an orphan panel with one row per orphan entry and a
"Copy magic number" action.

#### Scenario: Orphan panel with copy action

- GIVEN a successful import that returns 3 orphan entries
- WHEN the result panel renders
- THEN the orphan panel shows 3 rows, each with the `magicNumber`, `strategyNameHint`, `tradeCount`, and a "Copy" button that copies `magicNumber` to clipboard

#### Scenario: No orphans — orphan panel hidden

- GIVEN a successful import that returns `orphans: []`
- WHEN the result panel renders
- THEN the orphan panel is not visible

---

### Requirement: R14 — Frontend Trade Grid

The system MUST provide a `StrategyTradesGrid` Angular component (standalone, OnPush, ag-grid-community)
opened per strategy from the AccountDetail view. Columns: Ticket, OpenTime, Type, Volume, Symbol,
OpenPrice, ClosePrice, S/L, T/P, Commission, Swap, Profit, CloseReason. Open trades MUST be visually
distinguished (e.g. row CSS class or badge).

#### Scenario: Grid shows all trade columns

- GIVEN a strategy with 2 closed trades and 1 open trade
- WHEN the StrategyTradesGrid is opened
- THEN all 14 columns are visible and the open trade row has a distinct visual style

---

## Edge Cases

### Scenario: Cascade delete on Strategy removes its trades

- GIVEN a strategy with associated `StrategyTrade` rows
- WHEN the strategy is deleted
- THEN all its `StrategyTrade` rows are cascade-deleted

### Scenario: Upload to deleted or non-existent TradingAccountId

- GIVEN a `TradingAccountId` that does not exist
- WHEN the import endpoint is called
- THEN HTTP 404 is returned and no data is persisted
