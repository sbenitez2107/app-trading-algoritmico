# Changelog

All notable changes to **App Trading Algorítmico** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.11.0] - 2026-05-02

### Added
- **Magic Number column in the strategies grid**: new column inside the "MT4 (Live)" group rendering each strategy's `magicNumber` as a plain integer. Visible by default (positioned first inside the MT4 group, before Net Profit) and toggleable from the column picker. Strategies without an assigned magic show an empty cell.
- **Pinned TOTAL row in the strategies grid**: a bold pinned-bottom row aggregates summable columns across all loaded strategies (Total Profit, Profit (pips), Trades, Wins, Losses, Cancelled, Gross Profit/Loss on the SQX side; Net Profit and Trade Count on the MT4 side). The Name column is replaced with the literal `TOTAL` label on this row, and clicking it does NOT open the trades panel. Averages, ratios and percentages are intentionally left blank because summing them is meaningless.
- **Pinned TOTAL row in the trades grid**: equivalent bottom row aggregating Commission, Swap, Taxes and Profit; the Net Profit valueGetter automatically computes the column total from those four. Ticket column shows `TOTAL` instead of a ticket number on this row.

### Changed
- **Heatmap precision in the Performance Analysis modal**: monthly and yearly return cells now render with 2 decimal places (was 1). Aligns the heatmap with the precision used everywhere else in the modal.
- **Trades-panel header layout**: rebuilt as a 3-column CSS grid (title | Performance icon | close icon). The Performance entry is now an icon-only button (📊) instead of the previous "📊 Performance" labelled button, freeing horizontal space; the title truncates with ellipsis when the strategy name is long.

### Fixed
- **Trades-panel header silently unstyled**: the `.account-detail__loading`, `.account-detail__grid` and `.account-detail__trades-panel*` BEM blocks were accidentally nested inside a `:host ::ng-deep` wrapper, which compiled to invalid descendant selectors and dropped every rule on the trades-panel header. Restored the BEM scope by closing the `::ng-deep` block and reopening `.account-detail` for the affected children.

---

## [0.10.0] - 2026-04-26

