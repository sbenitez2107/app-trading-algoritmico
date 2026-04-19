# Design: Add Strategies to Demo Accounts

## Technical Approach

Reuse `Strategy` entity as a first-class record that can be attached to a `BatchStage`, a `TradingAccount`, or both. Relax `BatchStageId` to nullable and add a nullable `TradingAccountId` FK. Expose two nested-resource REST endpoints under `api/trading-accounts/{accountId}/strategies` (list + upload). Frontend adds a lazy `:accountId` route under `darwinex` with an ag-grid-community grid exposing all ~50 KPI columns plus a custom column picker. Row click on `AccountsListComponent` navigates to the detail.

Maps directly to proposal approach (Option A) and to spec requirements `R1..R6` (account-strategies) and `M1..M4` (strategy-model).

---

## Architecture Decisions

### Decision: Nested-resource controller

**Choice**: New `TradingAccountStrategiesController` at route `api/trading-accounts/{accountId}/strategies`.
**Alternatives**: Add endpoints to `StrategiesController` (`api/strategies?accountId=`) or to `TradingAccountsController`.
**Rationale**: Nested path reads naturally for a 1:N relation and keeps unrelated concerns out of existing controllers. Consistent with the `api/batches/{batchId}/stages/{stageId}/strategies` nesting already in `StrategiesController`.

### Decision: Dual `OnDelete(SetNull)` on both FKs

**Choice**: Both `BatchStageId` and `TradingAccountId` FKs use `DeleteBehavior.SetNull`.
**Alternatives**: Leave `BatchStage` Cascade (breaks dual-linked strategies); `Restrict` on `TradingAccount` (requires caller to cleanup first).
**Rationale**: Symmetric semantics. A strategy survives deletion of either parent if it has the other. Existing `BatchService.DeleteAsync` / `RollbackStageAsync` already call `RemoveRange` explicitly on pipeline-only strategies before stage deletion — `SetNull` is a safe fallback for dual-linked cases.

### Decision: Orphan prevention at service layer (no DB constraint)

**Choice**: `AddToAccountAsync` always sets `TradingAccountId`; pipeline paths always set `BatchStageId`. No DB `CHECK` constraint.
**Alternatives**: DB `CHECK (BatchStageId IS NOT NULL OR TradingAccountId IS NOT NULL)`.
**Rationale**: EF Core migrations on SQL Server can apply CHECK constraints but they complicate rollback and testing. Construction-by-invariant is simpler and matches the spec's explicit "service-layer only" decision (M4).

### Decision: Custom column picker for ag-grid-community

**Choice**: Sidebar with checkbox list driven by a `visibleColumns` signal; `columnDefs` computed from it.
**Alternatives**: ag-grid Enterprise tool panel (paid, out of scope); menu dropdown.
**Rationale**: Sidebar is discoverable, fits dark-first layout, zero extra deps. ~50 KPIs need a scrollable column group — a dropdown would be cramped.

### Decision: Pagination on GET (matches pipeline pattern)

**Choice**: `?page=1&pageSize=20` query parameters, returns `PagedResult<StrategyDto>`.
**Rationale**: Same pattern as `GetByStageAsync`; consistent with existing `PagedResult<T>` record.

---

## Data Flow

### Upload flow (POST)

```
AccountDetail → AddStrategyModal → StrategyService.addToAccount(FormData)
     │
     ▼
POST /api/trading-accounts/{id}/strategies  [multipart: name, sqxFile, htmlFile]
     │
     ▼
TradingAccountStrategiesController.Create
     │
     ▼
IStrategyService.AddToAccountAsync
     ├── verify account exists           → 404 if not
     ├── ISqxParserService.ExtractPseudocodeAsync(sqxStream)
     ├── IHtmlReportParserService.ParseAsync(htmlStream)   → 400 if null
     ├── build Strategy (TradingAccountId=id, BatchStageId=null)
     ├── StrategyKpiMapper.ApplyKpis(entity, report.Kpis)
     ├── hydrate MonthlyPerformance entries
     └── SaveChangesAsync → StrategyKpiMapper.ToDto → 201 Created
```

### Stage delete with dual-linked strategy

