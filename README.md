# BENT — Algorithmic Trading Platform

> **v0.3.0** | Personal platform for algorithmic trading management — from strategy development in Strategy Quant X to live account administration across brokers, prop firms, and capital managers.

---

## Features

### Strategy Workflow (SQX Pipeline)
Full pipeline dashboard to manage the lifecycle of trading strategies created in Strategy Quant X.

- **Pipeline stages**: Builder → Retester → Optimizer → Demo → Live
- **Batch management**: Each batch (remesa) travels independently through the pipeline, associated to an asset + timeframe + building block
- **3-level dashboard**: Asset Overview (cards) → Pipeline Detail (grid) → Stage Detail (KPIs + strategy table)
- **ZIP upload**: Upload .sqx strategy files in bulk with automatic pseudocode extraction from settings.xml
- **Inline KPI editing**: Sharpe Ratio, Ret DD/Ratio, WinRate, ProfitFactor, TotalTrades, NetProfit, MaxDrawdown
- **Building Blocks CRUD**: Manage SQX BB configurations (.sqb file upload with XML parsing)

### Trading Accounts
- Connect and manage broker/platform accounts (MT4/MT5)
- Darwinex (demo/live) and Axi support
- AES-256 encryption for account credentials

### User Preferences
- Multi-language (EN/ES) with instant header toggle — default Spanish
- Dark/Light theme with instant header toggle — default dark
- Preferences persisted in user profile via backend API

### Authentication & Security
- ASP.NET Core Identity + JWT Bearer tokens
- Role-based authorization: Admin, Trader, Viewer
- Functional route guards + HTTP interceptor
- Sensitive data masked in logs

### Planned
- Risk management dashboard
- Deployment tracker (demo/live accounts)
- Prop firm challenge tracker (FTMO, The Trading Pits)
- Capital manager performance tracking (Axi Select, Darwinex)
- Automated KPI extraction from .sqx binary format

---

## 🧱 Architecture

This is a **monorepo** containing two projects:

```
app-trading-algoritmico/
├── app.trading.algoritmico.api/     # Backend — .NET 10 / Clean Architecture
└── app.trading.algoritmico.web/     # Frontend — Angular 21 / Signals
```

### Backend (`app.trading.algoritmico.api`)
- **Framework**: .NET 10 — ASP.NET Core
- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → WebAPI)
- **Database**: SQL Server — Entity Framework Core 10 (Fluent API, Migrations)
- **Authentication**: ASP.NET Core Identity + JWT Bearer
- **API**: REST (Commands) + GraphQL via HotChocolate (Queries)
- **Observability**: Serilog structured logging + OpenTelemetry
- **Testing**: xUnit + FluentAssertions + Moq

### Frontend (`app.trading.algoritmico.web`)
- **Framework**: Angular 21 — Standalone Components + Signals
- **Styling**: SCSS — Dark-first trading dashboard theme
- **Package Manager**: pnpm
- **Auth**: JWT Interceptor + Functional Route Guards
- **i18n**: @ngx-translate (es/en)

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) + [pnpm](https://pnpm.io/)
- SQL Server (local or Docker)

### Run Both Projects

```bash
# Recommended — via workflow
@[/run-all]
```

**Manually:**

```bash
# Backend
cd app.trading.algoritmico.api
dotnet run --project src/AppTradingAlgoritmico.WebAPI --launch-profile Development

# Frontend (separate terminal)
cd app.trading.algoritmico.web
pnpm install
pnpm start
```

### Access Points
| Service | URL |
|---------|-----|
| Web App | http://localhost:4200 |
| Swagger UI | https://localhost:5001/swagger |
| GraphQL | https://localhost:5001/graphql |

---

## 🔑 Default Credentials (Development Seed)

| Field | Value |
|-------|-------|
| Email | `admin@trading.local` |
| Password | `Admin@123!` |
| Role | `Admin` |

> ⚠️ **Never use development seeds in production.**

---

## 📁 Repository Structure

```
app-trading-algoritmico/
│
├── app.trading.algoritmico.api/         # .NET 10 Backend
│   ├── src/
│   │   ├── AppTradingAlgoritmico.Domain/
│   │   ├── AppTradingAlgoritmico.Application/
│   │   ├── AppTradingAlgoritmico.Infrastructure/
│   │   └── AppTradingAlgoritmico.WebAPI/
│   └── tests/
│       ├── AppTradingAlgoritmico.UnitTests/
│       └── AppTradingAlgoritmico.IntegrationTests/
│
├── app.trading.algoritmico.web/         # Angular 21 Frontend
│   └── src/
│       ├── app/
│       │   ├── core/
│       │   ├── features/
│       │   └── shared/
│       └── styles/
│
├── .agents/                             # Agent orchestration skills & workflows
├── universal-skills/                    # Shared agent skills (git, docs, testing)
├── AGENTS.md                            # Root orchestration protocol
├── README.md                            # This file
└── CHANGELOG.md                         # Version history
```

---

## 📄 License

Private — Personal use only.
