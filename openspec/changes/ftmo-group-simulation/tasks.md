# Tasks: FTMO group simulation, on its own Simulator screen

Seven chained slices, committed one at a time to `main` by the user via `/commit`, in this order:
**B1 -> B2 -> B3 -> F1 -> F2 -> F3 -> F4** (design.md "Migration / Rollout"). Each slice starts only after the
previous one is green and committed, and each is independently buildable and green (see the Work Units
table). F1 has no backend call, so it may be committed before B2 if the user prefers. Read
`.claude/conventions/backend-core.md` + `backend-testing.md` before any `.cs`, and `frontend-core.md` +
`frontend-data.md` + `frontend-design.md` before any `.ts/.html/.scss`. Paths: `API/` =
`app.trading.algoritmico.api/src/AppTradingAlgoritmico`, `TESTS/` =
`app.trading.algoritmico.api/tests/AppTradingAlgoritmico.UnitTests/Ftmo`, `WEB/` =
`app.trading.algoritmico.web/src/app`.

## Hard rules (checkpoint at every task that touches them)

1. **Strict TDD.** RED before GREEN, every RED observed failing for the right reason. Each slice has at
   least one **falsification** task: break the code, observe red, restore, observe green.
2. **Engine files are untouchable.** `FtmoBreachEvaluator.cs` is NEVER edited, and neither are the race,
   funded phase, enumerator, open-position sweep, calendar, projector or `FtmoMultiStartReadService`
   (`ComputeRun`). The only shipped file B1 may modify is `FtmoSimulationInputs.cs`. The golden pin
   (`FtmoBreachSimulationReadServiceGoldenPinTests.cs`) and the snapshot pin
   (`FtmoBreachSimulationReadServiceTests.cs`, ~:1037) stay green and unedited. If any task seems to need an
   engine edit, STOP and report.
3. **Merger tests are mandatory** (B1): two members with duplicate per-run `RowIndex` values, both counted
   in trading days, both seen by concurrency, and the low and high series renumbered identically.
4. **No existing assertion is edited** unless a user decision forces it; report each case. New tests go
   in new files (also for the web: no edits to existing `.spec.ts`).
5. **Group benchmark (B2).** Gated by `FTMO_BENCH`, median of 3 within 5 s, k in {2,4,6,8,10}. It sets
   `MaxMembers`; the endpoint (controller + DI) must not ship before the measurement is recorded.
6. **Frontend translations.** Tests render with the REAL `en.json`/`es.json`: no `{{`, no raw keys, every
   placeholder key gets its params (PR1c/1d lessons). A banned-wording sweep over the `SIMULATOR`
   namespace in EN and ES, plus an EN/ES parity test (keys and placeholder names).
7. **Enum zero-value tests.** Every value-0 member gets its own test, including
   `FtmoGroupRefusal.InvalidRequest = 0` (backend and TS mirror).
8. **Theme variables.** Only declared theme variables (`src/styles/_variables.scss`), no hardcoded colours
   (the white-panel bug). A new `simulator.theme.spec.ts` extends the check to every new component.
9. **Process safety.** Never kill a process. Every `dotnet build/test` uses `-p:BaseOutputPath=bin-scratch/`
   and the folder is deleted at the end of each slice. Wrap every command in `timeout`. No DB access
   without explicit user authorization (state the target first). No `git commit/push/merge/rebase`.
10. **No banned survival wording** ("passed", "safe", "survived", "would have passed", ES equivalents) in any
    new enum member, DTO, disclosure text or i18n key.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| B1 / B2 / B3 | ~350 / ~450-550 / ~350 |
| F1 / F2 / F3 / F4 | ~150 / ~350 / ~400-650 / ~250 |
| Total | ~2,300-2,650 lines (additions + deletions, tests included) |
| 400-line budget risk | High: B2 over, F3 likely over, B1/B3/F2 near the line |
| Delivery strategy | ask-on-risk (plan already accepted; flags only) |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Flags: **B2** is expected to exceed 400. **F3** is the largest overrun risk. Its tasks are ordered so the
cut is clean after Phase F3.3 (form + readout + service); if the diff there plus Phase F3.4-F3.6 is
clearly above ~500, STOP and ask the user whether to split before continuing.

### Gate commands (verbatim from `.claude/skills/commit/SKILL.md`; run per slice)

Backend (repo root):
```bash
timeout 600 dotnet format app.trading.algoritmico.api/AppTradingAlgoritmico.slnx --include <touched .cs> --verify-no-changes
timeout 900 dotnet build app.trading.algoritmico.api -warnaserror -p:BaseOutputPath=bin-scratch/
timeout 1800 dotnet test app.trading.algoritmico.api -p:BaseOutputPath=bin-scratch/   # full suite once, end of slice
# focused: ... --filter "FullyQualifiedName~FtmoGroupMerger"
```
Frontend (from `app.trading.algoritmico.web/`):
```bash
timeout 300 npx prettier --check <touched .ts/.html/.scss, relative to this dir>
timeout 600 npx tsc --build --emitDeclarationOnly false --noEmit
timeout 1800 npx ng test --watch=false   # full suite once, end of slice
```
Never pipe a gate into `head`/`tail` and read `$?`; redirect to a file and test the exit code.

### Suggested Work Units

| Unit | Goal | PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|----|----------------------|-----------------|-------------------|
| 1 | Limits/symbol split + pure merger | B1 | `dotnet test ... --filter "FullyQualifiedName~Ftmo"` | N/A: no endpoint; proof = shipped Ftmo suites unedited | Revert restores `ResolveSharedAsync`; merger unreferenced |
| 2 | Group service, DTO, endpoint, benchmark, cap | B2 | `--filter "FullyQualifiedName~FtmoGroup"`; bench: `FTMO_BENCH=1 ... -c Release --filter FtmoGroupBenchmark` | Benchmark table; optional authorized read-only DB check (B2.7) | Revert removes new files + 1 DI line |
| 3 | Diagnostics + candidates read | B3 | `--filter "FullyQualifiedName~FtmoGroupDiagnostics|FtmoGroupCandidates"` | N/A: pure + EF test DB | Revert drops diagnostics property + GET |
| 4 | Route, sidebar, shell | F1 | `npx ng test --watch=false --include <simulator specs>` | Open `/simulator/ftmo` (user) | Revert route + sidebar block |
| 5 | Picker + candidates client | F2 | same, picker specs | Picker on SBDEMO2 (user, needs B3) | Revert picker + service additions |
| 6 | Form, run lifecycle, panels, refusals | F3 | same, form/page/mappers/i18n specs | Run a SBDEMO2 group in EN and ES (user, needs B2) | Revert form + page wiring |
| 7 | Diagnostics panel | F4 | same, diagnostics specs | Diagnostics on a real run (user) | Revert panel + VM |

