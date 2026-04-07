# Skill Registry — imox-ta
*Generated: 2026-04-06*

## Convention Files (Index Files)

- `AGENTS.md` — Root orchestration protocol, global skill routing
- `app.trading.algoritmico.api/AGENTS.md` — Backend skill routing (.NET 10)
- `app.trading.algoritmico.web/AGENTS.md` — Frontend skill routing (Angular 21)

---

## User Skills (Global — `~/.claude/skills/`)

| Skill | Trigger |
|-------|---------|
| go-testing | Go tests, Bubbletea TUI testing |
| skill-creator | Creating new AI skills |
| sdd-init | `/sdd-init` — Initialize SDD context |
| sdd-apply | `/sdd-apply` — Implement tasks |
| sdd-verify | `/sdd-verify` — Validate implementation |
| sdd-explore | `/sdd-explore` — Investigate ideas |
| sdd-propose | `/sdd-propose` — Create change proposal |
| sdd-spec | `/sdd-spec` — Write specifications |
| sdd-design | `/sdd-design` — Technical design |
| sdd-tasks | `/sdd-tasks` — Task breakdown |
| sdd-archive | `/sdd-archive` — Archive change |

---

## Universal Skills (`universal-skills/`)

### agent-orchestration
- **Path**: `universal-skills/agent-orchestration/SKILL.md`
- **Triggers**: Features spanning multiple projects (Host + Web), multi-step planning, complex delegation
- **Compact Rules**:
  - Use Agent Manager (Orchestrator) role for planning/delegation
  - Define API contracts before implementation (Contract-First)
  - Backend Dev: clean-architecture + csharp-dotnet skills
  - Frontend Dev: angular + design-core skills

### documentation-standard
- **Path**: `universal-skills/documentation-standard/SKILL.md`
- **Triggers**: Writing TSDoc/JSDoc, creating/updating READMEs, documenting ADRs
- **Compact Rules**:
  - Every exported member MUST have TSDoc; @reactive tag for Signals
  - Feature READMEs must include Business Rules section
  - Significant architectural changes require ADR records
  - No TODO comments without ticket numbers

### git-commit
- **Path**: `universal-skills/git-commit/SKILL.md`
- **Triggers**: Making git commits, completing features, `/commit` command
- **Compact Rules**:
  - Imperative mood: add, fix, refactor (NOT past tense); MAX 60 chars subject
  - Types: feat, fix, refactor, chore, docs, test
  - Scope derived from branch name

### git-pr
- **Path**: `universal-skills/git-pr/SKILL.md`
- **Triggers**: After push + feature completion, `/pr` command
- **Compact Rules**:
  - PR title follows Conventional Commit format
  - Mandatory sections: Context, Changes, Testing Steps, Screenshots, Checklist

### skill-creator
- **Path**: `universal-skills/skill-creator/SKILL.md`
- **Triggers**: User requests new skill, converting docs into reusable skill
- **Compact Rules**:
  - kebab-case names; SKILL.md in `{skill-name}/` folder
  - Output in English regardless of input language

### testing-standards
- **Path**: `universal-skills/testing-standards/SKILL.md`
- **Triggers**: Writing any test (Unit/Integration/E2E), reviewing test PRs, naming test methods
- **Compact Rules**:
  - AAA Pattern: Arrange, Act, Assert (mandatory)
  - Naming: `MethodName_Scenario_ExpectedResult`
  - Pyramid: 70% Unit, 20% Integration, 10% E2E
  - FIRST: Fast, Independent, Repeatable, Self-validating, Timely

---

## Agent/Orchestration Skills (`.agents/skills/`)

### analyst-requeriment
- **Path**: `.agents/skills/analyst-requeriment/SKILL.md`
- **Triggers**: "Analiza este ticket [URL]", requirement review, test plan generation
- **Compact Rules**:
  - Generate markdown report: `Analys_Report/REQ-[TicketID]-[BriefName].md`
  - STOP before execution — ask for user approval

### architecture-documenter
- **Path**: `.agents/skills/architecture-documenter/SKILL.md`
- **Triggers**: Starting new feature, changing architectural patterns, new integrations, finishing features
- **Compact Rules**:
  - Create ADR for "how" decisions; update `documentation/architecture/SYSTEM_BLUEPRINT.md`
  - Feature docs: `documentation/functionality/{feature-name}.md`

### check-execution-status
- **Path**: `.agents/skills/check-execution-status/SKILL.md`
- **Triggers**: "Are projects running?", system health check, verify execution status
- **Compact Rules**:
  - Backend: `curl -k https://localhost:5001/health`
  - Frontend: `Test-NetConnection -ComputerName localhost -Port 4200`

