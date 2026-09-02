# Design: Strategy Portfolio Simulator — Slice 1 (REVISION 2): Strategy-Scoped Backtest Import

Engram mirror: `sdd/strategy-portfolio-simulator/design`. Supersedes revision 1 (filename-inferred
attribution). Scope: import + evidence model. No engine, no resizing, no selection.

## Why This Is A Revision, Not A Fix

Import moves out of the standalone Backtests page into a per-row action on the account's strategies
grid (`account-detail.component.ts:382-426`, the `Actions` cellRenderer — a plain DOM builder with
three buttons; a fourth follows the same shape). The strategy is therefore KNOWN at import time and
attribution becomes an explicit FK.

That is not a UI preference. The filename convention, name matching, duplicate fan-out,
`AttributionStatus`, the `Unmatched` panel and the corroborated cascade-orphan BLOCKER **all existed
only because attribution was INFERRED**. Importing where the strategy is already known deletes that
class of defect instead of fixing it. Revision 1's D3/D3a/D3b were correct answers to a question this
revision stops asking.

## Domain Model: Three Artifacts Per Strategy

| Artifact | Source | Answers | Cannot answer |
|---|---|---|---|
| **Deploy run** | AlgoWizard, params actually running, full 10y, 2-decimal MM | sizing, R-normalization, correlation, breach probability | anything out-of-sample |
| **Evaluation run** | AlgoWizard, PREVIOUS WF window's params, full 10y, 2-decimal MM | trades at/after `OosFromDate` are genuinely out-of-sample | sizing of what is deployed |
| **WF export** | SQX Optimizer "Walk-Forward Results" → Export | the boundary date + per-window IS/OOS KPIs | anything per-trade |

`Deploy` is named for what it ANSWERS ("the parameters actually running"), not for which SQX window
produced them — so a strategy running the Optimizer "original" is a `Deploy` run, correctly amber.

Measured: with deployed (last-window) params only **3 of 329 trades / 47 days** fall past the
boundary — useless as OOS. Previous-window params move the boundary to 2025-05-26: **23 trades /
429 days**. Thin per strategy; the simulator evaluates GROUPS, so a 10-20 strategy portfolio pools
230-460 OOS trades.

---

## Architecture Decisions

### D1 — Attribution is an FK (SUPERSEDES revision 1 D1)

| Option | Tradeoff | Decision |
|---|---|---|
| Keep `BacktestRunStrategy` join + name matching | Preserves the entire inferred-attribution defect class | Rejected |
| `BacktestRun.StrategyId` NOT NULL FK, cascade | Attribution is supplied, never derived; orphan runs become unrepresentable | **CHOSEN** |

`Strategy → BacktestRun → BacktestTrade` is `Cascade`. **Behaviour change, stated deliberately**:
deleting a strategy now deletes its runs AND their trades, where revision 1 dropped only a link.
That is correct under FK attribution — a run with no strategy has no meaning and no name to
re-match. `Restrict` was rejected as hostile in a single-user app.

DELETED with this decision: `BacktestRunStrategy`, `StrategyNameKey`, `RunLabel`, `AttributionStatus`,
`DeriveAttributionStatus`, `Unmatched` panel, filename parsing, duplicate fan-out, `Reattributed`
and `Conflict` outcomes.

### D2 — Structural isolation from `StrategyTrades` (SURVIVES UNCHANGED)

`IBacktestDbContext` (narrow surface, no `StrategyTrades`) + `IBacktestDbContextFactory`
(fresh context per retry attempt) are **not** re-opened. Both failure modes the factory fixes were
corroborated by a detached refuter against the EF Core 10 docs. The only edit: drop
`BacktestRunStrategies` from the interface, add `StrategyWalkForwardExports` and
`WalkForwardWindows`. `GetStrategyNameIndexAsync` is DELETED — nothing matches names any more.

### D3 — Idempotency by slot (SUPERSEDES revision 1 D3/D3a/D3b)

