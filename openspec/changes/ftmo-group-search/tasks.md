# Tasks: FTMO group search (automatic 2..4 strategy combinations)

Eleven chained slices (plus one optional), committed one at a time to `main` by the user via `/commit`, in this order:
**1a -> 1b -> 1c -> 1d -> 2a -> 2b -> 3a -> 3b -> 3c -> 4a -> 4b -> (5 optional, last)** (design.md "File Changes").
Each slice starts only after the previous one is green and committed, is independently buildable and green, and
targets <= ~450 lines. Slices 3a-4b are frontend-only and have no backend dependency until 2b is committed (the
UI is exercised by the user only after 2b). Read `.claude/conventions/_index.md`, `backend-core.md` +
`backend-testing.md` before any `.cs`, and `frontend-core.md` + `frontend-data.md` + `frontend-design.md` before
any `.ts/.html/.scss`. Paths: `API/` = `app.trading.algoritmico.api/src/AppTradingAlgoritmico`, `TESTS/` =
`app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests/Ftmo`, `WEB/` =
`app.trading.algoritmico.web/src/app`, `SVC/` = `API/Infrastructure/Services`.

## Hard rules (checkpoint at every task that touches them)

1. **Strict TDD.** RED before GREEN, every RED observed failing for the right reason. Each slice has at least one
   **falsification** task: break the code, observe red, restore, observe green.
2. **Engine files are never edited**: `FtmoBreachEvaluator.cs`, the race, funded phase, enumerator, open-position
   sweep, calendar, projector, merger (`FtmoGroupMerger.cs`), `FtmoGroupComputation.cs` (D6: NO diagnostics
   opt-out), `FtmoGroupDiagnostics.cs`, and `FtmoMultiStartReadService.cs`; plus the tripwire's 18 slice files. If
   any task seems to need an engine edit, STOP and report.
3. **Member-resolution extraction (1a) is behaviour-preserving.** Existing group tests stay unedited, including
   the query-count test (`FtmoGroupSimulationReadServiceTests.cs:612-640`). The golden pin and the snapshot pin stay
   green. `git diff --stat -- app.trading.algoritmico.api/tests` lists only NEW files.
4. **Parity tests.** (a) `FtmoDailyLossProfile` vs `FtmoBreachEvaluator.Evaluate` on fixtures; (b)
   `FtmoPeakConcurrency` vs the `ComputeGroup`-reported peak; (c) every shortlisted group's metrics equal a direct
   group-simulation run of the same members and inputs.
5. **Determinism.** Same input gives an identical output, no RNG, no hash-order iteration. New tripwire tests
   (1d) scan the pure search files for `Random`, `Guid.NewGuid`, `DateTime.Now|UtcNow`, `SaveChanges`,
   `FtmoBreachEvaluator`, and the one-group files for combination loops.
6. **Benchmark (1d).** Gated by `FTMO_BENCH`, Release, median of 3. It sets `ShortlistSize`, the budget defaults
   and `MaxPoolSize`; the table is recorded in the test doc comment and the PR description.
7. **Calibration (D9, 1d).** Separate task, gated by `FTMO_CALIBRATION_CONNECTION`, read-only interceptor throws
   on any non-SELECT. It MUST STOP and ask the user for DB authorization, naming the target connection and
   environment, before running. The result is recorded. The endpoint (2b) does not ship before it is recorded.
8. **Frontend.** Real `en.json`/`es.json` in tests: no `{{`, no raw keys, every placeholder gets its params. No
   truthiness on values that can be 0. Fractions converted ONCE at display. Theme variables only
   (`src/styles/_variables.scss`). Number inputs use `step="any"` and the form is `novalidate` (post-F4 lesson).
   Any selected-items list stays visible regardless of filters (post-F4 lesson). Banned wording (incl.
   "survival"/"supervivencia") never appears in any key, DTO member or text.
9. **Commit gates MUST FAIL the commit on any red** (lesson from 6861399: a flaky test slipped through because the
   gate script did not stop). Each gate command is run separately, output redirected to a file, exit code tested
   explicitly; any non-zero stops the slice. A red full suite is re-run ONCE to classify: a failure that
   reproduces is a blocker; a failure that does not is recorded by name and reported to the user, never silently
   accepted. Known open item: ONE unidentified flaky frontend test (1 failure in 9 full runs, not reproduced).
10. **Process safety.** Never kill a process. Every `dotnet build/test` uses `-p:BaseOutputPath=bin-scratch/` and
    the folder is deleted at the end of each slice. Wrap every command in `timeout`. No DB access without
    explicit user authorization (state the target first). No `git commit/push/merge/rebase` by agents.
11. **Language and units.** English artifacts. Shares, headroom, ceiling are fractions on the wire; "funded
    no-breach share" is the only name for `FundedNoBreachAtEndOfData / StartCount`.

## Review Workload Forecast

| Slice | Content | Est. lines |
|-------|---------|-----------|
| 1a | Resolution extraction, daily-loss profile, peak concurrency | ~420 |
| 1b | Cache, eligibility, enumeration, proxy, shortlist, engine, budget/cancel hooks | ~450 |
| 1c | Headroom, ranking comparer | ~380 |
| 1d | Benchmark, calibration, tripwire | ~350 |
| 2a | DTOs, registry, worker | ~500 (flagged) |
| 2b | Controller, DI | ~250 |
| 3a | Model, service, mappers, route, sidebar, i18n | ~420 |
| 3b | Form, progress, page, polling lifecycle | ~450 |
| 3c | Ranked table | ~400 |
| 4a | Scatter, frontier | ~300 |
| 4b | Deep link into the group page | ~300 |
| 5 (optional) | Risk grid | ~300 |
| Total | | ~4,200 (additions + deletions, tests included) |

| Field | Value |
|-------|-------|
| 400-line budget risk | High (11 slices, 1b and 2a near or over) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Flags: **2a** may exceed ~450; cut point after Phase 2a.2 (registry + DTOs), worker as a follow-up slice 2a-ii if
the diff is above ~500. **1b**: if above ~500 after Phase 1b.3, split the proxy/shortlist from the budget hooks.
The user confirms the chain strategy and the slice order before apply.

### Gate commands (verbatim from `.claude/skills/commit/SKILL.md`; run per slice, each separately, exit code tested)

Backend (repo root):
```bash
timeout 600 dotnet format app.trading.algoritmico.api/AppTradingAlgoritmico.slnx --include <touched .cs> --verify-no-changes > gate-format.log 2>&1 || { echo "RED: format"; exit 1; }
timeout 900 dotnet build app.trading.algoritmico.api -warnaserror -p:BaseOutputPath=bin-scratch/ > gate-build.log 2>&1 || { echo "RED: build"; exit 1; }
timeout 1800 dotnet test app.trading.algoritmico.api -p:BaseOutputPath=bin-scratch/ > gate-test.log 2>&1 || { echo "RED: test"; exit 1; }   # full suite once, end of slice
# focused: ... --filter "FullyQualifiedName~FtmoGroupSearch"
# afterwards: delete bin-scratch/ and gate-*.log
```
Frontend (from `app.trading.algoritmico.web/`; never from the repo root):
```bash
timeout 300 npx prettier --check <touched .ts/.html/.scss, relative to this dir> > gate-prettier.log 2>&1 || { echo "RED: prettier"; exit 1; }
timeout 600 npx tsc --build --emitDeclarationOnly false --noEmit > gate-tsc.log 2>&1 || { echo "RED: tsc"; exit 1; }
timeout 1800 npx ng test --watch=false > gate-ngtest.log 2>&1 || { echo "RED: ng test"; exit 1; }   # full suite once, end of slice
```
Never pipe a gate into `head`/`tail` and read `$?`. `dotnet format` reports on whole files: pre-existing debt
outside the slice's hunks is reported to the user, not reformatted.

### Suggested Work Units

