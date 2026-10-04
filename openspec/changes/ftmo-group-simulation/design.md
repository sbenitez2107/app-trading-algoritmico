# Design: FTMO group simulation, on its own Simulator screen

> Proposal: `proposal.md` (12 settled decisions). Exploration: Engram #2979. Paths are relative to
> `app.trading.algoritmico.api/src/AppTradingAlgoritmico.*` (backend) and `app.trading.algoritmico.web/src/app` (frontend).
> Size note: this artifact exceeds the 800-word skill budget on purpose; the orchestrator asked for
> exact signatures, per-PR tables and ten numbered decisions.

## Technical Approach

Resolve inputs per member, project each member's run with the shipped `ProjectRun`, trim to the
group's common window, merge into one globally renumbered `ProjectedTrade` series per kind, and feed
it unchanged to the shipped `FtmoMultiStartReadService.ComputeRun`. Group-only output (window,
coverage, refusals, diagnostics) is computed by new pure classes. No engine file is edited.
`FtmoBreachEvaluator.cs` is never edited. The only shipped file modified is `FtmoSimulationInputs.cs` (B1).

## Data Flow

```
POST api/ftmo-simulations/group ──> FtmoSimulationsController (validate shape, 400)
        │
        v
FtmoGroupSimulationReadService (DB, batched, ct)
  1 strategies by ids   2 runs by strategyIds   3 limits row
  4 specs by symbols    5 calibrations by symbols   6 trades by runIds
        │  ResolveLimits + ResolveSymbol (pure, per distinct symbol)
        │  ProjectRun per (member, kind)  [shipped, unedited]
        v
FtmoGroupComputation.ComputeGroup(members, params)   (pure, per kind)
   refusals ─> FtmoGroupMerger.Intersect ─> FtmoGroupMerger.Merge (trim, sort, renumber, row map)
        │                                           │
        v                                           v
 ComputeRun(Guid.Empty, kind, seg, low, high, ...)  FtmoGroupDiagnostics(merged, rowMap, run.Starts)
        │   [shipped: enumerator, race, funded, evaluator — unedited]
        v
FtmoGroupSimulationDto { envelope, kinds[ {window, coverage, refusals, Run: FtmoMultiStartRunDto, diagnostics} ] }
        │
        v
/simulator/ftmo page ─> picker | form | FtmoRunPanelComponent (reused) | diagnostics panel
```

## Architecture Decisions

### D1 — B1: split `ResolveSharedAsync` (`FtmoSimulationInputs.cs:59-186`)

**Choice**: Two resolution units plus a composition that keeps the old entry point byte-for-byte equivalent.

```csharp
internal sealed record LimitsResolution(
    FtmoSimulationRefusal? Refusal, decimal DailyPct, decimal MaxPct, decimal? ProfitTargetPct);

// Lines 71-94 verbatim: LimitsNotConfigured -> ProductNotTwoStep -> DrawdownModelNotStatic.
internal static async Task<LimitsResolution> ResolveLimitsAsync(AppDbContext db, string broker, CancellationToken ct);

internal sealed record SymbolResolution(
    FtmoSimulationRefusal? Refusal,          // spec -> calibration -> FX, first failure wins (lines 96-160)
    FtmoInstrumentSpec? Spec, LotGrid? FtmoGrid, decimal PointValue,
    (decimal Low, decimal High) FxBand,      // (1,1) when the spec settles in USD
    FtmoSimulationRefusal? ZoneRefusal,      // TimeZoneDataUnavailable or null; evaluated only when Refusal is null
    TimeZoneInfo? SourceZone);

// Pure. `calibration` is read only when the spec is usable (preserves line 121's guard).
internal static SymbolResolution ResolveSymbol(
    FtmoInstrumentSpec? spec, SymbolCalibration? calibration, decimal? fxLow, decimal? fxHigh);

// Thin wrapper: spec query; calibration query only if the spec is usable; then ResolveSymbol.
internal static async Task<SymbolResolution> ResolveSymbolAsync(
    AppDbContext db, string sqxSymbol, decimal? fxLow, decimal? fxHigh, CancellationToken ct);

internal static bool TryResolveBerlin(out TimeZoneInfo? berlinZone);
internal static bool SettlesInAccountCurrency(FtmoInstrumentSpec spec); // ProfitCurrency == "USD"
```