Identity is **`(StrategyId, Kind)`** — one deploy run and one evaluation run per strategy, UNIQUE.
The 5-outcome decision table collapses to three:

| Slot state | Outcome | Writes |
|---|---|---|
| empty | `Imported` | insert run + trades |
| occupied, same `ContentHash` | `Unchanged` | none |
| occupied, different `ContentHash` | `Replaced` | delete run's trades, insert new |

**`ContentHash` is demoted from identity to a de-duplication key and MUST lose its UNIQUE index.**
Under FK attribution the same bytes legitimately produce two runs (one SQX strategy deployed under
one name on two accounts). Rejected keeping the unique index: it would make the second account's
import fail with an opaque constraint violation.

The re-import decision stays INSIDE the retried unit (`PersistOneFileAsync`) so an attempt after an
unacknowledged commit re-reads committed state and settles as `Unchanged`. That property is
Work Unit 1's and is preserved verbatim.

### D4 — Calibration (SURVIVES + one amendment)

`SymbolPointValueCalibrator`, MAE on SL-closed trades ONLY (never `Profit`), median + evidence,
floor 3, `Inconsistent` above 0.5% spread, per-SYMBOL pooled across runs, recomputed end-of-batch
from persisted trades — all unchanged.

**AMENDMENT — deduplicate by `ContentHash`.** D1 reintroduces exactly the double-counting revision 1's
join table existed to prevent: the same file imported for two same-named strategies stores its 329
trades twice. The median is immune (all samples are exactly 100.000) but `SampleCount` would report
370 where 185 is the truth, and `SampleCount` is the evidence the status depends on.

| Option | Tradeoff | Decision |
|---|---|---|
| Accept duplication, document it | `SampleCount` becomes a lie; `InsufficientSamples` could be cleared by re-importing the same file | Rejected |
| Re-introduce a join table | Reinstates inferred attribution to protect one integer | Rejected |
| Calibrate from one run per distinct `ContentHash` | Two lines in the calibration query; `ContentHash` finally earns its column | **CHOSEN** |

### D5 — Segment (SURVIVES + a file-level guard)

`BacktestSegment` + `SegmentIndex` + `SampleTypeRaw` verbatim: unchanged. **New guard**: a trade-list
file whose rows carry more than one distinct `Sample type` is REJECTED whole, naming the observed
values. Same precedent and same justification as the existing single-symbol guard — a downstream
statistic (OOS attribution by date) depends on the run being one continuous full-period simulation.

This is a structural rejection on an observable file property, not an inference about intent, and it
is what makes D15 safe: a 1-decimal Optimizer export (`IS` + `OOS1`) is refused with a precise reason
instead of quietly becoming a run.

### D6 — Transaction boundaries (SURVIVES, narrowed)

Per-file transaction, batch-resilient, per-file exception boundary (`GetBaseException()` diagnosis,
`OperationCanceledException` re-thrown) — unchanged from Work Unit 2. The batch is now at most 3
files for one strategy instead of 117, which does not change the shape and does not justify
re-opening it.

### D7 — REST + controller placement (SURVIVES, extended)

REST for command and reads (HotChocolate is still README-only — no package reference, no
`AddGraphQLServer`). Strategy-scoped commands go on a **new `StrategyBacktestsController`**, matching
the existing `TradingAccountStrategiesController` nested-resource precedent; `StrategiesController`
stays at 11 endpoints, at the God-Service line.

| Verb | Route | Status |
|---|---|---|
| POST | `/api/strategies/{strategyId}/backtests/{kind}` (`deploy`\|`evaluation`, ONE file) | New |
| POST | `/api/strategies/{strategyId}/walk-forward` (ONE file) | New |
| GET | `/api/strategies/{strategyId}/backtests` (summary + robustness) | New |
| GET | `/api/backtests/runs`, `/runs/{id}/trades`, `/calibrations` | Survive (read-only page) |
| POST | `/api/backtests/import` (multi-file batch) | **Deleted** |