---

## B1 — Input split + pure merger (~350; `FtmoSimulationInputs.cs`, `FtmoGroupMerger.cs`)

### Phase B1.0 — Baseline
- [x] B1.0.1 Run the shipped suite filtered `Ftmo` plus `BacktestPortfolioRiskTripwireTests`; record pass count. All green before any edit. _[Req: Engine untouched]_ _Done: baseline 311 passed / 4 skipped (Ftmo + tripwire filter); full suite baseline 1027/4._
- [x] B1.0.2 Read `FtmoSimulationInputs.cs:59-186` and one existing EF-test-DB suite (`FtmoMultiStartReadServiceTests`) to copy its fixture helpers. _Done: read._

### Phase B1.1 — `ResolveLimitsAsync` / `ResolveSymbol` / `SettlesInAccountCurrency` (design D1)
- [x] B1.1.1 RED `TESTS/FtmoSimulationInputsResolveSymbolTests.cs`: refusal order spec -> calibration -> FX (one test per refusal); calibration is NOT read when the spec is refused (a calibration that would refuse never wins). _Done: RED: 36 of 42 tests failed with NotImplementedException (stubs) before GREEN._
- [x] B1.1.2 RED same file: USD-settling spec yields FxBand `(1,1)` with null fx; non-USD uses the band; `FxRateNotDeclared`; `InvalidFxBand`; `ZoneRefusal` (`TimeZoneDataUnavailable`) is set only when `Refusal` is null and is separate from it; `SourceZone` resolved; `SettlesInAccountCurrency`; `TryResolveBerlin`. _Done: RED: same run; USD (1,1), FX refusals, ZoneRefusal separate, helpers._
- [x] B1.1.3 RED `ResolveLimitsAsync` tests (in-memory db): `LimitsNotConfigured` -> `ProductNotTwoStep` -> `DrawdownModelNotStatic` order; success carries DailyPct/MaxPct/ProfitTargetPct. _Done: RED: same run; limits order and carried values._
- [x] B1.1.4 RED composition precedence through `ResolveSharedAsync`: `InvalidRequest` wins over `TimeZoneDataUnavailable`; DB queries issued unchanged when limits refuse (symbol queries not issued). _Done: pin by design: these 6 ResolveSharedAsync composition tests were already green on the unrefactored code and stay green after; their falsification is B1.1.7._
- [x] B1.1.5 GREEN `FtmoSimulationInputs.cs`: add `LimitsResolution`, `SymbolResolution`, `ResolveLimitsAsync`, pure `ResolveSymbol`, `ResolveSymbolAsync`, `TryResolveBerlin`, `SettlesInAccountCurrency`; recompose `ResolveSharedAsync` with the same signature and `SharedResolution` shape. `ProjectRun` untouched. _Done: GREEN: 42/42; `ProjectRun` untouched._
- [x] B1.1.6 Pin: run every shipped Ftmo suite, golden pin and snapshot pin; confirm `git diff --stat -- app.trading.algoritmico.api/tests` lists only NEW files (zero modified). _[Req: Engine untouched, refactor behaviour-preserving]_ _Done: Ftmo+tripwire filter 353 passed / 4 skipped (311 + 42 new); golden and snapshot pins green; `git diff --stat` on tests is empty (only 2 NEW files)._
- [x] B1.1.7 Falsification: swap spec/calibration order in `ResolveSymbol`; confirm B1.1.1 red; also move the zone refusal before `InvalidRequest` in the composition and confirm B1.1.4 red; restore both, green. _Done: falsified: calibration-before-spec gave 4 red (incl. SpecRefused_CalibrationThatWouldRefuseNeverWins); zone refusal before InvalidRequest gave ResolveSharedAsync_InvalidRequestAndUnresolvableZone red; restored, 42/42 green._

