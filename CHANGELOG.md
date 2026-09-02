# Changelog

All notable changes to **App Trading Algorítmico** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.22.1] - 2026-09-01

### Fixed
- **The readiness column showed its translation key instead of a label.** Every row of the account grid read `SQX.BACKTESTS.READINESS_EVALUABLE` verbatim. The translations existed and nothing resolved them; they now update with the language without a reload.
- **A CSV with only a header was accepted as a successful import.** It created a run holding no trades, the readiness marker then reported that strategy as sizeable on evidence that did not exist, and re-importing over an existing slot deleted the trades already there. Such a file is now a named rejection, and the marker requires actual trades before it claims anything.
- **The walk-forward export skipped the length validation the trade list already had.** An over-long parameter list or file name reached the database as a truncation error no retry can recover from, instead of the named rejection the shared field-length contract promises.
- **Importing both slots at once could report a failure for data that landed.** The two requests race to create the same symbol's calibration row; the loser hit the unique index after its own run and trades had already been committed. The calibration write now resolves the conflict, and a calibration that still fails is surfaced as a warning beside a successful import rather than as a failed one.
- **The backtests screen showed "nothing imported yet" when the backend was unreachable.** A failed load now says so, instead of rendering identically to an empty database.
- **Rolling the backtest schema migration back threw instead of rolling back.** It recreated unique indexes over columns it had just filled with a constant, so any account with more than one imported run could not revert. Note the trade-off now made explicit: the rollback discards imported backtest runs and trades, and says so at the point where it happens.
- **An out-of-sample boundary could be taken from another strategy's walk-forward export.** The resolver never checked that the export belonged to the run's strategy, and the tests paired two different strategies and asserted that as correct.

### Changed
- The design document's out-of-sample section described the opposite of the implemented behaviour — an empty result where the code deliberately yields none at all — along with several type names that were renamed before release. Corrected, since three production comments cite it by name as the authority.

---

## [0.22.0] - 2026-09-01

### Added
- **Import SQX backtest trade lists per strategy** — a new action on each row of an account's strategy grid opens a dialog that accepts the strategy's AlgoWizard exports. The strategy is known from the row, so a run is attributed by an explicit foreign key; nothing is inferred from a filename.
- **Two run kinds, each answering only what it can support.** A *Deploy* run carries the last walk-forward window's parameters and backs sizing, risk normalisation, correlation and breach work — it can never yield an out-of-sample claim. An *Evaluation* run carries the previous window's parameters, so trades after the boundary are genuinely unseen. Measured on the reference strategy: with deployed parameters only 3 of 329 trades fall past the boundary, against 23 over 429 days with the previous window's.
- **Walk-forward export import** — the SQX Optimizer's Walk-Forward Results table becomes its own artefact, owning the out-of-sample boundary date and the per-window IS/OOS KPIs. A run imported before its export stays amber and turns green when the export arrives, with no re-import.
- **Per-symbol point value calibration**, derived from MAE on stop-loss exits only. MAE equals the stop distance solely when the stop is what closed the trade, so no other exit is used; profit is never used, because it carries spread and commission. A symbol whose samples disagree by more than 0.5% is reported as inconsistent rather than calibrated.
- **Readiness marker on the strategy grid** — no backtest, deploy run only (sizing available but not honestly evaluable), or ready for evaluation. It answers which strategies can be used, not which have been touched.
- **Backtests screen** listing imported runs and symbol calibrations with the evidence behind each.

### Changed
- Backtest trades live in their own tables and never touch `StrategyTrades`. SQX ticket numbers are not unique across runs — 27 collide between two exports of the same strategy as genuinely different trades — so reusing the live import's upsert key would corrupt data silently.
- Backtest imports run each file in its own transaction through a per-attempt database context, so a retry cannot silently drop a column update or duplicate an entity graph. Field lengths are validated in the parser against a single shared source of truth, so an over-long value is a named row rejection instead of a database error that aborts the batch.
- The CSV parsers pin their decimal convention per column: the trade list uses dots for prices and commas for money, while the walk-forward export uses commas throughout except inside its parameters field, where the roles invert. A mismatched token fails loudly rather than being reinterpreted.

