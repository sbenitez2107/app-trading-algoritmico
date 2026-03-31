# 📈 App Trading Algorítmico

> **Personal platform for algorithmic trading management** — from strategy development in Strategy Quant to live account administration across brokers, prop firms, and capital managers.

---

## 🎯 Purpose

This is a personal full-stack application designed to centralize and streamline all activities related to algorithmic trading. The platform covers the full lifecycle:

1. **Strategy Development** — tooling to manage and track strategies built in Strategy Quant X.
2. **Risk Management** — monitoring and controlling risk parameters across accounts and strategies.
3. **Deployment Management** — orchestrating strategy go-lives on demo and live accounts.
4. **Account Management** — administering accounts across brokers, prop firms, and capital managers.

---

## 🏗️ Domain Areas

### 📐 Strategy Management (Strategy Quant X)
- Track strategy versions, backtests, and optimization runs.
- Store walk-forward analysis results and robustness metrics.
- Manage the strategy lifecycle: development → validation → deployment → monitoring.

### 🛡️ Risk Management
- Define and enforce risk parameters per strategy and per account.
- Track drawdown limits, lot sizing rules, and exposure caps.
- Monitor real-time account equity and margin levels.

### 🚀 Deployment & Execution
- Manage strategy deployments on **demo** and **live** accounts.
- Track go-live dates, version history, and performance since deployment.
- Monitor execution quality (slippage, spread, fill rate).

### 💼 Account & Entity Management
Centralized administration for all external entities:

| Entity Type | Examples |
|-------------|----------|
| **Brokers** | Axi, Darwinex Zero, IC Markets |
| **Capital Managers** | Axi Select, Darwinex (Darwin) |
| **Prop Firms** | FTMO, The Trading Pits, My Forex Funds |

Manage credentials, account status, balance history, challenge phases (prop firms), and performance metrics per entity.

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
