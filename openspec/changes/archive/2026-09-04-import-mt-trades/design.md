# Design: Import Darwinex MT4 Trade Statement

## Technical Approach

Mirror the existing `HtmlReportParserService` (AngleSharp + pure DTO output) to add `MtStatementParserService`. Drive the import from a new `TradeImportService` that upserts `StrategyTrade` on `(StrategyId, Ticket)`, aggregates orphans, and writes one `AccountEquitySnapshot` per call. New REST endpoint on `TradingAccountsController` for upload, new endpoint on `StrategiesController` for listing. Fully additive migration: two tables + one nullable column + one filtered unique index on `Strategy(TradingAccountId, MagicNumber)`.

Upload limits are already 200 MB in `Program.cs` (both Kestrel and `FormOptions`). Nothing to change.

## Architecture Decisions

| Topic | Options | Decision | Rationale |
|---|---|---|---|
| Entity name | `MtTrade` / `ExecutedTrade` / `StrategyTrade` | **`StrategyTrade`** | Match proposal; domain-aligned (a trade belongs to a strategy); broker-neutral. |
| Parser → service boundary | Parser returns entities vs. pure DTO | **Pure DTO (`ParsedMtStatementDto`)** | Testable without EF; follows `HtmlReportParserService` pattern. |
| Match key | Global `MagicNumber` / `(Account, MagicNumber)` | **`(TradingAccountId, MagicNumber)`** | Same EA magic can exist on different Darwinex accounts (demo vs. real). |
| Unknown magic number policy | Fail import / skip row / orphan-in-response | **Orphan-in-response, not persisted** | Single-user UX: surface hint + tradeCount so user sets `MagicNumber` and re-imports. |
| Idempotency | Delete+insert / INSERT-only skip / **upsert** | **Upsert on `(StrategyId, Ticket)`** | Open trades flip to closed between imports — must UPDATE, not INSERT twice. |
| Transaction | Per-row / per-file | **Per-file** | File is a single logical broker snapshot; all-or-nothing is simplest and safe at hundreds-to-thousands-rows scale. |
| Bulk strategy | `AddRange`+`SaveChanges` / `EFCore.BulkExtensions` | **`AddRange` + `SaveChanges`** | Fixture is ~50 rows; staying <10k rules out bulk libs (YAGNI). Chunk at 500 for headroom. |
| Error surface | `Result<T>` / exceptions | **Exceptions for system, HTTP codes for domain** | Match existing controller pattern (`KeyNotFoundException` → 404, null parse → 400). No `Result<T>` used elsewhere. |
| Query pre-fetch | N+1 per trade / single load | **Single query**: `_db.Strategies.Where(s => s.TradingAccountId == id && s.MagicNumber != null).ToDictionaryAsync(s => s.MagicNumber!.Value)` | O(1) lookup inside parse loop. |

## Data Flow

```
IFormFile ──▶ TradingAccountsController
              │
              ▼
         TradeImportService.ImportAsync(accountId, stream)
              │
              ├── MtStatementParserService.ParseAsync(stream) ──▶ ParsedMtStatementDto
              │                                                   { Trades[], Summary }
              ├── Load strategies where MagicNumber != null
              ├── For each trade:
              │     match → upsert StrategyTrade
              │     no match → orphan bucket
              ├── Persist AccountEquitySnapshot from Summary
              └── SaveChangesAsync (single transaction)
              │
              ▼
        TradeImportResultDto { imported, updated, skipped, orphans[], snapshot }
```

## File Changes