### document-functionality
- **Path**: `.agents/skills/document-functionality/SKILL.md`
- **Triggers**: After completing feature with new vertical, creating new API/UI patterns
- **Compact Rules**:
  - Store in: `documentation/functionality/{feature-name}.md`
  - Documentation is not "finished" until code is verified and markdown created

### documentation-features
- **Path**: `.agents/skills/documentation-features/SKILL.md`
- **Triggers**: Creating comprehensive feature docs, preparing feature for replication
- **Compact Rules**:
  - Output: `[ProjectRoot]/documentation/features/[feature-name-kebab-case].md`
  - Sections: Overview, Architecture, Backend Layer, Frontend Layer, Database, Replication Steps

### frontend-standards
- **Path**: `.agents/skills/frontend-standards/SKILL.md`
- **Triggers**: Building modals/dialogs/notifications/buttons, implementing standard UI patterns
- **Compact Rules**:
  - Modals: `.modal-overlay`, `.confirmation-modal`; `showConfirm(title, message, action)`
  - Buttons: Primary (blue), Secondary (white/bordered), Danger (red)

### git-compare-branches
- **Path**: `.agents/skills/git-compare-branches/SKILL.md`
- **Triggers**: "Compare branch X with Y", conflict detection, merge planning
- **Compact Rules**:
  - `git diff --name-status target..source`; never merge without user confirmation

### grid-standard
- **Path**: `.agents/skills/grid-standard/SKILL.md`
- **Triggers**: Creating list-view/monitor page, refactoring existing grid, adding filtering/sorting/pagination
- **Compact Rules**:
  - Backend: `GridRequestDto`/`GridResponseDto`; server-side filtering/pagination
  - Frontend: `MatSort`, `MatPaginator`; global filter with 400ms debounce

### job-orchestrator
- **Path**: `.agents/skills/job-orchestrator/SKILL.md`
- **Triggers**: Managing background jobs, changing schedules, triggering jobs manually
- **Compact Rules**:
  - Entity: `ScheduledJob`; API: GET/PUT `/api/scheduler`, POST `/api/scheduler/{id}/trigger`
  - 5-part cron syntax; validate expressions

### perform-testing
- **Path**: `.agents/skills/perform-testing/SKILL.md`
- **Triggers**: "Run tests", "Test the application", "Validate the module"
- **Compact Rules**:
  - Frontend: `pnpm run test -- --watch=false --browsers=ChromeHeadless`
  - Backend: `dotnet test`; always report Pass/Fail counts

### root-orchestrator
- **Path**: `.agents/skills/root-orchestrator/SKILL.md`
- **Triggers**: Feature involving both Database/Backend API AND Frontend UI/Logic
- **Compact Rules**:
  - Phase 1: Backend (Data Layer → API Layer → Repositories/Services)
  - Phase 2: Frontend (Data Services → Angular Features/Components)

### visual-qa
- **Path**: `.agents/skills/visual-qa/SKILL.md`
- **Triggers**: Testing UI layout responsiveness, visual stress testing
- **Compact Rules**:
  - Viewports: Mobile (375px), Tablet (768px), Desktop (1366px)
  - Check overflow, zero state, many items edge cases

---

## Backend Agent Skills (`app.trading.algoritmico.api/.agents/skills/`)

### create-skill
- **Path**: `app.trading.algoritmico.api/.agents/skills/create-skill/SKILL.md`
- **Triggers**: "Create a new skill called [Name]", adding new agent capabilities

### generate-hangfire
- **Path**: `app.trading.algoritmico.api/.agents/skills/generate-hangfire/SKILL.md`
- **Triggers**: "Create a background job", "Setup hangfire", "Schedule a recurring task"
- **Compact Rules**:
  - SET TenantId manually in job class (no HTTP context available)
  - Register: `AddHangfire()` + `AddHangfireServer()` + `app.UseHangfireDashboard()`

### manage-db
- **Path**: `app.trading.algoritmico.api/.agents/skills/manage-db/SKILL.md`
- **Triggers**: "Add migration", "Update db", "Check database"
- **Compact Rules**:
  - Build first; stop on failure
  - `dotnet ef migrations add [Name] --project Infrastructure --startup-project WebAPI --output-dir Persistence/Migrations`

### refit-client
- **Path**: `app.trading.algoritmico.api/.agents/skills/refit-client/SKILL.md`
- **Triggers**: Creating new external integration, adding endpoints to existing client
- **Compact Rules**:
  - Interface in `Application/Interfaces`; register in `Infrastructure/DependencyInjection.cs`
  - Attach Polly retry (3x exponential) + circuit breaker (5 failures, 30s)