### Phase B1.2 — `FtmoGroupMerger` (design D2)
- [x] B1.2.1 RED `TESTS/FtmoGroupMergerTests.cs` (**mandatory, hard rule 3**): two members with duplicate per-run `RowIndex 0..n-1` yield globally unique gapless merged `RowIndex 0..N-1`. _[Req: Merge, scenario unique]_ _Done: RED: 24 of 26 failed with NotImplementedException (the 2 passing are the no-renumbering demonstrations)._
- [x] B1.2.2 RED row map round-trips to `(member, origRow)`; per-member counts match; same-Open ties break by member then row; overlapping members interleave by Open. _[Req: Merge]_ _Done: RED: same run._
- [x] B1.2.3 RED **Low/High identical renumbering** (same `RowIndex` per position at both ends); a `Low[i]`/`High[i]` disagreement on `RowIndex/OpenSource/CloseSource` throws `InvalidOperationException`. _Done: RED: same run; mismatch on RowIndex/Open/Close/length throws, also outside the window._
- [x] B1.2.4 RED **trading days**: member A scalable `RowIndex 5` on Berlin day D1, member B scalable `RowIndex 5` on D2; counted through the shipped race over the merged series = 2, both days seen. _[Req: Merge, scenario trading days]_ _Done: RED: same run; counted through `AttributeOpenDays` + `CachedOpenDays` (=2); a no-renumbering concatenation counts 1 (silent loss, asserted)._
- [x] B1.2.5 RED **concurrency**: A `RowIndex 5` 10:00-12:00 and B `RowIndex 5` 11:00-13:00; evaluating the merged series through the unedited evaluator detects concurrent open position, and equals the result for the same trades with distinct indices. _Done: RED: same run; merged high series gives BreachContingent/ConcurrentOpenPosition, equals distinct indices; no-renumbering concatenation gives clean Breached (asserted)._
- [x] B1.2.6 RED `Intersect`/trim: window `[max first Open, min last Close]` with Unscalable rows included in ranges; empty -> null; a row opened before start or straddling the end is dropped at both edges; Unscalable rows kept inside; in-window counts per member; surviving trades keep their size (no re-sizing). _Done: RED: same run._
- [x] B1.2.7 GREEN create `API/Infrastructure/Services/FtmoGroupMerger.cs` (`MemberSeries`, `GroupWindow`, `RowOrigin`, `MergedSeries`, `Intersect`, `Merge`) exactly per D2: sort `(OpenSource, MemberOrder, OriginalRowIndex, i)`, `with { RowIndex = r }`. _Done: GREEN: 26/26. Also added `OrderMembers` (ascending StrategyId, distinct) beyond D2, for the member-order test._
- [x] B1.2.8 Falsification (mandatory): temporarily keep the original `RowIndex` instead of renumbering; confirm B1.2.1, B1.2.4 and B1.2.5 go red; also change the trim to keep straddling rows (drop the `Close <= W.End` test) and confirm B1.2.6 red; restore, green. _Done: falsified: keeping the original RowIndex gave 5 red (B1.2.1, RowMap roundtrip, B1.2.4, both B1.2.5 tests); dropping `Close <= End` gave 2 red (both trim tests); restored, 26/26 green._

### Phase B1.3 — Gate
- [x] B1.3.1 Format, build `-warnaserror`, full test suite once; golden pin and snapshot pin green; tripwire green. `git diff --stat` shows `FtmoSimulationInputs.cs` as the only modified shipped file and no change to `FtmoBreachEvaluator.cs`. Delete `bin-scratch/`. _Done: `dotnet format --verify-no-changes` exit 0 (after formatting my own new test file), build -warnaserror 0 warnings, full suite 1095 passed / 4 skipped (1027 + 42 + 26); only `FtmoSimulationInputs.cs` modified among shipped files; `bin-scratch` deleted._

## B2 — Group service, DTO, endpoint, benchmark, cap (~450-550; over budget, flagged)

### Phase B2.1 — Enum, limits, DTOs
- [x] B2.1.1 RED `TESTS/FtmoGroupEnumZeroDefaultTests.cs` (new file; do not edit `FtmoEnumZeroDefaultTests.cs`): `default(FtmoGroupRefusal) == InvalidRequest`, value 0; exact member list and numeric values (8 members); banned-wording sweep over enum names. Any other new enum introduced in this slice gets its own zero test. _Done: RED: 5 of 7 failed against a deliberately mis-ordered stub enum and empty limits (the 2 banned-wording sweeps pass vacuously on empty data); GREEN 7/7._
- [x] B2.1.2 RED `FtmoGroupSimulationLimits`: `MaxMembers` between 1 and 8; exactly three disclosure texts covering concurrent-breach dominance, same-close ordering, eligibility rules not modelled; no banned wording. _Done: RED: `MaxMembers_IsBetweenOneAndEight` and the 3-disclosure test failed on the stub (`MaxMembers = 0`, no disclosures); GREEN. `MaxMembers` is still the unmeasured placeholder 8 (see B2.4.4)._
- [x] B2.1.3 GREEN create `API/Domain/Enums/FtmoGroupRefusal.cs`, `API/Application/DTOs/Backtests/FtmoGroupSimulationDto.cs`, `API/Application/Interfaces/IFtmoGroupSimulationReadService.cs`, `API/Infrastructure/Services/FtmoGroupSimulationLimits.cs` (placeholder `MaxMembers = 8`, finalised in B2.5). Decisions recorded here: **`FtmoGroupMemberDto` gains `SourceTimeZoneId`** (open item: zone in the member DTO, not the envelope); **`FtmoGroupMemberRefusalDto(StrategyId, Name, FtmoGroupRefusal Reason, FtmoSimulationRefusal? RunReason)`** with `RunReason` non-null only when `Reason == MemberRunRefused`. _Done: created the enum, DTO file, interface, limits. Decisions recorded: `FtmoGroupMemberDto.SourceTimeZoneId` and `FtmoGroupMemberRefusalDto(StrategyId, Name, Reason, RunReason)` as specified. Additions beyond the plan: `FtmoGroupSimulationRequest` (nullable body) + `FtmoGroupSimulationParameters` (validated) and `UnknownStrategyIds` on the envelope (names the ids behind `MemberNotFound`)._