`{kind}` is a route segment, not a form field: the declaration is then legible in logs and an
unknown value is a 400 from model binding before the service is touched. That legibility matters
precisely because D11 proves the kind cannot be verified.

### D8 — Run kind: enum + an unexpressible OOS claim

The question is not where to store the kind; it is *what stops a deploy run being read as if it
carried OOS evidence*.

| Option | Tradeoff | Decision |
|---|---|---|
| Separate `DeployRuns`/`EvaluationRuns` tables | Structural, but forks the parser, idempotency, retry and per-symbol calibration (which must pool ALL runs) into a UNION | Rejected |
| `bool IsOutOfSampleCapable` | Booleans do not extend; a third kind needs a second boolean and the two drift | Rejected |
| `Kind` enum + one guarded accessor | Same storage, and the claim is made underivable rather than merely filtered | **CHOSEN** |

An enum alone stops nothing — every read site must remember to filter. What stops it:

```csharp
// The ONLY way to obtain the boundary. No other read path exposes OosFromDate.
// Nested INSIDE OosWindow, whose constructor is private, so the resolver is the only code the
// compiler permits to build one. Called as OosWindow.Resolver.TryGetOosWindow(...).
public static bool TryGetOosWindow(BacktestRun run, StrategyWalkForwardExport? export, out OosWindow? window)
```

It returns `false` when `run.Kind != Evaluation`, **or** `export is null`, **or**
`export.StrategyId != run.StrategyId`.

A deploy run yields **no window at all** — not an empty window, not an empty sequence of trades.
That distinction is the whole point and it must not be softened: an empty result reads as "measured,
and there was nothing out of sample", which is a claim about the evidence. `null` reads as "there is
no boundary here", which is the truth, and it forces the caller to handle the case instead of
quietly rendering a zero. `OosWindow` is a class rather than a struct for the same reason — a struct
would always have a `default` instance whose boundary is `DateTime.MinValue`, a window admitting
every trade ever imported.

The third condition is not defensive noise: an export belongs to exactly one strategy, so a
mismatched pair would apply a boundary produced by a different parameter set to these trades. The
grid's aggregate already correlates on `StrategyId`; the single-run path states the same rule rather
than trusting its callers to pair correctly.

There is no hand-written `CloseTime >= x` filter anywhere in the OOS path — every comparison lives
in `OosWindow.cs`, which is a convention checked by grep, not a structural guarantee, and the source
says so in those words.

### D9 — WF export parser: a SEPARATE service, not a shared policy

Two files, two decimal conventions. One shared policy corrupts one of them, so there is no shared
policy: `WalkForwardExportParserService` is its own pure service with its own column table, and
zero culture state is shared with `SqxTradeListParserService`.

| Trap | Handling |
|---|---|
| 1. Every numeric column is COMMA-decimal (trade list uses DOTS for prices) | Separate parser, separate `DecimalColumns` table. Never sniffed. |
| 2. Inside `Parameters` the roles INVERT (`,` separates, `.` is decimal) | `Parameters` is simply NOT in the decimal table — it is opaque text, split on `,`, each `key=value` parsed dot-decimal invariant. Trailing `,` yields an empty token that is dropped, not failed. |
| 3. Last row is un-elapsed: OOS values are the literal `N/A`, period carries ` (future)` | `N/A` recognised BEFORE any numeric parse → `null` + `IsFutureWindow`. NEVER 0. `N/A` on a non-last row is a rejection. |
| 4 (unnamed, found while reading the fixture) | Periods are `dd.MM.yyyy` (`19.03.2021`, `26.05.2025` disambiguate), NOT the trade list's `yyyy.MM.dd HH:mm:ss`. Split on `" - "` after stripping ` (future)`. |

Measured invariant, enforced: exactly FOUR columns are `N/A` on the future row (Net profit OOS,
Ret/DD OOS, Drawdown OOS, Avg trades/month OOS). `Days OOS` is populated (`381`), and so are all
four IS columns. So `IsFutureWindow` has two independent signals — the suffix and the four nulls —
and **disagreement rejects the file**, because disagreement means the export format changed.

