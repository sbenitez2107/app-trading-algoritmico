# Proposal: Strategy Portfolio Simulator — Slice 1: SQX Trade-List Import

## Intent

The end goal is a **prescriptive** simulator: from an account's strategy pool it returns the recommended *group* (size decided by the engine, not the user), sized under Money Management for a concrete funding service. That engine is currently unbuildable, and not for architectural reasons — the data does not exist.

- SQX data in the DB is **monthly profit only** (`StrategyMonthlyPerformances`: 17,688 rows, 140 strategies). Correlation, VaR, drawdown and resizing all need a **daily/per-trade** series. Monthly profit cannot produce one.
- Live demo trades exist and are clean (1,572 closed, 100% StopLoss coverage) but are **too thin to select on**: 117 strategies averaging 13 trades, 13 with ≥30, one with ≥50 — against the academy's own `Min # Trades > 200`.

So the first shippable slice is the **SQX trade-list importer**. It is the prerequisite that unblocks everything downstream, and it is also the test substrate the engine will be TDD'd against.

## Scope

### In Scope

- `SqxTradeListParserService`: `;`-delimited, comma decimals, `yyyy.MM.dd HH:mm:ss`, quoted 16-column format; `Sample type` → `IS | OOS<n> | IST` segment; `Close type` → close reason.
- **Multi-file import in one operation** — 117 hand-exported files one at a time is not viable.
- New `BacktestRun` + `BacktestTrade` tables. **`Ticket` is NOT unique across runs** (27 verified collisions between the two fixtures, different trades). Backtest trades must never touch `StrategyTrades` or its by-ticket upsert.
- Per-symbol **point-value calibration from `MAE` on SL-closed trades only** — measured exact (100.000, zero variance); `Profit` gives 100.47–102.15 because it carries spread/commission. Persist sample count + spread so the number is auditable; guard `ClosePrice == OpenPrice`.
- **Realized risk per trade** stored for SL-closed trades, explicitly `null` otherwise. Never store the configured $200: realized risk spans $103.91–$406.88 (median $152.77).
- Symbol bridge: the CSV `Symbol` (`XAUUSD_M1_UTC02`) matches `Strategy.Symbol`, so the import also closes the SQX-vs-broker naming gap for backtest data.
- Import summary UI + `GET` read endpoint (per-run counts, segment split, calibration result, rejected rows).

### Out of Scope

- **Slice 2** — R-normalization, the lot-grid resizer (Size Decimals, Max Lots = 10), portfolio scoring, Darwinex Zero VaR check.
- **Slice 3** — the group selector and its anti-overfitting apparatus (structural constraints, shuffled-returns random benchmark, portfolio-level walk-forward).
- **AXI Select** (no rulebook) and the per-service strategy layer (challenges/month, assets, structure size) — pending mentorship.
- Prop-firm daily-breach modelling. First target service is Darwinex Zero, the only one already documented and modelled.
- Any change to `StrategyTrade`, `TradeImportService`, or `PortfolioStrategy.Weight`.
- Cross-broker symbol aliasing (`ndx` vs `us100.cash`) on the live side.

### What Slice 1 Explicitly Cannot Claim

- It recommends nothing. No group, no size, no ranking.
- It does not resize anything. `PortfolioStrategy.Weight` keeps its documented fiction until slice 2.
- **It cannot recover per-trade risk for non-SL exits.** The export has no StopLoss column; `MAE` equals the SL distance only when the trade was closed *by* the SL. For winners the risk denominator is unobservable and must be imputed in slice 2 — an approximation with a real error bar, not a measurement. See open question 1.

## Capabilities

### New Capabilities

- `sqx-backtest-import`: parse and persist SQX/AlgoWizard trade-list exports as run-scoped backtest trades, multi-file, IS/OOS segment preserved.
- `symbol-point-value-calibration`: derive and persist per-symbol point value from SL-closed trades via `MAE`, with auditable sample evidence.

### Modified Capabilities