| Unit | Goal | PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|----|----------------------|-----------------|-------------------|
| 1 | Extract member resolution + daily-loss profile + peak | 1a | `dotnet test ... --filter "FullyQualifiedName~FtmoGroupMemberResolution|FtmoDailyLossProfile|FtmoPeakConcurrency|FtmoGroupSimulationReadService"` | N/A: no endpoint; proof = shipped Ftmo suites unedited | Revert restores inline resolution; the two pure classes are unreferenced |
| 2 | Pure funnel engine | 1b | `--filter "FullyQualifiedName~FtmoGroupSearchEngine|FtmoProjectionCache"` | N/A: pure + SQLite test DB | Revert removes new files + nothing else |
| 3 | Headroom + ranking | 1c | `--filter "FullyQualifiedName~FtmoLimitHeadroom|FtmoGroupSearchRanking"` | N/A: pure | Revert removes new files |
| 4 | Benchmark, calibration, tripwire, constants | 1d | `FTMO_BENCH=1 ... -c Release --filter FtmoGroupSearchBenchmark`; calibration only with user authorization | Benchmark table; authorized read-only calibration on SBDEMO2 | Revert removes tests + constant values |
| 5 | Job DTOs, registry, worker | 2a | `--filter "FullyQualifiedName~FtmoGroupSearchJobRegistry|FtmoGroupSearchWorker"` | N/A: fake engine | Revert removes new files + DI lines |
| 6 | Endpoints | 2b | `--filter "FullyQualifiedName~FtmoGroupSearchController"` | Optional authorized end-to-end start/poll/cancel on SBDEMO2 (user) | Revert removes controller + DI line |
| 7 | Model, service, mappers, route, sidebar, i18n | 3a | `npx ng test --watch=false --include <search specs>` | Open `/simulator/ftmo/search` (user) | Revert route + sidebar link + keys |
| 8 | Form, progress, page, polling | 3b | same, form/progress/page specs | Run a search on SBDEMO2 (user, needs 2b) | Revert components |
| 9 | Ranked table | 3c | same, table specs | Read a real ranking in EN and ES (user) | Revert table |
| 10 | Scatter + frontier | 4a | same, scatter specs | Click a point on a real result (user) | Revert scatter |
| 11 | Deep link in the group page | 4b | same, deep-link specs | Open a result in `/simulator/ftmo`, no auto-run (user) | Revert page param handling |

---

## 1a — Extraction + daily-loss profile + peak (~420)

### Phase 1a.0 — Baseline
- [x] 1a.0.1 Run the shipped suite filtered `Ftmo` plus `BacktestPortfolioRiskTripwireTests`; record the pass count. All green before any edit. _[Req: One-group computation unchanged]_ _Done: baseline 1228 passed / 8 skipped (supplied by the orchestrator, not re-run separately); final run 1263 / 8 = 1228 + 35 new tests._
- [x] 1a.0.2 Read `FtmoGroupSimulationReadService.cs:33-231` and `FtmoGroupSimulationReadServiceTests.cs:612-640` (CountingInterceptor) to copy fixtures. _Done._