### D10 — `OosFromDate` is owned by the WF export, never copied onto the run

`OosFromDate` = the OOS start of the **second-to-last** row (`windows[^2].OosStart` = 2025-05-26 in
the fixture). Positional, because the user's process is positional: deployed params come from the
LAST row, evaluation params from the one before it. Fewer than 2 windows → rejected with a reason.

| Option | Tradeoff | Decision |
|---|---|---|
| Store `OosFromDate` on the evaluation run at import | This is the D3a defect verbatim: a value owned by artifact A copied onto artifact B, which cannot observe A changing. Re-importing a newer WF export would leave every run on the old boundary | Rejected |
| Store on the WF export, derive the run's window at read | One owner, one parse, immutable source | **CHOSEN** |

**Run imported before its WF export**: the run exists, its trades are stored, it yields no OOS window,
the marker stays amber. Importing the WF export later turns it green with **zero re-import**. That is
the whole payoff of not copying.

`DeployParameters` and `EvaluationParameters` are stored verbatim from rows `[^1]`/`[^2]`. Two
~120-char strings, and the ONLY cross-check available against D11's undetectable kind — the user can
compare them against what they pasted into AlgoWizard.

### D11 — The user MUST declare which run a file is

**Verified impossible to detect.** The trade-list CSV has exactly 16 columns (header read directly)
and none carries strategy parameters. Two AlgoWizard runs of the same strategy differ only by the
parameters that produced them. There is not even prefix-identity to exploit: row 1 of the two
fixtures shares an open time but differs in size and close time — divergence from the first trade.

What CAN be detected is detected: **WF export vs trade list, from the header line**
(`"Period IS";"Period OOS";…` vs `"Ticket";"Symbol";…`), deterministically, with no user input.

| Option | Tradeoff | Decision |
|---|---|---|
| One drop zone, infer everything | Must guess deploy-vs-evaluation; a wrong guess is a SILENT false OOS claim — the exact defect class this revision deletes | Rejected |
| Radio button + one drop zone | Same declaration, more clicks, and permits 2 files under 1 label | Rejected |
| One modal, THREE labelled slots | The slot IS the declaration; the header check then VALIDATES the slot | **CHOSEN** |

Each slot is optional and independently re-importable, so partial import (one now, one later) and
re-import-to-refresh both fall out. A WF export dropped in a trade-list slot is rejected naming the
mismatch, and vice versa: the one thing that can be verified IS verified, the one thing that cannot
be is declared by placement.

### D12 — Grid readiness marker: server-derived, one extra query per page load

Marker is a **total function**, derived, never stored:

| Marker | Condition |
|---|---|
| white `None` | no run of either kind **holding at least one trade** |
| amber `SizingOnly` | a run holding trades, but not (evaluation run AND WF export AND ≥1 trade at/after boundary) |
| green `Evaluable` | evaluation run AND WF export AND ≥1 trade at/after `OosFromDate` |

Amber and green are both affirmative claims about what the strategy supports, so the white condition
is about TRADES, not about run rows. Position sizing is computed from the trades; a run holding none
supports nothing, and reporting it as "can be sized" would be exactly the unsupported evidence claim
this marker exists to prevent. The two booleans behind it are named `HasSizingEvidence` and
`HasOosEvidence` for that reason — an earlier `HasAnyRun` described the query rather than the claim.

| Option | Tradeoff | Decision |
|---|---|---|
| `Strategy.HasBacktest` column written at import | The D3a defect verbatim — no code runs on a cascade | Rejected |
| Client-side join over a second `GET /runs` call | Second HTTP call, second source of truth, re-runs on every `strategies()` write | Rejected |
| Two derived fields on `StrategyDto`, computed in `GetByAccountAsync` | One extra query per page load; zero per render | **CHOSEN** |