- `strategy-model`: `Strategy` gains the backtest-run association used to attribute an imported file to a strategy (exact key pending open question 2).

## Approach

Mirror `import-mt-trades` end to end — it is the direct precedent: pure DTO-returning parser with no EF dependency, service-layer orchestration, additive migration, multipart upload on the existing 200 MB infra, unmatched input surfaced as a first-class panel rather than silently dropped.

Two deliberate departures, both forced by measured facts:

1. **Separate table, run-scoped key.** `(BacktestRunId, RowIndex)` instead of `(StrategyId, Ticket)`. Reusing the ticket key would silently corrupt data — proven, not suspected.
2. **Calibration is a persisted artifact, not a computation.** The point value is derived once per symbol from SL-closed trades and stored with its evidence, so slice 2 consumes a reviewed number rather than re-deriving it per run.

The `_OOST` fixture already carries both walk-forward segments (IS: 151 trades 2016–2020; OOS1: 186 trades 2020–2026), so preserving `Sample type` at import is what makes slice 3's portfolio-level walk-forward buildable at all. Dropping it here would be unrecoverable.

Strict TDD: both fixtures are committed, so the parser is written against real data from the first failing test.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Entities/BacktestRun.cs` | New | StrategyId, SourceFileName, Symbol, ImportedAt, segment coverage, trade count |
| `Domain/Entities/BacktestTrade.cs` | New | RunId, RowIndex, Ticket (non-key), Symbol, Type, Open/Close time+price, Size, Profit, Balance, Segment, CloseType, MAE, MFE, RealizedRisk (nullable) |
| `Domain/Entities/SymbolCalibration.cs` | New | Symbol, PointValue, SampleCount, MinObserved/MaxObserved, CalibratedAt |
| `Domain/Enums/BacktestSegment.cs` | New | `InSample`, `OutOfSample`, `FullSample` |
| `Infrastructure/Persistence/Configurations/` | New | Three configurations; unique `(BacktestRunId, RowIndex)`; cascade delete run → trades |
| `Infrastructure/Persistence/Migrations/` | New | Additive only — three tables, zero changes to existing ones |
| `Application/Interfaces/ISqxTradeListParser.cs` | New | Parser contract |
| `Infrastructure/Services/SqxTradeListParserService.cs` | New | CSV parse, invariant-culture-safe European decimals |
| `Infrastructure/Services/BacktestImportService.cs` | New | Multi-file orchestration, calibration, per-file result |
| `Application/DTOs/Backtests/` | New | Parsed/result/calibration DTOs |
| `WebAPI/Controllers/StrategiesController.cs` | Modified | `POST /backtests/import` (multi-file), `GET /backtests` |
| `web/.../import-backtests-modal/` | New | Multi-file drop, per-file mapping + result table |
| `web/src/assets/i18n/{en,es}.json` | Modified | Import, calibration, rejection strings |
| `tests/.../Fixtures/ListOfTrades_XAUUSD_H1_{IST,OOST}.csv` | Existing | Already committed — TDD substrate |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Backtest trades leak into `StrategyTrades` and corrupt live data via the by-ticket upsert | **High if unguarded** | Separate table + separate service; no shared key; a test asserts `StrategyTrades` row count is unchanged after import |
| File→strategy attribution is unsolved: the CSV identifies the *symbol*, not the strategy | **High** | Open question 2 — blocks slice 1 completion, not just design |
| Point value calibrated from too few SL trades on a thin symbol | Med | Persist `SampleCount` + observed min/max; refuse to calibrate below a minimum sample; surface the spread in the UI |
| Non-SL risk imputation is mistaken for a measurement in slice 2 | **High** | Store `RealizedRisk` as `null`, never as a default; the honest gap is visible in the data, not buried in a service |
| European decimal parsing under a non-invariant server culture | Med | Explicit `CultureInfo` in the parser; a test runs under a comma-decimal culture |
| 117 files × ~300 trades = ~35k rows in one request | Med | Chunked insert (precedent: 500/batch); per-file result so a partial failure is legible |
| Re-importing the same export duplicates a run | Med | Run identity from `(StrategyId, SourceFileName, content hash)`; re-import replaces the run rather than appending |
| The exploration artifact (obs 2320) recommends deriving point value from `Profit` | Certain | **Superseded** by the later data spike (obs 2318). `MAE` is exact; `Profit` is contaminated. Design must not follow the stale recommendation |
| Slice 1 exceeds the 400-line review budget | High | Recommend chained PRs: (1) parser + fixtures, (2) domain + migration + import service, (3) endpoints + UI + i18n |

## Rollback Plan

1. Revert the frontend and backend commits.
2. Run the migration `Down` — drops `BacktestRuns`, `BacktestTrades`, `SymbolCalibrations`.
3. **Zero data loss.** The change adds three tables and touches no existing row, column, index or service. `StrategyTrades`, `StrategyMonthlyPerformances`, `Portfolios` and `BrokerRiskLimits` are untouched by construction.
4. Frontend-only rollback is safe: the API is simply unused.

## Dependencies

- Committed fixtures: `app.trading.algoritmico.api/tests/Fixtures/ListOfTrades_XAUUSD_H1_{IST,OOST}.csv`.
- The user's ability to bulk-export trade lists per strategy from AlgoWizard/Optimizer (confirmed).
- `import-mt-trades` as the architectural precedent (multipart upload, orphan-panel UX, chunked insert).
- Answers to open questions 1 and 2 before `sdd-design`.

## Success Criteria

- [ ] Importing both fixtures in **one** operation yields two runs, 329 + 337 trades, with no `StrategyTrades` row created or modified.
- [ ] The `_OOST` run splits `IS = 151` / `OOS1 = 186`; the `_IST` run is 329 rows of `IST`.
- [ ] XAUUSD calibrates to point value **100.000** with min = max across the 185 SL-closed trades.
- [ ] `RealizedRisk` is populated for SL-closed trades (range $103.91–$406.88, median $152.77) and `null` for every other close type.
- [ ] The 27 colliding tickets are imported as 27 distinct trade pairs, not 27 upserts.
- [ ] Re-importing an identical file replaces the run; total trade count is unchanged.
- [ ] A parser test passes under a comma-decimal server culture.
- [ ] A degenerate row (`ClosePrice == OpenPrice`) is rejected with a reason, not divided by zero.
- [ ] `dotnet format` and `pnpm format` pass clean.

## Proposal Question Round

Interactive questioning was unavailable inside this phase. These are product/data questions, and 1 and 2 are load-bearing enough to change the slice.

1. **Can the SQX export include a Stop Loss column?** If AlgoWizard/Optimizer can add SL (or SL distance) to the trade-list column set, the non-SL risk denominator stops being an imputation and becomes a measurement — which removes the largest honesty caveat from slice 2 entirely. Worth checking the export config before we design around the gap.
2. **How does a file map to a strategy?** The CSV identifies the symbol (`XAUUSD_M1_UTC02`), not the strategy — and many strategies share it. Options: a filename convention you control at export time, a per-file dropdown in the import modal (fine for 5 files, painful for 117), or a new SQX identifier stored on `Strategy`. This decides both the UX and whether `strategy-model` changes.
3. **Minimum SL-trade sample before a symbol is allowed to calibrate?** XAUUSD has 185 and is exact. A symbol with 4 SL trades should probably refuse rather than publish a fragile point value. Suggested floor: 30.
4. **IS-vs-OOS scoring policy.** Slice 3's walk-forward selects on one window and holds on the next. Should slice 1 already refuse to import a run whose segments overlap in time, or accept anything and let the engine decide later?

Assumptions taken meanwhile: backtest trades live in their own tables and never share the live by-ticket key; point value is calibrated per symbol from SL-closed trades only; `RealizedRisk` is `null` rather than defaulted for non-SL exits; `Sample type` is preserved verbatim at import; Darwinex Zero is the only funding service in view for the whole feature.