### Phase 1a.1 — `FtmoGroupMemberResolution` (design D1)
- [x] 1a.1.1 RED `TESTS/FtmoGroupMemberResolutionTests.cs`: staged methods `LoadNamesAsync` / `LoadMembersAsync` / `LoadTradesAsync` / `BuildGroupParams` / `ToKindInput` return what the inline code produced (source zone = first resolved member zone; echo band = non-USD among THIS group's members). _[Req: One-group computation unchanged]_ _RED: new test file did not compile without `FtmoGroupMemberResolution` (CS0234/CS0246). Done._
- [x] 1a.1.2 RED query-count pin through the new stages: names+limits issued before runs; an unknown id or limits refusal issues no runs/specs/trades query (same counts as `:612-640`). _Done: unknown id = 1 command, limits refusal = 2, full run = 6 in order. Passes on the unmodified service by design (pins); its falsification is 1a.1.6._
- [x] 1a.1.3 GREEN create `SVC/FtmoGroupMemberResolution.cs` (`internal static`) by moving the listed line ranges VERBATIM; modify `SVC/FtmoGroupSimulationReadService.cs` to call the stages in the same order and keep every group-wide refusal precedence. Behaviour-preserving. _Done: service 263 -> 140 lines; staged calls in the same order._
- [x] 1a.1.4 Pin: run every shipped Ftmo suite, golden pin, snapshot pin; `git diff --stat -- app.trading.algoritmico.api/tests` shows zero modified files. _[Req: Existing suites pass unedited; response byte-identical]_ _Done: group service tests, query-count test, golden pin, snapshot pin green UNEDITED; `git diff --stat -- tests` is empty (new files only)._
- [x] 1a.1.5 RED/GREEN byte-identical response: serialize the group endpoint result for a fixture group before and after (snapshot test in a NEW file, JSON equality). _[Req: Group endpoint response unchanged]_ _RED: placeholder hash mismatched on the unmodified service; real hashes captured pre-refactor (stable over 2 runs), then GREEN after it. Done._
- [x] 1a.1.6 Falsification: reorder two stages so the runs query runs before the limits refusal; confirm the query-count pin red; restore. _Done: moved `LoadMembersAsync` before the limits refusal; the limits-refusal pin (5 commands, not 2) and the in-order pin went red; restored. NOTE: the shipped query-count test stayed green under this mutation (it only counts the happy path)._

### Phase 1a.2 — `FtmoDailyLossProfile` (design D3)
- [x] 1a.2.1 RED `TESTS/FtmoDailyLossProfileTests.cs`: worst day loss and its day on hand-computed series; moving floor seeded as the evaluator; day-change reference = balance carried from the previous day; null nets skipped. _[Req: Proxy, daily]_ _RED: `FtmoDailyLossProfile` did not exist (compile error). Done._
- [x] 1a.2.2 RED **parity (hard rule 4a)**: on fixtures, `Evaluate(...).Daily.Verdict != NoBreachObserved` iff `WorstDayLoss > dailyPct x capital`, and the first breach day equals the profile's first exceeding day. The evaluator is only CALLED from tests. _Done: 5 fixtures (quiet, breach day, merged-two, merged-three with null nets, Berlin boundary at late-March DST) x 2 FX ends x 5 allowances; non-vacuous guard._
- [x] 1a.2.3 GREEN create `SVC/FtmoDailyLossProfile.cs` (pure) replicating `FtmoBreachEvaluator.cs:128-137,159-184`. _Done._
- [x] 1a.2.4 Falsification: measure from initial capital instead of the previous-day balance; confirm 1a.2.1/1a.2.2 red; restore. _Done: reference = initial capital -> 8 of 13 red (all four parity fixtures that breach, plus the hand cases); restored._

### Phase 1a.3 — `FtmoPeakConcurrency`
- [x] 1a.3.1 RED `TESTS/FtmoPeakConcurrencyTests.cs`: A 10-12, B 11-13, C 11:30-11:45 -> peak 3; close 12:00 / open 12:00 not concurrent; zero-duration and Unscalable rows ignored. _[Req: Proxy, peak]_ _RED: `FtmoPeakConcurrency` did not exist (compile error). Done._
- [x] 1a.3.2 RED **parity (hard rule 4b)**: proxy peak equals `ComputeGroup(...).Diagnostics.Peak.PeakConcurrentOpen` on the same members. _Done: 3 parity fixtures vs `ComputeGroup(...).Diagnostics.Peak.PeakConcurrentOpen` (peak 3 asserted, boundary touch, window trim)._
- [x] 1a.3.3 GREEN create `SVC/FtmoPeakConcurrency.cs` mirroring `FtmoGroupDiagnostics.cs:166-211` (closes before opens). `FtmoGroupDiagnostics.cs` NOT edited. _Done (counter sweep, closes before opens)._
- [x] 1a.3.4 Falsification: process opens before closes at the same instant; confirm 1a.3.1 red; restore. _Done: opens-before-closes -> 3 red (2 unit + the boundary parity); restored._

### Phase 1a.4 — Gate
- [x] 1a.4.1 Format, build `-warnaserror`, full suite once; golden/snapshot pins, tripwire green; `FtmoBreachEvaluator.cs`, `FtmoGroupComputation.cs`, `FtmoGroupDiagnostics.cs`, `FtmoGroupMerger.cs`, `FtmoMultiStartReadService.cs` unchanged; `FtmoGroupSimulationReadService.cs` is the only modified shipped file. Delete `bin-scratch/`. _Done: `dotnet build -warnaserror` 0 warn / 0 err; `dotnet format --verify-no-changes` exit 0 (after whitespace-fixing two NEW test files); full suite 1263 passed / 8 skipped; `bin-scratch/` deleted._

## 1b — Funnel engine (~450)

**1b status (apply, partial): Phases 1b.1-1b.3 DONE; STOPPED at the cut point.** Diff after 1b.3 is 1,162 lines (source 428: `FtmoGroupSearchEngine.cs` 318, `FtmoProjectionCache.cs` 91, `FtmoGroupSearchLimits.cs` 19; tests 734), over the ~500 cut point, so Phase 1b.4 (full computation, budget, cancel) and the 1b.5 gate move to follow-up slice 1b-ii. RED: compile errors (types absent), then two fixture bugs fixed test-side (proxy fixtures). Falsification (1b.3.6): proxy trimmed to the POOL window -> 3 red (window-shrink, pool-independence, determinism); proxy carrying state across candidates (enumeration-order dependence) -> 4 red (parallel==sequential, window, worse-of-kinds, from-scratch); also Rebind without MemberOrder, identical-check skipped (2 red), cap `>` -> `>=` (red). Deviations: the 1% rule uses `k x risk` (spec/tasks) not `peak x risk` (design D3 text); `Plan` is the pure funnel up to the shortlist (no `ComputeGroup`). Gates run: build `-warnaserror --no-incremental` exit 0 (0 warn / 0 err); `dotnet format --verify-no-changes` on touched files exit 0; full suite 1312 passed / 8 skipped (1263 + 49 new).

### Phase 1b.1 — Constants and projection cache (design D2, D5)
- [x] 1b.1.1 RED `TESTS/FtmoProjectionCacheTests.cs`: key `(StrategyId, Kind)`; per-candidate rebinding `with { MemberOrder = i }` in ascending-`StrategyId` order; shared close-instant -> bookkeeping-day dictionary; per-member instrument set and coverage; cache reference released after the job (weak-reference test). _[Req: Cache released]_
- [x] 1b.1.2 GREEN create `SVC/FtmoGroupSearchLimits.cs` (provisional `ShortlistSize` 150, `DefaultMaxFullSimulations`, `DefaultMaxWallClock` 15 min, `MaxPoolSize` 40, ceilings; finalised in 1d) and `SVC/FtmoProjectionCache.cs`.

### Phase 1b.2 — Eligibility (design D3)
- [x] 1b.2.1 RED `TESTS/FtmoGroupSearchEligibilityTests.cs`: missing-kind, spec/calibration/FX refusal, unresolved zone, refused or rowless projection each excluded with its reason; non-USD without band excluded while USD still forms candidates; `IdenticalDeployEval` excluded by default, included and flagged on opt-in; eligible + exclusions = P. Zero-valued reason enum member has its own test. _[Req: Eligibility; Identical Deploy/Eval]_
- [x] 1b.2.2 GREEN eligibility in `SVC/FtmoGroupSearchEngine.cs` (new): exclusion reasons listed, none dropped.

### Phase 1b.3 — Enumeration, prunes, proxy, shortlist
- [x] 1b.3.1 RED enumeration: ascending `StrategyId`, lexicographic, by size; same input twice gives an identical sequence. _[Req: Determinism]_
- [x] 1b.3.2 RED prunes with counts that reconcile (enumerated = removed by cap + removed by pair conflict + removed by 1% rule + remaining): per-instrument cap default 1 and cap 2 admits; pair conflict = different zones or `Intersect == null` (Helly); Academy 1% rule (risk 50, capital 10000, k>=3 removed, k=2 stays; off by default). _[Req: Cap; 1% rule]_
- [x] 1b.3.3 RED proxy on each candidate's OWN window: {A,B} vs {A,B,C} with late-starting C; value equals the proxy computed from scratch on that window; same candidate in two pools gives equal values; empty window -> `NoCommonWindow` counted; member with no in-window rows -> `MemberHasNoTradesInWindow` counted, not shortlisted. Worse-of-kinds and FX-ends daily-used fraction is the score. _[Req: Proxy window]_
- [x] 1b.3.4 RED shortlist: per-k quota `floor(N/#k)` filled best-proxy-first, remainder by global proxy order, ties by sorted id tuple; parallel proxy (`Parallel.For`, results by candidate index) gives the same output as sequential.
- [x] 1b.3.5 GREEN enumeration, prunes, proxy (`Intersect` + `Merge` + `FtmoDailyLossProfile` + `FtmoPeakConcurrency`) and shortlist in the engine.
- [x] 1b.3.6 Falsification: trim the proxy to the pool window instead of the candidate window; confirm 1b.3.3 red; also let the proxy depend on enumeration order; restore.

**1b-ii status: Phases 1b.4-1b.5 DONE.** `Simulate` (engine stage `Simulating`): sequential, both budgets checked before each candidate (max full simulations first, then an injected `Func<bool>` wall-clock check), progress callback, token passed into `ComputeGroup`; cancel returns a `Cancelled`-flagged outcome with the partial results (worker 2a maps it to `Cancelled`). Deviation: the every-256-proxies budget check of 1b.4.4 is NOT implemented (the proxy stage is parallel and pure; the budget is enforced around the simulation stage); revisit in 2a/1d if the benchmark shows the proxy pass needs a deadline.

### Phase 1b.4 — Full computation, budget, cancel
- [x] 1b.4.1 RED `ComputeGroup` runs only for shortlisted candidates, once per kind; full-sim count = shortlist size x kinds evaluated; a refused kind carries its refusal and no metrics. _[Req: Shortlist only]_
- [x] 1b.4.2 RED **parity (hard rule 4c)**: for each shortlisted candidate, metrics equal `FtmoGroupSimulationReadService.SimulateAsync` for the same members, risk, capital, grid and FX (SQLite fixture, outcome counts, shares, order statistics).
- [x] 1b.4.3 RED budget and cancel hooks: simulation budget stops with the limit named and the not-computed count; wall-clock budget stops (injectable clock in the registry layer; the pure engine receives a deadline check delegate); cancelled token throws `OperationCanceledException` and returns partial results; progress snapshots monotonic. _[Req: Budget; Cancel]_
- [x] 1b.4.4 GREEN engine stage `Simulating` with budget check before each candidate and every 256 proxies, progress callback, cancellation token passed into `ComputeGroup`.
- [x] 1b.4.5 Falsification: drop the budget check; confirm 1b.4.3 red. Run two identical searches; assert identical shortlists and metrics.

### Phase 1b.5 — Gate
- [x] 1b.5.1 Format, build, full suite once; pins and tripwire green; engine files unchanged; delete `bin-scratch/`.

## 1c — Headroom and ranking (~380)

### Phase 1c.1 — `FtmoLimitHeadroom` (design D4)
- [x] 1c.1.1 RED `TESTS/FtmoLimitHeadroomTests.cs`: (a) daily used from the previous-midnight balance (10500 open, 400 loss -> divided by the daily allowance, not initial capital), worse FX end taken and reported; (b) per-start pass with P1 `Open >= StartSourceOpen`, `Close <= Phase1.OutcomeSourceClose`, P2/funded subset rule `Open >= T && Close > T`, each phase starting at capital, null nets skipped. _[Req: Headroom a, b]_
- [x] 1c.1.2 RED worst vs median across starts (2%, 4%, 8% of a 10% allowance -> worst 0.8, median 0.4, ranking uses 0.8; even count = mean of the two middle values); each kind carries its own values, never blended. Invariant: a phase with `BreachLimit` in {Max, BothSameClose} has `maxUsed > 1`, otherwise `<= 1`.
- [x] 1c.1.3 RED rank scalar: `headroom = 1 - max(dailyUsed, worstMaxUsed)` taken on the worse kind (lower per-kind headroom). A value of 0 is preserved (no truthiness).
- [x] 1c.1.4 GREEN create `SVC/FtmoLimitHeadroom.cs` (pure, outside the evaluator).
- [x] 1c.1.5 Falsification: use the median in the rank scalar; confirm 1c.1.2 red; restore.

### Phase 1c.2 — `FtmoGroupSearchRanking.Comparer` (design D5)
- [x] 1c.2.1 RED one test per key 1..6: (1) both kinds evaluated, no `RaceRefusal`, `Summary != null` first, others after in id order with their refusal; (2) breach share `(P1+P2+FundedBreached)/StartCount`, worse kind; (3) headroom descending; (4) funded no-breach share descending, null last; (5) `DaysToBothTargets.Median` ascending, null last; (6) member count, then worse peak, then sorted-Guid sequence. Exact decimals, no epsilon. _[Req: Deterministic ranking]_
- [x] 1c.2.2 RED ties: 3-member before 4-member when equal on 1-5; equal sizes and peak fall to id order; the elimination ceiling (default 0.05) sets `WithinCeiling` and never reorders or drops rows; zero-valued enum members each tested.
- [x] 1c.2.3 RED determinism: shuffled input order (deterministic permutation, no RNG) gives the identical ranking; a refused-kind candidate ranks after a clean worse one.
- [x] 1c.2.4 GREEN create `SVC/FtmoGroupSearchRanking.cs`; wire headroom and ranking into the engine result.
- [x] 1c.2.5 Falsification: swap keys 2 and 3; confirm red; restore. Gate: format, build, full suite once, pins; delete `bin-scratch/`.

**1c review test gaps (1d Part 0), DONE** in `TESTS/FtmoGroupSearchReviewGapTests.cs` (8 tests, tests only, no production bug found): RELIABILITY-001 funded phase deepest with exact T2 boundary (zero-duration row AT T2); -002 `BuildEntry` pins headroom 0.2 for both "Deploy worse" and "Evaluation worse"; -003 two independent pipelines ranked in opposite input order are deep-equal in every entry field; -004 `StartMaxUsed` vs the real evaluator through `ComputeGroup` on three fixtures (P1 max breach, funded max breach, daily-only breach); -005 ranked `Peak` equals the worse-kind `Diagnostics.Peak`. Falsified: funded branch removed, `Open > T2`, `Close >= T2`, Deploy-only rebind, best kind instead of worse, eval-only, clock jitter in `BuildEntry`, wrong allowance, proxy peak of one kind only: each red. The stale D6 text in design.md is fixed.

## 1d — Benchmark, calibration, tripwire (~350)

### Phase 1d.1 — Tripwire
- [x] 1d.1.1 RED `TESTS/FtmoGroupSearchTripwireTests.cs` (`CallerFilePath` root technique as `BacktestPortfolioRiskTripwireTests.cs:49-57`): pure search files contain none of `Random`, shuffle, `Guid.NewGuid`, `DateTime.Now|UtcNow`, `SaveChanges`, `FtmoBreachEvaluator`; registry and worker excluded from the `Guid.NewGuid`/clock checks; one-group files (`FtmoGroupComputation`, `FtmoGroupDiagnostics`, `FtmoGroupMerger`, `FtmoGroupSimulationReadService`) contain no combination loop or ranking call. Falsify with a temporary `new Random()` in a pure file. _[Req: Determinism; no loop in one-group capability]_ _Done: `FtmoGroupSearchTripwireTests` (7 tests): no Random/Shuffle/seed, no `Guid.NewGuid`/clock, no `SaveChanges`/`FtmoBreachEvaluator` in the pure files and the `FtmoGroupSearch*.cs` glob (registry/worker exempt from the id/clock check only); the engine composes `ComputeGroup` and never `ComputeRun`; one-group files hold no search reference, combination or shortlist. Falsified: temporary `new Random()`, `DateTime.UtcNow`, `FtmoBreachEvaluator`, a `ComputeRun` call in the engine, a `Shortlist` member in the merger: each turned its test red; restored._
- [x] 1d.1.2 Tripwire that `FtmoBreachEvaluator.cs`, `FtmoMultiStartReadService.cs`, `FtmoGroupComputation.cs` and the 18 slice files are unchanged (hash or git-diff check as the existing tripwire does). _Done: SHA-256 pins (CRLF-normalised) of 11 engine files + the 18 slice files. RED = placeholder pins (mismatch), then real pins. Falsified: one extra space in `FtmoGroupComputation.cs` turned it red; restored._

### Phase 1d.2 — Benchmark (hard rule 6)
- [x] 1d.2.1 RED/GREEN `TESTS/FtmoGroupSearchBenchmarkFixtureTests.cs` + `FtmoGroupSearchBenchmarkFixture.cs`: closed-form P=24 pool (no `Random`), reuse `FtmoGroupBenchmarkFixture`. _Done: `FtmoGroupSearchBenchmarkFixture` (24 strategies from `FtmoGroupBenchmarkFixture`, Eval shifted +1 h) + 4 shape tests: all eligible, funnel (12,926, 0 pruned: 276/2,024/10,626), usable k=4 proxy, deterministic. RED = compile error (fixture absent)._
- [x] 1d.2.2 GREEN `TESTS/FtmoGroupSearchBenchmarkTests.cs` with `[BenchmarkFact]` (`FTMO_BENCH=1`), Release, median of 3: enumeration + proxy over P=24 (12,926 candidates) gated at 60 s; full group computation at k=2,3,4. Confirm it is SKIPPED without `FTMO_BENCH`. _Done: 3 `[BenchmarkFact]`s (gate median-of-3 at 60 s; informational sweep P=16/24/32/40; full computation per candidate k=2/3/4). SKIPPED without `FTMO_BENCH` (3 skipped in the focused run)._
- [x] 1d.2.3 Measure: `FTMO_BENCH=1 timeout 1800 dotnet test app.trading.algoritmico.api -c Release -p:BaseOutputPath=bin-scratch/ --filter "FullyQualifiedName~FtmoGroupSearchBenchmark"`. Record the table in the doc comment and PR description. _[Req: Benchmark]_ _Done, two Release runs; table in the test doc comment and design D5: gate median 56.1 s / 39.2 s (a third, falsification run 37.4 s); per-candidate full computation 1.5-1.7 / 1.9-2.9 / 2.4-3.5 s for k=2/3/4. PR description: copy the doc-comment table._
- [x] 1d.2.4 Set `ShortlistSize`, `DefaultMaxFullSimulations`, `DefaultMaxWallClock`, `MaxPoolSize` from the table so `simulation budget x time per group <= wall-clock default`; add a test asserting that relation. If the 60 s proxy gate fails at P=24: lower `MaxPoolSize` or parallelise the proxy; if still failing STOP and report (never edit an engine file). _Done: `MaxPoolSize` 40 -> 24; `ShortlistSize` 150, `DefaultMaxFullSimulations` 150, `DefaultMaxWallClock` 15 min kept (150 x 3.5 s = 525 s <= 900 s). `FtmoGroupSearchLimitsTests` (3): RED on `MaxPoolSize = 40` (102,050 candidates x 4.4 ms = 449 s), GREEN at 24; P=25 projects to 67 s, so 24 is the largest. The 60 s gate passes at P=24 but with a thin margin on this noisy machine (single samples 37.6-60.9 s)._
- [x] 1d.2.5 Falsification: lower the gate below the measured median; confirm red; restore. _Done: gate lowered to 1 s -> red (`found 37s`); restored._

### Phase 1d.3 — Calibration (design D9, hard rule 7)
- [x] 1d.3.1 GREEN `TESTS/CalibrationFactAttribute.cs` (skipped unless `FTMO_CALIBRATION_CONNECTION`), `TESTS/FtmoGroupSearchCalibrationTests.cs`: no-tracking context, interceptor throws on any non-SELECT command (own unit test of the interceptor); ground truth = full simulation of every k=2 (276) and k=3 (2,024) candidate and every 10th k=4 candidate in enumeration order; metric recall@K for K in {10, 25} per k. Falsify: an INSERT through the interceptor throws. _Done: `CalibrationFactAttribute` (skipped without `FTMO_CALIBRATION_CONNECTION`), `FtmoGroupSearchCalibration` (sample + recall + `ReadOnlyCommandInterceptor` + `NoSaveChangesInterceptor`), `FtmoGroupSearchCalibrationTests` (guard tests on SQLite: 22 pass; the real-DB fact is SKIPPED). Falsified: guard made a no-op -> insert/update/delete tests red; stride off by one -> sample test red; recall denominator fixed at K -> recall test red; restored._
- [x] 1d.3.R Review correction (RELIABILITY-001..004): the read-only interceptor is now a literal-aware ALLOWLIST (SELECT/WITH start, no `;` but one trailing, no INTO/EXEC/EXECUTE; unterminated literal/comment refused); 18 bypass rows RED against the old denylist (14 accepted), GREEN after; a test runs the calibration's own EF query shapes on SQLite through the guard. Calibration asserts recall@10 AND recall@25, fails when no size has >= 10 sampled candidates, prints per-size sample counts; recall is tie-robust at the K boundary (production comparer minus the id tie-break); k=4 sampling bias documented (doc comment + D9). `FtmoGroupSearchLimitsTests` pins 150 / 150 / 15 min / 24 (the near-vacuous ratio test removed); the thin margin (one P=24 sample at 60.9 s) is recorded in the benchmark doc, the limits test and D5.
- [ ] 1d.3.2 **STOP and ask the user for DB authorization** before running, naming the target connection and environment (expected: local SBDEMO2 pool, read-only). Never run without an explicit yes. _[Req: Calibration]_
- [x] 1d.3.3 Run (after authorization) and record recall@10, recall@25 per k in the doc comment and PR description. Trust threshold: recall@10 and recall@25 >= 0.9 per k (k with >= 10 sampled candidates; at least one k must qualify); otherwise raise `ShortlistSize` or revise the score, re-record. If the user declines, record "not run" and 2b stays blocked. _Done 2026-10-06 (calib-4.log): PASSED. SBDEMO2 pool, FX 1.05..1.20, maxPerInstrument=2; 130 strategies, 18 eligible (USATECHIDXUSD=12, XAUUSD=6); 4029 enumerated, 1719 survivors, shortlist 150; full ground truth of all 1719 groups (k4 stride 1). recall@10 and recall@25 = 1.000 for k=2, 3 and 4; DEPTH d100 = K (surrogate order equals the true order on the top 25). Run took 4.5 h, ~9.4 s per group, sequential in the harness. History: the old proxy (worst DailyUsed, then Peak) failed with recall@25 0.72 / 0.36 / 0.16 and was replaced by `FtmoRaceSurrogate`. Lesson: a strided k4 truth gave a false failure (recall 0.6 / 0.24, calib-3.log) because the sample's top K does not match a global quota. Harness: `FTMO_CALIBRATION_TRUTH_FILE` caches the truth (fingerprint + schema version), a DEPTH diagnostic, failures reported per size._
- [ ] 1d.3.5 Measure the real per-group full-simulation time in the job runner (parallel). The calibration measured ~9.4 s per group sequentially; 9.4 s x 150 exceeds the 15-min budget if the runner is sequential, so the budget and shortlist size must be confirmed against the parallel measurement. _Partial 2026-10-06: a real Debug-API run took about 18 s per group and stopped at the budget after 51 of 150 groups._
- [x] 1d.3.6 Rebalance the budget after the real run: `ShortlistSize` 75 (per-size quota 25, DEPTH d100 = 25), `DefaultMaxFullSimulations` 75, `DefaultMaxWallClock` 30 min, ceilings kept (500 / 3600 s); simulate the shortlist interleaved by size (`FtmoGroupSearchEngine.InterleaveBySize`, applied in `Plan`) so a budget stop represents every size. `FtmoGroupSearchLimitsTests` pins updated (75 / 75 / 30 min / ceilings; the budget x measured-time relation kept); `FtmoGroupSearchInterleaveTests` (7: order, uneven 5/5/3, missing size, permutation, plan order, budget stop of 3 = one per size, ranking independent of order). Falsified: `Plan` without the interleave -> 2 tests red; restored. Frontend fixtures/specs aligned (150 -> 75). Full suite 1611 passed / 12 skipped.
- [ ] 1d.3.4 Gate: format, build, full suite once, pins, tripwire; delete `bin-scratch/`. _Code gate run for the 1d code: format exit 0 (10 touched .cs), `build -warnaserror --no-incremental` 0 warn / 0 err, full suite 1401 passed / 12 skipped (baseline 1357 / 8: +44 tests, +3 benchmark +1 calibration skipped); tripwire green; engine files unchanged (hash pins). Left unchecked until 1d.3.3 is recorded._

## 2a — Job DTOs, registry, worker (~500, flagged)

**2a-ii status: DONE.** The real `FtmoGroupSearchRunner` (`SVC/FtmoGroupSearchRunner.cs`, `internal sealed`, `IServiceScopeFactory` + `TimeProvider`): the per-job scope exists only inside `LoadAsync` (strategies of the account, limits, then `LoadMembersAsync` and `LoadTradesAsync`: six `AsNoTracking` queries, none per strategy; an account with no strategies stops after 1), so the scope and its `DbContext` are disposed before the cache build, `Plan`, `Simulate` and `Rank`; the cache is a local (test seam `OnCacheBuilt` + weak reference proves release on completion and on cancel, with the runner kept alive). Pool above `MaxPoolSize` eligible, a missing limits row, a missing/unusable request all throw `FtmoGroupSearchRefusedException` (new, internal), which the worker shows as the `Failed` message (the fixed message still hides any other exception). Stages: Loading, Eligibility (= projecting + eligibility), Proxy, Simulating, Ranking. Deviations: (1) the enum has no `Projecting`, so the projection stage is reported as `Eligibility` (the 2a.1.1 pin forbids a new member); (2) `FtmoGroupSearchRowDto` gained a trailing `Symbols` (nullable default, so the committed-to-be worker tests compile unedited); (3) the wall-clock budget runs from the start of the Simulating stage; (4) the worker has a 4-line `catch (FtmoGroupSearchRefusedException)`; (5) eligibility is evaluated once by the runner for the pool check and again inside `Plan` (cheap, pure). Risk: a pool mixing source time zones uses the first resolved zone for the whole pool (`BuildGroupParams`); the engine prunes mixed-zone groups but a group wholly in a later zone is simulated with the first zone. Falsified: scope not disposed, cache kept in a runner field, an extra per-strategy query, `>` to `>=` on the pool bound, wall clock ignored, kind headroom shifted, refusal message dropped: each red; restored.

### Phase 2a.1 — DTOs and enums
- [x] 2a.1.1 RED `TESTS/FtmoGroupSearchEnumZeroDefaultTests.cs` (new file): `default(FtmoGroupSearchStatus)` is a non-completing member (`Unknown = 0`) and not `Completed`; exact members (`Running`, `Completed`, `StoppedAtBudget`, `Cancelled`, `Failed`) and numeric values; same for the stop-reason and exclusion-reason enums, each zero member with its own test; banned-wording sweep (incl. "survival") over enum names, DTO property names and `Disclosures`. _[Req: Lifecycle; Disclosures]_ _Done: `FtmoGroupSearchEnumZeroDefaultTests` (7): Status/Stage/StopReason/IneligibleReason zero members each tested, numbers pinned, banned-wording sweep over enum names, DTO property names and the disclosure texts. RED: types absent (CS0103/CS0246, 62 errors). Falsification of the sweep: the sweep test also asserts the regex catches "SurvivalShare", "passed", "Safe", "survived"._
- [x] 2a.1.2 GREEN create `API/Application/DTOs/Backtests/FtmoGroupSearchDto.cs` (request, job, progress, ineligible, row; fractions; per-kind headroom incl. median drawdown and end used; `StoppedAtBudget` stop reason and not-computed count; funnel counts) and `API/Application/Interfaces/IFtmoGroupSearchJobs.cs`. _Done: `FtmoGroupSearchDto.cs` + `IFtmoGroupSearchJobs.cs` (also holds `IFtmoGroupSearchRunner`). DEVIATION: the per-kind headroom DTO has worst daily, worst max, median max and the rank scalar; no "end used" (the shipped `KindHeadroom` does not carry the FX end)._
- [x] 2a.1.3 RED/GREEN disclosures always present, including `Running` and budget-stopped: N = candidates enumerated before filtering, shortlist and fully-simulated counts, selection bias, elimination risk not certification, group disclosures, lost on API restart. _[Req: Disclosures]_ _Done: disclosures built on EVERY snapshot by the registry (Running, StoppedAtBudget, terminal); N = `Funnel.Examined`; texts for selection bias, closed-trade elimination risk, the 3 group disclosures, restart loss._

### Phase 2a.2 — Registry (design D7)
- [x] 2a.2.1 RED `TESTS/FtmoGroupSearchJobRegistryTests.cs`: at most one job; `TryStart` atomic under concurrent callers (exactly one wins); second start while Queued/Running returns the running id; start allowed after Completed/StoppedAtBudget/Cancelled/Failed; progress snapshot immutable and monotonic; the last terminal job is retained until the next start; cancel idempotent on a terminal job; unknown id -> not found. _[Req: Lifecycle]_ _Done: `FtmoGroupSearchJobRegistryTests` (16 cases): 16 threads x 200 rounds, exactly one winner; 409 carries the running id; start allowed after each of the 4 terminal statuses; monotonic and immutable snapshots; retention; cancel idempotent / false for unknown; unknown id null._
- [x] 2a.2.2 GREEN create `SVC/FtmoGroupSearchJobRegistry.cs` (singleton, lock, `Volatile.Write` snapshots). _Done: `SVC/FtmoGroupSearchJobRegistry.cs` (singleton, lock for writes, `Volatile` snapshot reads, one-slot `Channel<Guid>`)._
- [x] 2a.2.3 Falsification: remove the lock; confirm the concurrent-start test red (repeat to avoid a lucky pass); restore. _Done: `lock` removed from `TryStart` -> concurrent-start test red (first run); restored. Also: monotonic clamp removed -> red; restored._

### Phase 2a.3 — Worker
- [x] 2a.3.1 RED `TESTS/FtmoGroupSearchWorkerTests.cs` (fake engine): picks a queued job from a bounded `Channel<Guid>(1)`; per-job DI scope disposed BEFORE the pure engine runs; cancel reaches `Cancelled` with partial ranked results and no work running; budget stop reports `StoppedAtBudget` naming the limit; engine exception -> `Failed`, worker keeps serving; `stoppingToken` cancels the job; projection cache released at the end. _[Req: Cancel; Budget; Cache released]_ _PARTIAL (worker only, fake runner, `ManualClock : TimeProvider`, no sleeping): Completed, budget stop naming the limit + not-computed, engine Cancelled flag with partial rows, cancel through the registry reaching the runner token, cancel before claim, exception -> Failed without leaking the message and the worker keeps serving, stoppingToken cancels the job, elapsed stamped from the clock, `ExecuteAsync` serves a queued job. NOT done (2a-ii, with the real runner): per-job DI scope disposed before the engine, projection cache released at job end._
- [x] 2a.3.2 GREEN create `SVC/FtmoGroupSearchWorker.cs` (`BackgroundService`, loads pool through `FtmoGroupMemberResolution`, `AsNoTracking`, linked CTS). Registration deferred to 2b. _PARTIAL: `SVC/FtmoGroupSearchWorker.cs` (`BackgroundService`, linked CTS, `IFtmoGroupSearchRunner` + `TimeProvider` injected). NOT done (2a-ii): the real runner (loads the pool through `FtmoGroupMemberResolution`, `AsNoTracking`, builds the cache, calls `Plan`/`Simulate`/`Rank`, maps to the DTOs). Registration stays in 2b._
- [x] 2a.3.3 Falsification: do not link `stoppingToken`; confirm the shutdown test red; restore. _Done: `stoppingToken` not linked -> shutdown test red (the test is bounded by `WaitAsync(10 s)`, so it fails instead of hanging); restored. Also: budget stop mapped to Completed -> 2 red; restored._
- [x] 2a.3.4 Gate: format, build, full suite once, pins, tripwire; delete `bin-scratch/`. _Done in 2a-ii (see the 2a-ii status above). Registration of the runner/worker stays in 2b._
- [x] 2a.3.5 Review correction (RESILIENCE-001/002/003/005). _Done: (001) `ServeAsync` (the loop behind `ExecuteAsync`) ends any non-terminal job when it exits, via the new `registry.FinishCurrent`: `Cancelled` on host stop, `Failed` + rethrow on an unexpected fault. (002) `ProxyAll`/`Plan` take a `CancellationToken` passed as `ParallelOptions.CancellationToken` (results stay slot-indexed, deterministic); a cancel in the Proxy stage returns `Cancelled` with no rows. The runner's CPU stages run on `Task.Factory.StartNew(LongRunning)`, a dedicated thread, because a job holds its thread for up to 15 min (the proxy's `Parallel.For` still borrows pool workers by design). (003) the pool bound is checked after the five loading queries and BEFORE the trades and the cache, on the members passing every trade-independent eligibility check (both kinds, symbol usable, zone resolved): an upper bound of the eligible count, so a pool whose excess members fail only the projection is now refused (stricter than before); the post-cache check is removed (it could no longer fire). (005) structured worker logs: start, end (status, stop reason, examined, rows, ms), cancel, refusal (warning + message), failure (error + exception), worker-stopped abandonment. 14 new tests (`FtmoGroupSearchResilienceTests` 10, runner 4); no existing assertion changed. Falsified: token dropped from `ParallelOptions`, pool check moved after the trades, `LongRunning` to `None`, `FinishCurrent` removed, refusal log removed: 8 tests red; restored._
- Known, not fixed: RESILIENCE-004 (a single source zone per pool is assumed; every spec is `Asia/Jerusalem` today); RESILIENCE-006 and -007 are informational.