**Cost, concretely.** `account-detail.component.ts:707` fetches page 1 / pageSize 500 — ONE call for
all 123 rows. `GetByAccountAsync` already materialises the page then issues ONE
`WHERE pageIds.Contains(...)` trade query. This adds exactly ONE more: a grouped aggregate over
`BacktestRuns ⋈ BacktestTrades ⋈ StrategyWalkForwardExports` keyed by the page's strategy ids,
counting trades and OOS trades per (strategy, kind). ~40k trade rows aggregated server-side with a
`(BacktestRunId, CloseTime)` index — tens of ms, once per page load, **not per row and not per
render**. Client cost is zero beyond a `switch` in a `cellClass`, called only for the ~20 virtualised
visible cells, identical in shape to the existing `symbolToColor` cellStyle.

If it ever becomes slow the fix is a cached projection with explicit invalidation — not a column
written at import.

### D13 — Robustness: windows STORED verbatim, every aggregate DERIVED

| Option | Tradeoff | Decision |
|---|---|---|
| Store the degradation ratio on the export | Goes stale the instant the formula changes, with no signal that it did — D3a/D10 again | Rejected |
| Store nothing, re-parse the file on read | Requires still holding a file we do not keep | Rejected |
| Store the window rows (faithful transcription), derive aggregates | Same treatment the trade rows get; deriving 5-10 rows is free | **CHOSEN** |

`WalkForwardRobustnessCalculator` — pure static in `Infrastructure/Services`, matching
`SymbolPointValueCalibrator`. **Excludes the future window from every statistic** (that is trap 3's
real consequence). Returns, from the fixture: `RetDdIsMedian = 16.36`, `RetDdOosMedian = 1.16`,
`RetDdDegradationRatio ≈ 14.1`, `ProfitableOosWindowPercent = 100` (5/5), `MaxOosProfitSharePercent`,
`ElapsedWindowCount = 5`. `RetDdOosMedian == 0` yields a NULL ratio, never infinity.

Domain grounding (`03_Validacion_y_Stress_Test.md` §4-5): `Profitable Runs > 70%` and
`Max Profit in one run < 40-50%` are academy criteria computable from exactly these columns, and
`Ret/DD (Oro) > 10` is passed by the IS side and missed by ~9x on the OOS side. Slice 1 RECORDS
these; judging them is slice 3.

### D14 — No-OOS strategies: flagged in the RESULT, never a preference toggle

Forward constraint on slice 3, binding on slice 1's model so the checkbox cannot be introduced later:

1. `BacktestReadiness` is DERIVED and never user-settable — **there is no column to flip** (D12).
2. `SimulationResult` MUST carry a `required` non-nullable `EvidenceProfile` (included strategies
   counted by readiness), computed from the inputs. A result without it is unconstructible.
3. **Slice 1 MUST NOT add** any settings/preference table, any `Strategy.IncludeInSimulator` flag, or
   any account-agnostic risk aggregate (C5). Inclusion is per-simulation and explicit; the readiness
   of what was included is a property of the RESULT, not of a setting.

A strategy running the Optimizer "original" is a `Deploy` run → amber → includable, and the amber
count lands in the result's evidence profile. Overfitting cannot re-enter through a silent toggle
because no toggle exists to be silent.

### D15 — The 1-decimal OOST trade-list import is DROPPED

Asked honestly. IMOX doctrine (`06_Money Management.md`) sets Size Decimals = 1 in the Builder phase
explicitly as a "filtro contra falsa precisión", 2 decimals only at Retester/Live. The 1-decimal
export is therefore a deliberately coarse mining-phase artifact. Measured consequence: realized SL
risk spans 104-407 against a 200 target with 33.8% of trades pinned at minimum lot — sizing is not
merely imprecise, it is **non-proportional to risk**, which breaks the only three things the engine
wants from a trade stream (R-normalisation, correlation, breach probability).

The one thing it uniquely offered was per-trade OOS timestamps from the Optimizer's own walk-forward.
The evaluation run supplies exactly that with correct sizing, so the evaluation run **strictly
dominates it on every consumed axis**. Verdict: it does not earn its place as a data source.