### Phase B2.2 — `ComputeGroup` (design D4; open item: mixed causes)
- [x] B2.2.1 RED `TESTS/FtmoGroupComputationTests.cs`: missing kind refuses ONLY that kind with `MemberMissingKind` listing every missing member; a member with no runs refuses both. _[Req: Kinds]_ _Done: RED: 25 of 26 `FtmoGroupComputationTests` failed with NotImplementedException (the 26th, the cancellation test, saw the same stub); GREEN 26/26 after fixture corrections (see B2.2.6)._
- [x] B2.2.2 RED member-level: all failing members listed with their own reason, none dropped; `RiskNotEstimable` refuses only its kind; symbol-level reasons (`InstrumentSpecMissing`, `PointValueNotCalibrated`, FX) refuse both kinds; the other kind runs when clean. _[Req: Member-level, FX]_ _Done: covered in the same file (all failing members listed, RiskNotEstimable one kind only, symbol-level reasons as a theory over 4 reasons, both kinds)._
- [x] B2.2.3 RED **mixed causes (open item, chosen option):** a kind with one member missing the kind and another `RiskNotEstimable` carries `Refusal = MemberRunRefused` and a per-member list where B shows `Reason = MemberMissingKind` and C shows `Reason = MemberRunRefused, RunReason = RiskNotEstimable`. A kind whose failing members all share one cause carries that cause (`MemberMissingKind`, `MemberRunRefused`, or `MemberHasNoTradesInWindow`). _Done: covered: mixed causes carry `MemberRunRefused` with each member own reason; uniform causes carry that cause._
- [x] B2.2.4 RED **`NoCommonWindow` per kind**: non-overlapping Deploy ranges and overlapping Eval ranges -> Deploy `NoCommonWindow` with every member's coverage and no blamed member, Eval runs; and the swapped case. `MemberHasNoTradesInWindow` lists the member and leaves the other kind alone. _[Req: Window, new scenario]_ _Done: covered: NoCommonWindow per kind both ways with coverage and no blamed member; MemberHasNoTradesInWindow lists the member and leaves the other kind alone._
- [x] B2.2.5 RED a refused kind has no `Run`, no starts, no summary; `Segment` common vs `Unknown`; `UnscalableCount` is the in-window sum; `ProfitTargetMismatch` passes through; `RunId` is `Guid.Empty`; Deploy and Eval never share a figure. _Done: covered: no Run/window/coverage on refusal, segment common vs Unknown, in-window `UnscalableCount`, ProfitTargetMismatch pass-through, `RunId = Guid.Empty`, own Kind per result._
- [x] B2.2.6 RED **cross-member concurrency**: A open across B's breach close produces `ConcurrentOpenPosition`/Contingent through the unedited evaluator on the merged series; USD member projects `(1,1)`, EUR member with band flags `FxRoundingSensitive` when a start flips. _Done: covered: cross-member concurrency gives Phase1 BreachedFirst + Contingent through the unedited evaluator; duplicate per-run RowIndex across members counts every member trading days (4 days -> TargetReachedFirst); USD+EUR members with a band flip flag FxRoundingSensitive. Fixture note: the window trim drops rows outside the intersection, so each fixture gives members extra rows to keep the tested rows inside it._
- [x] B2.2.7 GREEN create `API/Infrastructure/Services/FtmoGroupComputation.cs` (pure; stage order: member refusals -> `Intersect` -> `Merge` -> empty-member check -> `ComputeRun(Guid.Empty, ...)`); `ct` checked between kinds. No `DbContext` use. _Done: `FtmoGroupComputation.cs` (pure; no DbContext). `GroupParams.SourceZone` is nullable and throws if a member reaches the replay without one (wiring defect), instead of a silent fallback zone. A `Projection.Refusal` (run-level reason from `ProjectRun`) is read alongside the symbol-level `Refusal`._
- [x] B2.2.8 Falsification: build the merge without renumbering inside `ComputeGroup`; confirm B2.2.6 red and that `AttributeOpenDays` keys collapse; restore. _Done: falsified: replaced the merged indices with the original per-run RowIndex inside ComputeGroup -> exactly the two tests went red (contingent breach and trading-days count: the AttributeOpenDays keys collapse); restored, 26/26 green._

### Phase B2.3 — Service (design D5; EF test DB as in `FtmoMultiStartReadServiceTests`)
- [x] B2.3.1 RED `TESTS/FtmoGroupSimulationReadServiceTests.cs`: **one-member equivalence** per kind, every `Run` field equal to the single multi-start result except `RunId`; the fixture is asserted to have no same-close tie that disagrees with open order; a SECOND test documents the tie case (no equality claim). One-member refusal (`InstrumentSpecMissing`) matches the single endpoint. _Done: RED: 24 of 25 service tests failed with NotImplementedException (the passing one is the data-only banned-wording sweep). One-member equivalence per kind: `BeEquivalentTo(single, ComparingRecordsByMembers, Excluding RunId)`, fixture asserted in (Open,row) order with no disagreeing close tie and more than one start. Tie test: the group result DIFFERS from the single result (documented). One-member `InstrumentSpecMissing` matches the single endpoint._
- [x] B2.3.2 RED dedup (`[B,A,B]` -> members A,B ascending, B in `DuplicateIdsRemoved`, k=2); request order does not change the response; same name differing in case on two accounts warns for both and keeps both members. _Done: dedup `[B,A,B]`, order independence, case-insensitive same-name warning keeping both members._
- [x] B2.3.3 RED group-wide refusals: unknown id `MemberNotFound` naming ids; `initialCapital = 0` -> `InvalidRequest`; unconfigured limits -> `SharedInputsRefused` with `SharedRefusal`; **`MixedSourceTimeZones`** lists each member with its `SourceTimeZoneId` and nothing is merged; `TimeZoneDataUnavailable` -> `SharedInputsRefused` (group-wide, added to the spec). On every group-wide refusal `Kinds` is empty (choice, see Risks) and the three disclosures are present. _Done: MemberNotFound (names ids), InvalidRequest (capital 0 / risk 0 / step 0), LimitsNotConfigured and ProductNotTwoStep (limits carried), MixedSourceTimeZones (each member with its zone), TimeZoneDataUnavailable (unknown zone id); every group-wide refusal has empty `Kinds` and the three disclosures. Precedence implemented: InvalidRequest (no query) -> MemberNotFound -> limits -> mixed zones -> zone unavailable._
- [x] B2.3.4 RED sizing fixed before trim (a member with out-of-window trades keeps full-run sizes); `Members` echoed in `memberOrder` with applied FX band; echoed band = declared band if any non-USD member else `(1,1)`; envelope `Status` is `Refused` only for a group-wide refusal, otherwise the shipped non-refused status (member-level refusals live in `Kinds`). _Done: sizing fixed before trim (A calibration rows outside the window, still runs), members echoed in order with currency/zone/applied band, echoed band declared-or-(1,1), all-USD ignores a declared band, FxRateNotDeclared and InvalidFxBand per member for both kinds, symbol-level failure refuses both kinds, envelope `Evaluated` for member-level refusals._
- [x] B2.3.5 RED query count is the same for k=2 and k=6 (six fixed queries, `AsNoTracking`); a cancelled token throws `OperationCanceledException`; banned-wording sweep over DTO property names and `Disclosures`. _Done: query count on in-memory SQLite (a relational provider is needed to count commands; a test-only subclass clears `nvarchar(max)` so the full AppDbContext builds): 6 commands for k=2 and for k=6; cancelled token throws OperationCanceledException; banned-wording sweep over all group DTO type/property names and the disclosures._
- [x] B2.3.6 GREEN create `API/Infrastructure/Services/FtmoGroupSimulationReadService.cs` (six batched queries, `ResolveLimitsAsync` + `ResolveSymbol` per distinct symbol, `ProjectRun` per member and kind, `ComputeGroup` per kind). _Done: `FtmoGroupSimulationReadService.cs`: strategies, limits, runs, specs, calibrations, trades (one query for every replayable run, grouped in memory), all AsNoTracking, `ct` on each. Group-wide refusals return early with fewer queries._
- [x] B2.3.7 Falsification: remove the dedup (or the `StrategyId` sort); confirm B2.3.2 red; restore. _Done: falsified twice: (1) dropping dedup+sort -> 3 tests red (dedup, request order, member echo); (2) adding a per-member query -> the query-count test red ("Expected six to be 8 ... found 12"). Both restored, 25/25 green._

