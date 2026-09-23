# Exploration — BacktestRun platform attribution

> **Slice C of layer 0 in `openspec/SIMULATOR_ROADMAP.md`.** Named as deferred in slice B's
> Non-Goals: *"`PlatformType? SourcePlatform` on `BacktestRun` — slice C."*
>
> Engram topic: `sdd/explore/backtest-run-platform-attribution`. Mirrored here by the orchestrator —
> the `sdd-explore` agent type has no file-write tool. **Orchestrator verification notes are marked
> `[ORCH]`** and were checked against the code directly, not inherited from the exploration.

## Current state

- `Domain/Enums/PlatformType.cs` is exactly `{ MT4 = 0, MT5 = 1 }`. **[ORCH] Confirmed verbatim.**
  MT4 is the CLR zero default, so a non-nullable field silently claims MT4 on every unset row.
- Comments in `CostDecompositionStatus.cs` and `EmbeddedCostAvailability.cs` name
  `PlatformType.MT4 = 0` (alongside `FundingService.Other = 0`) as a precedent optimistic-enum-zero
  hazard this repo has *"already been bitten by twice"* — documented institutional memory, not
  speculation.
- `PlatformType` is used today only by `TradingAccount.Platform` (non-nullable `int`, no DB default
  constraint) and its three DTOs.
- `BacktestRun` has **no** platform field. It carries `SourceFileName`, `ContentHash`, `StrategyId`,
  `Kind`, `Symbol`. **[ORCH] Confirmed** against `Domain/Entities/BacktestRun.cs`.
- The SQX trade-list CSV has exactly 16 columns (Ticket … Comment). **None encodes platform**, and
  the filename is not parsed for attribution. **[ORCH] Confirmed** against `BacktestImportService`
  and `openspec/specs/sqx-backtest-import/spec.md`: **the import genuinely knows nothing about
  platform at write time.** It must be caller-supplied.
- No evidence (KB-wide grep) that the 16-column trade-list *structure* differs between MT4 and MT5
  exports. The difference is in cost timing/booking and platform capabilities, not file shape.
- `Strategy.TradingAccountId` is nullable and `TradingAccount.Platform` records the **live
  deployment** platform, not the SQX **build** target.
- Angular `account-form.component.ts` defaults a new account's platform to `0 as PlatformType`.
  **[ORCH] Confirmed at line 58** — a live instance of the same defect class already in production
  UI. Out of scope here; recorded as a follow-up.
- No GraphQL exists anywhere in the API despite the tech-stack doc listing it. `BacktestRun` is
  REST-only via `StrategyBacktestsController` / `BacktestsController`.
- **[ORCH]** Slice A's `DemoBacktestComparabilityReadService.RunIdQuery` and slice B's
  `CostDecompositionReadService.RunQuery` are explicit narrow `.Select()` projections naming only
  `r.Id` (and `r.Symbol` for B). A new column does **not** disturb either — the repo's design-D2/D3
  "never `SELECT *`" convention pays off here.
- **[ORCH]** Two read DTOs project `BacktestRun` for display: `BacktestRunDto` (paged list,
  `BacktestReadService.GetRunsAsync`) and `BacktestRunSummaryDto` (per-strategy,
  `GetByStrategyAsync`). The exploration named only the first.
- `SIMULATOR_ROADMAP.md` § *Planned change of inputs — October 2026* already records the problem:
  MT5 for all new builds, **no clean cutover** (*"The existing 123 strategies on SBDEMO2 are MT4"*),
  therefore *"the evidence layer must know which platform produced each run"* and *"carry the
  platform on the evidence from the start"*.
- **[ORCH] Verified verbatim** at `.agents/knowledge/imox/SERVICE_Darwinex_Zero.md:521-525`:
  *"MT4 => Roundtrip (100% applied at the entry) MT5 => 50% applied at the entry and 50% at the
  exit"*, flagged there as a backtest reconciliation gotcha. Same round-trip cost, different day
  attribution.
- Migration precedent `20260426190244_AddInitialBalanceToTradingAccount.cs` backfills a nullable
  decimal with a stated *"project default"* ($100,000) via `migrationBuilder.Sql`, framed as an
  analytics baseline rather than a historical assertion. **[ORCH] Confirmed** — it is the structural
  precedent for a commented backfill, though the *kind* of claim differs (see below).
- Code cannot prove the existing `BacktestRun` row count or their platform. No DB query was run
  (none authorized).

## Recommendation (as explored)

`PlatformType? SourcePlatform` (nullable) on `BacktestRun`, populated at import from a
**caller-supplied** value — never derived, because no derivation path is reliable. Plain nullable
rather than an `EmbeddedCostAvailability`-style multi-state enum, because no consumer branches on
*why* it is missing today.

## Where the exploration was overtaken

The exploration recommended **not** backfilling existing rows, on the grounds that asserting MT4
would fabricate a fact the code cannot support (`INDEX.md` §5). That reasoning is correct *about the
code* and is preserved. It was overtaken by evidence the code does not hold: on **2026-09-21 the
user explicitly asserted** that every `BacktestRun` loaded up to that date came from MT4-era work.
`INDEX.md` §5 forbids the **system inventing** a datum; it does not forbid **recording one the user
supplies**. The backfill is therefore legitimate *only* while its provenance travels with it — which
is why the proposal makes the migration comment load-bearing rather than decorative.

## Open questions the exploration raised

1. Whether `TradingAccount.Platform` should be a UI pre-fill suggestion. **Resolved:** suggestion
   only, never the source of truth — and the UI is deferred, so it does not arise in this slice.
2. Whether "unknown, never recorded" and "known but not yet supplied" ever need to be
   distinguished. **Deferred**, with a named trigger (see the proposal's R3).
3. Whether a backfill is wanted from the user's own SQX records. **Resolved by the user on
   2026-09-21** — yes, all pre-existing rows are MT4.
