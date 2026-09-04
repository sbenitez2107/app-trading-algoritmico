# Tasks: Import Darwinex MT4 Trade Statement

> Strict TDD mode: for every testable unit, the RED task (failing test) precedes the GREEN task (implementation).
> Migration is generated but NOT applied without explicit user authorization.

---

## Phase 1: Domain + Application Contracts (~2h)

> Foundation: entities, DTOs, interfaces, EF configs — everything downstream depends on these.

### 1.1 Strategy entity — add MagicNumber + Trades navigation
- [x] 1.1 Modify `Domain/Entities/Strategy.cs` — add `int? MagicNumber` property and `ICollection<StrategyTrade> Trades { get; init; } = []`
  - Must compile without breaking existing code (nullable, no required attribute)

### 1.2 Domain entities — new
- [x] 1.2 Create `Domain/Entities/StrategyTrade.cs` — fields: `Guid Id`, `Guid StrategyId`, `long Ticket`, `DateTime OpenTime`, `DateTime? CloseTime`, `string Type`, `decimal Size`, `string Item`, `decimal OpenPrice`, `decimal? ClosePrice`, `decimal StopLoss`, `decimal TakeProfit`, `decimal Commission`, `decimal Taxes`, `decimal Swap`, `decimal Profit`, `string? CloseReason`, `bool IsOpen`; navigation `Strategy Strategy`
- [x] 1.3 Create `Domain/Entities/AccountEquitySnapshot.cs` — fields: `Guid Id`, `Guid TradingAccountId`, `DateTime ReportTime`, `decimal Balance`, `decimal Equity`, `decimal FloatingPnL`, `decimal Margin`, `decimal FreeMargin`, `decimal ClosedTradePnL`, `string Currency`; navigation `TradingAccount TradingAccount`

### 1.3 Application DTOs — new directory `Application/DTOs/Trades/`
- [x] 1.4 Create `Application/DTOs/Trades/ParsedMtTradeDto.cs` — sealed record with all parsed fields per design contract (Ticket, MagicNumber, StrategyNameHint, CloseReason, IsOpen, all price/time/money fields)
- [x] 1.5 Create `Application/DTOs/Trades/ParsedSummaryDto.cs` — sealed record: `DateTime ReportTime`, `decimal Balance`, `decimal Equity`, `decimal FloatingPnL`, `decimal Margin`, `decimal FreeMargin`, `decimal ClosedTradePnL`, `string Currency`
- [x] 1.6 Create `Application/DTOs/Trades/ParsedMtStatementDto.cs` — sealed record: `IReadOnlyList<ParsedMtTradeDto> Trades`, `ParsedSummaryDto Summary`
- [x] 1.7 Create `Application/DTOs/Trades/OrphanMagicNumberDto.cs` — sealed record: `int MagicNumber`, `string StrategyNameHint`, `int TradeCount`
- [x] 1.8 Create `Application/DTOs/Trades/SnapshotDto.cs` — sealed record mirroring `ParsedSummaryDto` fields (returned in response)
- [x] 1.9 Create `Application/DTOs/Trades/TradeImportResultDto.cs` — sealed record: `int Imported`, `int Updated`, `int Skipped`, `IReadOnlyList<OrphanMagicNumberDto> Orphans`, `SnapshotDto Snapshot`
- [x] 1.10 Create `Application/DTOs/Trades/StrategyTradeDto.cs` — sealed record with all display fields (Ticket, OpenTime, CloseTime, Type, Size, Item, OpenPrice, ClosePrice, StopLoss, TakeProfit, Commission, Taxes, Swap, Profit, CloseReason, IsOpen)

### 1.4 Application DTOs — modify existing
- [x] 1.11 Modify `Application/DTOs/Strategies/StrategyDto.cs` — add `int? MagicNumber` property (R-M3)

### 1.5 Application interfaces — new
- [x] 1.12 Create `Application/Interfaces/IMtStatementParserService.cs` — `Task<ParsedMtStatementDto?> ParseAsync(Stream htmlStream, CancellationToken ct = default)`
- [x] 1.13 Create `Application/Interfaces/ITradeImportService.cs` — `Task<TradeImportResultDto> ImportAsync(Guid accountId, Stream html, CancellationToken ct)` + `Task<PagedResult<StrategyTradeDto>> GetByStrategyAsync(Guid strategyId, TradeStatusFilter status, int page, int pageSize, CancellationToken ct)`
  - Define `TradeStatusFilter` enum (All, Open, Closed) in same file or adjacent enum file