### Phase B2.4 — Group benchmark (design D7; hard rule 5)
- [x] B2.4.1 RED `TESTS/FtmoGroupBenchmarkFixtureTests.cs`: closed-form (no `Random`), k members of 1,000 trades each shifted by `m x spacing / k` hours, every member `RowIndex 0..999`, cross-member overlap present. _Done: `FtmoGroupBenchmarkFixtureTests` (8): k members of 1,000 rows, RowIndex 0..999 each, shift = m x spacing / k hours, identical low/high, cross-member overlap > 100 rows, deterministic. RED: 8/8 NotImplemented; GREEN 8/8._
- [x] B2.4.2 GREEN `TESTS/FtmoGroupBenchmarkFixture.cs`; then `TESTS/FtmoGroupBenchmarkTests.cs` with `[BenchmarkFact]` (`FTMO_BENCH=1`), Release, median of 3, 5 s gate, `Merge` + `ComputeGroup` for BOTH kinds, Never and Fast profiles, asserted at `k = MaxMembers`. _Done: `FtmoGroupBenchmarkFixture.cs` and `FtmoGroupBenchmarkTests.cs`: Never gate (5 s, asserted at `MaxMembers`), Fast measured and REPORTED but not gated (the spec gates only Never; Fast costs about 2.5x more), sweep and informational tests. Skipped without `FTMO_BENCH` (4 skipped), run with it._
- [x] B2.4.3 Measure k in {2,4,6,8,10}: `FTMO_BENCH=1 timeout 1800 dotnet test app.trading.algoritmico.api -c Release -p:BaseOutputPath=bin-scratch/ --filter "FullyQualifiedName~FtmoGroupBenchmark"`. Record the table in the test doc comment and PR description. Confirm the test is SKIPPED without `FTMO_BENCH` and RUNS with it. _Done: measured, 2 full runs plus one partial (Never median of 3, both kinds, Release, 12 logical cores). k=2: 4.499 / 6.176 s; k=4: 11.387 / 17.583; k=6: 21.126 / 34.323; k=8: 54.498 / 55.207; k=10: 82.696 / 100.076; informational k=1: 2.768 / 3.131, k=3: 11.164 / 13.472. Partial run: k=2 4.277, k=4 12.485, k=6 21.190, Fast k=2 11.899, k=4 30.608, k=6 57.994. Table also in the benchmark test doc comment._
- [x] B2.4.4 Set `MaxMembers = min(8, largest k whose Never median <= 5 s)`. If even k=6 fails: lower the cap; if cap < 4 try parallel kinds via `Task.Run`; if still failing STOP and report (never edit the evaluator). _Done: orchestrator approved option A (per-start parallelism, 2026-10-04): `Parallel.For` inside `FtmoMultiStartReadService.ComputeRun` (loop mechanics only; results to a pre-sized array by start index; degree = processor count; `FtmoMultiStartParallelismTests`: 1 thread vs N identical, repeated runs identical, cancellation, degree guard, plus the case-(b) re-check; falsified by writing results in completion order -> 2 red, restored). Re-measured twice (Never median of 3, both kinds, Release): k=2 1.665/1.454, k=4 3.785/3.468, k=6 7.081/6.831, k=8 10.682/9.575, k=10 16.635/16.055 (k=1 0.438/0.463, k=3 2.018/2.154). Largest k within 5 s in BOTH runs = 4, so `MaxMembers = 4`. Gate test at k=4: 3.212 s; Fast at k=4 9.011 s (reported, not gated). PR1 multi-start benchmarks 6/6 passed in both runs (full composition never 0.317/0.267 s, fast 0.918/0.813 s)._
- [x] B2.4.5 Falsification: temporarily set the gate to a value the measured median exceeds; confirm red; restore. _Done: falsified: gate lowered to 1 s with `MaxMembers` 1 -> red ("found 3s, 158ms"); restored._

### Phase B2.5 — Endpoint (blocked until B2.4.4 is recorded)
- [x] B2.5.1 RED controller tests (follow a sibling FTMO controller test): unauthenticated -> 401; missing each required field -> 400 naming them; empty list -> 400; empty GUID -> 400; `sizeDecimals = 0` accepted; `MaxMembers` accepted; `MaxMembers + 1` -> 400 stating the cap; cap counts DISTINCT ids; unusable value is NOT a 400. _Done: `FtmoSimulationsControllerTests` (20): [Authorize] + route reflection, null body, each missing required field (theory of 9 incl. blank broker) names the fields, empty list, empty GUID, `sizeDecimals = 0` accepted, unusable value not a 400, cap accepted, cap+1 -> 400 stating the cap, cap counts DISTINCT ids, token passed through. RED: compile error (controller absent); GREEN 20/20; falsified (`>` -> `>=` on the cap): 2 red, restored._
- [x] B2.5.2 GREEN create `API/WebAPI/Controllers/FtmoSimulationsController.cs` (`[ApiController][Authorize]`, route `api/ftmo-simulations`, `POST group`, private `TryValidateGroupRequest`); modify `API/Infrastructure/DependencyInjection.cs` (+1 `AddScoped`). _Done: `FtmoSimulationsController.cs` (`[ApiController][Authorize]`, `api/ftmo-simulations`, `POST group`, private `TryValidateGroupRequest`); `DependencyInjection.cs` +1 `AddScoped`._