---

## [0.21.0] - 2026-08-29

### Added
- **Filter bar on the account monthly matrix** — strategy name search, symbol picker, timeframe picker, a positive/negative toggle on the year total, and numeric thresholds, all composing with AND. The row count shows how much the filter narrowed the account, and filters are deliberately transient: a narrowed matrix surviving a reload reads as missing data.
- **Per-month threshold gates** — `Max DD <`, `Return >`, `W/L >` and a monthly trade count. EVERY month that reports the quantity has to clear the bar, so a single bad month disqualifies a strategy however good its year total looks. The gates read the raw months rather than the selected metric, so they keep biting while the matrix shows something else, and a strategy with no months in the year is excluded — an absent month is not a passing month.
- **Trade count filters** — one on the year total, one as the per-month gate above, for screening out strategies whose numbers rest on too few trades.
- **Timeframe (TF) column** — sortable, plus its own picker in the filter bar. Empty for strategies that never came from a parsed SQX report.
- **Total row above the grid** — every month column and the year column aggregated across the currently filtered strategies, recomputed as you filter so the effect on the book is visible while choosing rather than after scrolling. Coloured by sign, following the same rule as the rest of the screen: the win rate splits at 50%, and a drawdown is never green.
- **Create a portfolio from the filtered strategies** — a dialog asking only for name and starting capital; broker, account type and equal weights come from the account the matrix belongs to. Weights are adjusted afterwards in the portfolio detail.

### Changed
- **Win rate cells lead with their trade counts** — `3/1 (75%)` instead of `75.00%`, in both the per-strategy and the portfolios matrices, and in the year column. The counts carry the confidence the percentage hides: 3/1 and 30/10 are both 75%, and only one of them means anything. They also make the decimals redundant, so the percentage rounds.
- **`GET /api/trading-accounts/{id}/strategies/monthly-returns` now returns `timeframe`** — read from the strategy, no extra query and no migration.

---

## [0.20.0] - 2026-08-23

### Added
- **Toggle the combined curve on the portfolio equity chart** — a **Combinada** button next to the ghost-mode toggle hides or shows the combined line. Hiding it hands the whole canvas to the contribution curves, which otherwise sit compressed against their own axis while being compared to a line two orders of magnitude larger. The Max DD marker and the stagnation band belong to the combined curve and follow its visibility, so the hand-drawn band no longer paints over an empty chart.

---

## [0.19.0] - 2026-08-23

### Added
- **Per-strategy contribution curves on the portfolio equity chart** — the combined curve can now be overlaid with each member's cumulative **weighted** P/L, so you can see who built the equity and who dragged it down. These are contribution curves, NOT each strategy's standalone equity: a standalone curve runs on the account's full initial balance and would not reconcile with the combined line next to it. Weighting by portfolio weight makes the decomposition exact — the final contributions sum to the combined curve's gain over initial capital.
- **`GET /api/portfolios/{id}/member-equity-curves`** — every member's contribution series in one request, computed from the same bulk trade load the combined curve already performs, so the chart never fans out into one request per strategy. New `PortfolioMemberEquityCurveDto`.
- **"Ver todas" ghost mode** — draws every remaining member as a faint grey line so the shape of the fan is readable at any member count. Ghosts carry no palette, so they are not subject to the eight-line colour cap.
- **Hover identifies any line** — pointing at a curve names the strategy and shows its contribution at that date. This is what makes the ghost fan usable, since those lines have no colour identity.
- **The monthly matrix remembers the selected metric** — return, max drawdown, underwater or win rate now survives navigation, stored per screen so the portfolios matrix and the per-strategy matrix can sit on different metrics.

### Changed
- **Contribution legend extracted into its own component** — `ContributionLegendComponent` is presentational; the parent keeps ownership of selection state and colour assignment.
- **Production build budgets aligned with the `docker` configuration** (initial 1MB warning / 2MB error, component styles 12kB error). The `production` entry still carried the stock Angular CLI defaults, which contradicted the calibrated values the `docker` configuration has used for a long time. `ng build` completes again.