```
BatchService.DeleteAsync(batchId)
     │  Include Stages.ThenInclude(Strategies)
     ▼
foreach stage in batch.Stages:
     db.Strategies.RemoveRange(stage.Strategies.Where(s => s.TradingAccountId == null))
     // dual-linked strategies are NOT explicitly removed
     ▼
db.BatchStages.RemoveRange(batch.Stages)
     ▼
SaveChangesAsync
     │  EF applies SetNull for the FK on any remaining
     │  strategy referencing the deleted stage
     ▼
Dual-linked strategies preserved with BatchStageId=null, TradingAccountId intact
```

---

## File Changes

### Backend

| File | Action | Description |
|------|--------|-------------|
| `Domain/Entities/Strategy.cs` | Modify | `public Guid BatchStageId` → `public Guid? BatchStageId`; `BatchStage BatchStage = null!` → `BatchStage? BatchStage`; add `public Guid? TradingAccountId` + `public TradingAccount? TradingAccount` |
| `Domain/Entities/TradingAccount.cs` | Modify | Add `public ICollection<Strategy> Strategies { get; set; } = [];` |
| `Infrastructure/Persistence/Configurations/StrategyConfiguration.cs` | Modify | Flip BatchStage FK to `SetNull` + `IsRequired(false)`; add TradingAccount FK with `SetNull` |
| `Infrastructure/Migrations/{timestamp}_AddStrategyTradingAccountFk.cs` | New | Alter column + add FK (manual review required) |
| `Application/Interfaces/IStrategyService.cs` | Modify | Add `GetByAccountAsync` + `AddToAccountAsync` |
| `Infrastructure/Services/StrategyService.cs` | Modify | Inject `ISqxParserService`, `IHtmlReportParserService`; implement new methods |
| `WebAPI/Controllers/TradingAccountStrategiesController.cs` | New | Nested resource controller with GET + POST |

### Frontend

| File | Action | Description |
|------|--------|-------------|
| `features/darwinex/darwinex.routes.ts` | Modify | Add `demo/:accountId` child route |
| `features/darwinex/account-detail/account-detail.component.{ts,html,scss}` | New | OnPush + Signals; grid + column picker sidebar |
| `features/darwinex/add-strategy-modal/add-strategy-modal.component.{ts,html,scss}` | New | OnPush + Signals; file inputs + submit |
| `features/darwinex/accounts-list/accounts-list.component.html` | Modify | `(click)="navigateToDetail(acc, $event)"` on `<tr>` |
| `features/darwinex/accounts-list/accounts-list.component.ts` | Modify | Add `navigateToDetail` with target-element guard |
| `core/services/strategy.service.ts` | Modify | Add `getByAccount` + `addToAccount` |

---

## Interfaces / Contracts

### Entity changes

```csharp
// Strategy.cs
public Guid? BatchStageId { get; set; }
public BatchStage? BatchStage { get; set; }

public Guid? TradingAccountId { get; set; }
public TradingAccount? TradingAccount { get; set; }

// TradingAccount.cs
public ICollection<Strategy> Strategies { get; set; } = [];
```

### EF configuration wiring

```csharp
// StrategyConfiguration.cs (replaces existing HasOne(BatchStage) block)
builder.HasOne(x => x.BatchStage)
    .WithMany(bs => bs.Strategies)
    .HasForeignKey(x => x.BatchStageId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasOne(x => x.TradingAccount)
    .WithMany(t => t.Strategies)
    .HasForeignKey(x => x.TradingAccountId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);
```

### IStrategyService additions

```csharp
Task<PagedResult<StrategyDto>> GetByAccountAsync(
    Guid accountId, int page = 1, int pageSize = 20, CancellationToken ct = default);

Task<StrategyDto> AddToAccountAsync(
    Guid accountId, string name, Stream sqxStream, Stream htmlStream, CancellationToken ct = default);
```

### AddToAccountAsync skeleton

