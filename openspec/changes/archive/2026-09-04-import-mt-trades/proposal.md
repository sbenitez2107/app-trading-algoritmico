# Proposal: Import Darwinex MT4 Trade Statement

## Intent

We need to close the loop between SQX backtests and real broker execution. Today each `Strategy` is linked to a `TradingAccount` but carries no executed trades — so there's no way to see what an EA actually did on Darwinex, no real KPIs, and no way to compare backtest against live performance. This change imports the broker-side MT4 HTML "Detailed Statement" from Darwinex, parses closed + open trades, attributes them to the right EA via magic number, and stores an account equity snapshot at the time of import. Sprint 1 scope is the trade list only; real KPIs and equity-over-time charts are deferred.

## Scope

### In Scope

- Domain: new `StrategyTrade` entity, new `AccountEquitySnapshot` entity, add `MagicNumber` (int?) to `Strategy`
- EF configurations + migration (additive: adds two tables, one column, one unique index `(StrategyId, Ticket)`)
- Infrastructure: `MtStatementParserService` (Darwinex MT4 HTML, AngleSharp, mirrors `HtmlReportParserService` style)
- Application: `ITradeImportService` with upsert-by-`(StrategyId, Ticket)`, orphan aggregation (no persistence)
- REST endpoint: `POST /api/trading-accounts/{id}/trades/import` (multipart `IFormFile`, returns `{ imported, updated, skipped, orphans: [{ magicNumber, strategyNameHint, tradeCount }], snapshot }`)
- REST endpoint: `GET /api/strategies/{id}/trades` (paged list, newest first)
- Frontend: `ImportTradesModal` (file drop + result summary with orphan panel), `StrategyTradesGrid` (ag-grid sub-grid or modal per strategy)
- Frontend: add `magicNumber` input to `AddStrategyModal` and strategy detail/edit views
- i18n: EN + ES strings for modal, grid headers, toasts, orphan panel
- Tests: xUnit parser tests against `strategies/Report Trades DW DEMO2.htm` fixture; service upsert tests; Vitest tests for the import modal reducer logic

### Out of Scope

