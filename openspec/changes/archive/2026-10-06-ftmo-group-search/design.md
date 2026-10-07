# Design: FTMO group search (automatic 2..4 strategy combinations)

## Technical Approach

A new pure funnel (`FtmoGroupSearchEngine`) composes the shipped `ComputeGroup`
(`FtmoGroupComputation.cs:47`) once per shortlisted candidate. It never loops inside the
one-group capability. Stages: load the pool (read-only, once) -> eligibility -> projection cache ->
enumerate C(P,k) in `StrategyId` order with exact monotone prunes -> proxy on the candidate's own
merged common window -> per-k shortlist -> full `ComputeGroup` (both kinds) -> headroom -> rank.
The funnel runs inside one in-memory background job (a singleton registry plus a
`BackgroundService`), polled by a new `/simulator/ftmo/search` page. `FtmoBreachEvaluator.cs`,
`ComputeRun`, `FtmoGroupComputation.cs`, `FtmoGroupDiagnostics.cs`, `FtmoGroupMerger.cs` and the 18
tripwire slice files are never edited.

## Architecture Decisions

### D1 Member-resolution extraction
**Choice**: Move lines `FtmoGroupSimulationReadService.cs:33-41` (records), `:57-60` (strategies
query), `:87-138` (runs/specs/calibrations/resolutions/members), `:170-181` (trades),
`:184-191` (echo band/rules/`GroupParams`) and `:207-231` (`ToKindInput`) VERBATIM into a new
`internal static class FtmoGroupMemberResolution` with staged methods:
`LoadNamesAsync(db, ids, ct)`, `LoadMembersAsync(db, knownOrdered, names, fxLow, fxHigh, ct) -> ResolvedMembers`
(Members, Resolve, Symbols), `LoadTradesAsync(db, members, ct)`,
`BuildGroupParams(members, resolve, limits, berlin, capital, fxLow, fxHigh)` and `ToKindInput(...)`.
`SimulateAsync` keeps the orchestration and every group-wide refusal in its current precedence
(`:52-162`). It calls the stages in the same order, so it keeps the same six queries, the same
short-circuits before the limits and runs queries, and the same `CountingInterceptor` count
(`FtmoGroupSimulationReadServiceTests.cs:612-640`).
**Alternatives**: one `ResolveAsync` returning everything. Rejected: it would issue the runs, specs
and trades queries after an unknown-id or limits refusal, which changes the query count.
Duplicating the code in the search loader was also rejected: the two copies would drift, which
breaks the "metrics equal a direct run" criterion.
**Rationale**: the search reuses the exact projection inputs and the per-group `GroupParams`
rule (source zone = first resolved member zone; echo band = non-USD among THIS group's members,
`:184-185`). That is the only way parity can hold. Existing tests stay unedited.

### D2 Per-job projection cache
**Choice**: `FtmoProjectionCache`, built once per job. Key `(StrategyId, Kind)`, value is the
`GroupMemberKindInput` from `ToKindInput` at the job's single risk, grid and FX band, with
placeholder `MemberOrder`. Per candidate the inputs are rebound with `with { MemberOrder = i }`
(i = index in ascending-`StrategyId` order). It also holds one shared
`Dictionary<DateTime, DateOnly>` (close instant -> `FtmoDayClock.Attribute(..).BookkeepingDay`) and
each member's instrument set and coverage `[first open, last close]`. The cache dies with the job.
**Memory**: about 2 kinds x 2 FX ends x rows x ~120 B. P=24 with ~1k trades is ~12 MB; P=60 with
~3k trades is ~85 MB. `MaxPoolSize` (D5) bounds this.
**Alternative**: re-projecting per candidate. Rejected: up to 4x12,926 redundant `ProjectRun`
calls. The projection depends on (trades, grid, symbol, risk, FX) only, never on the group.