### 1.6 EF configurations
- [x] 1.14 Modify `Infrastructure/Persistence/Configurations/StrategyConfiguration.cs` — add `MagicNumber` column; add filtered unique index `(TradingAccountId, MagicNumber) WHERE MagicNumber IS NOT NULL`; add `HasMany(s => s.Trades).WithOne(t => t.Strategy).HasForeignKey(t => t.StrategyId).OnDelete(DeleteBehavior.Cascade)`
- [x] 1.15 Create `Infrastructure/Persistence/Configurations/StrategyTradeConfiguration.cs` — PK `Id`; unique index `(StrategyId, Ticket)`; decimal precision: prices `(18,5)`, money fields `(18,2)`; string lengths: `Type` max 20, `Item` max 30, `CloseReason` max 20; FK `StrategyId` references `Strategy`
- [x] 1.16 Create `Infrastructure/Persistence/Configurations/AccountEquitySnapshotConfiguration.cs` — PK `Id`; FK `TradingAccountId` → `TradingAccount`; all decimal fields `(18,2)`; `ReportTime` required; no `UpdatedAt`

### 1.7 EF DbContext update
- [x] 1.17 Modify `Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<StrategyTrade> StrategyTrades` and `DbSet<AccountEquitySnapshot> AccountEquitySnapshots`

---

## Phase 2: Migration (~30min, user-gated)

> Additive only — two new tables + one nullable column + one filtered unique index.

- [x] 2.1 Generate EF migration — run `dotnet ef migrations add AddStrategyTradeAndEquitySnapshot` from `Infrastructure` project (user authorizes; agent proposes command only)
- [x] 2.2 Review generated migration file — verify `Up()` adds `StrategyTrades` table, `AccountEquitySnapshots` table, `MagicNumber` nullable column on `Strategy`, filtered unique index; verify `Down()` reverses all cleanly
- [x] 2.3 Apply migration — `dotnet ef database update` executed 2026-04-21 against localhost.AppTA (user authorized). Migration `20260422132625_AddStrategyTradeAndEquitySnapshot` applied successfully.
- [x] 2.4 **HYGIENE REFACTOR (2026-04-21)**: Unified migration folders. Moved 5 legacy files from `Infrastructure/Migrations/` → `Infrastructure/Persistence/Migrations/` with namespace updated. Zero external references broken. EF verified all 13 migrations resolve correctly. Legacy folder deleted.

---

## Phase 3: Parser — TDD (~3h)

> Fixture: `strategies/Report Trades DW DEMO2.htm`. Tests come first (RED → GREEN).

### RED — parser tests
- [x] 3.1 Copy fixture to `tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/Fixtures/Report Trades DW DEMO2.htm` (embedded resource or file copy as part of test project setup)
- [x] 3.2 Create `tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/MtStatementParserServiceTests.cs` with failing tests covering:
  - R2/R6: closed trade row → correct Ticket, MagicNumber, StrategyNameHint, CloseReason=SL, all price/time fields
  - R2/R7: title suffix `[tp]` → CloseReason=TP
  - R2/R7: title without bracket → CloseReason=null
  - R2/R7: title with unknown suffix `[other]` → CloseReason=Other
  - R4: cancelled row (ticket 263492812) → absent from parsed result
  - R5: Working Orders tickets (263455666–263535623) → absent from parsed result
  - R3: open trade (ticket 263502096) → `CloseTime=null`, `IsOpen=true`, `ClosePrice=null`
  - R6: malformed title (no `#`) → row skipped, no exception
  - R1: empty stream → `ParseAsync` returns null
  - R10: Summary parsed → `Balance=102730.18`, `Equity=102918.16`, `FloatingPnL=187.98`, `ReportTime=2026-04-21T07:06:00`

### GREEN — parser implementation
- [x] 3.3 Create `Infrastructure/Services/MtStatementParserService.cs` — implements `IMtStatementParserService`; AngleSharp; locates `<b>Closed Transactions:</b>`, `<b>Open Trades:</b>`, `<b>Working Orders:</b>`, `<b>Summary:</b>` section markers; iterates `<tr>` rows per section; applies regex `^#(\d+)\s+(.+?)(?:\[(\w+)\])?$` to first `<td>` title; detects `colspan=4` cancelled rows and skips; parses Summary section for equity fields; maps CloseReason suffix case-insensitive; returns null if no markers found