## 2b — Controller and DI (~250; blocked until 1d.3.3 is recorded)

- [x] 2b.1 RED `TESTS/FtmoGroupSearchControllerTests.cs`: `[Authorize]` and route reflection (401 pinned); 202 + Location with the job id and no computation in the request; 400 with `{message}` for each missing required field (naming it; `sizeDecimals = 0` accepted), `minMembers < 2`, `maxMembers > MaxMembers`, `minMembers > maxMembers`, `maxPerInstrument < 1`, budget above its ceiling, empty or unknown `tradingAccountId`, non-positive capital or risk, invalid grid; 409 `{runningJobId}`; `GET {id}` 200 / 404; `GET current` 200 / 204; `DELETE {id}` 204, idempotent on a terminal job, 404 unknown. _[Req: Endpoint contract; Lifecycle]_
- [x] 2b.2 GREEN create `API/WebAPI/Controllers/FtmoGroupSearchController.cs` (route `api/ftmo-simulations/group-search`; `FtmoSimulationsController.cs` NOT edited); modify `API/Infrastructure/DependencyInjection.cs` (+3 registrations: registry singleton, worker hosted service, engine/jobs interface).
- [x] 2b.3 Falsification: `>` to `>=` on the `MaxMembers` bound; confirm red; restore.
- [ ] 2b.4 Optional runtime check (needs user authorization, names the target): start, poll, cancel one SBDEMO2 search; record start-to-first-progress time.
- [x] 2b.5 Gate: format, build, full suite once, pins, tripwire; `git diff --stat` shows `FtmoSimulationsController.cs` and engine files unmodified; delete `bin-scratch/`.

