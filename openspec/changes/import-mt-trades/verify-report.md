# Verify Report: import-mt-trades

**Verified**: 2026-09-04 (first verification, no prior report existed)
**HEAD at verification**: fdcb962 (feature originally shipped at f2e647b; three unrelated commits landed on top: 308edf2, 04cd462, 04da10a)
**Verifier**: sdd-verify (independent, read-only)

## Measured Evidence (run by me, not inferred)

- Backend build: `dotnet build AppTradingAlgoritmico.slnx -warnaserror` -> Build succeeded, 0 Warning(s), 0 Error(s). Matches stated baseline.
- Backend tests: `dotnet test AppTradingAlgoritmico.slnx --no-build` -> Passed: 417, Failed: 0, Skipped: 0, Total: 417. Matches stated baseline exactly.
- Frontend tests: `pnpm run test --watch=false` (Vitest via ng test) -> 30 test files, 371 tests, all passed.
- No database was touched (in-memory EF provider only in all backend tests; SQL Server behaviors such as cascade-delete FK enforcement and the filtered unique index are not exercised at runtime by any test -- see finding below).

## Task Completion

`tasks.md`: 61/61 checked (57 base + a documented post-hoc "backend gap" fix). All code referenced by the tasks exists on disk and compiles. No unchecked tasks -- full verification proceeds.

## Sharpest Finding -- Spec Was Never Merged / Change Was Never Archived

`import-mt-trades` is still sitting in `openspec/changes/import-mt-trades/` -- it is not in `openspec/changes/archive/`. Its capability spec `mt-trade-import.md` does not exist anywhere under `openspec/specs/` (flat or directory form) -- it only exists inside this unarchived change folder.

Regarding the `strategy-model` collision:
- `openspec/specs/strategy-model.md` (flat) was authored by commit `d875c4e` ("feat: SQX HTML parser, Darwinex demo strategies...") -- not by this change.
- `openspec/specs/strategy-model/spec.md` (directory) was authored by the later `sqx-backtest-import` change's own archival (`ffdbaff`).
- Neither file contains any MagicNumber / R-M1 / R-M2 / R-M3 content. Both describe an unrelated topic: `BatchStageId` nullability and the `TradingAccount` FK on `Strategy`.

Conclusion: this is not the same spec promoted twice -- it is two different changes reusing the capability name `strategy-model` for two unrelated deltas, and neither ever incorporated `import-mt-trades`'s own `strategy-model.md` delta (the MagicNumber field). The R-M1/R-M2/R-M3 requirements are fully implemented in code (verified below) but undocumented in any canonical spec -- they exist only in this never-archived change. If `import-mt-trades` is archived as-is with the standard merge step, it will try to merge into `strategy-model.md`, which already diverged for unrelated reasons -- this needs a manual, careful merge, not a blind overwrite.

## Requirement-by-Requirement Verdict -- mt-trade-import.md