`ResolveSharedAsync(db, request, ct)` keeps its signature and `SharedResolution` shape (`:30-43`) and
composes: runs (`:61-69`) -> `ResolveLimitsAsync` -> `ResolveSymbolAsync` (only if limits passed, so
the DB queries issued are identical) -> request validity / `InvalidRequest` (`:162-167`) -> `ZoneRefusal`
or Berlin failure -> `TimeZoneDataUnavailable` (`:169-181`). Zone resolution has no side effects, so
evaluating it inside `ResolveSymbol` and applying its refusal after `InvalidRequest` keeps the exact
precedence. Every refusal path fills `SharedResolution` with the same field values it has today.
`ProjectRun` (`:188-232`) is unchanged.

| Option | Tradeoff | Decision |
|---|---|---|
| Pure `ResolveSymbol` + async wrapper | Group can batch-load specs and calibrations; no N+1 | **Chosen** |
| Only an async per-symbol method | One query pair per distinct symbol (N+1 on symbols) | Rejected |
| Fold the zone refusal into `Refusal` | Breaks `InvalidRequest`-before-timezone precedence | Rejected |

**Callers** (all keep compiling unchanged): `ResolveSharedAsync`: `FtmoBreachSimulationReadService.cs:43`,
`FtmoMultiStartReadService.cs:48`. `ProjectRun`: `FtmoBreachSimulationReadService.cs:84`,
`FtmoMultiStartReadService.cs:97`, plus the new group service. **Proof**: every existing Ftmo suite runs
unedited, including the golden pin (`FtmoBreachSimulationReadServiceGoldenPinTests.cs:65`) and the snapshot
pin (`FtmoBreachSimulationReadServiceTests.cs:1037`). New tests cover only the new pure `ResolveSymbol` and `ResolveLimitsAsync`.

### D2 — Pure merger `FtmoGroupMerger` (new, B1)

```csharp
internal static class FtmoGroupMerger
{
    internal sealed record MemberSeries(int MemberOrder, IReadOnlyList<ProjectedTrade> Low, IReadOnlyList<ProjectedTrade> High);
    internal readonly record struct GroupWindow(DateTime Start, DateTime End);
    internal readonly record struct RowOrigin(int MemberOrder, int OriginalRowIndex);
    internal sealed record MergedSeries(
        IReadOnlyList<ProjectedTrade> Low, IReadOnlyList<ProjectedTrade> High,
        IReadOnlyList<RowOrigin> RowMap,              // index == merged RowIndex
        IReadOnlyList<int> InWindowCountByMember);    // index == MemberOrder

    internal static GroupWindow? Intersect(IReadOnlyList<MemberSeries> members); // null == empty
    internal static MergedSeries Merge(IReadOnlyList<MemberSeries> members, GroupWindow window);
}
```

- **Window**: per member, `F_m = min(OpenSource)` and `L_m = max(CloseSource)` over all its projected rows,
  Unscalable rows included. `W = [max F_m, min L_m]`. `max F > min L` means empty.
- **Trim**: keep a row when `Open >= W.Start && Close <= W.End`. A row that straddles the end
  (`Open <= W.End < Close`) is dropped: it would carry the group beyond the point where another member
  has data. A row opened before `W.Start` is dropped for the same reason, which is also the no-leak rule
  `SliceFromStart` uses (`FtmoStartEnumerator.cs:79-89`).
- **Trim after projection**. Â (`TradeRiskNormalizer`, `FtmoSimulationInputs.cs:204`) is a property of
  the whole run. Trimming first would re-size every trade and break one-member equivalence.
- **Low/high consistency**: `ProjectRun` builds `Low[i]` and `High[i]` from the same `trades[i]`
  (`:220-225`), so pairing is by position. The trim predicate reads only instants, which do not depend
  on FX. `Merge` throws `InvalidOperationException` if `Low[i]` and `High[i]` disagree on
  `RowIndex/OpenSource/CloseSource` (a wiring bug).
- **Renumber**: sort the surviving `(member, i)` by `(OpenSource, MemberOrder, OriginalRowIndex, i)`, then
  set merged `RowIndex = position` on both ends via `low[i] with { RowIndex = r }`.
  `ProjectedTrade` is a `sealed record` (`FtmoTradeProjector.cs:46`), so `with` works. The trailing `i`
  only makes the sort deterministic and is unreachable in production, because
  `(BacktestRunId, RowIndex)` is unique (`BacktestTradeConfiguration.cs:17`).