### Verify Phase 3
- [x] 3.4 Verify all `MtStatementParserServiceTests` pass (run `dotnet test --filter MtStatementParserServiceTests`)

---

## Phase 4: Service — TDD (~3h)

> Uses `StrategyInMemoryDbContext` pattern (already exists). Moq for parser. Tests first.

### RED — service tests
- [x] 4.1 Create `tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/TradeImportServiceTests.cs` with failing tests covering:
  - R9 / first import: no existing rows → `Imported=N`, `Updated=0`, N rows in `StrategyTrades`
  - R9 / re-import: same file → `Imported=0`, `Updated=N`, row count unchanged
  - R8 / orphan: magic number with no matching strategy → orphan entry with correct `MagicNumber`, `StrategyNameHint`, `TradeCount`
  - R8 / all orphan: all strategies have null MagicNumber → `Imported=0`, all trades are orphans
  - R10 / snapshot always written: one `AccountEquitySnapshot` row per call regardless of trade count
  - R1 / 404: non-existent TradingAccountId → `KeyNotFoundException` thrown
  - R1 / 400: parser returns null → appropriate exception (e.g. `ArgumentException`) thrown
  - R12 / GetByStrategy Open filter: only `IsOpen=true` rows returned
  - R12 / GetByStrategy Closed filter: only `CloseTime != null` rows returned
  - R12 / GetByStrategy ordering: closed trades by `CloseTime DESC`, open trades by `OpenTime DESC`

### GREEN — service implementation
- [x] 4.2 Create `Infrastructure/Services/TradeImportService.cs` — implements `ITradeImportService`; loads strategies in single query `Where(s => s.TradingAccountId == id && s.MagicNumber != null).ToDictionaryAsync(s => s.MagicNumber!.Value)`; upserts `StrategyTrade` on `(StrategyId, Ticket)` — check existing by ticket list, then insert or update; sets `IsOpen` on upsert; aggregates orphans by MagicNumber (group → one entry per distinct magic); appends `AccountEquitySnapshot`; wraps in single `SaveChangesAsync` transaction; throws `KeyNotFoundException` on missing account; throws on null parse result
- [x] 4.3 Implement `GetByStrategyAsync` in `TradeImportService.cs` — EF query with `TradeStatusFilter` switch; order by `CloseTime DESC NULLS LAST, OpenTime DESC`; paged result

### Verify Phase 4
- [x] 4.4 Verify all `TradeImportServiceTests` pass (run `dotnet test --filter TradeImportServiceTests`)

---

## Phase 5: WebAPI Endpoints + DI (~1h)

### RED — controller tests
- [x] 5.1 Controller tests split into two files matching `<Controller><Area>Tests.cs` project convention:
  - `tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/TradingAccountsControllerImportTests.cs` — 3 POST tests (200 / 404 / 400)
  - `tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/StrategiesControllerTradesTests.cs` — 1 GET test (200 paged)

### GREEN — controller + DI
- [x] 5.2 Modify `WebAPI/Controllers/TradingAccountsController.cs` — added `POST api/trading-accounts/{id}/trades/import` accepting `IFormFile`; injected `ITradeImportService` as third ctor param; catch `KeyNotFoundException` → 404; catch `ArgumentException` → 400; return 200 with `TradeImportResultDto`. (Refactor 2026-04-22: first draft created a separate `TradeImportController` class — moved into `TradingAccountsController` to match the task spec and keep Swagger grouping under a single tag.)
- [x] 5.3 Modify `WebAPI/Controllers/StrategiesController.cs` — added `GET api/strategies/{id}/trades` with `[FromQuery] string status = "all"`, `[FromQuery] int page = 1`, `[FromQuery] int pageSize = 50`; parse status case-insensitive to `TradeStatusFilter`; call `ITradeImportService.GetByStrategyAsync`; return 200 with paged result. Constructor extended to accept `ITradeImportService` as second param.
- [x] 5.4 Modify `Infrastructure/DependencyInjection.cs` — registered `IMtStatementParserService → MtStatementParserService` (Scoped) and `ITradeImportService → TradeImportService` (Scoped)