### Added
- **Performance Analysis modal per strategy**: 30+ KPIs across 6 sections (Returns, Drawdown & Risk-Adjusted, Trade Stats, Streaks, Other) plus a year-by-month compounding-return heatmap. Each KPI has a `?` icon with a CSS tooltip explaining the metric and showing good/bad ranges. Two entry points: a 📊 button in the Actions column of the Strategies grid, and a 📊 Performance button in the trades panel header. Powered by two new endpoints — `GET /api/strategies/{id}/analytics` and `GET /api/strategies/{id}/monthly-returns` — both built from the imported MT4 trades. Sharpe is computed over a synthetic daily-return series annualised with √252 (footnote in the modal flags that this differs from SQX's trade-by-trade Sharpe).
- **`StrategyAnalyticsCalculator`** pure-computation service: stateless, no DB dependency. Single entry point `Compute(initialBalance, trades)` returns the full `StrategyAnalyticsDto`; `ComputeMonthlyReturns(...)` returns the compounding bucket series. Calculates Total Return %, CAGR, Yearly/Monthly/Daily Avg Profit, AHPR, Max Drawdown $/%, Return/DD Ratio, Annual Return / Max DD (Calmar), Stagnation, Sharpe, SQN, Std Deviation, Profit Factor, Payout Ratio, Expectancy, R-Expectancy, streaks, Z-Score / Z-Probability, Exposure %, plus all aggregates (counts, gross profit/loss, avg/largest win/loss, commission/swap/taxes).
- **`InitialBalance` on `TradingAccount`**: required field on creation (Spanish form label "Balance Inicial"), used as the baseline for return / drawdown / CAGR calculations. Migration `AddInitialBalanceToTradingAccount` backfills existing rows with $100,000 so analytics work out of the box for legacy data. Optional `Currency` on the create/update form.
- **Strategies grid: SQX/MT4 column groups**: the account-detail grid now uses ag-grid `ColGroupDef` to group the existing SQX backtest KPIs under "SQX (Backtest)" (blue) and a new bank of 7 live KPIs under "MT4 (Live)" (green): Net Profit, Total Return %, Win %, Profit Factor, Return/DD, Max DD %, Sharpe. The endpoint `GET /api/trading-accounts/{id}/strategies` now joins `StrategyTrades` once per page and runs the analytics calculator per strategy to populate `live*` fields. Strategies without imported trades render `—` in every MT4 cell. Column picker shows two sub-headers (SQX / MT4) for selective visibility. Five MT4 columns visible by default alongside the SQX defaults.
- **Trades grid: Net Profit column + Close Reason column + colored Status badge + row tinting**: per-trade Net Profit (`profit + commission + swap + taxes`) is calculated client-side; rows are tinted green / red / blue (open) based on net P/L using `getRowStyle` (inline). Status renders as a colored badge (`Open` green / `Closed` gray) instead of the auto-checkbox. Close Reason is parsed from the MT4 statement and rendered with semantic colors (`TP` green, `SL` red, `Trailing` yellow). The MT4 parser now preserves the raw close-reason suffix in uppercase (previously collapsed everything outside `SL`/`TP` into `"Other"` and lost trailing-stop information).

### Changed
- **Currency formatting across the UI**: every monetary KPI in the strategies grid (12 columns: totalProfit, yearlyAvgProfit, dailyAvgProfit, monthlyAvgProfit, drawdown, averageTrade, grossProfit/Loss, averageWin/Loss, largestWin/Loss) and the trades grid (commission, swap, profit, net profit) now renders as `$1,234.56` via the new `formatCurrency` helper at `shared/utils/format.ts`.
- **Date formatting in the trades grid**: Open Time and Close Time now render as `DD/MM/YYYY HH:MM:SS` (local timezone) via the new `formatDateTime` helper.
- **Pagination on the strategies grid**: page sizes available are now `5 / 10 / 20 / 50 / 100`; default is `5` to leave room for the trades panel below.
- **Click-row to select a strategy**: clicking any row in the strategies grid opens the trades panel below it (with name + KPI strips + trades grid). Previously this was reachable only via a per-row 📊 icon, which is now removed in favor of the Actions-column Performance button.

### Fixed
- **Strategy trades grid SL / TP fields shown as empty**: the frontend `StrategyTradeDto` declared `sl` / `tp` but the API serializes `stopLoss` / `takeProfit`. Aligned the type and the column field references — now the values render correctly. Same DTO also gained `closeReason` and `taxes`, which the backend already exposed but the frontend was ignoring.
- **Net Profit row tint pre-existing CSS classes ignored by ag-grid**: replaced `rowClassRules` with inline `getRowStyle`. ag-grid 35 applies row backgrounds as inline styles on `.ag-row-odd/even` and beats CSS classes by specificity even with `!important`.

---

## [0.9.0] - 2026-04-26

### Added
- **Auto-assign MT4 magic numbers by strategy name**: during `POST /api/trading-accounts/{id}/trades/import`, orphan magic numbers whose `StrategyNameHint` matches a `Strategy.Name` (case-insensitive, trimmed) within the same account are auto-linked when a single match exists and that strategy has no magic yet. Result DTO now includes `AutoAssigned: IReadOnlyList<AutoAssignedStrategyDto>`. Anti-destructive: never overwrites an existing magic, never resolves ambiguous (multi-match) hints.
- **Manual assign-magic flow from the import modal**: new endpoint `POST /api/trading-accounts/{accountId}/strategies/{strategyId}/magic-number` (with `409` on conflict, `404` on missing). The result DTO now also exposes `AvailableStrategies` (every strategy in the account). The import modal renders a per-orphan `<select>` of strategies plus an **Assign** button that links the magic and re-imports the same statement file in one round-trip — no need to close the modal or re-pick the file.
- **Edit Stage modal: independent `Input` and `Passed`**: the workflow Edit Stage modal exposes both `inputCount` and `outputCount` as separately editable fields for non-Builder stages. Builder shows only `Passed` (it has no upstream input). Non-blocking warning when `Passed > Input`. Fixes a pre-existing bug where editing Builder wrote to `inputCount` (invisible) instead of `outputCount`.
- **Advance Stage modal: separate `Passed` and `Input next stage`**: `BatchService.AdvanceAsync` now accepts `passedCount` + `nextInputCount` (replacing the single `strategyCount`). The modal mirrors values automatically until the user types into the second field manually, and warns on `nextInput > passed`. ZIP file count still wins when provided.
- **Row-click selects strategy + trades panel below the grid**: clicking any row in the SBDEMO Strategies grid opens a panel below with the strategy name, two KPI strips (`Backtest (SQX)` and `Live (imported trades)`), and the trades grid. Previously this was only reachable via a per-row 📊 icon (now removed; row-click + close-panel button replace it).
- **Strategy trades summary endpoint**: new `GET /api/strategies/{id}/trades/summary` returns aggregated KPIs across **every** imported trade — independent of the grid's pagination window — computed in a single SQL aggregate. Powers the Live KPI strip (Total Profit, Net Profit, Commission, Swap, Win/Loss, Trades).
- **Pagination page-size 5/10**: account strategies grid offers 5, 10, 20, 50, 100 page sizes (default 5), giving room for the trades panel below.

### Changed
- **Pipeline cell display reflects persisted `outputCount` directly**: the previous status-mask rule (`passed = 0` when stage status ≠ Completed) is gone. New stages created by `AdvanceAsync` are initialized with `OutputCount = 0` instead of mirroring `InputCount`, so a Pending/Running stage naturally renders `input / 0` until the user edits the passed count manually. User-edited passed values are now respected even on non-Completed stages.
- **`StrategyTradesGridComponent` reactivity**: migrated from classic `@Input` + `ngOnInit` to `input.required<string>()` + `effect()`. The grid now refetches trades automatically when the parent switches the active strategy (previously it stayed stuck on the initially-mounted id).
- **Frontend DTO realignment**: `OrphanMagicNumberDto`, `SnapshotDto`, and `TradeImportResultDto` in `trading-account.service.ts` now match the actual API shape (`magicNumber`, `strategyNameHint`, `tradeCount`, full snapshot fields). The previous mismatch was rendering "undefined trades" in the orphan list.

### Fixed
- **Trades grid did not reload on row change** (see migration to signal input above) — selecting a second strategy now correctly fetches its trades.
- **Builder edit wrote the wrong field**: the Edit Stage modal's previous single-input flow wrote `inputCount` for Builder while displaying `outputCount`. Fixed by the per-stage-type save logic in the redesigned modal.
- **i18n**: new keys `SQX.WORKFLOW.PASSED`, `SQX.WORKFLOW.INPUT_NEXT`, `SQX.WORKFLOW.INPUT_GT_PASSED_WARNING`, `SQX.WORKFLOW.PASSED_GT_INPUT_WARNING` in `en.json` and `es.json`.

---

## [0.8.0] - 2026-04-24

### Added
- **Import Darwinex MT4 trade statements**: new end-to-end flow to ingest real broker trades onto existing Strategies. New `StrategyTrade` entity (ticket, open/close times, symbol, volume, prices, SL/TP, commission, swap, profit, CloseReason, IsOpen) and `AccountEquitySnapshot` entity (balance, equity, floating P&L, margin). New DTOs under `Application/DTOs/Trades/`. New `IMtStatementParserService` (AngleSharp-based HTML parser handling Darwinex MT4 `.htm`/`.html` statements — Closed Transactions, Open Trades, Working Orders, Summary sections; regex-driven magic-number extraction from title attributes; skips cancelled rows and Working Orders range). New `ITradeImportService` upserts trades by `(StrategyId, Ticket)`, aggregates orphans (magic numbers with no matching Strategy), appends one equity snapshot per call, and exposes `GetByStrategyAsync` with `TradeStatusFilter` (All/Open/Closed). Two new endpoints: `POST /api/trading-accounts/{id}/trades/import` (multipart IFormFile → `TradeImportResultDto`) and `GET /api/strategies/{id}/trades` (paginated, filterable by status). Frontend: new `ImportTradesModalComponent` (file validation, result summary, orphan panel with clipboard-copy of magic numbers) and `StrategyTradesGridComponent` (14-column ag-grid, open/closed/all tabs). `AccountDetailComponent` wires both components; new "Import Trades" button and per-row trades toggle.
- **Magic Number on Strategy**: `Strategy` entity gains nullable `int? MagicNumber` column with filtered unique index `(TradingAccountId, MagicNumber) WHERE MagicNumber IS NOT NULL`. `POST /api/trading-accounts/{id}/strategies` accepts optional `magicNumber` form field; `IStrategyService.AddToAccountAsync` persists it. Frontend: `AddStrategyModalComponent` exposes a Magic Number input with integer validation. This is the bridge that lets imported MT4 trades match the right Strategy.
- **Currency on TradingAccount**: new optional `Currency` column on `TradingAccount`. `TradeImportService` falls back to this when the parsed statement header does not carry an explicit currency.
- **i18n keys**: `DARWINEX.IMPORT_TRADES.*`, `DARWINEX.TRADES_GRID.COL_*` (14 grid column headers), and `DARWINEX.ADD_STRATEGY.MAGIC_NUMBER_*` added to both `en.json` and `es.json`. Keys are ready for future wiring; darwinex components keep hardcoded English strings for now (consistent with the rest of the feature).

### Changed
- **EF migration folders unified**: the legacy `Infrastructure/Migrations/` folder was removed and its 5 files (initial schema, trading-accounts migration, and snapshot) moved to `Infrastructure/Persistence/Migrations/` with namespaces updated to match. All 13 migrations now resolve from a single path.
- **Workflow scripts (`run-all`, `stop-all`)**: refactored to bash-native commands (`dotnet run`, `pnpm start`, `kill-by-port`) using the harness's `run_in_background` parameter instead of PowerShell wrappers — shell-agnostic quoting, surgical kill by port (4200/5000/5001), verification step.

### Fixed
- **`StrategyTradesGridComponent` flaky test**: bumped timeout to 15 s on the first test that exercises ag-grid's initial `TestBed.createComponent`. In the full parallel Vitest suite (14 files), ag-grid bootstrap in jsdom regularly exceeded the default 5 s timeout even though the test passed in isolation.

### Security
- No new secrets. New endpoint `POST /api/trading-accounts/{id}/trades/import` remains `[Authorize]`.

---

## [0.7.0] - 2026-04-21

### Added
- **Account strategies grid pagination**: ag-grid client-side pagination enabled with page-size selector (20 / 50 / 100). The grid now uses `domLayout="autoHeight"` so it sizes to the visible rows with no internal vertical scroll — page navigation is the primary way to move through the list. `AccountDetailComponent.loadStrategies` fetches up to 500 rows per account in one call (threshold above which we would need server-side paging).

---

## [0.6.0] - 2026-04-20

### Added
- **Strategy indicator columns**: three new columns extracted from the `.sqx` strategy XML, togglable from the grid column picker: **Entry Indicators** (indicators used in entry signal conditions — `StdDev, ADX` for DAX-style strategies, `LinearRegression, LowestInRange` for classic), **Price Indicators** (indicators used to compute the entry order price — `HighestInRange`, `SessionHigh`, etc.), and **Indicator Params** (compact format `"Name(k1=v1, k2=v2); ..."`). Handles both XML patterns SQX emits: `categoryType="indicator"` with name in `@key`, and `categoryType="simpleRules"` (bundled indicator+comparison like `StdDevRising`) with name in `@mI`. Price indicators are collected from `<Then>` → `<Param key="#Price#">` → Formula descendants (categories `indicator` + `priceValue`). Platform params (`#Chart#`, `#Direction#`, `#Symbol#`, `#Size#`) excluded.
- **Grid preset update**: save changes over an existing preset without creating a new one. New `PUT /api/users/me/grid-presets/{id}` endpoint preserves the preset name and overwrites `VisibleColumns` + `ColumnOrder`. Frontend: floppy-disk icon 💾 per preset in the dropdown captures the current grid state and calls update.
- **Grid preset now captures real column order**: save and update both read the live column state from ag-grid (`gridApi.getColumnState()`), including drag-reorder and column-picker visibility. Applying a preset restores both the visibility and the order via `applyColumnState({ state, applyOrder: true })`. All 46 KPI columns remain in the grid at all times (toggled via `hide` property) so `applyColumnState` can reorder hidden columns correctly.

### Fixed
- **SQX parser was reading the wrong XML file inside the `.sqx` archive**. Switched from `settings.xml` (bulky Walk-Forward results container with `<ResultsGroup>` root) to `strategy_Portfolio.xml` (clean `<StrategyFile><Strategy>…<Rules><signals>` structure). This silently broke pseudocode extraction since the EA Import feature was first introduced (it returned the literal fallback "Unable to parse strategy", 24 chars) and prevented indicator column population. Pseudocode now extracts the full strategy definition (~800+ chars typical), and the 3 indicator columns populate correctly for all strategy styles. A fallback to `settings.xml` is kept for backward compatibility with older .sqx formats.

### Added
- **IMOX Knowledge Base**: Agent knowledge base at `.agents/knowledge/imox/` with 10 IMOX Academy documents (SQX config, mining workflow, validation protocol, asset profiles, money management). New `trading-domain` skill routes agents to the correct documents before domain decisions.
- **HTML Report Parser**: Automatic KPI extraction from SQX `.html` reports during EA Import. New `HtmlReportParserService` (AngleSharp 1.1.2) parses ~46 KPIs + monthly performance + backtest metadata (Symbol, Timeframe, BacktestFrom, BacktestTo). `Strategy` entity extended from 7 → 52 KPI columns. New `StrategyMonthlyPerformance` entity with unique (StrategyId, Year, Month) index. `BatchService.ImportFromZipAsync` pairs `.sqx` + `.html` by base filename inside the uploaded ZIP.
- **Add strategies to Darwinex demo accounts**: Demo trading accounts can now receive strategies uploaded directly (bypassing the SQX pipeline). New endpoints `GET /api/trading-accounts/{id}/strategies` (paginated) and `POST /api/trading-accounts/{id}/strategies` (multipart: name + .sqx + .html report). Backend: `Strategy.BatchStageId` and `Strategy.TradingAccountId` are both nullable FKs with `SetNull` delete behavior; `StrategyService.AddToAccountAsync` parses .sqx (pseudocode) and .html (KPIs) and saves the strategy linked to the account. Frontend: `AccountDetailComponent` with ag-grid strategy table + column picker sidebar, `AddStrategyModalComponent` (modal with signals + `canSubmit` computed), and `AccountsListComponent` row-click navigation to `/darwinex/demo/:accountId` (demo accounts only, ignores button clicks).
- **Account strategy grid UX**: Title shows the demo account name. Back button navigates to `/darwinex/demo`. Actions column (renamed from unnamed) hosts comments 💬 and delete 🗑️ icons. Trash icon hard-deletes a strategy through a confirmation modal (`DELETE /api/strategies/{id}`). Symbol column tints cell background + left border with a deterministic color per asset (same Symbol → same color). Add Strategy modal auto-suggests the `name` from the first uploaded filename (only if the field is empty). Timeframe column visible between Symbol and KPIs.
- **Column presets (named, per-user)**: New `StrategyGridPreset` entity (UserId, Name, VisibleColumnsJson, ColumnOrderJson) with unique (UserId, Name). CRUD endpoints under `/api/users/me/grid-presets`. Frontend: preset dropdown in the toolbar + `SavePresetModalComponent` for named captures. Applying a preset updates the `visibleColumns` signal and grid columns re-render.
- **Strategy comments (append-only bitácora)**: New `StrategyComment` entity for immutable per-strategy notes/observations/parameter-decisions. Endpoints `GET /api/strategies/{id}/comments` (ordered newest-first) and `POST /api/strategies/{id}/comments`. `CreatedBy` is populated from the JWT `NameIdentifier` claim. Frontend: `StrategyCommentsModalComponent` opened from the Actions column shows history + textarea + "Add comment" (disabled while empty). Plain text only; comments cannot be edited or deleted.
- **Upload limits**: Raised `Kestrel.Limits.MaxRequestBodySize` and `FormOptions.MultipartBodyLengthLimit` to 200MB globally to cover SQX HTML reports with large trade tables.
- **SDD hybrid workflow bootstrap**: `openspec/` directory created with `config.yaml`, `specs/`, and `changes/archive/`. First SDD change (`add-strategies-to-demo-accounts`) archived with full trail (explore, proposal, specs, design, tasks, apply-progress, verify-report, archive-report).

### Changed
- **Strategy KPI field names**: Renamed to match the SQX overview exactly — `NetProfit → TotalProfit`, `WinRate → WinningPercentage`, `MaxDrawdown → Drawdown`, `TotalTrades → NumberOfTrades`. Frontend types + templates synced.
- **Pipeline stage editing**: Builder stage now correctly edits `inputCount` (strategy count created). Edit button available on all stages regardless of status.
- **Pipeline rollback**: Completed stages can now be rolled back. Previously blocked by status check. Rollback button changed to ⏪ icon.
- **Pipeline rollback/delete**: Preserves strategies dual-linked to a trading account (strategies with `TradingAccountId != null` are excluded from cascade removal; EF `SetNull` takes over).
- **`ISqxParserService`**: Refactored from `ParseZipAsync` to single-file `ExtractPseudocodeAsync(Stream)`. ZIP orchestration (pairing `.sqx` + `.html`) moved to `BatchService`. Unused `ParsedStrategyDto` removed.

### Security
- No new secrets introduced. Upload limits raised intentionally for SQX report parsing; endpoints remain `[Authorize]`.

---

## [0.4.2] - 2026-04-13

### Added
- **Strategy Rules Analyzer**: New SQX menu item at `/sqx/strategy-analyzer`. CRUD for global validation rules (checklist) used to evaluate strategies post-Optimizer before selecting for BT or Demo. Backend: `AnalyzerRule` entity, EF migration, REST API (`/api/analyzer-rules`), seed with 6 initial rules. Frontend: checklist view with priority ordering, create/edit modal, delete confirmation.
- **Pre-commit skill v1.2**: Mandatory checklist before every `git commit` with Engram memory sync as Step 7.

### Changed
- **Batch list Asset column**: Now shows only the asset name (Oro, Nasdaq, DAX) instead of the symbol+name combination.
- **App version**: Bumped to 0.4.2 in environment files and sidebar UI.

### Fixed
- **Analyzer rule service URL**: Corrected to use `API_BASE_URL` injection token with `/api/` prefix instead of `environment.apiUrl` directly.

### Planned
- Risk management dashboard
- Deployment tracker (demo/live accounts)
- Prop firm challenge phase tracker (FTMO, The Trading Pits)
- Capital manager performance tracking (Axi Select, Darwinex)
- Automated KPI extraction from .sqx strategy files
- Per-stage configuration for Strategy Workflow pipeline
- Date tracking (start/end) per pipeline stage

---

## [0.4.0] - 2026-04-13

### Added
- **Home Dashboard — Strategy Workflow Running**: New section showing all currently running batch stages across assets. Each card displays Asset+Timeframe, BuildingBlock, Stage, counts (Builder shows total, others show input/passed), and elapsed time since stage was set to Running. Click navigates to Pipeline Detail; "Stage detail →" button navigates to Stage Detail.
- **Drag & drop asset cards**: Reorder cards in Strategy Workflow overview by dragging. Order persists in localStorage (`bent_asset_card_order`). Uses `@angular/cdk/drag-drop` with `cdkDropListOrientation="mixed"` for grid layout. New cards appear at end of saved order.
- **Delete batch**: Trash button (🗑️) next to advance button in pipeline grid. Confirmation modal before deletion. Cascades delete to all stages and strategies. `DELETE /api/batches/{id}` endpoint.
- **`RunningStartedAt` in BatchStageSummaryDto**: Pipeline summary now includes the running start timestamp for elapsed time calculation in dashboards.

### Changed
- **Performance**: `BatchService.GetAllAsync` and `GetByIdAsync` now use direct LINQ projection to DTO instead of `.Include()` chains. Eliminates cartesian explosion. Response time reduced from **54s to 0.97s** (~55x faster) for typical batch counts.

### Removed
- Dead code: unused `ToDto(Batch b)` helper method (replaced by inline projection).

### Notes
- DB running in Docker WSL2 adds minor network latency; combined with optimized queries, this is now negligible.

---

## [0.3.1] - 2026-04-12

### Added
- **Pipeline status model**: Simplified to Pending → Running → Completed. Toggle buttons (▶/⏸) to start/stop running directly from the pipeline grid. `RunningStartedAt` timestamp tracked.
- **Edit/Delete stage**: Edit strategy counts and delete stages (rollback to previous) for non-completed stages. `DELETE /api/batches/{batchId}/stages/{stageId}` endpoint.
- **Pipeline totals row**: Summary row showing input/passed totals per stage with pass rate percentages.
- **Cell display format**: Builder shows total created, other stages show `input / passed` with % rate.
- **Asset overview redesign**: Cards grouped by asset with timeframe rows. Support for multiple timeframes per asset.
- **Session expiry redirect**: Auth interceptor now detects 401 responses and redirects to login automatically.
- **SQX logo**: Strategy Quant official logo in sidebar, replacing placeholder shield icon.
- **Favicon**: New trading chart pulse SVG favicon. Title updated to "BENT — Trading Automatico".
- **Pre-commit skill**: `/pre-commit` checklist for code review before commits.
- **Optional ZIP upload**: Strategy count can be entered manually without uploading .sqx files (for data migration).
- **Advance with 0**: Pipeline stages can be advanced with 0 strategies.
- **Advance modal**: Shows batch name for context.

### Changed
- Timeframes reduced to M15, M30, H1, H4 only.
- Pending stage cells now have amber background.
- Advance stage icon changed to ⏭ (skip forward) to differentiate from ▶ (run).
- Login page footer and security badges removed.

### Fixed
- Auth interceptor handles 401 and redirects to login.
- i18n keys resolved correctly after consolidating to `public/assets/i18n/`.

---

## [0.3.0] - 2026-04-11

### Added
- **Strategy Workflow (SQX Pipeline)**: Full pipeline dashboard for trading strategies (Builder → Retester → Optimizer → Demo → Live). 3-level UI: Asset Overview cards, Pipeline Detail grid, Stage Detail with KPI table. Batch creation with ZIP upload of .sqx files, stage advancement, inline KPI editing, pseudocode viewer.
- **Building Blocks CRUD**: Management of SQX Building Block configs with .sqb file upload. 4 types: Base, Trend, Volatility, Reversion.
- **Assets Management**: Create trading assets from the Workflow dashboard with timeframe selection.
- **SQX Parser Service**: Extracts pseudocode from .sqx files (nested ZIP + XML parsing).
- **Multi-language (EN/ES)**: Default Spanish. Header toggle for instant switching. Persisted in user profile.
- **Dark/Light Theme**: CSS variable theming. Default dark. Header toggle. Persisted in user profile.
- **User Preferences API**: `GET/PATCH /api/user/preferences` for language and theme. Returned in login response.
- **App Version Display**: v0.3.0 shown in sidebar.

### Changed
- Login page footer and security badges removed.
- Default language changed from EN to ES.
- AuthResponseDto extended with preferences.

### Fixed
- i18n files consolidated to `public/assets/i18n/` (Angular 21 Vite compatibility).

---

## [0.2.0] - 2026-04-10

### Added
- **Trading Accounts Module**: Added `TradingAccount` entity and CRUD features to the `.NET` Host, allowing connection to brokers and platforms (MT4/MT5).
- **AES-256 Encryption**: Created `AesEncryptionService` in the backend so all Trading Account passwords are automatically encrypted/decrypted transparently and are never exposed as plain text over HTTP (`"***"` returned to frontend).
- **Frontend Trading Accounts Area**: Angular UI interface to handle demo and live accounts with interactive modals and custom reactive forms.
- **Improved Sidebar Navigation**: Added a robust nested routing configuration for `darwinex/demo` and `darwinex/live`, visually structured using native Angular Signals for expansion states.
- **Auth User Header**: Replaced hardcoded frontend user placeholders with a dynamic indicator showing initials and current login email of the user.

### Changed
- App name updated to **BENT**.
- Main layout visual restructuring (removed dummy dashboard cards, old notifications, and AM avatar).
- Angular service `ChangeDetectionStrategy.OnPush` propagation correctly mitigated with `ChangeDetectorRef.markForCheck()` implementation for HTTP calls inside asynchronous UI updates.
---

## [0.1.1] — 2026-03-31

### Changed
- Synchronized `AGENTS.md` (root, API, Web) references to use correct connection string (`DefaultConnection`) and .NET 10 versioning.
- Updated root `AGENTS.md` commands table to mirror available workflows properly.

---

## [0.1.0] — 2026-03-31

### Added
- Repository initialized with monorepo structure:
  - `app.trading.algoritmico.api` — .NET 10 backend (Clean Architecture)
  - `app.trading.algoritmico.web` — Angular 21 frontend (Signals + Standalone Components)
- Root `AGENTS.md` orchestrator with full skill routing protocol
- Backend skills configured:
  - `clean-architecture` — Layer structure and dependency rules
  - `csharp-dotnet` — C# coding standards for .NET 10
  - `entity-framework` — EF Core 10 patterns (Fluent API, migrations, seeding)
  - `webapi-patterns` — REST + GraphQL (HotChocolate) conventions
  - `security` — JWT + ASP.NET Core Identity + CORS
  - `auditing` — HTTP audit middleware (masking, truncation)
  - `external-integrations` — Refit + Polly for broker/market data APIs
  - `testing` — xUnit + FluentAssertions + Moq patterns
  - `dotnet-automation` — CLI build and self-healing protocol
- Frontend skills configured:
  - `angular` — Angular 21 patterns (Signals, Standalone, Control Flow)
  - `design-core` — Dark-first trading dashboard theme (SCSS, BEM, design tokens)
- Shared agent skills: `root-orchestrator`, `analyst-requeriment`, `perform-testing`, `frontend-standards`, `job-orchestrator`, `grid-standard`
- Workflows: `run-all`, `run-host`, `run-web`, `stop-all`, `stop-host`, `stop-web`, `restart-host`
- Database: SQL Server, ASP.NET Core Identity (Users, Roles)
- Default roles seeded: `Admin`, `Trader`, `Viewer`

### Architecture Decisions
- **No multitenancy** — single-user personal platform
- **CQRS pattern** — REST for commands (POST/PUT/DELETE), GraphQL for queries (GET)
- **pnpm** as frontend package manager
- **Dark-first** UI theme with trading domain color semantics (gain: green, loss: red)
- **Namespace**: `AppTradingAlgoritmico.*` across all backend layers

---

> **Legend**: Added · Changed · Deprecated · Removed · Fixed · Security