It keeps ONE job: **regression fixture**. `ListOfTrades_XAUUSD_H1_OOST.csv` stays committed — it is
the only fixture exercising `IS`/`OOS1` splitting, `SegmentIndex`, the 27 colliding tickets, and now
D5's multi-segment rejection. Deleting the file would delete real parser coverage.

Re-adding it later (if Optimizer-native OOS correlation is ever wanted) is a new `Kind` member plus
relaxing D5's guard for that member — no schema change. Cheap enough to defer honestly.

---

## Data Flow

    Strategies grid row ──> [4th action button] ──> Import modal (3 labelled slots)
       │                                              │  slot = the declaration (D11)
       │                                              ▼
       │        POST /api/strategies/{id}/backtests/{deploy|evaluation}   POST .../walk-forward
       │                     │                                                  │
       │                     ▼                                                  ▼
       │       SqxTradeListParserService                    WalkForwardExportParserService
       │       (dot prices, comma money)                    (comma everywhere, dot inside
       │                     │                               Parameters, N/A → null)
       │                     ▼                                                  │
       │        BacktestImportService ──> IBacktestDbContextFactory             ▼
       │          (StrategyId, Kind) slot         │            StrategyWalkForwardExports
       │                     │                    ▼                     └─ WalkForwardWindows
       │                     │            BacktestRuns ── BacktestTrades         │
       │                     ▼ (per touched symbol, DISTINCT ContentHash)        │
       │        SymbolPointValueCalibrator ──> SymbolCalibrations                │
       │                                                                        │
       └── GET /trading-accounts/{id}/strategies ── OosWindow.Resolver.ReadinessRows ──┘
                     (the grid marker — the ONLY production consumer of the boundary here)

NOT WIRED IN SLICE 1 — named so the gap is visible instead of assumed:

- `TryGetOosWindow(run, export)` exists, is compiler-fenced and is tested, but has **no production
  caller**. The per-run out-of-sample view that would use it is a later slice. The aggregate above
  reaches the same boundary by a different path, and both live in `OosWindow.cs` so the comparison
  is written once.
- `WalkForwardRobustnessCalculator` was **not built**.
- `GET /strategies/{id}/backtests` reports what EXISTS and deliberately carries **no** readiness
  field. Deriving the marker there as well as in `GetByAccountAsync` would be two definitions of one
  rule, free to disagree — the exact defect class D3a was about.

## File Changes