### Security
- **AngleSharp 1.1.2 → 1.7.1** — patches GHSA-pgww-w46g-26qg (moderate). AngleSharp backs the SQX HTML report and MT statement parsers; the parser and import test suites (50 tests, including one over a real HTML fixture) pass unchanged.
- **Microsoft.EntityFrameworkCore.Sqlite 10.0.0 → 10.0.11** — pulls a patched `SQLitePCLRaw.lib.e_sqlite3`, closing GHSA-2m69-gcr7-jv3q (high, test-only). `dotnet list package --vulnerable --include-transitive` now reports all five projects clean.

### Removed
- **Redundant `Microsoft.Extensions.Configuration.Abstractions` package reference** from the Infrastructure project (NU1510) — it already arrives transitively. Removing it is what surfaced both advisories above, which it had been masking under `-warnaserror`.

---

## [0.18.0] - 2026-08-21

### Added
- **Selectable metric in the monthly matrices** — both the portfolios × months view and the per-strategy view in the account detail gain a metric switch: compounding **Return** (default), **Max DD within month**, **Underwater**, and **W/L** (win rate). The two drawdown metrics are offered side by side because they answer different questions: *Max DD within month* resets its peak on the 1st, so a cell reports how much that month hurt and reads 0 for an up-only month; *Underwater* carries the all-time peak (the same convention as the headline Max DD column), so one bad month keeps surfacing until a new high is made. Win-rate cells expose the raw win/loss counts on hover.
- **Per-month drawdown and win/loss data** — `MonthlyReturnDto` now carries `MaxDrawdownPercent`, `UnderwaterPercent`, `WinCount` and `LossCount`, computed in the same pass over the month's trades, so no extra query or roundtrip is involved.
- **Typed funding guardrails (`GuardrailKind`)** — guardrails are discriminated into `LossLimits` (Other/FTMO/Axi) and `VarTarget` (Darwinex Zero), so a service whose rulebook defines no daily-loss limit is no longer forced to invent one. The risk-limits modal switches its field set with the selected funding service, and `RiskLimitsService.UpsertAsync` validates per kind. Additive migration; existing rows become `LossLimits`.
- **Monthly VaR estimator** — rolling 30-calendar-day sums of the existing daily net series with the 5th percentile taken directly (no √t scaling). Reported portfolio-wide and per broker, alongside band position against `[floor, target]` and the implied Risk Engine multiplier. Labelled as an honest approximation wherever it appears.

### Changed
- **Monthly bucketing math now lives in one place.** `PortfolioAnalyticsCalculator` and `StrategyAnalyticsCalculator` each carried their own copy of the month grouping and compounding loop. Both now delegate to `AnalyticsSeries.BuildMonthlyReturns`, so a weighted portfolio stream and a single strategy's trades are measured by the exact same code and cannot drift.
- **Year column adapts to the selected metric.** Returns compound, drawdowns report the worst month (header becomes *Peor* / *Worst*), and the win rate is recomputed from the summed counts rather than averaged across months — averaging would weigh a 2-trade month like a 200-trade one. Sorting also flips to ascending for the drawdown metrics, where the smallest value is the best row.

---

## [0.17.0] - 2026-08-01

### Added
- **Delete portfolios** — the portfolios list now has an **Acciones** column with a delete button and a confirmation dialog. The removed row leaves the grid without a refetch; member strategies and their trades are untouched.
- **Monthly return tooltip per portfolio** — a new **Mensual** column shows each portfolio's monthly-returns heatmap on hover, so the figure is readable without opening the detail page. Reuses the same heatmap component rendered in the portfolio detail.
- **Portfolios monthly returns matrix** — a **Retorno mensual** button next to *+ Nuevo Portfolio* swaps the KPI grid for a portfolios × months view with year navigation and per-column sorting, mirroring the per-strategy Monthly Returns view in the account detail.
- **`GET /api/portfolios/monthly-returns?broker=`** — returns the monthly compounding returns of every portfolio of a broker in one request. Trades are bulk-loaded in a single query (no N+1); feeds both the matrix view and the row tooltips. New `PortfolioMonthlyReturnsDto`.
- **Per-strategy monthly returns** — the account detail (Cuentas Demo/Live) gains a **Monthly Returns** toggle showing a strategies × months matrix, backed by `GET /api/trading-accounts/{accountId}/strategies/monthly-returns` and a new `StrategyMonthlyReturnsDto`.
- **Portfolios list summary endpoint** — `GET /api/portfolios/summary?broker=` fuses each portfolio's header fields with its combined analytics KPIs so the grid loads in one roundtrip. New `PortfolioSummaryDto`.