## 3a — Model, service, mappers, route, sidebar, i18n (~420)

- [x] 3a.1 RED `WEB/features/simulator/simulator.routes.spec.ts` is NOT edited; new `simulator.search.routes.spec.ts`: `ftmo/search` lazy-loads the page; existing `ftmo` entry unchanged. _[UI Req: Route]_ _RED: `ftmo-group-search-page.component` unresolved. GREEN: route added; existing spec unedited._
- [x] 3a.2 RED `WEB/core/models/ftmo-group-search.model.spec.ts`: enums with explicit numbers mirroring the backend, every value-0 member asserted by its own test (status `Unknown = 0`, stop reason, exclusion reason); typed fixtures without casts. _RED: model module unresolved. GREEN: enums with explicit numbers, every zero member tested, typed no-cast fixtures with key counts, banned-wording sweep over member and key names._
- [x] 3a.3 RED `WEB/core/services/ftmo-group-search.service.spec.ts` (HttpTestingController): `start` POST body (FX only when needed, `sizeDecimals = 0` and null kept), 409 maps to the running id, `get(id)`, `getCurrent()` (204 -> null), `cancel(id)`, 404 -> lost state; errors through the shared error mapper. _[UI Req: Progress]_ _RED: service unresolved. GREEN: 18 HttpTestingController cases (409 -> alreadyRunning, 400 detail, 404 -> `SIMULATOR.FTMO_SEARCH.ERRORS.LOST`, 204 -> null, DELETE). DEVIATION: the shared mapper is not exported and existing services stay unedited, so the service carries its own small mapper (same keys `ERRORS.INVALID_QUERY` / `ERRORS.REQUEST_FAILED`)._
- [x] 3a.4 RED `WEB/features/simulator/ftmo-group-search.mappers.spec.ts`: fractions -> percent ONCE (0.045 -> 4.5%, 0.8 -> 80%, never 8000%); ceiling comparison on raw fractions (input 5 -> 0.05; breach 0.049 highlighted; worse kind decides: 0.03/0.06 not highlighted, 0.03/0.04 highlighted); null median -> explicit absent, real 0 stays 0; exact-value status/reason mapping, unknown -> UNKNOWN with raw value. _[UI Req: Fractions; Enum]_ _RED: mappers unresolved. GREEN: `fractionToPercent`/`percentToFraction`/`breachShare`/`isWithinCeiling`/`medianVm`/enum key maps; a refused kind is never within the ceiling._
- [x] 3a.5 GREEN create `WEB/core/models/ftmo-group-search.model.ts`, `WEB/core/services/ftmo-group-search.service.ts` (new, existing services NOT edited), `WEB/features/simulator/ftmo-group-search.mappers.ts`, `simulator.routes.ts` (+ `ftmo/search`; modify), minimal shell `ftmo-group-search-page/*` (title + always-visible disclosure). _Done: model, service, mappers, route `ftmo/search`, shell page (title + disclosure)._
- [x] 3a.6 RED sidebar spec (new file): search entry after the group entry, link `/simulator/ftmo/search`, ES text rendered with real dictionaries, the existing `/simulator/ftmo` link still opens the group page (with `[routerLinkActiveOptions]="{exact:true}"`). GREEN modify `WEB/shared/layout/main-layout/main-layout.component.html`. _[UI Req: Route, labels]_ _Done: `main-layout.search.spec.ts` (order, href, EN/ES text, active state on each page); both submenu links use `[routerLinkActiveOptions]="{ exact: true }"`. Falsified: exact removed on the group link -> active-state test red._
- [x] 3a.7 RED/GREEN `public/assets/i18n/{en,es}.json`: `SIMULATOR.FTMO_SEARCH.*` and `SIMULATOR.NAV.FTMO_SEARCH`; new `simulator.search.i18n.spec.ts` (EN/ES key and placeholder parity, banned-wording sweep incl. "survival"/"supervivencia", shell rendered with real dictionaries: no `{{`, no raw key). Extend `simulator.theme.spec.ts` consumption only through a NEW theme spec for the search CSS. _[UI Req: i18n; Disclosure visible]_ _Done: `SIMULATOR.NAV.FTMO_SEARCH` and `SIMULATOR.FTMO_SEARCH.*` in EN/ES (neutral Spanish); `simulator.search.i18n.spec.ts` + `simulator.search.theme.spec.ts`._
- [x] 3a.8 Falsification: add a banned word to one ES key; confirm red; return the ceiling as a percent in a fixture; confirm 3a.4 red; restore. Gate: prettier, `tsc --build`, full `ng test` once. _Done: ceiling returned as a percent -> 3 red; fraction scaled twice -> 5 red; "supervivencia" added to an ES key -> sweep red; sidebar exact removed -> 1 red; all restored (115 focused green). Gates: prettier 0, tsc 0, full ng test see report._