### Verify Phase 5
- [x] 5.5 Verified: 4/4 new Phase 5 tests pass. Full suite: 115/115 (111 + 4 new). Zero regressions.

---

## Phase 6: Frontend (~4h)

> Standalone Components + Signals + OnPush. Vitest 4. Tests first per unit.

### 6.1 Frontend models + service
- [x] 6.1 ~~Create `src/app/core/models/trade-import.model.ts`~~ — **DEVIATION**: Project convention uses inline interfaces in service files, not `*.model.ts` files. Added `TradeImportResultDto`, `OrphanMagicNumberDto`, `SnapshotDto` inline in `trading-account.service.ts`; added `StrategyTradeDto` inline in `strategy.service.ts`; added `magicNumber: number | null` to `StrategyDto`.
- [x] 6.2 Add `importTrades(accountId: string, file: File): Observable<TradeImportResultDto>` to `src/app/core/services/trading-account.service.ts` — POST multipart/form-data
- [x] 6.3 Add `getTradesByStrategy(strategyId: string, status?: 'open'|'closed'|'all', page?: number, pageSize?: number): Observable<PagedResult<StrategyTradeDto>>` to `src/app/core/services/strategy.service.ts`

### 6.2 AddStrategyModal — MagicNumber field (R-M2)
- [x] 6.4 Write failing Vitest spec in `src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.spec.ts` — test: valid integer submitted as `magicNumber: 2333376`; test: empty field submitted as `magicNumber: null`; test: non-numeric input shows validation error
- [x] 6.5 Modify `src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.ts` — add `magicNumber` signal, `magicNumberRaw` signal, `magicNumberError` computed; `onMagicNumberChange()` handler; extended `addToAccount()` call with `magicNumber` arg
- [x] 6.6 Modify `src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.html` — added MagicNumber number input with validation message
- [x] 6.7 Modify `src/app/features/darwinex/add-strategy-modal/add-strategy-modal.component.scss` — added `&__error` style for validation messages

### 6.3 ImportTradesModal (R13)
- [x] 6.8 Create `src/app/features/darwinex/import-trades-modal/import-trades-modal.component.spec.ts` — 9 tests: .html accepted, .htm accepted, .txt rejected, submit calls service, result signal set, orphan panel hidden when empty, orphan panel shows 3 rows, copy calls clipboard API
- [x] 6.9 Create `src/app/features/darwinex/import-trades-modal/import-trades-modal.component.ts` — standalone, OnPush, Signals: `isLoading`, `result`, `error`, `selectedFile`, `fileError`; `@Input({required:true}) accountId`; `@Output() closed`; `onFileChange()` validates extension; `submit()` → service; `copyMagicNumber()` → Clipboard API
- [x] 6.10 Create `src/app/features/darwinex/import-trades-modal/import-trades-modal.component.html`
- [x] 6.11 Create `src/app/features/darwinex/import-trades-modal/import-trades-modal.component.scss`

### 6.4 StrategyTradesGrid (R14)
- [x] 6.12 Create `src/app/features/darwinex/strategy-trades-grid/strategy-trades-grid.component.spec.ts` — 4 tests: 14 column headers, trade--open class rule, status='closed' service call, loading state
- [x] 6.13 Create `src/app/features/darwinex/strategy-trades-grid/strategy-trades-grid.component.ts` — ag-grid-community (already in project); 14 ColDefs; `rowClassRules`; `setStatus()` method
- [x] 6.14 Create `src/app/features/darwinex/strategy-trades-grid/strategy-trades-grid.component.html`
- [x] 6.15 Create `src/app/features/darwinex/strategy-trades-grid/strategy-trades-grid.component.scss`

### 6.5 AccountDetail wiring
- [x] 6.16 Modify `src/app/features/darwinex/account-detail/account-detail.component.ts` — added `showImportModal`, `showTradesGrid`, `activeStrategyId` signals; imported `ImportTradesModalComponent` + `StrategyTradesGridComponent`; added `openImportModal()`, `closeImportModal()`, `toggleTradesGrid()` methods
- [x] 6.17 Modify `src/app/features/darwinex/account-detail/account-detail.component.html` — added "Import Trades" button; wired `@if showImportModal()`; added trades grid toggle in actions; added trades panel `@if showTradesGrid()`

### 6.6 Verify Phase 6
- [x] 6.18 All 106 Vitest tests pass (14 test files). New Phase 6 tests: 27 (8 ImportTradesModal + 4 StrategyTradesGrid + 15 AddStrategyModal including 3 new magicNumber tests)