### Phase B2.6 — Gate
- [x] B2.6.1 Format, build `-warnaserror`, full suite once; golden pin, snapshot pin, tripwire (`BacktestPortfolioRiskTripwireTests`; no new file in `SliceFiles`) green; `FtmoBreachEvaluator.cs` unchanged; delete `bin-scratch/`. _Done: format `--verify-no-changes` exit 0 (17 touched .cs); build `-warnaserror` 0 warnings / 0 errors; full suite 1189 passed / 8 skipped (baseline 1164/8 + 25 new); golden pin, snapshot pin, tripwire green and unedited; `FtmoBreachEvaluator.cs` and every engine file unchanged (only `FtmoMultiStartReadService.cs` loop mechanics edited, by decision); bin-scratch deleted._

### Phase B2.7 — Real-data order check (needs user authorization; open item)
- [ ] B2.7.1 **STOP and ask the user** before running: state the target connection/environment. Read-only `SELECT` only, no writes. For the SBDEMO2 held runs, check (a) whether `RowIndex` order equals `OpenSource` order within each run and (b) how many pairs share a `CloseSource` with `RowIndex` order differing from Open order. Confirm table/column names from `BacktestTradeConfiguration.cs`. _NOT RUN (per instruction); the query text is in the apply report._
- [ ] B2.7.2 Record the result in the PR description and update the Open Questions of spec/design. It does not gate the slice: if the user declines, the item stays open and the tie caveat stands.

## B3 — Diagnostics + candidates read (~350)

### Phase B3.1 — `FtmoGroupDiagnostics` (design D6)
- [ ] B3.1.1 RED `TESTS/FtmoGroupDiagnosticsTests.cs`: per-member contribution (in-window, scalable, net low/high, raised/capped/unscalable); members' trades sum to the merged length and nets sum to the merged net at each end.
- [ ] B3.1.2 RED attribution: breach closed by one member -> sole-contributor start for the right phase (P1, P2, Funded); same-instant close by two members credits BOTH and counts a shared-close (tied) start; phase-subset filter (`Open >= T && Close > T`); reconciliation: sole starts of all members + shared-close starts = N deciding starts.
- [ ] B3.1.3 RED peak sweep: A 10-12, B 11-13, C 11:30-11:45 -> peak 3 with members A, B, C and the first instant; close at 12:00 and open at 12:00 not concurrent; zero-duration rows ignored.
- [ ] B3.1.4 GREEN create `API/Infrastructure/Services/FtmoGroupDiagnostics.cs` and the diagnostics DTOs; add `FtmoGroupDiagnosticsDto? Diagnostics { get; init; }` to the kind result as a NON-positional init property (no B2 construction site or assertion breaks); wire into `FtmoGroupComputation` for successful kinds only.
- [ ] B3.1.5 RED/GREEN: a successful kind carries diagnostics; refused kinds carry none; B2 one-member equivalence still green and unedited. Any new enum gets a zero-value test.
- [ ] B3.1.6 Falsification: process opens before closes at the same instant -> B3.1.3 red; credit only the first member of a shared close -> B3.1.2 red; restore.

### Phase B3.2 — Candidates read (design D8)
- [ ] B3.2.1 RED `TESTS/FtmoGroupCandidatesReadServiceTests.cs`: scoped to one account; per-kind run facts or absence; spec/calibration flags per run (`IsCalibrated` mirrors the null-`PointValue` rule); `NeedsFxBand` via `SettlesInAccountCurrency`; `ProfitCurrency`, `SourceTimeZoneId`; `NameExistsOnOtherAccount` case-insensitive; response carries `MaxMembers` and the account id; five fixed queries.
- [ ] B3.2.2 RED controller: `GET candidates` missing/empty `tradingAccountId` -> 400; unauthenticated -> 401.
- [ ] B3.2.3 GREEN create `IFtmoGroupCandidatesReadService.cs`, `FtmoGroupCandidatesDto.cs`, `FtmoGroupCandidatesReadService.cs`; modify `FtmoSimulationsController.cs` (+GET) and `DependencyInjection.cs`. `StrategiesController.cs` is not touched.
- [ ] B3.2.4 Falsification: drop the account filter; confirm B3.2.1 red; restore.
- [ ] B3.2.5 Gate: format, build, full suite once, pins, tripwire; `git diff --stat` shows no edit to `StrategiesController.cs` or engine files; delete `bin-scratch/`.

## F1 — Route, sidebar, shell (~150)

- [ ] F1.1 RED `WEB/features/simulator/simulator.routes.spec.ts`: `''` redirects to `ftmo`; `ftmo` lazy-loads the page; `app.routes.ts` has a TOP-LEVEL `simulator` path (not a child of the broker-account routes) inside the guarded layout. _[UI Req: Route]_
- [ ] F1.2 GREEN modify `WEB/app.routes.ts`; create `WEB/features/simulator/simulator.routes.ts` and `ftmo-group-simulation-page/*` (shell: title + always-visible short disclosure, OnPush, standalone).
- [ ] F1.3 RED sidebar spec (new `main-layout` spec; there is none today): Simulator group after FTMO, link to `/simulator/ftmo`, `simulatorExpanded` toggles, `toggleSidebar()` collapses it, labels render ES text. If the layout cannot be rendered cheaply, test the signals and assert the template keys instead, and note it.
- [ ] F1.4 GREEN modify `WEB/shared/layout/main-layout/main-layout.component.{ts,html}`; add `SIMULATOR.NAV.*` and shell keys to `public/assets/i18n/{en,es}.json`.
- [ ] F1.5 RED/GREEN `WEB/features/simulator/simulator.i18n.spec.ts`: EN/ES parity (keys and `{{placeholder}}` names) and banned-wording sweep over the `SIMULATOR` namespace (lowercased, diacritics stripped: passed, safe, survived, would have passed, aprobado, aprobo, seguro, sobrevivio, habria aprobado); shell renders with the REAL dictionaries (no `{{`, no raw key). Later slices only extend it.
- [ ] F1.6 RED/GREEN `WEB/features/simulator/simulator.theme.spec.ts`: no hex/rgb/hsl literal (fallbacks included) in the new components' CSS; every `var(--x)` is declared in `src/styles/_variables.scss`; the legacy undeclared names are absent.
- [ ] F1.7 Falsification: add a banned word to one ES key and a hardcoded `#fff` to the shell SCSS; confirm both specs red; restore.
- [ ] F1.8 Gate: prettier on touched files, `tsc --build`, full `ng test` once.