- **Why mandatory**: `AttributeOpenDays` keys by `RowIndex` (`FtmoChallengeRace.cs:248`), and the same map
  serves both FX ends (`FtmoMultiStartReadService.cs:148,159-160`). `HasConcurrentOpenPosition` skips
  "self" by `RowIndex` (`FtmoBreachEvaluator.cs:271`). Every member has rows `0..n-1`.
- **MemberOrder**: members are deduplicated, then sorted by `StrategyId` ascending. The group is a set,
  so the result does not depend on picker order (step 2 needs set semantics).

### D3 — FX per member

`ResolveSymbol` already returns `(1,1)` for USD-settling specs (`:141-145`) and the declared band
otherwise. That per-member `SymbolResolution.FxBand` is passed as `ProjectRun`'s `fxBand`, so USD members
project with 1 at both ends and EUR members (DAX) with the band. One request band is shared by all
non-USD members. The FTMO account currency lives in exactly one place: the constant
`UsdCurrency = "USD"` (`FtmoSimulationInputs.cs:21`). No entity stores it: `BrokerRiskLimits` has no
currency, and `TradingAccount.Currency` belongs to the source account. B1 exposes it through
`SettlesInAccountCurrency` so the candidates read does not repeat the literal. `ComputeRun` uses
`fxBand` only as an echo (`:142,192`). The group echo is the declared band if any member is non-USD,
otherwise `(1,1)`. Per-member bands are listed in the envelope.

### D4 — `ComputeGroup(members, params)` (pure, `FtmoGroupComputation`, B2)

```csharp
internal static FtmoGroupKindResultDto ComputeGroup(
    BacktestRunKind kind, IReadOnlyList<GroupMemberKindInput> members, GroupParams p, CancellationToken ct);
// GroupMemberKindInput(Guid StrategyId, string Name, int MemberOrder, Guid? RunId,
//                      FtmoSimulationRefusal? Refusal, FtmoSimulationInputs.RunProjection? Projection)
// GroupParams(TimeZoneInfo SourceZone, TimeZoneInfo BerlinZone, decimal InitialCapital, decimal DailyPct,
//             decimal MaxPct, decimal? ProfitTargetPct, (decimal, decimal) EchoFxBand, FtmoChallengeRulesDto Rules)
```

Steps: member refusals -> `Intersect` -> `Merge` -> refuse if any member has 0 in-window rows ->
`ComputeRun(Guid.Empty, kind, segment, merged.Low, merged.High, ..., unscalableCount, rules, ct)`.
Here `segment` is the members' common segment if they all agree, otherwise `Unknown`. That makes the
one-member result match on `Segment` too. `ComputeRun` only echoes the segment.

Checks against `ComputeRun` (`FtmoMultiStartReadService.cs:117-194`):

| Assumption | Holds on merged data? |
|---|---|
| `FtmoReplayCalendar.Build(projectedLow)` at `:134` calls `.First()` | Only when the list is non-empty. The empty-member guard ensures that |
| Anchor/start ordering by `(Open, RowIndex)` | Yes. The new `RowIndex` is monotone in `(Open, member, row)` |
| `Start1DiffersFromSingleStartAnchor` | Means "the window's earliest row is Unscalable". Same semantics, relabelled in the UI for groups |
| `unscalableCount` | Recomputed as `merged.Low.Count(Outcome == Unscalable)`, i.e. the in-window sum over members. Not `ProjectRun`'s full-run count |
| `FxRoundingSensitive` | True when any non-USD member's band flips a start. Meaningful as is |
| `ProfitTargetMismatch` (`:136-144`) | Comes from the broker-wide limits row, so it applies to the whole group. Returned unchanged with no diagnostics |

**One-member equivalence**: not unconditionally exact. Renumbering preserves `(Open, RowIndex)` order
exactly, but `(Close, RowIndex)` ties (`FtmoBreachEvaluator.cs:129-130`, `FtmoChallengeRace.cs:323-326`)
now break by Open instead of file row. **Criterion**: a one-member group equals the single
`FtmoMultiStartRunDto` on every field except `RunId` (Guid.Empty), whenever no two trades of that
member share a `CloseSource` with file-row order differing from Open order. The equivalence test uses a
fixture that satisfies this and asserts it. A second test documents the tie case.

### D5 — Service, DTOs, endpoint (B2)