### run-host
- **Path**: `app.trading.algoritmico.api/.agents/skills/run-host/SKILL.md`
- **Triggers**: "Run the backend", "Start the API", "Build and run host"
- **Compact Rules**:
  - `dotnet run --project src/AppTradingAlgoritmico.WebAPI --launch-profile Development`
  - Verify: Swagger at `https://localhost:5001/swagger`

### scaffold-feature
- **Path**: `app.trading.algoritmico.api/.agents/skills/scaffold-feature/SKILL.md`
- **Triggers**: "Create a new feature for [Entity]", "Scaffold [Entity]"
- **Compact Rules**:
  - Domain → Application → Infrastructure → WebAPI layers in order
  - Register in `DependencyInjection.cs`; run `manage-db` skill after

---

## Backend Domain Skills (`app.trading.algoritmico.api/skills/`)

### auditing
- **Path**: `app.trading.algoritmico.api/skills/auditing/skill.md`
- **Triggers**: Implementing HTTP auditing middleware, modifying AuditLog entity, handling sensitive data in logs
- **Compact Rules**:
  - Filter: Skip GET, HEAD, OPTIONS (read-only)
  - Mask: Replace sensitive keys (password, token, secret, apiKey, credentials) with `"***"`
  - Truncate: Limit payloads to 1024 chars

### clean-architecture
- **Path**: `app.trading.algoritmico.api/skills/clean-architecture/skill.md`
- **Triggers**: Creating domain entities, implementing services/repositories, adding endpoints, reviewing PRs
- **Compact Rules**:
  - Dependencies point inward; Domain knows nothing about outer layers
  - Interfaces in Application, Implementations in Infrastructure
  - CQRS: REST = Commands (POST/PUT/DELETE), GraphQL = Queries (GET/Read)
  - Structured Logs with UserId + TraceId; AutoMapper for all Entity↔DTO mapping