---

## Phase 7: Integration Smoke (~1h)

> End-to-end with the real fixture file. No DB required — uses in-memory EF + real fixture stream.

- [x] 7.1 Created `tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/MtTradeImportIntegrationTests.cs` — two smoke tests using `InMemoryDbContextFactory` + real `MtStatementParserService` (not mocked) + real `TradeImportService`: (1) no matching strategies → orphans + snapshot + no persisted trades, (2) matching magic 2333376 → imports>0 + no cancelled ticket 263492812 + no Working Orders range.
- [x] 7.2 Verified: 2/2 integration smoke tests pass. Full suite: 120/120 (was 115 + 3 new backend-gap + 2 new smoke). Zero regressions.

### Backend gap closed (discovered 2026-04-23, fixed in same session as Phase 7)
- [x] Persist `magicNumber` on `POST /api/trading-accounts/{id}/strategies`:
  - `IStrategyService.AddToAccountAsync(..., int? magicNumber = null, CancellationToken ct = default)`
  - `StrategyService.AddToAccountAsync` — sets `entity.MagicNumber = magicNumber`
  - `TradingAccountStrategiesController.CreateStrategy` — accepts `[FromForm] int? magicNumber = null` and forwards to service
  - Tests: 2 new service tests (`AddToAccountAsync_WithMagicNumber_PersistsMagicNumber`, `AddToAccountAsync_WithoutMagicNumber_PersistsNullMagicNumber`) + 1 new controller test (`PostStrategy_WithMagicNumber_PassesMagicNumberToService`)

---

## Phase 8: i18n + Docs (~30min)

- [x] 8.1 Modified `public/assets/i18n/en.json` — added `DARWINEX.IMPORT_TRADES.*` (TITLE, FILE_LABEL, SUBMIT, IMPORTING, RESULT_TITLE, IMPORTED, UPDATED, SKIPPED, ORPHANS_TITLE, ORPHANS_DESC, TRADES_COUNT, COPY_MAGIC, COPY) + `DARWINEX.ADD_STRATEGY.*` (MAGIC_NUMBER_LABEL, MAGIC_NUMBER_PLACEHOLDER, MAGIC_NUMBER_ERROR)
- [x] 8.2 Modified `public/assets/i18n/es.json` — same keys in Rioplatense Spanish
- [x] 8.3 Added `DARWINEX.TRADES_GRID.COL_*` keys (EN + ES) for 14 grid column headers (Ticket, Type, Volume, Symbol, OpenTime, CloseTime, OpenPrice, ClosePrice, SL, TP, Commission, Swap, Profit, Status)

### i18n wiring decision (2026-04-23)
Keys EXIST in both `en.json` and `es.json` but the darwinex feature components use **hardcoded English strings** (no `translate` pipe). This matches the existing darwinex pattern (`add-strategy-modal`, `strategy-comments-modal`) and is consistent within the feature. Breaks from the skill-registry i18n rule but keeps internal consistency. If the decision is ever reversed, the keys are ready and only the templates need `| translate` additions.
- [x] 8.4 RESOLVED 2026-04-21: `Item` symbol stored **as-is** (lowercase broker format, no normalization). Parser must pass the value straight through to `StrategyTrade.Item`.
- [x] 8.5 RESOLVED 2026-04-21: `IsOpen` is **persisted** (plain `bit` column, NOT computed). `TradeImportService` must set it on every upsert (`true` if `CloseTime == null`). EF config must NOT use `HasComputedColumnSql`.
- [x] 8.6 RESOLVED 2026-04-21: `snapshotCurrency` — parse from HTML summary header; fallback to `TradingAccount.Currency` if parse fails. Never null.

---

## Dependency Summary

```
Phase 1 → Phase 2 (migration depends on EF config) → Phase 3 (parser compiles once interfaces exist)
Phase 3 → Phase 4 (service depends on parser interface + domain entities)
Phase 1,3,4 → Phase 5 (controller depends on service + DTOs)
Phase 1 → Phase 6 (frontend models mirror backend DTOs)
Phase 3,4,5 → Phase 7 (smoke test needs real implementations)
Phase 6,8 → Phase 7 can run in parallel with Phase 6
```

**Total tasks**: 57 checkboxes across 8 phases.