### Fixed
- **AG Grid cell styling never applied (29 rules across 5 screens).** Styles written in component-scoped `.scss` compile to `.foo[_ngcontent-<hash>]`, but AG Grid builds its cell, header and `cellRenderer` DOM imperatively, so those nodes never carry the attribute and the rules could not match. A second variant used `:global(...)`, a CSS-Modules construct Angular neither understands nor strips, which emitted an invalid selector the browser discarded outright. All affected rules moved to `:host ::ng-deep`, anchored under each component's own grid class. Visible effects:
  - *Trades grids (portfolio + strategy)*: net profit now renders green/red, close-reason chips green/red/amber/grey, and open vs closed status are distinguishable.
  - *Expenses list*: the whole table now uses the app's theme tokens instead of AG Grid's stock palette; headers are uppercased, cells vertically centred, and the row action buttons lose their default browser chrome.
  - *SQX asset overview*: timeframe, stage and status render as coloured pills, and the three status states are visually distinct.
  - *Portfolios list*: profit/return/CAGR are colour-coded by sign and the account type is tinted Live/Demo.
  - *Account detail*: strategy-grid action buttons render as borderless icons with hover states, and rows show a pointer cursor.
- **Dangling CSS variable in the expenses grid** — `--ag-border-color` referenced `--color-border`, which is not defined anywhere (the token is `--border-color`). Harmless while the rule was dead; corrected as part of reviving it.

### Changed
- **Shared trades-grid cell styles are now a Sass mixin.** `shared/trades-grid/_trades-grid-cells.scss` exposes `@mixin trades-grid-cells` instead of bare rules, because `@use` is only legal at file root and therefore cannot be nested inside `:host ::ng-deep`. Consumers include it from within their own `::ng-deep` block.
- **Portfolios list row navigation** moved from `rowClicked` to `cellClicked` so clicking the Mensual or Acciones columns no longer opens the portfolio detail.
- **`PortfolioService` bulk loading refactored** — `GetSummariesAsync` and `GetMonthlyReturnsByBrokerAsync` now share one `LoadPortfoliosWithMemberInputsAsync` helper, keeping the query count constant regardless of how many portfolios exist.

---

## [0.16.1] - 2026-06-21

### Security
- **AutoMapper 13.0.1 → 16.1.1** to patch **CVE-2026-32933** (high-severity DoS — uncontrolled recursion on deeply-nested object graphs triggering an uncatchable `StackOverflowException`). Bumped `Microsoft.IdentityModel.Tokens` and `System.IdentityModel.Tokens.Jwt` 8.9.0 → 8.14.0 to satisfy AutoMapper 16's transitive requirement, and migrated the DI registration to the v16 API (`AddAutoMapper(cfg => cfg.AddMaps(...))`). Runs under the free AutoMapper Community license (no key required; emits a startup log notice only).

---

## [0.16.0] - 2026-06-21

### Added
- **Strategy equity curve** — the EA detail (Cuentas Demo/Live) now shows an equity curve above the trades grid, covering the period the strategy's trades span. New `GET /api/strategies/{id}/equity-curve` endpoint (one point per closed trade, running equity from the account's initial balance with drawdown from the running peak) reusing the shared `AnalyticsSeries`; new `StrategyEquityPointDto`.
- **Equity chart annotations** — both the strategy and portfolio equity charts now mark the **max drawdown** (a marker at the trough labelled with its % and $) and shade the **longest stagnation window** as a translucent vertical band.
- **Strategy detail from a portfolio** — clicking a strategy row in the portfolio **Composición** grid opens the same strategy analytics modal used in the demo/live account detail.

### Changed
- **Portfolio Composición — Win % (MT4 Live)** now prepends the won/lost trade counts, e.g. `(2/3) 40.00%`.
- **Account strategies grid** now shows the **MT4 (Live)** column group before **SQX (Backtest)** by default (saved column presets keep their own order).