| Req | Description | Backing test | Verdict |
|---|---|---|---|
| R1 | POST upload endpoint, 200/404/400 | TradingAccountsControllerImportTests (3 tests) | PASS |
| R2 | Closed Transactions parsing, 14 cols | MtStatementParserServiceTests (normal row, TP, no-bracket) | PASS |
| R3 | Open Trades parsing | MtStatementParserServiceTests (open trade row test) | PASS |
| R4 | Cancelled rows skipped | MtStatementParserServiceTests (R4 cancelled-row test) | PASS |
| R5 | Working Orders section skipped | MtStatementParserServiceTests (R5 test) | PASS |
| R6 | Title regex + malformed title skip | MtStatementParserServiceTests (R6 malformed-title test) | PASS |
| R7 | CloseReason mapping sl->SL, tp->TP, absent->null, else->Other | MtStatementParserServiceTests (TS-suffix test) | FAIL -- spec contradicted by shipped code. MtStatementParserService.MapCloseReason was rewritten (commit 04cd462) to return the raw uppercased suffix for everything else (e.g. "TS", "MO", "SO"), not "Other". The code comment states explicitly that the previous implementation collapsed everything outside SL/TP into "Other" and lost trailing-stop information. The test suite was updated to match the new behavior and now asserts CloseReason == "TS" for a trailing-stop suffix -- it no longer asserts "Other" for unrecognised suffixes anywhere. mt-trade-import.md still documents the old mapping table and the "Unrecognised close reason suffix -> Other" scenario (R2/R7), which is no longer true of the shipped system. |
| R8 | Attribution by exact (TradingAccountId, MagicNumber), "no fuzzy name matching" | TradeImportServiceTests.ImportAsync_UnknownMagic_ProducesOrphanEntry (exact-match path) | PARTIAL / spec drift. Exact-match attribution is still correct and tested. But commit 308edf2 added a name-based auto-assign step (ImportAsync_HintMatchesSingleStrategyWithoutMagic_AutoAssignsAndImportsTrades and 3 related tests) that resolves orphans by matching StrategyNameHint against Strategy.Name and mutates Strategy.MagicNumber as a side effect of import. This is fuzzy name matching in the exact place the spec says "no fuzzy name matching." The behavior is well-tested, but it is undocumented anywhere in any spec -- a genuine capability was shipped with zero spec coverage. |
| R9 | Idempotent upsert on (StrategyId, Ticket) | ImportAsync_FirstImport_InsertsAllMatchedTrades, ImportAsync_ReImport_UpdatesExistingRows | PASS |
| R10 | One AccountEquitySnapshot per upload, ReportTime parse | ImportAsync_AlwaysWritesOneSnapshot (asserts 2 calls -> 2 rows), parser Summary test | PASS |
| R11 | Response DTO shape {imported, updated, skipped, orphans, snapshot} | n/a -- shape check | WARNING -- DTO grew. TradeImportResultDto now also carries AutoAssigned and AvailableStrategies (added in 308edf2). Additive, non-breaking, but the spec's documented contract is incomplete versus the shipped contract. |
| R12 | GET trades, status filter + ordering | GetByStrategyAsync_OpenFilter_..., _ClosedFilter_..., _AllFilter_OpenTradesAppearBeforeClosed | PASS (ordering now groups open-before-closed then CloseTime DESC, OpenTime DESC -- slightly more specific than the spec's "CloseTime DESC" text but consistent with intent; not a break) |
| R13 | ImportTradesModal frontend | import-trades-modal.component.spec.ts | PASS (component now lives under features/broker-accounts/..., not features/darwinex/... per tasks.md -- path drift only, functionally intact) |
| R14 | StrategyTradesGrid frontend, 14 columns, open-trade styling | strategy-trades-grid.component.spec.ts | PASS, but column set has grown to 16 (added Net Profit, Status) via shared buildTradeColumnDefs in trades-grid-shared.ts (commit 04cd462). Additive, not a break. |
| Edge: Cascade delete on Strategy removes its trades | -- | CRITICAL -- UNTESTED. No test in the suite deletes a Strategy with associated StrategyTrade rows and asserts cascade. EF configuration (StrategyTradeConfiguration) does set OnDelete(DeleteBehavior.Cascade), but per verification rules a spec scenario is compliant only when a covering test passed at runtime -- none exists. This also cannot be verified against real SQL Server without a live database, which I am prohibited from touching. Unverifiable at runtime in this pass; flagged as untested, not passing. |
| Edge: Upload to non-existent account -> 404, no data persisted | TradingAccountsControllerImportTests (404 test) + TradeImportServiceTests.ImportAsync_NonExistentAccount_ThrowsKeyNotFoundException | PASS |
| Edge: Malformed HTML -> 400 | TradingAccountsControllerImportTests (400 test) + ImportAsync_ParserReturnsNull_ThrowsArgumentException | PASS |
| Edge: Zero trades + Summary present | covered implicitly by ImportAsync_AlwaysWritesOneSnapshot (uses a statement with no trades) | PASS |

## Requirement-by-Requirement Verdict -- strategy-model.md (delta)

| Req | Description | Backing test | Verdict |
|---|---|---|---|
| R-M1 | Strategy.MagicNumber nullable, filtered unique index on (TradingAccountId, MagicNumber) | StrategyConfiguration.cs lines 82-87 -- HasFilter("[TradingAccountId] IS NOT NULL AND [MagicNumber] IS NOT NULL"), IsUnique() -- configuration matches spec exactly | Configuration matches. The unique-constraint-violation and cross-account-no-conflict scenarios are not exercised by any test using a real relational provider (EF in-memory provider does not enforce SQL unique/filtered indexes). Unverifiable at runtime without a live database, which I am prohibited from touching. Flag as unverifiable, not pass/fail. |
| R-M2 | AddStrategyModal optional numeric input, empty->null, non-numeric blocks | add-strategy-modal.component.spec.ts (3 magicNumber tests per task 6.4) | PASS |
| R-M3 | StrategyDto.magicNumber: int or null | StrategyDto.cs has MagicNumber property (confirmed in code); controller tests pass magicNumber through (PostStrategy_WithMagicNumber_PassesMagicNumberToService) | PASS |

## Design Coherence

design.md's architecture (parser -> service -> controller, single-transaction upsert, snapshot-per-call) matches the shipped code. The one material deviation -- auto-assign-by-name in TradeImportService -- is not in design.md either; it was introduced by unrelated later work outside the SDD pipeline for this change.

## Issues

### CRITICAL

1. Change never archived, spec never merged. `import-mt-trades` remains an open change; the mt-trade-import capability spec exists nowhere in openspec/specs/. Archiving now requires a deliberate, manual reconciliation of the strategy-model delta against the two already-diverged strategy-model specs (flat + directory) that belong to different, later changes.
2. R7 (CloseReason mapping) is factually wrong for the shipped system. The spec documents sl->SL, tp->TP, absent->null, else->Other; the code (since 04cd462) returns the raw uppercased suffix for everything else, and tests were updated to match the new behavior, confirming this is not an isolated bug but an intentional, undocumented spec change.
3. Cascade-delete edge case has zero runtime test coverage. No test deletes a Strategy with trades and asserts the StrategyTrade rows are removed. This is a documented spec scenario with no passing covering test -- must be reported as untested rather than accepted on the strength of the checked task or the EF fluent config alone.

### WARNING

4. R8 attribution rule ("no fuzzy name matching") is contradicted by later auto-assign-by-name code (308edf2), which is well tested but undocumented in any spec. Not a regression introduced by this change, but it means the spec this verification is asked to certify no longer accurately describes strategy attribution end-to-end.
5. R11 response DTO has grown (AutoAssigned, AvailableStrategies fields added) beyond what the spec documents -- additive/non-breaking but the spec is incomplete.
6. Frontend component paths moved from features/darwinex/... (per tasks.md) to features/broker-accounts/... -- cosmetic drift only, not a defect.
7. R-M1's unique-index enforcement and R14's exact column list are only verifiable against a real SQL Server instance / rendered grid respectively; both are out of reach for this read-only, no-DB verification pass.

### SUGGESTION

8. Consider archiving import-mt-trades together with an explicit spec-reconciliation step that documents the R7 mapping change and the auto-assign feature (retroactively, as its own change or as an addendum), rather than silently merging a spec that no longer matches the code.

## Unverifiable (explicitly, not pass/fail)

- SQL Server enforcement of the filtered unique index on (TradingAccountId, MagicNumber) -- requires a live database; prohibited from connecting.
- Cascade-delete behavior against a real relational engine -- requires a live database; prohibited from connecting.
- Any manual/visual confirmation of the rendered ImportTradesModal/StrategyTradesGrid UI -- not run in this pass (Vitest component tests were run and passed, which is the available substitute).

## Overall Verdict: FAIL

Rationale: task completion and the large majority of requirements are solidly backed by passing tests (417/417 backend, 371/371 frontend, clean warn-as-error build). However, this is the first-ever verification of a change that was fully implemented months ago and left unarchived, and independent inspection found the spec itself is stale in a substantive, behavior-changing way (R7), one documented edge-case scenario has no covering test (cascade delete), and the change's own capability spec was never merged anywhere -- so "does the spec still describe the shipped system" is answered no on at least one hard requirement (R7) plus one untested scenario (cascade delete). Per the decision gate (spec scenario has no passing covering test -> CRITICAL), this cannot be a clean PASS or PASS WITH WARNINGS.

Recommendation: do not archive as-is. Either (a) update mt-trade-import.md R7 to match shipped behavior and add a cascade-delete test before archiving, or (b) archive with an explicit addendum documenting the R7 change and the untested cascade-delete gap, and open a follow-up task for the missing test.