## 3b — Form, progress, page lifecycle (~450)

- [x] 3b.1 RED `ftmo-search-form/*.spec.ts` with real dictionaries (EN, ES): defaults (size 2..4, max per instrument 1, identical excluded, 1% rule off, ceiling 5%, capital `10000`, risk empty, broker `FTMO` and backtest grid from the shared constants, not editable); size bounds follow `MaxMembers` from the candidates read (5 not accepted, bound stated); FX inputs only when the pool has a non-USD strategy, none sent otherwise; Start disabled with empty risk, invalid capital, min > max or a running job; number inputs `step="any"`, form `novalidate` (RED proof as `ftmo-group-form.native-validation.spec.ts`); no truthiness on 0 (`maxPerInstrument` minimum, `sizeDecimals = 0`). _[UI Req: Form]_ _Done: form spec + mappers spec (defaults, bounds, FX, step any, novalidate, zero values); symbol filter omitted (no backend field)._
- [x] 3b.2 RED `ftmo-search-progress/*.spec.ts`: progress bar and funnel counts (examined, removed per stage, full sims done/planned, elapsed); Cancel enabled only while running; terminal states distinct and translated (completed, stopped at budget naming the limit and the not-computed count, cancelled, failed); value-0 status renders its own label; no `{{`, no raw key. _[UI Req: Progress]_ _Done: progress spec with real dictionaries, every terminal state, zero status._
- [x] 3b.3 RED page spec (fake timers): `timer(0, 1000)` polling -> `switchMap(get)` -> `takeWhile(!terminal, true)`; counts update; Start 202 attaches; 409 attaches to the running id with a notice; 404 on poll shows the lost notice and enables Start; Cancel sends DELETE, state shows cancelled, polling stops, partial table stays. _[UI Req: Progress]_ _Done: page spec with fake timers (202, 409, 400, 404, cancel)._
- [x] 3b.4 RED **leaving does not cancel (user decision 2026-10-04)**: destroy stops polling (no timers left) and sends NO DELETE; on init the page reads `GET current`: 200 re-attaches (progress and partial results shown, polling resumed), 204 shows the empty form; an explicit Cancel click is the only DELETE path. _[UI Req: Leaving; Returning]_ _Done: destroy stops polling, no DELETE; GET current 200/204._
- [x] 3b.5 GREEN create `ftmo-search-form/*`, `ftmo-search-progress/*`, extend `ftmo-group-search-page/*` (container; presentational children OnPush with `input()/output()`), mappers, i18n EN/ES; reuse `fractionToPct`-style helper once, no second conversion. _Done: form, progress, page, mappers, i18n EN/ES._
- [x] 3b.6 Falsification: send DELETE in `ngOnDestroy`/`takeUntilDestroyed` teardown; confirm 3b.4 red; drop `takeUntilDestroyed`; confirm the no-timers test red; restore. Extend parity, banned-wording and theme specs. Gate: prettier, `tsc --build`, full `ng test` once. _Done: falsified: DELETE on destroy, dropped takeUntilDestroyed, ceiling not converted, truthiness on raw, non-inclusive takeWhile; all red then restored. Gates: prettier 0, tsc 0, ng test 1002 passed._