| File | Action | Description |
|---|---|---|
| `Domain/Entities/BacktestRunStrategy.cs` | **Delete** | Join table → FK (D1) |
| `Domain/Enums/AttributionStatus.cs` | **Delete** | Attribution is no longer inferred |
| `Domain/Entities/BacktestRun.cs` | Modify | +`StrategyId` FK, +`Kind`; −`StrategyNameKey`, −`RunLabel`, −`StrategyLinks`, −`DeriveAttributionStatus` |
| `Domain/Enums/BacktestRunKind.cs`, `BacktestReadiness.cs` | Create | `Deploy=1, Evaluation=2`; `None=0, SizingOnly=1, Evaluable=2` |
| `Domain/Entities/StrategyWalkForwardExport.cs`, `WalkForwardWindow.cs` | Create | One export per strategy (unique FK) + its windows |
| `Domain/Constants/BacktestFieldLengths.cs` | Modify | −`FileNameOrKey` split usage; +`WalkForwardParameters` (1000 — the field is an opaque `key=value` list that grows with the number of optimised inputs) |
| `Application/Interfaces/IBacktestDbContext.cs` | Modify | −`BacktestRunStrategies`, −`GetStrategyNameIndexAsync`; +2 DbSets |
| `Application/Interfaces/IWalkForwardExportParser.cs`, `IWalkForwardImportService.cs`, `IBacktestReadService.cs` | Create | Contracts. Reads are a SEPARATE service from the importer: the runs list must join `Strategies` to show whose run a row is, and D2 forbids that on the importer's surface |
| `Application/DTOs/Backtests/*` | Modify/Create | −`StrategyNameRefDto`; +`ParsedWalkForwardExportDto`, `ParsedWalkForwardWindowDto`, `StrategyBacktestSummaryDto`, `WalkForwardRobustnessDto` |
| `Application/DTOs/Strategies/StrategyDto.cs` | Modify | +`BacktestReadiness BacktestReadiness`. `OosTradeCount` was **not** added — the grid needs the marker, not the count, and an unused field is a second thing to keep true |
| `Infrastructure/Services/WalkForwardExportParserService.cs` | Create | Pure, own column table (D9) |
| `Infrastructure/Services/WalkForwardRobustnessCalculator.cs` | ~~Create~~ **not built in slice 1** | Deferred with D13; no caller exists yet |
| `Infrastructure/Services/BacktestImportService.cs` | Modify | Slot idempotency (D3), calibration dedup (D4), −attribution |
| `Infrastructure/Services/StrategyService.cs` | Modify | `GetByAccountAsync` +1 grouped readiness query (D12) |
| `Infrastructure/Persistence/Configurations/*` | Modify/Create | −`BacktestRunStrategyConfiguration`; +2 configs; drop UNIQUE `ContentHash` |
| `Migrations/*_ReshapeBacktestRunsForStrategyScopedImport.cs` | Create | See Migration |
| `WebAPI/Controllers/StrategyBacktestsController.cs` | Create | 2 commands + 1 read |
| `WebAPI/Controllers/BacktestsController.cs` | Modify | −`POST /import`; 3 reads survive |
| `web/.../broker-accounts/account-detail/account-detail.component.ts` | Modify | 4th action button + readiness `cellClass` |
| `web/.../broker-accounts/import-strategy-backtests-modal/` | Create | 3 labelled slots |
| `web/.../sqx/backtests/import-backtests-modal/` | **Delete** | Standalone multi-file modal |
| `web/.../sqx/backtests/backtests-list/` | Modify | −`Unmatched` panel; read-only runs + calibrations survive |
| `web/src/app/core/services/backtest.service.ts` | Modify | New routes, new DTOs |
| `web/src/assets/i18n/{en,es}.json` | Modify | Slot labels, readiness legend, robustness |