| File | Action | Description |
|---|---|---|
| `Domain/Entities/StrategyTrade.cs` | New | Fields per spec R2/R3; `IsOpen` computed or persisted boolean. |
| `Domain/Entities/AccountEquitySnapshot.cs` | New | Fields per spec R10; immutable (no UpdatedAt use). |
| `Domain/Entities/Strategy.cs` | Modify | Add `int? MagicNumber`; add `ICollection<StrategyTrade> Trades = []`. |
| `Infrastructure/Persistence/AppDbContext.cs` | Modify | Add `DbSet<StrategyTrade>` and `DbSet<AccountEquitySnapshot>`. |
| `Infrastructure/Persistence/Configurations/StrategyConfiguration.cs` | Modify | Filtered unique index `(TradingAccountId, MagicNumber)` WHERE both NOT NULL; `HasMany(Trades).WithOne().OnDelete(Cascade)`. |
| `Infrastructure/Persistence/Configurations/StrategyTradeConfiguration.cs` | New | Unique index `(StrategyId, Ticket)`; decimals (prices 18,5 / money 18,2); string lengths (Type 20, Item 30, CloseReason 20). |
| `Infrastructure/Persistence/Configurations/AccountEquitySnapshotConfiguration.cs` | New | FK → TradingAccount, decimals (18,2), `ReportTime` required. |
| `Infrastructure/Persistence/Migrations/*` | New | Additive; generated, not applied without authorization. |
| `Application/Interfaces/IMtStatementParserService.cs` | New | `Task<ParsedMtStatementDto?> ParseAsync(Stream, CancellationToken)`. |
| `Application/Interfaces/ITradeImportService.cs` | New | `Task<TradeImportResultDto> ImportAsync(Guid accountId, Stream, CancellationToken)` + `Task<PagedResult<StrategyTradeDto>> GetByStrategyAsync(...)`. |
| `Infrastructure/Services/MtStatementParserService.cs` | New | AngleSharp, section-marker walk, regex `^#(\d+)\s+(.+?)(?:\[(\w+)\])?$`, cancelled/working-orders skip. |
| `Infrastructure/Services/TradeImportService.cs` | New | Upsert + orphan aggregation. |
| `Application/DTOs/Trades/` | New | `ParsedMtStatementDto`, `ParsedMtTradeDto`, `ParsedSummaryDto`, `TradeImportResultDto`, `OrphanMagicNumberDto`, `SnapshotDto`, `StrategyTradeDto`. |
| `Application/DTOs/Strategies/StrategyDto.cs` | Modify | Add `int? MagicNumber`. |
| `WebAPI/Controllers/TradingAccountsController.cs` | Modify | `POST {id}/trades/import` (`[RequestSizeLimit]` already global). |
| `WebAPI/Controllers/StrategiesController.cs` | Modify | `GET api/strategies/{id}/trades?status=open|closed|all`. |
| `Infrastructure/DependencyInjection.cs` | Modify | Register `IMtStatementParserService`, `ITradeImportService`. |
| Frontend | New/Modify | Per proposal — `ImportTradesModal`, `StrategyTradesGrid`, `AddStrategyModal` MagicNumber input. |
| `tests/AppTradingAlgoritmico.UnitTests/StrategyWorkflow/Fixtures/Report Trades DW DEMO2.htm` | New | Copied from `/strategies/`. |
| `tests/.../MtStatementParserServiceTests.cs` | New | Fixture + edge-case streams. |
| `tests/.../TradeImportServiceTests.cs` | New | In-memory EF like `StrategyInMemoryDbContext`. |

## Interfaces / Contracts

```csharp
public interface IMtStatementParserService
{
    Task<ParsedMtStatementDto?> ParseAsync(Stream htmlStream, CancellationToken ct = default);
}

public sealed record ParsedMtStatementDto(
    IReadOnlyList<ParsedMtTradeDto> Trades,
    ParsedSummaryDto Summary);

public sealed record ParsedMtTradeDto(
    long Ticket, int MagicNumber, string StrategyNameHint, string? CloseReason,
    DateTime OpenTime, DateTime? CloseTime,
    string Type, decimal Size, string Item,
    decimal OpenPrice, decimal? ClosePrice, decimal StopLoss, decimal TakeProfit,
    decimal Commission, decimal Taxes, decimal Swap, decimal Profit,
    bool IsOpen);

public sealed record ParsedSummaryDto(
    DateTime ReportTime, decimal Balance, decimal Equity,
    decimal FloatingPnL, decimal Margin, decimal FreeMargin, decimal ClosedTradePnL,
    string Currency);

public interface ITradeImportService
{
    Task<TradeImportResultDto> ImportAsync(Guid accountId, Stream html, CancellationToken ct);
    Task<PagedResult<StrategyTradeDto>> GetByStrategyAsync(
        Guid strategyId, TradeStatusFilter status, int page, int pageSize, CancellationToken ct);
}
```

## Testing Strategy

| Layer | What | How |
|---|---|---|
| Unit — parser | Normal closed row; TP close; no-bracket title; `[other]`; cancelled row skipped; Working Orders ignored; open trade (no CloseTime); malformed title; empty/invalid HTML returns null; Summary extraction | xUnit + FluentAssertions, stream from embedded fixture + synthetic HTML strings |
| Unit — service | First import inserts; re-import updates (idempotent); orphan aggregation by magic; snapshot always written; 404 on missing account | `StrategyInMemoryDbContext` pattern (EF InMemory) + Moq for parser |
| Unit — controller | 200/400/404 mapping | Mocked `ITradeImportService` |
| Frontend | Modal reducer + `StrategyTradesGrid` rendering | Vitest |

Strict TDD: write the parser test against the checked-in fixture FIRST; implement until green. Service tests drive upsert and orphan behaviours.

## Migration / Rollout

Additive migration only. Rollback = `Down()` drops two tables + one column + one index. Existing `Strategy` / `TradingAccount` rows untouched. Generated but NOT applied during apply phase without user authorization (project rule).

## Open Questions

- [ ] `Item` casing: broker sends lowercase (`ndx`, `xauusd`). Store as-is — confirm no normalization needed for UI.
- [ ] `IsOpen`: compute from `CloseTime == null` or persist as column? Proposal lists it as a field; persisting simplifies index filters but introduces denormalization. **Proposed**: persist to make index/filter cheap, set inside service on upsert.
- [ ] Currency on snapshot: parse from header (`Currency: USD`) or take from `TradingAccount`? **Proposed**: parse from header, fall back to account — lets future multi-currency imports work without schema change.