---

## [0.15.0] - 2026-06-18

### Added
- **Portfolio combined trades list** — new **Lista de trades** tab in the portfolio detail (between Resumen and Composición) showing every trade of all member strategies combined, with a leading **Estrategia** column identifying each trade's source strategy and a pinned TOTAL row. Powered by a new `GET /api/portfolios/{id}/trades` endpoint (paged, status-filterable) that reuses the existing member-trades query; new `PortfolioTradeDto`.

### Changed
- **Portfolio Resumen KPI cards reordered**, with the trade metrics grouped into a single **Trades** card (Trades W/L · Win Rate · monthly & daily trade averages) at the end of the KPI strip.
- **Extracted a shared `shared/trades-grid` module** (column defs, helpers, row styling) now consumed by both the strategy and portfolio trades grids.

### Fixed
- **Trades grids capped at 50 rows**: the portfolio and strategy trades grids paginate client-side but only fetched the first 50 trades from the server, so a portfolio/strategy with more trades silently showed only 50. Both now load the full set, using the server's reported total count.

---

## [0.14.0] - 2026-06-13

### Added
- **Strategy Portfolios module** — build and analyze portfolios of strategies, scoped per platform. Available as a **Portfolios** submenu under **Darwinex**, **FTMO**, and **Axi**; each portfolio uses only that platform's accounts (Demo or Live).
  - **Portfolio builder** (`/{broker}/portfolios/new`): pick Demo/Live, filter by account, and multi-select strategies in an ag-grid with the same SQX (Backtest) + MT4 (Live) column groups as the strategies grid; create with name + total capital.
  - **SQX-style combination**: a portfolio combines member strategies at full size (weight = raw position-size multiplier, default 1 — Net Profit and trade counts SUM like an SQX portfolio); drawdown, Sharpe, profit factor, CAGR, SQN, exposure, Z-score and streaks are recomputed on the merged weighted trade stream (capturing diversification — not averaged).
  - **Overview** with combined KPI strip + full SQX-style stats block (Rendimiento y Riesgo, Trades incl. monthly averages), a **Lightweight Charts** equity curve, a **monthly returns heatmap** (missing months shown as 0%), and a **profit-by-symbol donut** with per-symbol return % and trade counts.
  - **Composition** tab: sortable ag-grid with editable weights, per-member Aporte $ + contribution %, a pinned combined TOTAL row, and SQX/MT4 KPI groups for comparison.
  - **Risk** tab: **Historical VaR** (95% / 99%, daily, rolling 250-day window) in currency and % of capital, per-service breakdown, and **prop-firm guardrails** — per-broker risk limits (daily loss / max loss / profit target / drawdown model) that the user configures and verifies (never hardcoded), with VaR-vs-limit headroom and breach detection.
  - **Backend**: `Portfolio` + `PortfolioStrategy` + `BrokerRiskLimits` entities, `PortfolioAnalyticsCalculator` (on-demand, no stale data), `PortfolioService`/`RiskLimitsService`, REST `PortfoliosController` + `RiskLimitsController`, EF Core migrations.
- **Axi Select** is now a full platform in the sidebar (Cuentas Demo / Live / Portfolios), using the shared broker-accounts module.

### Changed
- **Extracted the Darwinex account components into a shared `broker-accounts` module** consumed by Darwinex, FTMO and Axi via a route factory (`brokerAccountsRoutes`), with broker-aware navigation. Removed the hardcoded `/darwinex/` navigation paths.
- **`StrategyAnalyticsCalculator`** now delegates its daily-series, Sharpe, drawdown, streak, SQN, exposure and Z-score primitives to a shared `AnalyticsSeries` helper reused by the portfolio calculator (per-strategy Sharpe stays byte-identical).
- Added a `GET /api/strategies/candidates` endpoint returning strategies (with SQX + live KPIs) eligible to join a portfolio of a given broker + account type.

---

## [0.13.0] - 2026-05-30