```csharp
public async Task<StrategyDto> AddToAccountAsync(
    Guid accountId, string name, Stream sqxStream, Stream htmlStream, CancellationToken ct)
{
    var accountExists = await db.TradingAccounts.AnyAsync(a => a.Id == accountId, ct);
    if (!accountExists) throw new KeyNotFoundException($"TradingAccount {accountId} not found.");

    var pseudocode = await sqxParser.ExtractPseudocodeAsync(sqxStream, ct);
    var report = await htmlParser.ParseAsync(htmlStream, ct)
        ?? throw new ArgumentException("Invalid SQX HTML report.");

    var entity = new Strategy
    {
        Name = name,
        Pseudocode = pseudocode,
        TradingAccountId = accountId,
        BatchStageId = null,
        Symbol = report.Symbol,
        Timeframe = report.Timeframe,
        BacktestFrom = report.BacktestFrom,
        BacktestTo = report.BacktestTo,
        CreatedAt = DateTime.UtcNow
    };
    StrategyKpiMapper.ApplyKpis(entity, report.Kpis);
    foreach (var mp in report.MonthlyPerformance)
        entity.MonthlyPerformance.Add(new StrategyMonthlyPerformance {
            Year = mp.Year, Month = mp.Month, Profit = mp.Profit, CreatedAt = DateTime.UtcNow
        });

    db.Strategies.Add(entity);
    await db.SaveChangesAsync(ct);
    return StrategyKpiMapper.ToDto(entity);
}
```

### REST endpoints

| Method | Path | Body | 200/201 | 400 | 404 |
|--------|------|------|---------|-----|-----|
| GET | `api/trading-accounts/{accountId}/strategies?page=1&pageSize=20` | — | `PagedResult<StrategyDto>` | — | account not found |
| POST | `api/trading-accounts/{accountId}/strategies` | multipart: `name`, `sqxFile`, `htmlFile` | `StrategyDto` | missing files OR unparseable HTML | account not found |

Controller template follows `BatchesController.Create` pattern: `[FromForm] string name`, `[FromForm] IFormFile? sqxFile`, `[FromForm] IFormFile? htmlFile`; wrap stream opens in `try/finally`, map `KeyNotFoundException → 404`, `ArgumentException → 400`.

### Frontend StrategyService additions

```typescript
getByAccount(accountId: string, page = 1, pageSize = 20): Observable<PagedResult<StrategyDto>> {
  const params = new HttpParams().set('page', page).set('pageSize', pageSize);
  return this.http.get<PagedResult<StrategyDto>>(
    `${this.apiUrl}/api/trading-accounts/${accountId}/strategies`, { params });
}

addToAccount(accountId: string, name: string, sqx: File, html: File): Observable<StrategyDto> {
  const form = new FormData();
  form.append('name', name);
  form.append('sqxFile', sqx);
  form.append('htmlFile', html);
  return this.http.post<StrategyDto>(
    `${this.apiUrl}/api/trading-accounts/${accountId}/strategies`, form);
}
```

### AccountDetailComponent (key shape)

```typescript
@Component({ standalone: true, changeDetection: ChangeDetectionStrategy.OnPush, /* ... */ })
export class AccountDetailComponent {
  readonly accountId = signal<string>(this.route.snapshot.paramMap.get('accountId')!);
  readonly account = signal<TradingAccountDto | null>(null);
  readonly strategies = signal<StrategyDto[]>([]);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);

  readonly visibleColumns = signal<Set<string>>(new Set([
    'name','totalProfit','winningPercentage','profitFactor',
    'drawdown','numberOfTrades','sharpeRatio'
  ]));

  readonly columnDefs = computed<ColDef[]>(() =>
    ALL_KPI_COLS.map(c => ({ ...c, hide: !this.visibleColumns().has(c.field!) }))
  );

  toggleColumn(field: string) {
    const next = new Set(this.visibleColumns());
    next.has(field) ? next.delete(field) : next.add(field);
    this.visibleColumns.set(next);
  }
}
```

`ALL_KPI_COLS` is a static array declared in the component file (one `ColDef` per KPI, with `filter: 'agNumberColumnFilter'` for numerics, `'agTextColumnFilter'` for strings). `suppressMovableColumns` is NOT set — drag-reorder is on by default.

### AddStrategyModalComponent

```typescript
@Component({ standalone: true, changeDetection: ChangeDetectionStrategy.OnPush, /* ... */ })
export class AddStrategyModalComponent {
  @Input({ required: true }) accountId!: string;
  @Output() strategyCreated = new EventEmitter<StrategyDto>();
  @Output() cancelled = new EventEmitter<void>();

  readonly name = signal('');
  readonly sqxFile = signal<File | null>(null);
  readonly htmlFile = signal<File | null>(null);
  readonly isSubmitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly canSubmit = computed(() =>
    this.name().trim().length > 0 && !!this.sqxFile() && !!this.htmlFile() && !this.isSubmitting());

  submit() { /* call strategyService.addToAccount, emit strategyCreated */ }
}
```