## 3c — Ranked table (~400)

- [x] 3c.1 RED `ftmo-search-table/*.spec.ts` with real dictionaries (EN, ES): rows in backend order (UI never re-sorts the default view); Deploy and Evaluation columns each show breach share, headroom (worst daily, worst drawdown, median drawdown), funded no-breach share, median days, never merged; members, symbols, peak concurrency; identical Deploy/Eval flag; a refused kind shows its translated reason (not `0`, not blank); null median -> absent marker. _[UI Req: Table; Both kinds]_ _Done: RED = missing modules (TS2307); real EN/ES dictionaries._
- [x] 3c.2 RED ceiling: worse-of-kinds highlight (0.03/0.06 no, 0.03/0.04 yes at 5%); editing the ceiling to 10% re-evaluates highlight and filter with NO request; the filter toggle lists only highlighted rows; ceiling never reorders or drops rows when the filter is off. _[UI Req: Ceiling]_
- [x] 3c.3 RED units and disclosures: 0.045 -> 4.5%, headroom 0.8 -> 80% (never 8000%); disclosure shows N (12,926) once with the selection-bias statement in EN and ES; excluded counts listed by reason; server `Disclosures` never rendered; each row has a deep-link action (target built in 4b, here a typed output event only). _[UI Req: Fractions; Disclosures]_
- [x] 3c.4 GREEN create `ftmo-search-table/*` (presentational, OnPush), mappers, i18n, wire into the page; theme variables only. A selected/filtered state never hides the user's chosen rows list (post-F4 lesson: filter changes keep any chosen-items list visible).
- [x] 3c.5 Falsification: `value ?? 0` on a null median; confirm red; scale a fraction twice; confirm red; restore. Real-dictionary sweep of the whole page in EN/ES over states: before search, running, completed, budget stop, cancelled, failed, lost, refused kind, unknown enum value. Gate: prettier, `tsc --build`, full `ng test` once. _Done: mutations null median `?? 0` and double scale both red, restored, green; full suite 1037 passed._

