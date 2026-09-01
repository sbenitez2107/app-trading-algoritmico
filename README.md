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
- **Backtest trade-list import**: Import a strategy's AlgoWizard trade list from its row in the account grid, plus the Optimizer's Walk-Forward Results export. Two run kinds are kept apart on purpose — a *Deploy* run (last window's parameters) backs sizing and correlation but can never yield an out-of-sample claim; an *Evaluation* run (previous window's parameters) is what makes the trades after the walk-forward boundary genuinely unseen
- **Per-symbol point value calibration**: derived from MAE on stop-loss exits only, since MAE equals the stop distance solely when the stop closed the trade. Samples disagreeing by more than 0.5% are reported as inconsistent rather than calibrated
- **Readiness marker**: each strategy row shows whether it has no backtest, a deploy run only (sizing available but not honestly evaluable), or material ready for evaluation

### Trading Accounts
- Connect and manage broker/platform accounts (MT4/MT5)
- Darwinex (demo/live), FTMO and Axi support
- AES-256 encryption for account credentials
- **Per-strategy monthly returns matrix** (strategies × months, sortable, with year navigation and a selectable cell metric: return, max drawdown within the month, underwater depth, or win rate)
  - Filter by strategy name, symbol, timeframe, or the sign of the year total
  - Per-month gates — `Max DD <`, `Return >`, `W/L >`, minimum trades per month — where EVERY month has to clear the bar, so one bad month disqualifies a strategy however good its year total looks
  - A Total row above the grid aggregates every column across the filtered strategies, recomputed live as you filter
  - Create a portfolio straight from the filtered set, at equal weights

### Strategy Portfolios
Per-platform portfolios (under Darwinex / FTMO / Axi) that combine strategies of a single broker + account type (Demo or Live).

- **Builder**: pick Demo/Live, filter by account, multi-select strategies with SQX (Backtest) + MT4 (Live) KPI columns
- **SQX-style combination**: strategies combine at full size (weight = position multiplier); drawdown / Sharpe / profit factor / SQN recomputed on the merged stream (diversification, not averages)
- **Combined stats**: KPI strip + full stats block, equity curve (Lightweight Charts), monthly returns heatmap, and a profit-by-symbol donut
- **Contribution curves**: overlay each member's cumulative weighted P/L on the combined equity curve to see who built the equity and who dragged it down — they sum exactly to the combined gain. A grey "ver todas" mode draws the whole fan at any member count, and hovering any line names its strategy. The combined curve itself can be toggled off so the contributions get the full canvas
- **Risk**: Historical VaR (95%/99%, daily, rolling 250d) and a rolling 30-day monthly VaR estimate + per-broker guardrails typed by kind — `LossLimits` (daily/max loss, profit target, with VaR-vs-limit headroom) or `VarTarget` (monthly VaR band and implied Risk Engine multiplier, for services that define no loss limits). Limits are user-sourced and verified, never hardcoded
- **List management**: delete a portfolio from the grid, preview any portfolio's monthly returns in a hover tooltip, or switch the whole list to a portfolios × months matrix with year navigation and a selectable cell metric — return, max drawdown within the month, underwater depth, or win rate (the choice is remembered per screen)

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