- `Application/Interfaces/IFtmoGroupSimulationReadService.cs`:
  `Task<FtmoGroupSimulationDto> SimulateAsync(FtmoGroupSimulationParameters p, CancellationToken ct)`.
- **Request** (body, nullable fields): `FtmoGroupSimulationRequest(IReadOnlyList<Guid>? MemberStrategyIds,
  string? Broker, decimal? InitialCapital, decimal? TargetRiskPerTrade, decimal? FxLow, decimal? FxHigh,
  int? SizeDecimals, decimal? Step, decimal? MinLot, decimal? MaxLots)`. It stays serialisable for a later
  "save as portfolio".
- **Validation**: `TryValidateFtmoBreachQuery` is `private` in `StrategyBacktestsController.cs:267`, and
  that file is out of scope, so the new controller mirrors its rules in a private
  `TryValidateGroupRequest`. Missing required fields return 400 with the same "There is no default."
  message. Member count after dedup must be in `1..FtmoGroupSimulationLimits.MaxMembers`, otherwise
  400. Non-positive capital/risk or an invalid grid becomes an `InvalidRequest` refusal in the service,
  exactly like the single path (`FtmoSimulationInputs.cs:162-167`).
- **Response**: `FtmoGroupSimulationDto(FtmoSimulationStatus Status, FtmoGroupRefusal? Refusal,
  FtmoSimulationRefusal? SharedRefusal, decimal? DailyLossLimitPct, decimal? MaxLossLimitPct,
  IReadOnlyList<FtmoGroupMemberDto> Members /*id, name, order, profitCurrency, sourceTimeZoneId, fxLow/High applied*/,
  IReadOnlyList<Guid> DuplicateIdsRemoved, IReadOnlyList<FtmoGroupNameWarningDto> DuplicateNameWarnings,
  IReadOnlyList<FtmoGroupKindResultDto> Kinds, IReadOnlyList<string> Disclosures)`.
  `FtmoGroupKindResultDto(BacktestRunKind Kind, FtmoSimulationStatus Status, FtmoGroupRefusal? Refusal,
  IReadOnlyList<FtmoGroupMemberRefusalDto> MemberRefusals /*(id, name, FtmoGroupRefusal Reason, FtmoSimulationRefusal? RunReason non-null only when Reason is MemberRunRefused)*/, FtmoGroupWindowDto? Window /*start,end*/,
  IReadOnlyList<FtmoGroupMemberCoverageDto> Coverage /*first open, last close, in-window rows*/,
  FtmoMultiStartRunDto? Run, FtmoGroupDiagnosticsDto? Diagnostics)`. `Run` reuses the shipped DTO
  unchanged (`FtmoMultiStartDto.cs:108-126`).
- **`FtmoGroupRefusal`** (new, `Domain/Enums`): `InvalidRequest = 0` (generic CLR default, following the
  `FtmoSimulationRefusal` precedent), `SharedInputsRefused`, `MemberNotFound`, `MemberMissingKind`,
  `MemberRunRefused`, `MixedSourceTimeZones`, `NoCommonWindow`, `MemberHasNoTradesInWindow`.
  **Group-wide** refusals (whole request, no kind runs): `InvalidRequest`, `MemberNotFound`,
  `SharedInputsRefused` (broker limits/product/drawdown, plus `TimeZoneDataUnavailable`) and
  `MixedSourceTimeZones` (user decision 2026-10-03: the zone is a property of the group, not of a member;
  `FtmoGroupMemberDto` gains `SourceTimeZoneId` so the refusal can list each member with its zone).
  **Member-level** reasons (spec, calibration, FX, `RiskNotEstimable`, missing kind, empty in window)
  refuse only the affected kind's panel (both panels when the cause belongs to the symbol) and list
  `(member, reason)`. A zone is never a member-level reason, and a segment mismatch is not a refusal at all
  (`Segment` becomes `Unknown`, D4). `NoCommonWindow` is kind-level but blames no member. It is evaluated
  **per kind** (orchestrator decision 2026-10-03): an empty Deploy window never hides a valid Eval result.
  The other kind still runs (D4).
- **Dedup and warnings**: duplicate ids are removed and echoed. Two distinct members with the same
  `Name` (case-insensitive) produce a warning, because they are likely one logical strategy imported on
  two accounts (roadmap :305-307).