## F2 — Strategy picker (~350)

- [ ] F2.1 RED `WEB/core/services/ftmo-simulation.service.group.spec.ts` (new file; existing service spec untouched): `getGroupCandidates(accountId)` issues `GET api/ftmo-simulations/candidates?tradingAccountId=`; errors map through the shared `mapFtmoRequestError` (HttpTestingController only).
- [ ] F2.2 GREEN create `WEB/core/models/ftmo-group-simulation.model.ts` (candidate interfaces mirroring B3); add `getGroupCandidates` to `ftmo-simulation.service.ts` (additive).
- [ ] F2.3 RED `WEB/features/simulator/ftmo-group-simulation.mappers.spec.ts` (pure picker logic, created here and extended in F3/F4): symbol filter + name search keep prior selection; `canSelect` stops at the supplied `maxMembers` and frees a slot on deselect; absent flags map to an explicit absent state (never blank or `0`); candidates with a missing kind/spec/calibration stay selectable. _[UI Req: Picker, Flags, Cap]_
- [ ] F2.4 RED `ftmo-group-picker/*.spec.ts` with real dictionaries: flags rendered (Deploy/Eval presence with date range, spec, calibration, other-account name flag) in EN and ES; cap message translated; selection emits no run request. Pure logic carries the weight if AG Grid is awkward in jsdom (follow `portfolio-builder.component.ts:73`).
- [ ] F2.5 RED page spec: default account `SBDEMO2` else the first; only the selected account's rows; switching account reloads through `switchMap` and clears the selection.
- [ ] F2.6 GREEN create `ftmo-group-picker/*` (presentational, OnPush, `input()/output()`) and mappers; modify the page (account select, candidates load, selection signal) and i18n.
- [ ] F2.7 Extend `simulator.theme.spec.ts` and `simulator.i18n.spec.ts` to the picker. Falsification: remove the cap check and confirm F2.3 red; restore.
- [ ] F2.8 Gate: prettier, `tsc --build`, full `ng test` once.

## F3 — Form, run lifecycle, reused panels, refusals (~400, likely over; see flag)

### Phase F3.1 — Model, enum, service
- [ ] F3.1.1 RED `ftmo-group-simulation.model.spec.ts`: `FtmoGroupRefusal` explicit numbers matching the backend, `InvalidRequest === 0` asserted explicitly (hard rule 7), plus every non-zero member.
- [ ] F3.1.2 GREEN extend the model (request, envelope, kind result, member refusal incl. `reason`/`runReason`, `sourceTimeZoneId`, diagnostics wire types); add `simulateGroup(body)` to the service. RED/GREEN service spec: POST body shape (deduplicated ids, FX only when shown), error mapping, 400.

### Phase F3.2 — Form and readout
- [ ] F3.2.1 RED form spec: capital `10000`, risk empty, grid `2/0.01/0.01/10` from `IMOX_RETESTER_LOT_GRID` labelled as the backtest grid, broker `FTMO`; one risk field only; `canRun` false with 0 members or empty risk, true when complete, `sizeDecimals = 0` legal; FX inputs shown only when a selected candidate `needsFxBand`, hidden values sent as `null`, Run enabled without a band.
- [ ] F3.2.2 RED readout: 4 members, risk 25, capital 10000 -> 100 (1.00%) vs 1% and daily limit; risk 50 -> 1% marked exceeded (neutral wording), 5% not, Run enabled; daily reference follows echoed `DailyLossLimitPct`, 5% before the first run; after a run, observed `peak x risk` is shown **per successful kind, labelled with its kind** (choice: no cross-kind figure); recomputes with no request.
- [ ] F3.2.3 GREEN create `ftmo-group-form/*`; extend mappers. Placeholders asserted with real dictionaries (every key gets its params).

### Phase F3.3 — Page run lifecycle
- [ ] F3.3.1 RED page spec: editing sends nothing; second Run blocked in flight; destroy cancels; 400/network error shows a translated error, re-enables Run and renders no stale or partial result.
- [ ] F3.3.2 GREEN page: explicit run, `running()`, `takeUntilDestroyed`, `mapFtmoRequestError` (mirror `ftmo-simulation-modal.component.ts:131-167`).

