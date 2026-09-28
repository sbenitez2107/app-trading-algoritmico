# Design: FTMO simulation UI — multi-start distribution, then single-start detail

## Technical Approach

A strategy-row modal in `broker-accounts` (PR1a–d, PR2a–b). No backend change: the originally proposed
PR0 (`GET api/ftmo-instrument-specs`) is dropped (see "Resolved: the lot grid hazard" below). The modal
gathers all 8 required query params, calls `GET api/strategies/{id}/ftmo-breach/multi-start` only on an
explicit Run, maps the DTO once into discriminated-union view models, and renders Deploy and Evaluation
as two separate panels. Every existing pattern is mirrored: modal lifecycle from
`account-detail.component.html:446-452`, enum mirrors and label records from `backtest.service.ts:12-41,290-362`,
signal form state from `group-risk-panel.component.ts:55-113`.

**Resolved: the lot grid hazard.** The four grid query params (`sizeDecimals`, `step`, `minLot`,
`maxLots`) are the SQX backtest's own SOURCE grid, used by `TradeRiskNormalizer`
(`FtmoBreachSimulationDto.cs:24`, `FtmoSimulationInputs.cs:162,204`). The FTMO grid is a different thing,
read by the backend itself from `FtmoInstrumentSpec` (`FtmoSimulationInputs.cs:111`), and the values
differ: FTMO has `maxLots` 1000 for XAUUSD (migration `20260925122543:32`), while `LotGrid.ImoxRetester`
has 10 (`LotGrid.cs:71`). Prefilling the request grid from `FtmoInstrumentSpec` would therefore declare
the wrong grid — this is why the originally proposed PR0 (a spec-listing endpoint) is dropped entirely.
Instead, the four source-grid fields are prefilled from a **hardcoded frontend constant** that mirrors
`LotGrid.ImoxRetester` (sizeDecimals 2, step 0.01, minLot 0.01, maxLots 10), defined once in
`ftmo-simulation.model.ts` (PR1a) and labelled in the UI as the **backtest (IMOX retester) lot grid**,
never as the FTMO grid. The fields stay editable. A missing `FtmoInstrumentSpec` row or an undeclared FX
rate is not detected client-side; it surfaces as the backend's own refusal (`InstrumentSpecMissing`,
`FxRateNotDeclared`, `InvalidFxBand` — `Domain/Enums/FtmoSimulationRefusal.cs`), each rendered with its
own distinct i18n message by the existing refusal-mapping mechanism (AD4). See AD7 (revised).

## Architecture Decisions