- **Controller**: `WebAPI/Controllers/FtmoSimulationsController.cs`, `[ApiController][Authorize]
  [Route("api/ftmo-simulations")]`, `POST group`. DI: `services.AddScoped<IFtmoGroupSimulationReadService,
  FtmoGroupSimulationReadService>()` next to `DependencyInjection.cs:113`.
- **Queries**: six fixed queries. All use `AsNoTracking` and `Contains` over id/symbol lists, and none
  depends on k. Trades load in one query (`BacktestRunId in runIds`) and are grouped in memory. All DB
  work finishes before computation starts, so the pure compute never touches the `DbContext`.
- **Cancellation**: `ct` goes to every query. `ComputeRun` checks it before each start (`:153`), and
  `ComputeGroup` checks it between kinds.
- **Disclosures**: `FtmoGroupSimulationLimits.Disclosures` holds the three proposal D10 texts. Each
  `Run` keeps the shipped multi-start disclosure.

### D6 — Diagnostics (`FtmoGroupDiagnostics`, pure, B3)

- **Per-member contribution, over the window, not per start**. Per-start nets over overlapping
  suffixes recount the same trades (they are not independent), and k × starts numbers would be
  unreadable. For each member and kind: in-window rows, scalable rows, net sum at the low and at the
  high end, and `RaisedToMinimum/CappedAtMaximum/Unscalable` counts. Exploration item 5 asks for these
  counts, and the DTO already exposes only `UnscalableCount`.
- **First-breach attribution**: for each `run.Starts` row, take the deciding breach: Phase 1
  `BreachedFirst`, else Phase 2, else Funded. Its candidate rows are the merged rows with
  `CloseSource == OutcomeSourceClose`, `Open >= StartSourceOpen`, `Net is not null`, and the phase-subset
  rule (`Open >= T && Close > T`, where T is the previous phase's close, matching
  `FtmoChallengeRace.cs:470-473` and `FtmoFundedPhase.cs:59`). Close instants and `Net is null` do not
  depend on FX, so either end's merged list gives the same set. `RowMap` turns that set into members.
  `PhaseResult` does not carry the breaching `RowIndex`, so **every member with a row at that close is
  credited**. This matches the D10 disclosure that ordering within a close is a modelling choice.
  Aggregated per member: starts credited by phase (P1/P2/Funded), plus `SoleContributorStarts` (set
  size 1). Per kind: `SharedCloseStarts` (set size > 1). Lookups go through a
  `Dictionary<DateTime, List<int>>` built once.
- **Peak concurrent open positions**: sweep over in-window scalable rows with `Close > Open`. Events
  are sorted by instant, and closes are processed before opens at the same instant, matching
  `FtmoOpenPositionSweep.cs:21-23` and the evaluator's strict overlap (`:274`). Outputs:
  `PeakConcurrentOpen`, `PeakFirstReachedSource`, `MembersAtPeak`. O(n log n).
- Nothing reads or modifies the evaluator. Diagnostics consume only the merged series, `RowMap` and the
  `ComputeRun` DTO.

### D7 — Group benchmark (B2)

- `tests/.../Ftmo/FtmoGroupBenchmarkFixture.cs`: k members. Each one is the shipped 1,000-trade fixture
  (`FtmoMultiStartBenchmarkFixture.cs:46`) shifted by `m × spacing / k` hours. Holds of 1-72 h against
  about 11 h of offset at k = 8 force cross-member overlap. Every member has `RowIndex 0..999`, so the
  benchmark exercises the merger's duplicate-index path. The fixture is closed-form and uses no `Random`.