### D3 Proxies and enumeration
**Choice**:
- Eligibility runs per strategy and every exclusion is listed with its reason. A strategy is
  eligible when:
  - it holds both a Deploy and an Evaluation run;
  - `SymbolRefusedBy == null` (spec, calibration and FX band);
  - its zone is resolved;
  - neither kind's projection is refused or rowless.

  The `IdenticalKinds` check compares Deploy and Eval projected Low and High series by
  `(OpenSource, CloseSource, Net, Outcome)`. Such strategies are excluded unless the user opts
  in, and are then flagged.
- Enumerate lexicographic index combinations over the eligible pool sorted by `StrategyId`, for
  k = kMin..kMax (`<= FtmoGroupSimulationLimits.MaxMembers`, `FtmoGroupSimulationLimits.cs:15`).
- Exact prunes, applied before the proxy:
  - (a) per-instrument count `> maxPerInstrument` (default 2, `FtmoGroupSearchLimits.DefaultMaxPerInstrument`;
    USER DECISION 2026-10-04: the first real calibration on SBDEMO2 had 130 strategies, 18 eligible, 4029 enumerated,
    72 survivors, all pairs, shortlist 72: with a cap of 1 the pool covered only 2 instruments (gold, NQ; DAX had no
    FX band, BTC no backtests), so recall 1.0 was vacuous. The calibration now fails when the shortlist holds every
    survivor);
  - (b) a pair conflict: different source zones, or disjoint coverage. In 1-D, pairwise-overlapping
    intervals share a common point (Helly), so the pair prune is exactly `Intersect != null`
    (`FtmoGroupMerger.cs:58-69`);
  - (c) Academy 1% rule (opt-in): k x risk > 1% x capital (k = member count, the spec's exact rule; a peak-based variant was rejected as it needs the proxy first).
- Proxy, per kind and per candidate: `Intersect` + `Merge` (the shipped pure functions, so it runs
  on the candidate's actual common window, which covers the window-shrink gotcha). Then:
  - `FtmoDailyLossProfile.Compute(merged.Low|High, dayOf, capital, dailyPct)`. This is a new pure
    class that REPLICATES the evaluator's moving-floor rule (`FtmoBreachEvaluator.cs:128-137`
    ordering and seed; `:159-184` the day-change reference = balance carried from the previous
    day; null nets skipped `:161-169`). It returns the worst day loss = max over days of
    (reference − min intraday balance), plus its day.
  - `FtmoPeakConcurrency.Compute(merged)` MIRRORS the private sweep (`FtmoGroupDiagnostics.cs:166-211`:
    scalable rows, `Close > Open`, closes before opens).
  - A member with zero in-window rows prunes with the reason `MemberHasNoTradesInWindow`.
- Proxy score = the race surrogate (`FtmoRaceSurrogate`, see D9): breach share, then headroom desc, then median days (none last), then peak, then the combo key. Run
  the proxy with `Parallel.For` writing results by candidate index, so the output is deterministic.
**Pins**: (1) parity test: on fixtures, `Evaluate(...).Daily.Verdict != NoBreachObserved` holds
iff `WorstDayLoss > dailyPct·capital`, and the first breach day equals the profile's first
exceeding day (evaluator only called from tests). (2) The proxy peak equals
`ComputeGroup(...).Diagnostics.Peak.PeakConcurrentOpen` on the same members.
**Alternative**: approximating with summed per-member daily P&L. Rejected: it ignores intraday
ordering and window shrink, and cannot be pinned exactly.

### D4 Headroom definitions (new pure `FtmoLimitHeadroom`, never inside the evaluator)
- **(a) Daily**: `dailyUsed = WorstDayLoss / (dailyPct·capital)`. It is computed on the merged
  common-window series at BOTH FX ends, and the worse end is reported. It is start-independent
  because the daily floor is relative to the previous day's balance. Caveat: a day split by a
  phase boundary can differ. This is disclosed.
- **(b) Max**: neither `ComputeRun` nor the row DTO carries a minimum balance
  (`FtmoMultiStartDto.cs:55-68`), so (b) is its own pass per start. It reuses the row's phase
  boundaries:
  - P1 = `Open >= StartSourceOpen`, `Close <= Phase1.OutcomeSourceClose`;
  - P2/funded use the subset rule `Open >= T && Close > T` (as `FtmoGroupDiagnostics.cs:111-112`),
    ending at their `OutcomeSourceClose`;
  - each phase starts at capital, and null nets are skipped.

  Per start, `maxUsed = max over phases and both FX ends of max(0, capital − minBalance) / (maxPct·capital)`.
  Report the WORST (max) and the MEDIAN (even count: mean of the two middle values) across starts.
  Invariant test: a phase with `BreachLimit` in {Max, BothSameClose} has `maxUsed > 1`, otherwise
  `<= 1` (fixtures without same-close ties).
- **Rank scalar**: `headroom = 1 − max(dailyUsed, worstMaxUsed)`, taken on the worse kind (the kind with
  the larger `max(dailyUsed, worstMaxUsed)`, i.e. the lower per-kind headroom). Each kind also reports its
  own headroom unblended. Fractions on the wire (0.8 = 80% of the allowance used).

### D5 Shortlist, ranking, constants
- **Constants** (`FtmoGroupSearchLimits`, set from the 1d benchmark and pinned by `FtmoGroupSearchLimitsTests`,
  the same way `MaxMembers` is). Evidence (Release, 12 logical cores, closed-form P=24 pool, two runs; the machine is
  noisy, so gates are medians of 3):
  - proxy stage at P=24 (12,926 candidates): median 56.1 s (run 1), 39.2 s (run 2), 37.4 s (falsification run),
    about 4.3 ms per candidate at the worst; single samples from 37.6 s to 60.9 s. P=32: 183-203 s; P=40: 356-485 s.
  - full computation of one candidate (both kinds): k=2 1.5-1.7 s, k=3 1.9-2.9 s, k=4 2.4-3.5 s (median of 3, two runs).
  - `ShortlistSize = 150`;
  - `DefaultMaxFullSimulations = ShortlistSize`: 150 x 3.5 s (slowest per-candidate median) = 525 s;
  - `DefaultMaxWallClock = 15 min` (900 s), so budget x time per group fits with 1.7x headroom;
  - `MaxPoolSize = 24` (was a provisional 40): the largest P whose C(P,2..4) proxy pass is within 60 s; P=25 projects
    to 67 s. The margin at P=24 is thin: a single P=24 sample reached 60.9 s, over the gate, so only the median passes; the proxy is the lever if the pool must grow (a cheaper proxy or a stricter
    pre-filter), not a bigger constant.
  - `ShortlistSize` stays provisional until the calibration (D9) is run: recall@10 < 0.9 forces a larger shortlist
    (and then a larger wall clock).
  - **Revised after a real run (2026-10-06).** In the Debug API with the default budget, one full simulation took about
    18 s and the job stopped at the budget after 51 of 150 groups; the shortlist was simulated in global ranked order,
    so almost no k=4 group was reached. The calibration shows the surrogate order holds the true top 25 of every size
    within the first 25 positions (DEPTH d100 = 25 for k=2, 3, 4; D9). New constants: `ShortlistSize = 75` (per-size
    quota 25 over sizes 2..4), `DefaultMaxFullSimulations = ShortlistSize`, `DefaultMaxWallClock = 30 min` (75 x 18 s =
    22.5 min). Ceilings unchanged (500 simulations, 3600 s).
  - **Simulation order.** `FtmoGroupSearchEngine.InterleaveBySize` turns the shortlist into a round-robin over the sizes
    (k=2 #1, k=3 #1, k=4 #1, k=2 #2, ...), each size best-first; a size that runs out drops from the rotation. `Plan`
    applies it, so `Simulate` stays "in the given order" and a budget stop leaves every size represented. The final
    ranking is unchanged: `FtmoGroupSearchRanking` orders the results independently of the order they were computed in.
- **Shortlist**: a per-k quota `floor(N / #k)`, filled best-proxy-first. The remainder goes to the
  global proxy order. Simulation follows shortlist order, so a budget stop cuts the tail.
- **Comparer** (`FtmoGroupSearchRanking.Comparer`, exact decimals, no epsilon):
  1. both kinds `Evaluated`, `RaceRefusal == null` and `Summary != null` sort first;
  2. breach share `(Phase1Breached+Phase2Breached+FundedBreached)/StartCount` ascending, worse kind;
  3. headroom descending;
  4. funded no-breach share `FundedNoBreachAtEndOfData/StartCount` descending, worse kind, null last
     (right-censored; never named "survival" in any field, key or text);
  5. `DaysToBothTargets.Median` ascending, worse kind, null last;
  6. member count, then worse peak, then the sorted-Guid sequence, all ascending.
- **Elimination ceiling** (default 0.05): a HIGHLIGHT (`WithinCeiling` flag) plus a client-side
  filter toggle. It is never a ranking key and never drops rows.

### D6 Diagnostics opt-out in `ComputeGroup`: NO
Diagnostics are O(n log n + starts) against the seconds that `ComputeRun` takes. Rank key 6 does
NOT read `Diagnostics.Peak`: it ranks on the proxy peak (`FtmoPeakConcurrency`, the worse kind),
which `FtmoGroupSearchReviewGapTests` pins equal to the full simulation's worse-kind
`Diagnostics.Peak.PeakConcurrentOpen` (1d, RELIABILITY-005). An opt-out would edit
`FtmoGroupComputation.cs:130` (a shipped file) for an unmeasurable gain. If the benchmark ever shows more than 2% of per-candidate time, revisit
with an optional `bool includeDiagnostics = true` parameter.

### D7 Job infrastructure
- **Registry** (`FtmoGroupSearchJobRegistry`, singleton) holds at most ONE job record (state,
  progress, result, CTS). `TryStart` is atomic (lock). When a job is Queued or Running, a second
  start returns 409 with the running job's id.
- **Worker** (`FtmoGroupSearchWorker : BackgroundService`) awaits a bounded `Channel<Guid>(1)`.
  Per job it creates a DI scope, loads the pool (D1 stages, `AsNoTracking`), disposes the scope,
  then runs the pure engine with a CTS linked to the job and to `stoppingToken`.
- **Progress**: `{stage: Loading|Eligibility|Proxy|Simulating|Ranking, processed, total,
  elapsedMs, fullSimulationsDone, maxFullSimulations}`, published as an immutable snapshot
  (`Volatile.Write`).
- **Cancel**: `DELETE` triggers the CTS. The token flows into `ComputeGroup` and from there into
  `Parallel.For` (`FtmoMultiStartReadService.cs:163-164`). The terminal state is `Cancelled`, with
  partial ranked results. Engine contract (1b-ii): the pure engine catches `OperationCanceledException` and returns a result flagged `Cancelled` carrying the candidates completed before the cancel (the one in flight is discarded); the worker (2a) maps that flag to the `Cancelled` status. A cancel is not a stop reason.
- **Budget**: checked before each candidate (the proxy stage is not budget-checked; see tasks.md 1d). On a hit the status is
  `StoppedAtBudget` (a distinct status, per the spec; never `Completed`), a stop reason names the limit hit
  (simulation count or time), the not-computed count is reported, and the results are ranked over what was
  evaluated. Status enum: `Running`, `Completed`, `StoppedAtBudget`, `Cancelled`, `Failed`, with a
  non-completing zero member (`Unknown = 0`) so the default never reads as `Completed`.
- **Threads**: candidates run sequentially. `ComputeRun` already uses all cores per kind
  (`:156`, measured 4x speed-up in `FtmoGroupBenchmarkTests.cs:23-34`), so outer parallelism would
  oversubscribe the pool and starve API requests. Only the cheap proxy stage is parallel.
- **Retention**: the last job (terminal included) is kept until the next start. There is no
  persistence. After an API restart, `GET` returns 404 and the UI shows "job lost (API restarted)".
- **Endpoints**: new `FtmoGroupSearchController`, `[Authorize]`, route
  `api/ftmo-simulations/group-search`. `FtmoSimulationsController.cs` is not edited.
  - `POST` -> 202 `{jobId}` + Location; 400 on validation; 409 `{runningJobId}`.
  - `GET {id}` -> 200 `FtmoGroupSearchJobDto`; 404 if unknown.
  - `GET current` -> 200 or 204.
  - `DELETE {id}` -> 204 (idempotent; terminal is a no-op); 404 if unknown.

### D8 Tripwire
Only new files are created, except `FtmoGroupSimulationReadService.cs` (D1),
`DependencyInjection.cs` (+3 registrations) and the frontend files in the table
(`simulator.routes.ts`, `main-layout.component.html`, `en.json`, `es.json`, the group page).
`FtmoGroupSearchTripwireTests` (same `CallerFilePath` root technique as
`BacktestPortfolioRiskTripwireTests.cs:49-57`) greps the pure search files for `Random`,
`Guid.NewGuid`, `DateTime.Now|UtcNow`, `SaveChanges` and `FtmoBreachEvaluator`. Registry and worker
are excluded from the `Guid.NewGuid`/clock checks. Existing backend tests edited: NONE. Two existing web specs (`simulator.result.i18n.spec.ts`, `simulator.routes.spec.ts`) were edited in the stabilization commit f0adffa for stability, with the assertions unchanged. The
shipped spec `ftmo-group-simulation-ui` receives a delta (deep link) from the spec phase.

### D9 Proxy calibration
`FtmoGroupSearchCalibrationTests`, `[CalibrationFact]`. It is skipped unless
`FTMO_CALIBRATION_CONNECTION` is set, and running it needs explicit user authorization (project DB
rule). It uses a no-tracking context, a SaveChanges interceptor that throws, and a command interceptor that is an
ALLOWLIST: after literal-aware stripping of comments and masking of string literals / quoted identifiers, a command
must start with `SELECT` or `WITH`, hold no `;` except one trailing, and no `INTO`, `EXEC` or `EXECUTE`. The SQL EF
Core generates for the calibration's own query shapes is proven to pass it (SQLite in memory).

On the SBDEMO2 pool (P≈24), ground truth is a full simulation of:
- every k=2 (276) and k=3 (2,024) candidate;
- every 10th k=4 candidate in enumeration order (deterministic, no RNG).

Metric: recall@K = |topK(full) ∩ shortlist| / K, for K ∈ {10, 25}, per k. The result is recorded
in the test's doc comment (precedent `FtmoGroupBenchmarkTests.cs:23-34`). Trust threshold:
recall@10 and recall@25 ≥ 0.9 per k with at least 10 sampled candidates; a run in which no k
qualifies fails. A candidate past position K that ties with the K-th entry on the full ranking key (all keys but
the id tie-break) counts as in the top K. Known bias: the k=4 stride is systematic over a lexicographic
enumeration, so it correlates with the leading members, and the k=4 truth is the top of the sample, not of all
k=4 candidates; k=4 recall can overstate the shortlist's recall (k=2 and k=3 are exhaustive). Otherwise raise `ShortlistSize` or revise the score before PR2 ships its
defaults.

**Result (2026-10-06, calib-4.log): PASSED.** The shortlist order is now the `FtmoRaceSurrogate` (`Services/FtmoRaceSurrogate.cs`), which replaced the old proxy (worst DailyUsed, then Peak) after that proxy failed with recall@25 of 0.72 / 0.36 / 0.16. Per monthly start the surrogate runs a cheap race (phase 1 +10%, phase 2 +5%, funded, min 4 trading days; daily floor from the previous Berlin midnight, static max floor; breach before target) and aggregates breach share, headroom and median days on the worse kind. Shortlist order: surrogate breach share, then headroom desc, then median days (none last), then Peak, then the combo key. Documented approximations: a breach on either FX end counts; the funded share is not modelled; zero-duration rows count differently for trading days. Pool: SBDEMO2, FX 1.05..1.20, maxPerInstrument=2; 130 strategies, 18 eligible (USATECHIDXUSD=12, XAUUSD=6); 4029 enumerated, 1719 survivors, shortlist 150; ground truth is the full simulation of all 1719 groups (k4 stride 1, which supersedes the stride 10 above). recall@10 and recall@25 = 1.000 for k=2, 3 and 4; DEPTH d100 = K. Run took 4.5 h, ~9.4 s per group sequential in the harness. Lesson: a strided k4 truth gave a false failure (recall 0.6 / 0.24, calib-3.log), since the sample's top K does not match a global quota. Harness: `FTMO_CALIBRATION_TRUTH_FILE` caches the truth (fingerprint + schema version); a DEPTH diagnostic; failures reported per size. The proxy stage at P=24 measured 49.7 / 58.1 s against the 60 s gate (thin margin). Closed: the 9.4 s x 150 budget item was superseded by the 75 / 30 min rebalance (1d.3.6). The parallel per-group measurement moves to a separate performance change (task 1d.3.5, partial). Re-run at shortlist 75 (2026-10-06, calib-5.log): PASSED. Production shortlist 75 (quota 25 per size) against the cached full truth (`TRUTH source=file`, truth-full.json, 1719 entries, k4 stride 1): recall@10 and recall@25 = 1.000 for k=2, 3 and 4; DEPTH d100 = K. Run took 45 s (truth read from the cache). This measures, rather than infers, recall at 75 (verify W6 resolved).

### D10 Frontend
- **Max per instrument** input defaults to 2 (minimum 1), user decision 2026-10-04 (see D3).
- **Route**: `{path:'ftmo/search'}` in `simulator.routes.ts:3-12`, plus a sidebar link after
  `main-layout.component.html:284-291`. The existing link gets `[routerLinkActiveOptions]="{exact:true}"`.
- **Components** (standalone, OnPush, signals):
  - container `FtmoGroupSearchPageComponent`;
  - presentational `ftmo-search-form`, `ftmo-search-progress`, `ftmo-search-table`, `ftmo-search-scatter`;
  - pure `ftmo-group-search.mappers.ts`;
  - new `FtmoGroupSearchService` + model, so the existing service is not edited.
- **Polling**: `timer(0, 1000)` -> `switchMap(get)` -> `takeWhile(!terminal, true)` ->
  `takeUntilDestroyed`. Destroy stops polling only; the job keeps running and `GET current`
  re-attaches on init. The Cancel button sends `DELETE`. A 404 sets the "lost" state.
- **Scatter**: plain SVG with theme CSS variables. `lightweight-charts` 5.2.0 is rejected:
  `createOptionsChart` gives a numeric x axis (`typings.d.ts:251-262`), but `setData` requires
  ordered data (`:2422`, one item per x). Median days are integers, so groups collide on x, and
  click hit-testing is per x and not per point.
- **Frontier**: Pareto-minimal on (x=median days, y=breach share), worse kind. Sort by
  (x, y) and keep a point iff y < running min. Exact duplicates share the status. Points with
  null x are not plotted, and their count is shown.
- **Deep link** (decision 2026-10-04): `/simulator/ftmo?account&members=a,b&risk&capital&fxLow&fxHigh`.
  **The job DTO carries the request (2026-10-06)**: `FtmoGroupSearchJobDto.Request` is the request as submitted (fractions stay
  fractions, set by the registry on start, present on POST 202 `job`, `GET {id}` and `GET current`). The page ALWAYS builds the
  link and the table ceiling from `job.request`; the local snapshot, the form fallback and the `LINK_FROM_FORM` notice are
  removed, and the "ceiling falls back to 5% on re-attach" limitation is resolved. The form is not re-populated on re-attach.
  Broker and the source lot grid are NOT link parameters: both pages use the same shared constants.
  The group page injects `ActivatedRoute` with `{optional:true}`, because the existing specs
  provide no router. It applies the parameters ONCE (account if known, else the default plus a
  notice), preselects member ids present in candidates (unknown or invalid ids dropped with a
  notice, capped at `maxMembers`) and positive finite numbers. It never auto-runs.
- **i18n**: `SIMULATOR.FTMO_SEARCH.*` (aligned with the UI spec) and `SIMULATOR.NAV.FTMO_SEARCH`, inside the namespace that
  `simulator.i18n.spec.ts` already checks for parity and banned wording ("passed", "survived"...).
- **Units**: shares, headroom and ceiling are FRACTIONS on the wire. They are converted to percent
  once, in the mappers (precedent `ftmo-group-simulation.mappers.ts:231-244`).

## Data Flow

    UI page --POST--> SearchController --> Registry(1 job) --Channel--> Worker
       ^  GET 1s                                             | scope: MemberResolution (6 read queries)
       |                                                     v
       +---- snapshot <-- Engine: eligibility -> ProjectionCache -> enumerate(prune)
                                  -> Proxy[Merge+DailyLossProfile+Peak] -> shortlist
                                  -> ComputeGroup x2 (shipped) -> Headroom -> Ranking

## File Changes (per PR; sizes = authored adds+dels incl. tests)

| PR | Files (Create unless noted) | Size |
|---|---|---|
| 1a | `Services/FtmoGroupMemberResolution.cs`; `FtmoGroupSimulationReadService.cs` (Modify); `FtmoDailyLossProfile.cs`; `FtmoPeakConcurrency.cs`; parity tests | ~420 |
| 1b | `FtmoProjectionCache.cs`, `FtmoGroupSearchEngine.cs`, `FtmoGroupSearchRanking.cs`, `FtmoGroupSearchLimits.cs`; engine/ranking/determinism/parity-vs-SimulateAsync tests | ~550 -> split engine / ranking |
| 1c | `FtmoLimitHeadroom.cs`; `FtmoGroupSearchBenchmarkTests.cs`, `FtmoGroupSearchCalibrationTests.cs`, `CalibrationFactAttribute.cs`, `FtmoGroupSearchTripwireTests.cs` | ~430 |
| 2 | `Application/DTOs/Backtests/FtmoGroupSearchDto.cs`, `Application/Interfaces/IFtmoGroupSearchJobs.cs`, `FtmoGroupSearchJobRegistry.cs`, `FtmoGroupSearchWorker.cs`, `WebAPI/Controllers/FtmoGroupSearchController.cs`, `DependencyInjection.cs` (Modify); tests | ~700 -> 2a registry+worker+DTOs, 2b controller |
| 3 | `core/models/ftmo-group-search.model.ts`, `core/services/ftmo-group-search.service.ts`, `features/simulator/ftmo-group-search.mappers.ts`, `ftmo-group-search-page/*`, `ftmo-search-form/*`, `ftmo-search-progress/*`, `ftmo-search-table/*`; `simulator.routes.ts`, `main-layout.component.html`, `en.json`, `es.json` (Modify); specs | ~1400 -> 3a service/model/mappers/route/i18n, 3b form+progress+page, 3c table |
| 4 | `ftmo-search-scatter/*`, frontier in mappers; `ftmo-group-simulation-page.component.ts` (Modify, deep link) + new spec | ~600 -> 4a scatter, 4b deep link |
| 5 | optional risk grid (engine re-run top N at 50/75/100%) | ~300 |

## Interfaces / Contracts

```csharp
// Built DTOs: Application/DTOs/Backtests/FtmoGroupSearchDto.cs
public sealed record FtmoGroupSearchRequest(Guid? TradingAccountId, int? MinMembers, int? MaxMembers,
    int? MaxPerInstrument, bool? IncludeIdenticalDeployEval, bool? OnePercentRule, decimal? EliminationCeiling,
    string? Broker, decimal? InitialCapital, decimal? TargetRiskPerTrade, decimal? FxLow, decimal? FxHigh,
    int? SizeDecimals, decimal? Step, decimal? MinLot, decimal? MaxLots, int? MaxFullSimulations,
    int? MaxWallClockSeconds); // all optional; no symbols field (the UI symbol filter was omitted for this reason)
public sealed record FtmoGroupSearchJobDto(Guid JobId, FtmoGroupSearchStatus Status, FtmoGroupSearchProgressDto Progress,
    FtmoGroupSearchStopReason StopReason, int NotComputed, IReadOnlyList<FtmoGroupSearchIneligibleDto> Ineligible,
    IReadOnlyList<FtmoGroupSearchRowDto> Rows, IReadOnlyList<string> Disclosures, string? ErrorMessage,
    FtmoGroupSearchRequest Request); // Request is set by the registry so a re-attached job links with its own request
public sealed record FtmoGroupSearchRowDto(int Rank, IReadOnlyList<Guid> MemberIds, IReadOnlyList<string> MemberNames,
    int PeakConcurrentOpen, bool IdenticalDeployEval, bool WithinCeiling, decimal Headroom,
    IReadOnlyList<FtmoGroupSearchKindHeadroomDto> KindHeadrooms, IReadOnlyList<FtmoGroupKindResultDto> Kinds,
    IReadOnlyList<string>? Symbols = null);
// KindHeadroomDto: Kind, WorstDailyUsed, WorstMaxUsed, MedianMaxUsed, Headroom (no FX end used; see tasks.md 2a.1.2).
// Progress: Stage, Processed, Total, ElapsedMs, FullSimulationsDone, MaxFullSimulations, Funnel.
```

## Testing Strategy

| Layer | What | Approach |
|---|---|---|
| Unit (pure) | daily-loss profile vs evaluator; peak vs diagnostics; prunes (Helly, cap, 1%); comparer keys 1..6; frontier; headroom invariants | xUnit + FluentAssertions, Strict TDD, fixtures from `FtmoGroupBenchmarkFixture` |
| Integration | extraction keeps query count and results; every ranked row's `Run` equals `SimulateAsync` for the same members; two runs give identical rankings | SQLite + `CountingInterceptor` |
| Job | 409 + id, cancel to `Cancelled`, budget stop, no work left running after a terminal state | registry/worker with a fake engine |
| Benchmark/manual | proxy throughput sets `MaxPoolSize`/`ShortlistSize`; calibration recall | `[BenchmarkFact]`, `[CalibrationFact]` |
| Frontend | mappers (units, frontier), polling lifecycle (fake timers), deep-link parse/invalid ids, real-dictionary EN/ES render | Vitest, real `en.json`/`es.json` |

## Threat Matrix

N/A: no shell, subprocess, VCS automation or executable classification. The new HTTP routes are
`[Authorize]` and single-user.

## Migration / Rollout

No migration and no persisted state. Ship the chained PRs in order. The UI is reachable only after
PR3. Rollback is a revert per PR: reverting PR1a restores the inline resolution, and reverting PR4b
removes the deep-link parameters.

## Open Questions

- [x] Constants are 75 / 30 min / 24 (were 150 / 15 min / 40); calibration recorded in D9.
- [x] RESOLVED (verify W6, 2026-10-06, calib-5.log): re-run at shortlist 75 (quota 25 per size) against the cached full truth (truth-full.json, 1719 entries): recall@10 and recall@25 = 1.000 for k=2, 3 and 4; DEPTH d100 = K; 45 s. Recorded in D9.
- [ ] OPEN (verify W7): the default budget does not fit 75 groups in a Debug build (about 28 s per group, a real run stopped at 66/75; Release is estimated at 75 x 3.5 s = 262 s). Pending the separate performance change.
- [x] RESOLVED (user decision 2026-10-04): leaving the search page does NOT cancel the job; it only stops
  polling. `GET current` re-attaches when the user returns. Explicit cancel stays on the Cancel button.