- MT5 statement format (wait for real sample)
- Multi-broker abstraction (YAGNI until a second broker appears)
- Equity chart over time (Sprint 2 — snapshots are persisted, chart isn't)
- Per-EA equity curve storage (computed on demand from cumulative trade P/L)
- Auto-linking orphans by name similarity (false-positive risk; user links manually)
- Working Orders section (pending, unfilled)
- Cancelled rows inside Closed Transactions (pending orders that expired)
- Time-zone normalization (store broker local time as given)
- `.mq4` parsing to auto-detect magic numbers (user enters manually)

## Capabilities

### New Capabilities

- `mt-trade-import`: parse Darwinex MT4 HTML statements, attribute trades to strategies by `(TradingAccountId, MagicNumber)`, persist `StrategyTrade` rows + `AccountEquitySnapshot`, return an orphan list for unmatched magic numbers.

### Modified Capabilities

- `strategy-model`: add nullable `MagicNumber` (int?) to `Strategy`; uniqueness scoped to `TradingAccountId` (filtered unique index on `(TradingAccountId, MagicNumber)` where both non-null).

## Approach

Mirror the existing `HtmlReportParserService` pattern exactly: `MtStatementParserService` uses AngleSharp, locates the four sections via their `<b>` markers (`Closed Transactions:`, `Open Trades:`, `Working Orders:`, `Summary:`), iterates rows, and extracts the magic number + strategy-name hint + close reason from the first `<td>`'s `title` attribute with a single regex. Cancelled and Working-Orders rows are skipped at parse time. The parser returns a pure DTO (`ParsedMtStatementDto` with `Trades`, `Summary`) — no EF dependency.

`TradeImportService` takes the DTO, groups trades by magic number, loads matching strategies for the account in a single query, and upserts each trade via `(StrategyId, Ticket)` — EF `AsNoTracking` + batch insert/update. Unmatched magic numbers become orphans returned in the response, NOT persisted. The equity snapshot is appended (immutable history). Re-importing the same file is idempotent by design.

Frontend uses the existing upload infra (multipart, 200MB limit is already in place). The import modal shows a three-state result: imported count, updated count, orphan list with "Copy magic number" action so the user can quickly paste it into the right strategy's edit form. The trades grid is an ag-grid sub-grid opened per strategy from the strategy row.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Entities/Strategy.cs` | Modified | Add `int? MagicNumber` + `ICollection<StrategyTrade> Trades` |
| `Domain/Entities/StrategyTrade.cs` | New | Ticket, OpenTime/CloseTime, Type, Size, Item, OpenPrice/ClosePrice, SL, TP, Commission, Taxes, Swap, Profit, CloseReason, IsOpen |
| `Domain/Entities/AccountEquitySnapshot.cs` | New | TradingAccountId, ReportedAt, Balance, Equity, FloatingPnL, Margin, FreeMargin, ClosedTradePnL, Currency |
| `Infrastructure/Persistence/Configurations/StrategyConfiguration.cs` | Modified | Add `MagicNumber` + filtered unique index `(TradingAccountId, MagicNumber)` |
| `Infrastructure/Persistence/Configurations/StrategyTradeConfiguration.cs` | New | Unique index `(StrategyId, Ticket)`, decimals, FK cascade delete on Strategy |
| `Infrastructure/Persistence/Configurations/AccountEquitySnapshotConfiguration.cs` | New | FK to TradingAccount, decimals |
| `Infrastructure/Persistence/Migrations/` | New | Additive migration (generated only, not applied) |
| `Application/Interfaces/IMtStatementParserService.cs` | New | Parser contract |
| `Infrastructure/Services/MtStatementParserService.cs` | New | AngleSharp parser |
| `Application/Interfaces/ITradeImportService.cs` | New | Orchestration contract |
| `Infrastructure/Services/TradeImportService.cs` | New | Upsert + orphan aggregation |
| `Application/DTOs/Trades/` | New | `ParsedMtStatementDto`, `TradeImportResultDto`, `OrphanMagicNumberDto`, `StrategyTradeDto` |
| `WebAPI/Controllers/TradingAccountsController.cs` | Modified | Add `POST {id}/trades/import` |
| `WebAPI/Controllers/StrategiesController.cs` | Modified | Add `GET {id}/trades` |
| `Infrastructure/DependencyInjection.cs` | Modified | Register both new services |
| `web/src/app/features/darwinex/add-strategy-modal/` | Modified | Add MagicNumber field |
| `web/src/app/features/darwinex/account-detail/` | Modified | Add "Import trades" button + modal |
| `web/src/app/features/darwinex/import-trades-modal/` | New | File upload + result view + orphan panel |
| `web/src/app/features/darwinex/strategy-trades-grid/` | New | ag-grid list of StrategyTrade per strategy |
| `web/src/assets/i18n/{en,es}.json` | Modified | Translations for modal/grid/orphans |
| `tests/AppTradingAlgoritmico.UnitTests/` | New | `MtStatementParserServiceTests`, `TradeImportServiceTests` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Duplicate ticket on re-import | High | Unique index `(StrategyId, Ticket)` + upsert semantics |
| Orphan magic numbers pollute response | Med | Group orphans by magic number (aggregate `tradeCount`) — not one entry per trade |
| Darwinex re-brands HTML header | Low | Match on stable section `<b>` markers, not header text |
| Broker local time ambiguity | Med | Store as given (no UTC conversion in v1); add `Timezone` field on snapshot for future |
| Magic number collision across accounts | Low | Filtered unique index is scoped to `TradingAccountId` — same magic OK across accounts |
| Large statement files (>10k trades) | Low | Streaming parse + batch insert; parser reads once, service inserts in chunks of 500 |
| Cancelled rows break 14-column assumption | Med | Detect `colspan=4` on last cell → skip row before column-count validation |
| User forgets to set MagicNumber → everything is orphaned | High | Orphan panel is the first-class UX — clear message + link to strategy edit |

## Rollback Plan

The migration is purely additive (two new tables, one new nullable column, one new unique index). Rollback steps:

1. Revert the frontend + backend commits
2. Run the migration's `Down` method — drops `StrategyTrade` and `AccountEquitySnapshot` tables, drops `MagicNumber` column and its unique index
3. No data loss on existing `Strategy` / `TradingAccount` rows — the change never modifies existing data
4. If only the frontend is rolled back, the API continues to work but is unused (safe state)

## Dependencies

- Existing `HtmlReportParserService` pattern (AngleSharp) — mirror its style, no new NuGet needed
- Existing multipart upload infra in `Program.cs` (200MB `RequestSizeLimit` already configured)
- Existing `Strategy` and `TradingAccount` entities and their relationships
- Existing i18n infrastructure (`ngx-translate` v17)
- Sample fixture: `strategies/Report Trades DW DEMO2.htm` (already in repo)

## Success Criteria

- [ ] Uploading `strategies/Report Trades DW DEMO2.htm` to an account with zero matching strategies returns `imported: 0` and one orphan entry per magic number, each with a non-empty `strategyNameHint` and `tradeCount > 0`
- [ ] After setting `MagicNumber` on at least one `Strategy` and re-uploading, those trades land in `StrategyTrade` with correct `Ticket`, times, prices, and `CloseReason`
- [ ] Re-importing the same file twice leaves the trade count unchanged (idempotency verified)
- [ ] Cancelled rows and Working Orders section are NOT persisted as trades
- [ ] One `AccountEquitySnapshot` row is written per successful import with `Balance`, `Equity`, `FloatingPnL`, `Margin`, `FreeMargin`, `ClosedTradePnL` parsed from the Summary section
- [ ] `GET /api/strategies/{id}/trades` returns trades ordered by `CloseTime DESC` (open trades first by convention)
- [ ] Adding a strategy via `AddStrategyModal` accepts a nullable numeric `MagicNumber` and persists it
- [ ] Parser unit tests cover: normal closed trade, open trade (no close time), cancelled row skipped, orphan row, malformed title attribute
- [ ] `dotnet format` and `pnpm format` pass clean