- `FtmoGroupBenchmarkTests.cs`: `[BenchmarkFact]` (`BenchmarkFactAttribute.cs`, `FTMO_BENCH=1`), Release,
  median of 3, gate 5 s (`FtmoMultiStartBenchmarkTests.cs:26`). It measures `Merge` + `ComputeGroup` for
  **both kinds** (the whole request's CPU), with Never and Fast profiles.
- **Cap**: measure k ∈ {2,4,6,8,10}. `FtmoGroupSimulationLimits.MaxMembers = min(8, largest k whose Never
  median ≤ 5 s)`. The measured table goes into the test's doc comment and the PR description. The gate
  test asserts at `k = MaxMembers`, so raising the constant forces a new measurement.
- **If even k = 6 fails** (expected cost about k² from `HasConcurrentOpenPosition` and the `OpenAt` scans),
  with no evaluator edit: (a) lower the cap (default); (b) run both kinds' `ComputeGroup` in parallel
  via `Task.Run`. That is safe because `ComputeRun` and its callees are static and pure, and DB access
  has already finished. (c) Per-start parallelism would mean re-implementing `ComputeRun`'s loop, so it is
  rejected unless (a)+(b) cannot reach k = 4.

**Parallelism decision (2026-10-04, orchestrator, after the first B2 benchmark failed k=2).** The sequential
`ComputeRun` cost ~1.4-1.6 s per member for both kinds: ~120 monthly starts each re-evaluate their whole suffix
at two FX ends, at ~25 us per row (k=1 already 2.8-3.1 s). Option (c) above was taken, but INSIDE
`FtmoMultiStartReadService.ComputeRun`'s per-start loop (only the loop mechanics are edited; no re-implementation
in the group service, so the single-strategy endpoint speeds up too). `Parallel.For` over the starts, degree =
`Environment.ProcessorCount` (optional `maxDegreeOfParallelism` parameter for tests), cancellation token in
`ParallelOptions` and checked per start, results written to a pre-sized array by start index and then assembled
by the unchanged code, so the output is identical to the sequential loop (pinned by the golden and snapshot pins,
the multi-start/service/equivalence suites unedited, and `FtmoMultiStartParallelismTests`). A worker exception is
rethrown with its own type (not an `AggregateException`). Thread-safety: per-start inputs are read-only
(`projectedLow/High` and the open-day attribution dictionary are only indexed), each start builds its own slices,
`CachedOpenDays` (per `RunPhase`) and results; the only statics are immutable (`PhaseResult.NotStarted`,
`FundedResult.NotStarted`) and `TimeZoneInfo` is thread-safe. `FtmoBreachEvaluator.cs` is untouched. The two
kinds are NOT run in parallel with each other: the per-start `Parallel.For` already saturates the cores, a second
layer would add exception wrapping and nothing measurable. (b) in the list above is therefore not used.

### D8 — Picker candidates read (B3)

`GET api/ftmo-simulations/candidates?tradingAccountId={guid}` on the same controller, through
`IFtmoGroupCandidatesReadService`. `StrategiesController.cs:30` is not touched: its DTO has no run data
and serves portfolios.
`FtmoGroupCandidatesDto(Guid TradingAccountId, int MaxMembers, IReadOnlyList<FtmoGroupCandidateDto>)`;
`FtmoGroupCandidateDto(Guid StrategyId, string Name, string? Symbol, FtmoGroupCandidateRunDto? Deploy,
FtmoGroupCandidateRunDto? Evaluation, bool NameExistsOnOtherAccount)`;
`FtmoGroupCandidateRunDto(Guid RunId, string? Symbol, int TradeCount, DateTime? FirstOpen, DateTime? LastClose,
bool HasInstrumentSpec, bool IsCalibrated, string? ProfitCurrency, bool NeedsFxBand, string? SourceTimeZoneId)`.
It runs five fixed queries: strategies of the account; runs of those strategies; one `GroupBy(BacktestRunId)`
aggregate over trades (count, min open, max close); specs and calibrations by distinct run symbols;
names on other accounts. Flags are per run, because Deploy and Eval symbols could differ.
`IsCalibrated` mirrors the null-PointValue rule (`FtmoSimulationInputs.cs:128-131`).

### D9 — Frontend (F1-F4)