| # | Choice | Rejected | Rationale |
|---|---|---|---|
| AD1 | A modal opened from the row actions (`account-detail.component.ts:482-527`), rendered via `@if (ftmoTargetStrategy(); as t)`. | A `/strategies/:id/ftmo` route. | No per-strategy route exists. Every per-strategy view in the codebase is a modal (explore #2848). Destroying the modal via `@if` gives cancellation for free (AD10). |
| AD2 | `core/models/ftmo-simulation.model.ts` holds the enums, DTOs, and the `IMOX_RETESTER_LOT_GRID` constant. `core/services/ftmo-simulation.service.ts` holds `getMultiStart` and, in PR2, `getSingleStart`. `BacktestRunKind` and `BacktestSegment` are reused from `backtest.service.ts:19-35`. | Growing `backtest.service.ts`, which is already 500 lines. One service per endpoint. | Two cohesive methods avoid a God service. Redeclaring the run-kind enums would let them drift. |
| AD3 | The wire DTOs are interfaces that mirror the C# records 1:1. A pure `ftmo-simulation.mappers.ts` builds `FtmoRunPanelVm`. The container exposes `panels = computed(() => toPanels(result()))`. | Mapping inside the templates or components. A full domain-model layer. | `frontend-data.md` ("Mapping data in Component" anti-pattern). Enum values are decoded once, at a tested seam. The templates then switch on **string** discriminants, so no template can ever test a `0` for truthiness. |
| AD4 | Explicit numeric mirrors (table below). Labels are `Record<Enum, string>` of i18n keys, reached through accessors: `x === null \|\| x === undefined ? null : MAP[x] ?? UNKNOWN_KEY`. | `switch` with `default: ''` (`portfolio-detail.component.ts:701-709`). | `Record<Enum,…>` fails to compile when a member is missing. An unknown value renders `FTMO_SIMULATION.UNKNOWN_VALUE` with the raw number, never an empty string and never another member's label. |
| AD5 | Container: `FtmoSimulationModalComponent` (form, spec load, run lifecycle, result). Presentational children: `FtmoRunPanelComponent`, `FtmoOutcomeBarsComponent`, `FtmoOrderStatsTableComponent`, and in PR2 `FtmoSingleStartDetailComponent`. All are OnPush with `input.required<Vm>()`. | Putting the form in its own component. A single monolithic template. | The form has no reuse, and 10 `model()` bindings would add wiring without value. The panels are pure functions of their VM, so they can be tested without HTTP. |
| AD6 | One signal per field (`number \| null`, `string`). Fields use `[ngModel]`/`(ngModelChange)` with `FormsModule`, as the group-risk-panel precedent does. `canRun = computed(...)` requires all 8 fields — broker, sqxSymbol, initialCapital, targetRiskPerTrade, sizeDecimals, step, minLot and maxLots — to be `!== null`, finite numbers, and `!running()`. On Run, the inputs are snapshotted into `lastQuery`. | Reactive `FormGroup`. Requiring only 4 fields. | The controller returns 400 unless all 8 are present (`StrategyBacktestsController.cs:275-282`); `fxLow`/`fxHigh` remain optional. Invalid values that are present (for example `step=0`) pass through to the backend's own `InvalidRequest` refusal, which is its single validation surface. `sizeDecimals=0` is legal, so there is no truthiness check anywhere. |
| AD7 | **(Revised — no spec fetch)** The four source-grid fields (`sizeDecimals`, `step`, `minLot`, `maxLots`) are prefilled once, on modal open, from a hardcoded constant in `ftmo-simulation.model.ts` that mirrors `LotGrid.ImoxRetester` (2 / 0.01 / 0.01 / 10), labelled as the backtest (IMOX retester) lot grid. The fields stay editable. There is no client-side spec lookup and no facts box: a missing `FtmoInstrumentSpec` row, an undeclared FX rate, or an invalid FX band are not detected client-side — they surface as the backend's own refusal (`InstrumentSpecMissing`, `FxRateNotDeclared`, `InvalidFxBand`) when Run is activated, each rendered with its own distinct i18n message. | Fetching `FtmoInstrumentSpec` rows and writing the FTMO grid into the request fields (the original PR0 plan). A read-only facts box sourced from a spec endpoint. | Writing the FTMO grid in would substitute it for the source grid (see the hazard above) — this is why PR0 is dropped entirely, not merely display-only. The backend's own refusal enums already give the user a specific, correct reason without a second endpoint. |
| AD8 | account-detail passes `strategyId`, `strategyName`, `sqxSymbol = strategy.symbol` verbatim, and `broker = account()?.broker ?? null` (the `account-detail.component.html:97` precedent). broker and sqxSymbol stay editable. The action is shown on every row. | `route.snapshot.data['broker']`. Hiding the action outside `/ftmo`. | This follows the Monthly modal. Accounts are filtered by broker, so an `/ftmo` account yields `'FTMO'`. On other brokers the backend's refusal (`LimitsNotConfigured`) is displayed honestly, with no magic-string gating. |
| AD9 | `.ftmo-sim__runs { display:grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 30rem), 1fr)) }` inside the 72rem modal shell (`strategy-analytics-modal.component.scss:19`). There are always two fixed slots, Deploy then Evaluation, ordered by kind value. A missing kind shows `NO_RUN_HELD`. The panels contain no green. | Tabs. A `@media` query. Server order. | Two slots side by side stack on their own without a breakpoint and never merge. Green means gain (`frontend-design.md`), which would read as "passed". |
| AD10 | `run()` returns early while `running()`. It clears `result` and `error`, then subscribes with `takeUntilDestroyed(destroyRef)`. HttpClient aborts the request on unsubscribe, which trips `RequestAborted`, so `ct` reaches `FtmoMultiStartReadService.cs:59`. Errors map to `FtmoRequestError { key, detail }`: a 400 becomes `ERRORS.INVALID_QUERY` with the server's `{ message }` (`StrategyBacktestsController.cs:255`) as detail data; anything else becomes `ERRORS.REQUEST_FAILED`. | Leaving the subscription alive, as the analytics modal does. A `switchMap` trigger. | Run is disabled in flight, so a second request cannot be sent. `rxjs-interop` is already in use (`account-detail.component.ts:211`). A stale result is never shown after an error. |
| AD11 | **(Revised — disclosure is always-visible data, not a translated sentence)** All copy lives under a new top-level `FTMO_SIMULATION` namespace in `en.json` and `es.json`. An i18n-keyed disclosure block is always visible, even before a run — this is an explicit, intentional exception to "every visible string is i18n" (see the spec's revised requirement). Each run's server `disclosure` and `notModelled` text is bound **verbatim as data**, exactly as the server returns it, and is never looked up as, or expected to resolve to, an i18n key. | Translating the server's disclosure. Reusing the `SQX` keys. Requiring the server text to itself be an i18n key. | The namespace scopes the banned-word test, because SQX keys legitimately use "passed" (`es.json:175`). The server text is authoritative data (`frontend-data.md`, "UI vs Data") and is shown as-is, not translated per-locale. |
| AD12 | PR2 adds a section below the runs with its own "Show single-start detail" action. It calls `getSingleStart(strategyId, lastQuery())`, is enabled only after a multi-start success, and has its own in-flight flag. | Firing both requests from Run with `forkJoin`. | Run still sends exactly one request, so PR1 behaviour and specs stay unedited. `lastQuery` keeps the detail consistent with the distribution on screen. |
| AD13 | **Dropped.** PR0 (`FtmoInstrumentSpecsController`, `IFtmoInstrumentSpecReadService`, `FtmoInstrumentSpecReadService`) is removed entirely; there is no backend change in this change. See "Resolved: the lot grid hazard" above and the proposal's "Dropped: PR0" section. | — | The spec-listing endpoint existed only to prefill the request grid, which was the wrong grid to send (see the hazard). Nothing else in this change consumes it. |

### Enum mirrors (explicit values, copied from `Domain/Enums/*.cs`)

| Enum | Members (value) | PR |
|---|---|---|
| `FtmoSimulationStatus` | **Refused=0**, Evaluated=1 | 1a |
| `FtmoSimulationRefusal` | **InvalidRequest=0**, ProductNotTwoStep=1, LimitsNotConfigured=2, DrawdownModelNotStatic=3, InstrumentSpecMissing=4, PointValueNotCalibrated=5, FxRateNotDeclared=6, InvalidFxBand=7, RiskNotEstimable=8, RunSegmentsDisagree=9, TimeZoneDataUnavailable=10 | 1a |
| `FtmoChallengeRaceRefusal` | **ProfitTargetMismatch=0** | 1a |
| `FtmoChainOutcome` | **Phase1UndecidedAtEndOfData=0**, Phase1Breached=1, Phase2Breached=2, Phase2UndecidedAtEndOfData=3, FundedBreached=4, FundedNoBreachAtEndOfData=5 | 1a |
| `FtmoPhaseOutcome` | **NotStarted=0**, TargetReachedFirst=1, BreachedFirst=2, NeitherByEndOfData=3 | 1a |
| `FtmoFundedOutcome` | **NotStarted=0**, BreachedFirst=1, NoBreachByEndOfData=2 | 1a |
| `FtmoFirstBreachingLimit` | **Daily=0**, Max=1, BothSameClose=2 | 1a |
| `FtmoBreachPointClass` | **Clean=0**, Contingent=1 | 1a |
| `FtmoFxBandEnd` | **FxLow=0**, FxHigh=1, BothEnds=2 | 1a |
| `FtmoStartGrain` | **Monthly=0** | 1a |
| `FtmoBreachVerdict` | **BreachContingent=0**, Breached=1, NoBreachObserved=2 | 2a |
| `BreachContingencyCause` | **ConcurrentOpenPosition=0**, AmbiguousSourceTime=1, InvalidSourceTime=2, DstMismatchWindow=3, UnscalableTradeExcluded=4, FxRoundingSensitive=5 | 2a |
| `BacktestRunKind` / `BacktestSegment` | reused (Deploy=1, Evaluation=2 / **Unknown=0**, InSample=1, OutOfSample=2, InSampleTest=3) | — |

The wire format is integers: no `JsonStringEnumConverter` is registered anywhere in `src/`; the only `JsonSerializerOptions` is `GridPresetService.cs:12`. `DateOnly` values arrive as `"yyyy-MM-dd"` and are formatted by slicing the string, **never** through `new Date()`, because a UTC-midnight parse shows the previous day at UTC-3.

## Data Flow

```
User   Modal (container)        FtmoSimulationService       API                      ReadService
 │ open ──→ sourceGrid fields prefilled from the IMOX_RETESTER_LOT_GRID constant (no request)
 │ Run ──→ canRun && !running → running=true, lastQuery=snapshot
 │         getMultiStart(id,q) ──→ GET .../ftmo-breach/multi-start?8 params[+fx]
 │                                   400 {message} ◀── TryValidateFtmoBreachQuery
 │                                   200 FtmoMultiStartDto ◀── SimulateAsync(ct)
 │         result.set(dto) → panels = computed(toPanels) → RunPanel ×2 → Bars / StatsTable
 │ close → @if destroys → takeUntilDestroyed → XHR abort → RequestAborted(ct)
```

Per-run VM state, in order: `status === Refused` → `refused`; else `raceRefusal !== null` → `raceRefused`
(with `storedProfitTargetPct`); else `summary === null` → `noStarts`; else `evaluated`. Every state
renders the disclosure, `monthsWithoutStart` (count plus `yyyy-MM`), the start-1 flag, `notModelled`,
the FX band and the unscalable count. Outcome rows iterate a fixed six-value order, and a value missing
from the server renders `NOT_REPORTED`, never a fabricated 0. The stats rows are phase-1, phase-2,
both targets, **funded from the funded start (headline)**, funded from the chain start (secondary,
muted), and censored runway. A `null` renders "—"; `0` renders "0".

## File Changes

`api/src/…` = `app.trading.algoritmico.api/src/AppTradingAlgoritmico.*`, `web/…` = `app.trading.algoritmico.web/src/app/…`, `FM/` = `web/features/broker-accounts/ftmo-simulation-modal/`.

| PR | File | Action |
|---|---|---|
| 1a | `web/core/models/ftmo-simulation.model.ts` | Create — the 10 enums, the multi-start DTOs, `FtmoSimulationQuery`, the `IMOX_RETESTER_LOT_GRID` constant |
| 1a | `web/core/services/ftmo-simulation.service.ts` + `.spec.ts` | Create — `buildFtmoQueryParams`, error mapping |
| 1b | `FM/ftmo-simulation.mappers.ts` + `.spec.ts` | Create — labels, accessors, `toPanels`, formatters |
| 1b | `public/assets/i18n/{en,es}.json` | Modify — the `FTMO_SIMULATION.*` namespace |
| 1b | `FM/ftmo-simulation.i18n.spec.ts` | Create — EN/ES parity, banned words, every label key resolves |
| 1c | `FM/ftmo-{outcome-bars,order-stats-table,run-panel}/*.{ts,html,scss,spec.ts}` | Create |
| 1d | `FM/ftmo-simulation-modal.component.{ts,html,scss,spec.ts}` | Create |
| 1d | `web/features/broker-accounts/account-detail/account-detail.component.{ts,html}` (+ spec) | Modify — row button (i18n title), `ftmoTargetStrategy` signal, `@if` host |
| 2a | model, service (+`getSingleStart`), mappers, i18n, and their specs | Modify — `FtmoBreachVerdict`, `BreachContingencyCause`, single-start DTOs |
| 2b | `FM/ftmo-single-start-detail/*` | Create |
| 2b | the container `.ts`/`.html`, plus a new `ftmo-simulation-modal.single-start.spec.ts` | Modify / Create — the PR1 spec file stays untouched |

## Interfaces / Contracts

```ts
export type FtmoRunPanelState = 'notHeld' | 'refused' | 'raceRefused' | 'noStarts' | 'evaluated';
export interface FtmoRequestError { key: string; detail: string | null } // detail = server 400 message
// buildFtmoQueryParams: every required param via .set(k, String(v)); fxLow/fxHigh only when !== null
// IMOX_RETESTER_LOT_GRID: hardcoded frontend constant mirroring LotGrid.ImoxRetester (LotGrid.cs:71):
// { sizeDecimals: 2, step: 0.01, minLot: 0.01, maxLots: 10 } — labelled "backtest (IMOX retester) lot grid"
```

## Testing Strategy (Strict TDD: RED first)

| Layer | What | Approach |
|---|---|---|
| Vitest service | URL; all 8 params, with `sizeDecimals=0` sent; fx omitted when null; 400 → `INVALID_QUERY` + message; 500 → `REQUEST_FAILED` | `HttpTestingController` (`backtest.service.spec.ts` style) |
| Vitest mappers | **Every value-0 member**: Refused, InvalidRequest, ProfitTargetMismatch, Phase1UndecidedAtEndOfData, both NotStarted, Daily, Clean, FxLow, Monthly, BreachContingent, ConcurrentOpenPosition, Unknown. Each state branch; six rows with zeros; Min=0 renders "0"; N=0 renders "—"; `yyyy-MM` slicing; distinct i18n message per `InstrumentSpecMissing`/`FxRateNotDeclared`/`InvalidFxBand` | Pure functions, no TestBed |
| Vitest i18n | Keys under `FTMO_SIMULATION` only; lowercase, diacritic-stripped substring check against the 9 banned phrases; EN/ES key parity | Imports the JSON files (the `portfolio-detail.component.spec.ts:5-6` precedent) |
| Vitest components | Panels render every state; stacking uses two `<section>`s; the container blocks a second Run in flight (one `expectOne`); an edit sends no request; an error clears the result; destroying the fixture cancels the request | TestBed + `TranslateModule.forRoot()`, mocked service |

## Threat Matrix

N/A. There is no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary. This change makes no backend change.

## Migration / Rollout

No migration and no backend change. The PRs are chained in the order 1a → 1b → 1c → 1d → 2a → 2b, and
each is additive. The feature becomes reachable only when 1d lands; 1a–1c add code with no importer yet.
Rollback is a revert of any slice in reverse order. There is no persisted state.

Estimated sizes, in authored lines including tests: 1a ~360 · 1b ~500 · 1c ~600 · 1d ~600 (PR1 total
~1,900 lines across 1a–1d) · 2a ~400 · 2b ~450. **400-line budget risk: High.** 1b–1d exceed the budget,
so `sdd-tasks` must either split them further or record a `size:exception`.

## Open Questions

- [x] **Resolved**: the SOURCE grid fields are prefilled with the `IMOX_RETESTER_LOT_GRID` constant
  (2 / 0.01 / 0.01 / 10, mirroring `LotGrid.ImoxRetester`) and labelled as the backtest (IMOX retester)
  lot grid. Prefilling from a spec fetch is not viable (see the hazard above); the endpoint is dropped.
- [ ] Non-blocking: should the row action be hidden outside FTMO accounts?