### Added
- **Expenses Management Module**: complete feature for tracking professional trading expenses across all accounts, brokers, and providers.
  - **Backend (.NET)**: Domain entity `Expense` with enum `ExpenseCategory` (6 types: Mentoría IMOX, Servidor Hetzner, FTMO, WSF, Darwinex Zero, Servidor fxvps.pro), full CRUD `ExpenseService`, REST `ExpensesController` with analysis endpoints (monthly summaries, year-over-year, projections for next 12 months), EF Core migration applied.
  - **Frontend (Angular)**: `ExpenseService` with typed DTOs, standalone `ExpensesListComponent` and `ExpenseFormComponent` (reactive forms with validation), integrated into main-layout menu under "Gestión y Administración".
  - **Data Grid (ag-grid)**: sortable and filterable columns (Date, Description, Category, Notes, Amount USD, Actions), pagination (50 rows/page), responsive column widths, total amount displayed in footer.
  - **All currency in USD** as requested.

---

## [0.12.1] - 2026-05-16

### Added
- **Docker setup**: multi-stage `Dockerfile` for both projects (`api` on .NET 10 SDK → ASP.NET runtime, `web` on Node 22 → nginx 1.27), a root `docker-compose.yml` wiring the API + web + existing SQL Server container, an nginx reverse-proxy config for the SPA (`/api/` + `/swagger/` → API, SPA fallback, aggressive caching for hashed assets), and `.dockerignore` files for both projects. Local `.env` variables drive secrets (`DB_CONNECTION_STRING`, `JWT_KEY`, encryption keys) — nothing is hardcoded.
- **`docker` build configuration in `angular.json`**: dedicated Angular build target used inside the web Dockerfile, with stricter bundle budgets (1MB warn / 2MB error initial, 6kB / 12kB per component style) and `outputHashing: all`.

### Changed
- **`.gitignore`**: now ignores `*.csproj.lscache` (JetBrains Rider per-csproj language service cache) and `.env` / `.env.*` (with `!.env.example` exception). Closes a gap where Rider was generating untracked cache files in every csproj directory.

---

## [0.12.0] - 2026-05-16

### Added
- **`# Trades` and `Win / Loss` columns in the MT4 (Live) group**: new columns inside the strategies grid showing live trade count (from imported MT4 trades) and a `wins/losses (rate%)` cell rendered via the new `winLossPair` formatter that reads `liveWinCount` + `liveLossCount` from the row. Both visible by default; the pinned TOTAL row aggregates wins and losses across all loaded strategies.
- **Live KPI fields on `StrategyDto`**: three new ints — `LiveWinCount`, `LiveLossCount`, `LiveStagnationInDays` — populated from `StrategyAnalyticsCalculator` output and exposed by `GET /api/trading-accounts/{id}/strategies`. They feed the new grid columns and the redesigned trades-panel header without requiring a second `/analytics` fetch.
- **Indicators row in the trades-panel header (Backtest section)**: three side-by-side cards (Entry Indicators, Price Indicators, Indicator Params) below the main KPIs, only shown when at least one is populated. Each card renders one item per row — Entry/Price split by `,`, Indicator Params split by `;` so the commas inside parentheses (indicator arguments) are preserved. Long strings stay readable thanks to the new `kpi-card--text` modifier (block layout, word-break, smaller font, thin row separators).

### Changed
- **Trades-panel header redesign**: 2 strips per section, 6 cards each, all using the default `.kpi-strip` grid (no more `--cols-7` / `--cols-5` modifiers).
  - **Backtest (SQX)** row 1: Ret / DD Ratio · Profit Factor · Sharpe Ratio · Drawdown · Stagnation Days · Win / Loss Ratio. Removed Total Profit, Win %, Trades and Avg Trade from the header (still available in the grid).
  - **Live (imported trades)** row 1 (risk-adjusted): Ret / DD Ratio · Profit Factor · Sharpe Ratio · Max Drawdown · Stagnation Days · Win / Loss.
  - **Live (imported trades)** row 2 (cash-flow): Magic Number · Total Profit · Net Profit · Commission · Swap · Trades.
- **Default page size in the strategies grid**: `5` → `10`, and removed `5` from the page-size selector (now `[10, 20, 50, 100]`). The previous default left only ~3 visible rows once a trades panel was open; 10 is a better trade-off.

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