Files whose CONTENT is untouched: `SqxTradeListParserService` (except D5's guard),
`SymbolPointValueCalibrator`, `BacktestFieldLengths` validation, `BacktestDbContextFactory`,
`BacktestTrade`, `SymbolCalibration`.

## Testing Strategy (TDD — RED first, against committed fixtures)

| Layer | What | Approach |
|---|---|---|
| Unit — WF parser | 6 windows; `15239,94`→15239.94; `ProfitTargetCoef1=5.4`→5.4 NOT 54 (trap 2); trailing `,`→5 params not 6; last row → 4 OOS nulls + `IsFutureWindow`, `Days OOS`=381 NOT null; `dd.MM.yyyy` both sides; `N/A` on a non-last row → rejected; suffix/nulls disagreement → rejected; `de-DE` culture identical; <2 windows → rejected; `OosFromDate == 2025-05-26`; `Deploy`/`EvaluationParameters` from rows `[^1]`/`[^2]` | xUnit + FluentAssertions, real `WFParamsExport_XAUUSD_H1.csv`, no DB |
| Unit — robustness | IS median 16.36, OOS median 1.16, ratio ≈14.1; future window absent from all 5 statistics; `ProfitableOosWindowPercent == 100`; OOS median 0 → NULL ratio not ∞ | Pure static |
| Unit — trade parser | Every existing test survives byte-identical EXCEPT the deleted filename contract; NEW: `_OOST` (mixed `IS`/`OOS1`) → rejected naming the values (D5) | Existing fixtures |
| Unit — import service | `(StrategyId, Kind)` uniqueness; same bytes → 2 strategies → 2 runs both kept; `Unchanged`/`Replaced`; `StrategyTrades` count unchanged (regression survives); retry invoke-twice == invoke-once with the NEW decision table inside the unit | SQLite harness (`BacktestTestDbContext`) — InMemory enforces no unique index |
| Unit — calibration dedup | Same bytes for 2 strategies → `SampleCount` 185, NOT 370 (D4) | SQLite harness |
| Unit — readiness | All 8 combinations of (deploy?, evaluation?, wf?, oosTrades>0) → the total function; `TryGetOosWindow` returns false for every deploy run | Pure |
| Controller | Unknown `{kind}` → 400 before the service; WF export posted to the trade-list route → rejected naming the header mismatch; non-`.csv` rejected server-side | Direct instantiation + mocked service |
| Frontend | 4th button opens the modal with the strategy bound; 3 slots post independently; one failing slot does not block the others; readiness → class | Vitest |
| Runtime | `dotnet run` + curl all three fixtures against a real strategy id; assert marker white → amber → green | Manual harness |
| E2E | N/A — no harness in repo | — |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, or process-integration boundary. Two
requirements outside the matrix:

1. The `.csv` extension whitelist stays enforced **server-side**, not only in the modal.
2. `Path.GetFileName()` still sanitizes `IFormFile.FileName`, but the surface **shrinks**: the
   filename no longer decides which strategy receives the data, only what a display string says.
   Path traversal drops from an attribution vector to a cosmetic one. Sanitize anyway.

## Migration / Rollout

All four backtest tables hold **zero rows** (verified in tasks 2.7 / 9.2), so schema changes are free
and no data migration exists. ONE migration, `ReshapeBacktestRunsForStrategyScopedImport`:

1. DROP TABLE `BacktestRunStrategies`.
2. `BacktestRuns`: DROP `StrategyNameKey`, `RunLabel`, the UNIQUE index on `(StrategyNameKey, RunLabel)`,
   **and the UNIQUE index on `ContentHash`** (D3).
3. `BacktestRuns`: ADD `StrategyId` NOT NULL FK → `Strategies(Id)` ON DELETE CASCADE; ADD `Kind` NOT
   NULL; ADD UNIQUE `(StrategyId, Kind)`.
4. CREATE `StrategyWalkForwardExports` (UNIQUE `StrategyId`, cascade) + `WalkForwardWindows`
   (UNIQUE `(WalkForwardExportId, RowIndex)`, cascade).
5. ADD index `BacktestTrades(BacktestRunId, CloseTime)` for D12's aggregate.

`Down` restores revision 1's shape and **discards every imported backtest run and trade** to do it.
That is deliberate, and it was corrected after review: the original `Down` recreated unique indexes
over columns it had just backfilled with a constant, so any account holding more than one run — the
normal steady state, one Deploy slot plus one Evaluation slot — could not roll back at all. It threw
instead of reverting. It now issues `DELETE FROM [BacktestTrades]` and `DELETE FROM [BacktestRuns]`
ahead of every constraint those rows would violate, and states the loss at the point it happens. A
rollback of a table-dropping migration is inherently lossy; what is not acceptable is one that
refuses to run. The data is re-importable from the source CSVs.
The `DeriveBacktestRunAttributionStatus` migration already dropped its column — nothing to undo, only the
enum and the method to delete. Frontend rolls back independently; `sqx/backtests` survives read-only.

Review budget: forecast **High**. Suggested slices: (1) WF parser + robustness calculator + their
tests (pure, no schema); (2) migration + entities + import service reshape + calibration dedup;
(3) controllers + readiness query; (4) Angular modal + grid marker + i18n.

## Open Questions

- [ ] Can SQX emit a Stop Loss column? Schema is nullable-ready either way. (Carried from rev 1.)
- [ ] Should a WF export with NO future window (`HasFutureWindow == false`) shift the positional rule?
      Slice 1 records the flag, keeps the rule, branches on nothing. Revisit in slice 3.
- [ ] Recalibration is still not triggered by a strategy DELETE, and D1 makes deletes remove trades
      rather than links — so a stale `SymbolCalibration` is now slightly more reachable. Recorded as a
      known gap (already carried from Work Unit 3's out-of-scope list), not silently solved.