| Topic | Decision |
|---|---|
| Route | `app.routes.ts`: `{ path: 'simulator', loadChildren: () => import('./features/simulator/simulator.routes') }`. `SIMULATOR_ROUTES`: `'' -> redirect 'ftmo'`, `'ftmo'` with `loadComponent` for the page. It sits inside the guarded layout |
| Sidebar | New "Simulator" group in `main-layout.component.html` after FTMO (`:157-236`), with a `simulatorExpanded` signal and `toggleSimulator()`, collapsed by `toggleSidebar()` (`.ts:26-56`). Labels use `translate` (`SIMULATOR.NAV.*`) |
| Components | Container `ftmo-group-simulation-page`. Presentational `ftmo-group-picker` (AG Grid multi-row, following `portfolio-builder.component.ts:73`; symbol filter, search, flag columns; blocks selection beyond `maxMembers`), `ftmo-group-form`, `ftmo-group-diagnostics`. All standalone and OnPush, with `input()`/`output()` |
| Reuse | `FtmoRunPanelComponent`, `toRunPanelVm`, `toNotModelledItems` and the FTMO_SIMULATION keys are imported **in place** from `features/broker-accounts/ftmo-simulation-modal/`. The import is one-way, so there is no cycle. Moving them would edit the modal and its specs, which is out of scope. A move to `shared/ftmo/` is a follow-up refactor. `toNotModelledItems` takes an adapter `{ strategyId: '', runs }` |
| Lifecycle | Mirrors the modal (`ftmo-simulation-modal.component.ts:131-167`): runs only on click, `running()` blocks a second request, `takeUntilDestroyed` cancels on destroy, the error maps through the shared `mapFtmoRequestError`. Candidates reload on account change through `switchMap`, which also clears the selection |
| Service | `FtmoSimulationService` gains `getGroupCandidates(accountId)` and `simulateGroup(body)`. Both are additive and reuse its private error mapper |
| Account | Comes from `TradingAccountService.getAll()`. The default is the account named `SBDEMO2`, otherwise the first |
| k × risk | `computed`: `k × targetRisk / capital` against the academy 1% and the daily limit (echoed `DailyLossLimitPct`, falling back to FTMO 5% before the first run). After a run it also shows observed `PeakConcurrentOpen × risk` |
| FX inputs | Shown only when a selected candidate's run has `needsFxBand`. The UI learns this from the candidates read. When hidden, they are sent as `null`. The server stays authoritative (`FxRateNotDeclared`) |
| i18n | New `SIMULATOR` namespace (`NAV`, `FTMO_GROUP.*`) in `public/assets/i18n/{en,es}.json`. Panels keep `FTMO_SIMULATION.*` |
| Tests | Real-dictionary tests (PR1c/1d lesson): specs load the real `en.json`/`es.json` through a `TranslateLoader` and assert rendered text, never echoed keys. A parity + banned-wording sweep for `SIMULATOR` follows `ftmo-simulation.i18n.spec.ts:27-50` |
| Enum zero | TS `FtmoGroupRefusal` with explicit numbers. Labels via `labelFor` (`ftmo-simulation.mappers.ts:37-46`), with a dedicated test for the value-0 `InvalidRequest`. View-model state uses string discriminants |

### D10 — Tripwire and constraints

None of the new files appears in `BacktestPortfolioRiskTripwireTests.cs:27-47` `SliceFiles`.
`BacktestReadService.cs`, `BacktestsController.cs` and `GroupRiskAnalysisRequest.cs` are not touched.
The capability simulates exactly one caller-chosen group per request. There is no loop over
combinations, no ranking, no `Random`, no seed. `ComputeGroup` is the seam a step-2 generator will
call, and that generator must cross the tripwire deliberately.

## File Changes

| PR | File | Action |
|---|---|---|
| B1 (~350) | `Infrastructure/Services/FtmoSimulationInputs.cs` | Modify: D1 split |
| | `Infrastructure/Services/FtmoGroupMerger.cs` | Create |
| | `tests/.../Ftmo/FtmoGroupMergerTests.cs`, `FtmoSimulationInputsResolveSymbolTests.cs` | Create |
| B2 (~450-550) | `Domain/Enums/FtmoGroupRefusal.cs`; `Application/DTOs/Backtests/FtmoGroupSimulationDto.cs`; `Application/Interfaces/IFtmoGroupSimulationReadService.cs` | Create |
| | `Infrastructure/Services/FtmoGroupComputation.cs`, `FtmoGroupSimulationReadService.cs`, `FtmoGroupSimulationLimits.cs` | Create |
| | `Infrastructure/DependencyInjection.cs` | Modify: +1 registration |
| | `WebAPI/Controllers/FtmoSimulationsController.cs` | Create (POST group) |
| | `tests/.../Ftmo/FtmoGroupComputationTests.cs`, `FtmoGroupSimulationReadServiceTests.cs`, `FtmoGroupBenchmarkFixture.cs`, `FtmoGroupBenchmarkTests.cs`, `FtmoGroupEnumZeroDefaultTests.cs` | Create |
| B3 (~350) | `Infrastructure/Services/FtmoGroupDiagnostics.cs`, `FtmoGroupCandidatesReadService.cs`; `Application/Interfaces/IFtmoGroupCandidatesReadService.cs`; `Application/DTOs/Backtests/FtmoGroupCandidatesDto.cs` | Create |
| | `FtmoGroupComputation.cs`, `FtmoGroupSimulationDto.cs` (+diagnostics DTOs), `FtmoSimulationsController.cs` (+GET), `DependencyInjection.cs` | Modify |
| | `tests/.../Ftmo/FtmoGroupDiagnosticsTests.cs`, `FtmoGroupCandidatesReadServiceTests.cs` | Create |
| F1 (~150) | `app.routes.ts`, `shared/layout/main-layout/main-layout.component.{ts,html}`, `public/assets/i18n/{en,es}.json` | Modify |
| | `features/simulator/simulator.routes.ts`, `features/simulator/ftmo-group-simulation-page/*` (shell + spec) | Create |
| F2 (~350) | `core/models/ftmo-group-simulation.model.ts`, `features/simulator/ftmo-group-picker/*` | Create |
| | `core/services/ftmo-simulation.service.ts` (+candidates), page, i18n | Modify |
| F3 (~400) | `features/simulator/ftmo-group-form/*`, `features/simulator/ftmo-group-simulation.mappers.ts` (+spec), `simulator.i18n.spec.ts` | Create |
| | model (request/response, enum), service (+simulateGroup), page (run lifecycle, reused panels, readout), i18n | Modify |
| F4 (~250) | `features/simulator/ftmo-group-diagnostics/*` | Create |
| | mappers (+diagnostics VM), page, i18n | Modify |