### Phase F3.4 — Panels, refusals, window (checkpoint: size, see flag)
- [ ] F3.4.1 RED mapper/page specs: two labelled panels via the imported `FtmoRunPanelComponent` + `toRunPanelVm` (no edit to the modal); one hint before the first Run; a refused kind shows its refusal while the other renders; Deploy and Eval figures never mixed.
- [ ] F3.4.2 RED per-member reasons in the affected panel by strategy name, each reason with DISTINCT translated text (`RiskNotEstimable`, `PointValueNotCalibrated`, `InstrumentSpecMissing`, `FxRateNotDeclared`, `InvalidFxBand`, `MemberMissingKind`, `MemberHasNoTradesInWindow`); symbol-level appears in both panels; mixed-cause list shows each member's own reason; `NoCommonWindow` shows coverage with no blamed member.
- [ ] F3.4.3 RED group-wide refusal rendered ONCE above the panels (`InvalidRequest`, `SharedInputsRefused` with its `SharedRefusal`, `MemberNotFound`, `MixedSourceTimeZones` with each member's zone), no panel findings; uniqueness test: no two refusals render the same text; **value-0 `InvalidRequest` renders its label, not treated as absent**; unknown value renders UNKNOWN with the raw value and no `{{`.
- [ ] F3.4.4 RED window + per-member coverage per kind; shortened-window note when a member's range is wider; coverage on `NoCommonWindow`.
- [ ] F3.4.5 RED disclosures: group disclosure ONCE (3 points) in EN and ES, `DISCLOSURE_RESULT` + `NotModelled` once, server `Disclosures` never rendered, `DuplicateNameWarnings` name both members.
- [ ] F3.4.6 GREEN mappers (`labelFor`-style exact-value switch, `!== null`), page template, `SIMULATOR.FTMO_GROUP.*` keys EN/ES.

### Phase F3.5 — Gate
- [ ] F3.5.1 Real-dictionary sweep: render every component with fully populated data; assert no `{{` and no raw key in the text; extend parity/banned-wording/theme specs. Falsification: replace the exact-value switch with a truthy check and confirm the `InvalidRequest` test red; drop one placeholder param and confirm the leak test red; restore.
- [ ] F3.5.2 Prettier, `tsc --build`, full `ng test` once.

## F4 — Diagnostics panel (~250)

- [ ] F4.1 RED mappers spec: diagnostics VM, member ids resolved to names via `Members`; missing values map to explicit absent, never `0` (`null` vs `0` kept apart); shares computed from counts with a zero denominator rendering absent; tied (shared-close) starts labelled as tied.
- [ ] F4.2 RED `ftmo-group-diagnostics/*.spec.ts` with real dictionaries (EN and ES): three-member table (trades, scalable, net low/high, raised, capped, unscalable); attribution counts beside shares by phase plus sole-contributor and tied counts; peak 3 with members A, B, C named; no correlation text or key; no `{{`, no raw key.
- [ ] F4.3 GREEN create `ftmo-group-diagnostics/*` (presentational); extend mappers, page (one panel per successful kind) and i18n.
- [ ] F4.4 Extend parity, banned-wording and theme specs. Falsification: render a null value through `?? 0` and confirm the absent-value test red; restore.
- [ ] F4.5 Gate: prettier, `tsc --build`, full `ng test` once. User runs a SBDEMO2 group end-to-end at `/simulator/ftmo` in EN and ES.

---

## Spec-requirement to task map

Backend (16): Endpoint -> B2.3.3, B2.5.1-2 | Cap -> B2.1.2, B2.4.4, B2.5.1 | Dedup -> B2.3.2 | Merge -> B1.2.1-8 | Window -> B1.2.6, B2.2.4 | Kinds -> B2.2.1 | Group-wide -> B2.3.3 | Member-level -> B2.2.2-3 | FX -> B2.2.2, B2.2.6, B2.3.4 | Envelope -> B2.1.3, B2.2.5, B2.3.4 | One-member -> B2.3.1 | Diagnostics -> B3.1 | Disclosures -> B2.1.2, B2.3.3, B2.3.5 | Engine untouched -> B1.0.1, B1.1.6, B1.3.1, B2.6.1, B3.2.5 | Benchmark -> B2.4 | Candidates -> B3.2.

UI (15): Route -> F1.1-4 | Picker -> F2.3-6 | Flags -> F2.3-4 | Cap -> F2.3 | Form -> F3.2.1 | FX inputs -> F3.2.1 | Readout -> F3.2.2 | Run explicit -> F3.3 | Panels -> F3.4.1 | Refusals display -> F3.4.2-3 | Window/coverage -> F3.4.4 | Diagnostics panel -> F4 | Disclosures -> F1.2, F3.4.5 | i18n -> F1.5, F3.5.1, F4.4 | Enum -> F3.1.1, F3.4.3.

Gaps: `Start1DiffersFromSingleStartAnchor` relabelling for groups (design D4) has no spec requirement and would need an edit to the reused panel, so it is NOT tasked (reused panel text stays as is). The `TimeZoneDataUnavailable` handling was missing in the spec and is now added as group-wide (B2.3.3).

## Risks

- B2 over budget; F3 likely 500-650 lines. Cut points are named above.
- One zone per member is assumed. If a member's Deploy and Eval symbols resolve different zones, the mixed-zone check runs over all held runs and the member DTO shows the Deploy zone. Pin this in B2.3.3.
- Spec is silent on `Kinds` for group-wide refusals; chosen: empty list (the UI shows the refusal once and no panels).
- `MaxMembers` is unknown until B2.4.3; B3 candidates and the F2 cap read it, so B2 must be committed before B3.
- Shared AG Grid in jsdom may force logic-level tests in F2.
- `FtmoSimulationInputs.cs` is the single shared-file edit; B1.1.6 pin is the safety net.

## B2 review notes (2026-10-04, 4R: risk, resilience, readability, reliability — no BLOCKER/CRITICAL)

- [x] B2.7 Read-only SQX order check run on SBDEMO2 (user-authorised reads): 46 runs, 30,266 trades, 0 rows opening before the previous row, 0 disagreeing same-close pairs. The one-member equivalence holds exactly on real data.
- Info-level follow-ups (non-blocking):
  - RELIABILITY-001: a member with a held run but zero trades yields an unattributed `NoCommonWindow`; name the member (fold into B3).
  - RELIABILITY-002: `MixedSourceTimeZones` compares one kind's zone per member; harmless while Deploy/Eval share a symbol.
  - RELIABILITY-003: no worker-exception test for the `Parallel.For` unwrap path.
  - RELIABILITY-004: a mixed-cause kind-level refusal reads `MemberRunRefused`; per-member reasons are listed.
  - RESILIENCE-001: no `ILogger` on refusals or run duration.
  - RESILIENCE-002 / RISK: thread-pool use of `Parallel.For` per request; negligible for a single user.
  - READABILITY-001..003: the controller reads `MaxMembers` from Infrastructure; the validator duplicates `TryValidateFtmoBreachQuery`; a repeated per-member resolution expression.
