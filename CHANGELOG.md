# Changelog

All notable changes to **App Trading Algorítmico** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