## 4a — Scatter and frontier (~300)

- [x] 4a.1 RED mappers spec: Pareto frontier on (x = median days, y = breach share, worse kind): sort by (x, y), keep a point iff y < running min, exact duplicates share the status; (100d, 0.05), (120d, 0.03), (130d, 0.04) -> first two frontier, third not; null-median points omitted with their count; y in the same unit conventions as the table (fraction -> percent once). _[UI Req: Scatter]_
- [x] 4a.2 RED `ftmo-search-scatter/*.spec.ts` (plain SVG, real dictionaries): point at x=120, y=4%; frontier highlighted; ceiling line drawn or below-ceiling points distinguished; translated axis labels and omitted-count text; click on a point emits the group (typed output); no `{{`, no raw key.
- [x] 4a.3 GREEN create `ftmo-search-scatter/*` (SVG, theme variables only, no new chart library; `lightweight-charts` rejected per D10), frontier in the mappers, wire into the page, i18n.
- [x] 4a.4 Falsification: compare with `<=` in the frontier scan; confirm red; use a truthy check on `x`; confirm a zero-median case red; restore. Extend theme/i18n specs. Gate: prettier, `tsc --build`, full `ng test` once.

## 4b — Deep link into the group page (~300)

- [x] 4b.1 RED link builder (mappers): `/simulator/ftmo` with `account`, `members` (ascending `StrategyId`), `risk`, `capital`, `fxLow`, `fxHigh` (only when set); broker and lot grid NOT in the link; row and scatter point both navigate with it and do not run. _[UI Req: Deep link]_
- [x] 4b.2 RED new spec `ftmo-group-simulation-page.deeplink.spec.ts` (existing specs untouched; `ActivatedRoute` injected with `{optional:true}`): valid link preselects account, members, risk, capital, FX and sends NO run request; unknown/malformed ids ignored with a notice naming the count; ids over `MaxMembers` (from the candidates read, never hardcoded) truncated in link order with a cap notice; repeated ids count once; unknown or missing `account` falls back to the default, members dropped with a notice; invalid risk (`-5`, `abc`, `0`) leaves the field empty with a notice and Run disabled; empty `members` leaves no selection; edits after load are not overwritten (applied ONCE). _[Delta Req: deep link; Picker default override]_
- [x] 4b.3 RED existing behaviour: with no router provided or no params, the page behaves as before (SBDEMO2 default).
- [x] 4b.4 GREEN modify `WEB/features/simulator/ftmo-group-simulation-page/ftmo-group-simulation-page.component.ts` (+ `.html` for notices) and i18n EN/ES notices; `WEB/features/simulator/ftmo-group-search-page` wires row/point navigation.
- [x] 4b.5 Falsification: re-apply params on every account change; confirm the "edits respected" test red; compare ids by truthiness; restore. Gate: prettier, `tsc --build`, full `ng test` once. User opens a SBDEMO2 search result via the deep link in EN and ES (success criterion).

## 5 — Risk grid (OPTIONAL, last, only if the user asks after 4b)

- [ ] 5.1 Out of scope for this plan's commitments; if started, a new proposal/spec delta is required first (re-run top N at 50/75/100% risk). No tasks are committed here.

---

## Spec-requirement to task map

Backend `ftmo-group-search` (14): Endpoint contract -> 2b.1-2 | Eligibility -> 1b.2 | Per-instrument cap -> 1b.3.2 | Identical Deploy/Eval -> 1b.2 | 1% rule -> 1b.3.2 | Proxy on own window -> 1a.2, 1a.3, 1b.3.3 | Determinism and ranking -> 1b.3.1, 1c.2, 1d.1 | Headroom -> 1c.1 | Shortlist-only full computation (metrics equal direct run) -> 1b.4.1-2 | Job lifecycle -> 2a.1-3, 2b.1 | Budget -> 1b.4.3, 2a.3.1, 1d.2.4 | Composes without changing (extraction, tripwire, cache released) -> 1a.1, 1b.1.1, 1d.1, 2a.3.1 | Disclosures -> 2a.1.1, 2a.1.3 | Calibration -> 1d.3 | Benchmark -> 1d.2.

UI `ftmo-group-search-ui` (11): Route/sidebar -> 3a.1, 3a.6 | Form -> 3b.1 | Progress/cancel, 409, 404, leave/return -> 3b.2-4 | Table + ceiling -> 3c.1-2 | Fractions once -> 3a.4, 3c.3 | Scatter -> 4a | Deep link carries the group -> 4b.1 | Disclosures -> 3a.7, 3c.3 | i18n/banned wording -> 3a.7, 3b.6, 3c.5 | Enum exact-value -> 3a.2, 3a.4, 3b.2.
Delta `ftmo-group-simulation-ui` (2): Deep link requirement -> 4b.2-3 | Modified picker default (account override) -> 4b.2.

Gaps and notes:
- The spec lists `StoppedAtBudget` as a status while the earlier design used a `Completed` + flag; design D7 was aligned to the spec (distinct status).
- The spec's "Poll interval and table page size" are design choices: 1 s polling (D10); the table has no paging (no page size task). Add a paging task if the user wants it.
- Open question "lot grid in the link" stays closed: shared constant on both pages (4b.1).
- The 60 s proxy gate and calibration recall >= 0.9 depend on measured data; they may force a revised score or `MaxPoolSize` (risk below).

## Risks

- **Calibration needs DB authorization (1d.3.2).** If declined, 2b cannot ship (spec: the endpoint MUST NOT ship before the result is recorded); frontend slices 3a-4b can still be built against a mocked service.
- **Proxy may over-prune** (recall@10 < 0.9): forces a higher `ShortlistSize` (longer jobs) or a revised score, and re-measuring in 1d.
- **1b and 2a are near/over budget**; cut points named above. 2a carries three concerns (DTOs, registry, worker).
- **1a refactor of a shipped file** (`FtmoGroupSimulationReadService.cs`): the unedited query-count and byte-identical tests are the only safety net.
- **Frontend slices 3a-4b are chained on a real backend only after 2b**; the user's end-to-end run depends on it.
- **One unidentified flaky frontend test** (1 failure in 9 runs): the gate now fails on any red and classifies by re-running once.
- **No `ng lint`** in this project; the commit skill has no lint step.
- **Spec/design drift fixed here** (status enum, controller class, i18n namespace, deep-link parameters); the verify phase should re-read the corrected artifacts.

## 6 — Job DTO carries the request (2026-10-06, real-run defect)

- [x] 6.1 RED registry + controller: POST `job`, `GET {id}` and `GET current` return the submitted request (risk, capital, ceiling intact), also after progress and finish. GREEN: `FtmoGroupSearchJobDto.Request`, set by the registry. Falsified (request dropped on publish) red, restored.
- [x] 6.2 RED page: a re-attached job with request risk 100 and an empty form links with `risk=100`; the table ceiling comes from `job.request` (0.07 -> 7, null -> default). GREEN: snapshot, form fallback, `LINK_FROM_FORM` notice and i18n keys (EN, ES) removed. Falsified (risk dropped from link) red, restored. Re-populating the form on re-attach: skipped (needs account reselect and candidates reload).