### csharp-dotnet
- **Path**: `app.trading.algoritmico.api/skills/csharp-dotnet/skill.md`
- **Triggers**: Writing C# code in any layer, defining interfaces/classes/records, async methods
- **Compact Rules**:
  - File-scoped namespaces: `namespace X.Y.Z;` (no braces)
  - Nullable Reference Types enabled; `required` keyword for mandatory properties
  - All async methods accept `CancellationToken = default`
  - Primary Constructors for DI (C# 12+)

### dotnet-automation
- **Path**: `app.trading.algoritmico.api/skills/dotnet-automation/skill.md`
- **Triggers**: `dotnet build`, `dotnet test`, verifying .NET code
- **Compact Rules**:
  - Classify: Compilation (CS/MSB) vs Logic vs Runtime
  - Max 2 automatic correction attempts; then report to user

### entity-framework
- **Path**: `app.trading.algoritmico.api/skills/entity-framework/skill.md`
- **Triggers**: Configuring DbContext/DbSets, creating migrations, Fluent API, interceptors
- **Compact Rules**:
  - Fluent API in `OnModelCreating` (NO Data Annotations)
  - `SaveChangesInterceptor` for auditing and auto-assigning TenantId
  - Seeding via `IDataSeeder` with async `SeedAsync()`

### external-integrations
- **Path**: `app.trading.algoritmico.api/skills/external-integrations/skill.md`
- **Triggers**: Consuming broker APIs, integrating market data providers, typed HTTP clients
- **Compact Rules**:
  - Refit interfaces in `Application/Interfaces`
  - `AuthHeaderHandler` for token/API key propagation
  - Config in `appsettings.json: ExternalServices:[ServiceName]:BaseUrl`

### multitenancy
- **Path**: `app.trading.algoritmico.api/skills/multitenancy/skill.md`
- **Triggers**: X-Tenant-Id validation, filtering data by tenant, TenantId propagation
- **Compact Rules**:
  - `X-Tenant-Id` header required on all requests (except public routes)
  - `HasQueryFilter` on ALL entities with TenantId
  - `IMustHaveTenant` interface for tenant-aware entities

### security
- **Path**: `app.trading.algoritmico.api/skills/security/skill.md`
- **Triggers**: Configuring JWT auth, defining access policies, CORS, secrets management
- **Compact Rules**:
  - JWT: `ClockSkew = TimeSpan.Zero`; validate issuer, audience, lifetime, signing key
  - CORS: Never `AllowAnyOrigin`; list explicit origins
  - Rate Limiting: 100 permits/minute; SQL always via LINQ/parameterized

### testing
- **Path**: `app.trading.algoritmico.api/skills/testing/skill.md`
- **Triggers**: Creating Unit Tests, Integration Tests, configuring mocks, WebApplicationFactory
- **Compact Rules**:
  - Stack: xUnit + FluentAssertions + Moq + `WebApplicationFactory`
  - Naming: `MethodName_StateUnderTest_ExpectedBehavior`
  - `result.Should().Be(5)` — always FluentAssertions syntax

### webapi-patterns
- **Path**: `app.trading.algoritmico.api/skills/webapi-patterns/skill.md`
- **Triggers**: Creating REST Controllers, GraphQL Queries/Mutations, Middleware
- **Compact Rules**:
  - `[ApiController]`, `[Route("api/[controller]")]`, inject via constructor
  - XML docs on all endpoints; validate ModelState at start
  - GraphQL: `[UseProjection]`, `[UseFiltering]`, `[UseSorting]`, `IQueryable<T>`

---

## Frontend Web Skills (`app.trading.algoritmico.web/skills/`)

### angular
- **Path**: `app.trading.algoritmico.web/skills/angular/SKILL.md`
- **Triggers**: Creating Angular components/directives/pipes, writing services, refactoring NgModules
- **Compact Rules**:
  - Signals: `count = signal(0)`; `computed(() => count() * 2)`
  - Standalone components (default); modern DI: `inject(HttpClient)`
  - Control Flow: `@if`, `@for`, `@switch` (NOT `*ngIf`, `*ngFor`)
  - `changeDetection: ChangeDetectionStrategy.OnPush`

### angular-automation
- **Path**: `app.trading.algoritmico.web/skills/angular-automation/SKILL.md`
- **Triggers**: `ng build`, `ng test`, `ng lint`, verifying Angular code
- **Compact Rules**:
  - Test: `pnpm run test -- --watch=false --browsers=ChromeHeadless`
  - Build before test (catches template errors)
  - Max 2 auto-correction attempts

### clean-architecture (frontend)
- **Path**: `app.trading.algoritmico.web/skills/clean-architecture/SKILL.md`
- **Triggers**: Designing feature folder structures, reviewing PRs for violations, detecting circular deps
- **Compact Rules**:
  - UI → Application UseCases → Domain (inward only)
  - No business logic in UI Components; no circular deps

### data-services
- **Path**: `app.trading.algoritmico.web/skills/data-services/SKILL.md`
- **Triggers**: Creating API services, defining DTOs/Domain Models, HTTP handling, Signal-based state
- **Compact Rules**:
  - Pipeline: `DTO (backend) → Domain Model (UI) → Mapper (pure function)`
  - `toSignal(http.get(...).pipe(map(mapFn)), { initialValue: [] })`
  - Forbidden: `any` for API responses; mapping in Component; hardcoded Base URL

### design-core
- **Path**: `app.trading.algoritmico.web/skills/design-core/SKILL.md`
- **Triggers**: Creating/styling UI components, building dashboard layouts, implementing themes
- **Compact Rules**:
  - CSS Variables: `--bg-app`, `--bg-surface`, `--color-gain` (green), `--color-loss` (red)
  - BEM: `.c-card__header--highlighted`
  - Forbidden: Hardcoded hex codes; px for spacing (use rem); `@import` (use `@use/@forward`)

### i18n
- **Path**: `app.trading.algoritmico.web/skills/i18n/SKILL.md`
- **Triggers**: Creating new Angular components, adding user-facing text, managing `assets/i18n/`
- **Compact Rules**:
  - Naming: `FEATURE.COMPONENT.ELEMENT` (UPPERCASE dot notation)
  - Dual-Entry: Add to `en.json` AND `es.json` simultaneously (mandatory)
  - Pipe over Service: `{{ 'KEY' | translate }}` preferred

### security (frontend)
- **Path**: `app.trading.algoritmico.web/skills/security/SKILL.md`
- **Triggers**: Protecting routes, adding tokens to requests, JWT storage, dynamic HTML rendering
- **Compact Rules**:
  - Route Guards: `CanActivateFn`; return `router.createUrlTree()` if denied
  - Auth Interceptor: Attach `Authorization: Bearer {token}`
  - All routes under `/dashboard` protected by `authGuard` (inherited by children)

### run-web
- **Path**: `app.trading.algoritmico.web/universal-skills/run-web/SKILL.md`
- **Triggers**: "Run the frontend", "Start the web app", "Launch Angular"
- **Compact Rules**:
  - `pnpm install` (if first run); `pnpm start` (runs `ng serve`)
  - Port 4200; backend at `https://localhost:5001` must be running first

---

## Summary

| Category | Count |
|----------|-------|
| User Skills (Global) | 11 |
| Universal Skills | 6 |
| Agent/Orchestration Skills | 12 |
| Backend Agent Skills | 6 |
| Backend Domain Skills | 10 |
| Frontend Web Skills | 8 |
| **TOTAL** | **53** |