## Testing Strategy (Strict TDD: RED first per unit)

| Layer | What | How |
|---|---|---|
| Pin | B1 behaviour preservation | Every shipped Ftmo suite unedited and green, golden pin and snapshot pin included. `git diff --stat` shows the test folder untouched |
| Unit | `ResolveSymbol` | Each refusal in order; calibration not read when the spec is refused; USD yields `(1,1)`; zone refusal kept separate |
| Unit | Merger | Members with **duplicate per-run indices** (all 0..n-1) yield unique merged `RowIndex 0..N-1`, and `RowMap` round-trips to `(member, origRow)`; overlapping members are interleaved by Open; same-Open ties break by member, then row; low and high carry identical renumbering; low/high mismatch throws; window intersection, empty window, straddling rows dropped at both edges; Unscalable rows kept |
| Unit | `ComputeGroup` | Missing kind refuses that kind only; member refusals listed; empty-in-window refusal; segment common/Unknown; `unscalableCount` is the in-window sum; ProfitTargetMismatch passthrough; **cross-member concurrency** (A open across B's breach close) produces `ConcurrentOpenPosition`/Contingent through the unedited evaluator; a falsification test without renumbering shows `AttributeOpenDays` collapsing keys |
| Unit | Diagnostics | Same-close multi-member credit, sole contributor, phase-subset filter, peak sweep with zero-duration rows and close-before-open ties |
| Integration | Service (EF test DB as in `FtmoMultiStartReadServiceTests`) | **One-member equivalence** (D4 criterion), dedup, name warning, unknown id, mixed zones, DAX FX band, query count does not depend on k |
| Benchmark | D7 | `FTMO_BENCH=1`, Release, median of 3 |
| Frontend | Mappers, picker, form, page, diagnostics, i18n | Vitest. Real dictionaries; enum zero; `canRun` with 0 values; in-flight block; destroy cancels; FX visibility |

## Threat Matrix

N/A. The change adds no shell, subprocess, VCS/PR automation, executable-file classification or
process-integration boundary. It adds one authenticated (`[Authorize]`) read-only HTTP controller
and one SPA route. The member cap doubles as a CPU-exhaustion guard.

## Migration / Rollout

No migration, no persisted state. Seven chained PRs in order B1 → B2 → B3 → F1 → F2 → F3 → F4.
F1 can merge before B2, because the shell has no backend call. Rollback: revert the PRs in reverse
order. Reverting B1 restores `ResolveSharedAsync` with no consumer impact, because B2 is its only new caller.

## Open Questions

- [x] `MaxMembers = 4` (B2.4.4): after per-start parallelism the Never median (both kinds, Release, 2 runs) is k=2 1.665/1.454 s, k=4 3.785/3.468, k=6 7.081/6.831, k=8 10.682/9.575, k=10 16.635/16.055. Before it: k=2 4.499/6.176, k=4 11.387/17.583.
- [ ] Does SQX file-row order match Open order within a run? This only affects the D4 tie criterion. Verify on real SBDEMO2 data during B2.
