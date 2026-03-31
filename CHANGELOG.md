# Changelog

All notable changes to **App Trading Algorítmico** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned
- Strategy management module (Strategy Quant X integration)
- Risk management dashboard
- Deployment tracker (demo/live accounts)
- Account management for brokers, prop firms, and capital managers
- Prop firm challenge phase tracker (FTMO, The Trading Pits)
- Capital manager performance tracking (Axi Select, Darwinex)
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