### Row click guard on AccountsListComponent

```typescript
navigateToDetail(acc: TradingAccountDto, ev: Event): void {
  // Ignore clicks on action buttons inside the row
  if ((ev.target as HTMLElement).closest('button')) return;
  if (this.accountType !== 0) return; // demo-only in this change
  this.router.navigate(['/darwinex', 'demo', acc.id]);
}
```

Template: `<tr class="ta-table__row" (click)="navigateToDetail(acc, $event)">...`. Live rows simply do nothing (no navigation) until the live flow ships.

---

## Testing Strategy

| Layer | What | How |
|-------|------|-----|
| Unit (backend) | `StrategyService.GetByAccountAsync` returns paged result, 404 when account missing | xUnit + in-memory `AppDbContext` (EF InMemory) + FluentAssertions |
| Unit (backend) | `StrategyService.AddToAccountAsync` happy path, `KeyNotFoundException` on missing account, `ArgumentException` when parser returns null | xUnit + Moq for `ISqxParserService` + `IHtmlReportParserService`; EF InMemory for context |
| Unit (backend) | `StrategyConfiguration` wiring — deleting BatchStage with dual-linked strategy preserves row with `BatchStageId=null` | xUnit with SQLite in-memory (supports FK + SetNull) |
| Controller (backend) | `TradingAccountStrategiesController` maps `KeyNotFoundException → 404`, `ArgumentException → 400`, happy path → 201 | xUnit + mocked `IStrategyService` |
| Unit (frontend) | `StrategyService.getByAccount` builds correct URL + params; `addToAccount` builds FormData with name + both files | Vitest + `HttpTestingController` |
| Unit (frontend) | `AccountDetailComponent` — loads strategies on init, `columnDefs` reacts to `visibleColumns`, error signal set when account 404s | Vitest + TestBed |
| Unit (frontend) | `AddStrategyModalComponent` — `canSubmit` computed gates submit button, emits `strategyCreated` after success | Vitest + TestBed |
| Unit (frontend) | `AccountsListComponent.navigateToDetail` — ignores button clicks, navigates only for `accountType === 0` | Vitest + spy on `Router.navigate` |

TDD: every scenario above maps 1:1 to a spec scenario. Write tests first; signals/pure-functions keep seams narrow.

---

## Migration / Rollout

### EF migration

```bash
dotnet ef migrations add AddStrategyTradingAccountFk --project src/AppTradingAlgoritmico.Infrastructure --startup-project src/AppTradingAlgoritmico.WebAPI
```

Expected migration contents (MUST be verified manually — EF Core 10 has known column-rename guessing issues when many typed columns change simultaneously):

1. `DROP FK FK_Strategies_BatchStages_BatchStageId` (old Cascade FK)
2. `ALTER COLUMN Strategies.BatchStageId uniqueidentifier NULL` (lossless on SQL Server)
3. `ADD COLUMN Strategies.TradingAccountId uniqueidentifier NULL`
4. `ADD FK FK_Strategies_BatchStages_BatchStageId ON DELETE SET NULL`
5. `ADD FK FK_Strategies_TradingAccounts_TradingAccountId ON DELETE SET NULL`
6. `CREATE INDEX IX_Strategies_TradingAccountId`

**Rollout gate**: after generating, OPEN the file, confirm no `RenameColumn` / `DropColumn` / `DropTable` appears for unrelated columns. Run `dotnet ef migrations script` for a final review, apply against a local DB, verify existing rows still have their `BatchStageId` value intact.

### No data migration

Existing rows retain `BatchStageId`; new `TradingAccountId` starts NULL. No backfill required.

### Rollback

`migration down` drops the new FK + column and restores `BatchStageId NOT NULL` with Cascade. Any strategies inserted with `TradingAccountId` set and `BatchStageId=null` will fail rollback — acceptable because they are post-feature test data.

---

## Open Questions

- [ ] Column picker placement: confirmed **sidebar** (left or right of grid) for phase 1. Left-side is conventional for trees/filters — proposal: right side to keep grid anchored.
- [ ] Numeric formatting (decimal places per KPI) in the grid — use existing shared pipe if one exists, else plain number for phase 1.
- [ ] TradingAccount delete UX: if user deletes an account with attached strategies, should we warn ("N strategies will become unassigned")? Out of scope for this change — `SetNull` preserves data either way; UI warning can come later.
