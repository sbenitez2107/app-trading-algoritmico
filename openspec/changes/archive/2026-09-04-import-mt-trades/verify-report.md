# Verify Report: import-mt-trades

**Verified**: 2026-09-04 (re-verification, independent, read-only, after correction commit `8b5f3ea`)
**HEAD at verification**: `8b5f3ea` ("fix(mt-import): correct the spec to the shipped parser and cover cascade delete")
**Prior pass**: FAIL -- 3 CRITICAL, 4 WARNING, 1 SUGGESTION (this same verifier)
**Verifier**: sdd-verify (independent, read-only except one temporary, fully-reverted revert experiment described below)

## Measured Evidence (run by me, not inferred)

- Backend build: dotnet build AppTradingAlgoritmico.slnx -warnaserror -> Build succeeded, 0 Warning(s), 0 Error(s).
- Backend tests: dotnet test AppTradingAlgoritmico.slnx -> Passed: 419, Failed: 0, Skipped: 0, Total: 419 (417 baseline + 2 new StrategyTradeCascadeDeleteTests). Matches the stated baseline.
- Frontend tests: pnpm run test --watch=false -> 30 test files, 371 tests, all passed. Matches the stated baseline.
- Revert experiment (see CRITICAL 3 below): temporarily flipped StrategyTradeConfiguration.cs line 61 to DeleteBehavior.Restrict, ran only StrategyTradeCascadeDeleteTests -> both tests FAILED with SQLite Error 19: FOREIGN KEY constraint failed. Reverted via git checkout, re-ran the full backend suite -> 419/419 green again. git status --porcelain is clean; all scratch build directories and logs created during this pass were deleted.
- No live database was touched at any point.

## Per-Finding Verdict (against the prior FAIL report)

### CRITICAL 1 -- Change never archived / spec never merged: still open by construction, not re-judged as FAIL
Confirmed still true: import-mt-trades remains under openspec/changes/, not openspec/changes/archive/. This is expected -- archiving is a separate later step, not part of this pass. See spec-merge sanity check below.

### CRITICAL 2 -- R7 spec factually wrong: CLOSED
Diff of 8b5f3ea on openspec/changes/import-mt-trades/specs/mt-trade-import.md changes the scenario "Unrecognised close reason suffix" to "... is preserved, not bucketed", asserting CloseReason="OTHER_VALUE" for a [other_value] suffix, with an explicit note recording the prior wording and citing 04cd462 as the deliberate parser change. Checked against the shipped code:

    MtStatementParserService.cs:235-241
    private static string? MapCloseReason(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix)) return null;
        return suffix.Trim().ToUpperInvariant();
    }

[other_value] -> trimmed, upper-cased -> "OTHER_VALUE". Spec and code now agree exactly. PASS.

### CRITICAL 3 -- Cascade delete untested: CLOSED, verified by direct experiment, not by trusting the commit message
StrategyTradeCascadeDeleteTests.cs adds two tests using a SQLite in-memory connection opened with Foreign Keys=True, and deletes via ExecuteSqlRaw("DELETE FROM Strategies WHERE Id = {0}", ...) rather than through the EF change tracker -- this is the right design, because a Remove()-based test would only prove EF's client-side entity-graph cascade, not that the database itself enforces it.

The commit message's own claim of "verified they catch a regression" was not accepted at face value; the revert was run independently:
- Flipped StrategyTradeConfiguration.cs line 61 from Cascade to Restrict.
- Ran StrategyTradeCascadeDeleteTests alone -> both FAILED (FOREIGN KEY constraint failed), confirming the tests are load-bearing against this exact configuration line.
- Reverted with git checkout, confirmed the line reads Cascade again, and re-ran the full 419-test suite -> green.

One caveat worth recording precisely, because it changes what the test proves: StrategyTrade.StrategyId is declared as a non-nullable Guid (StrategyTrade.cs line 7). For a required relationship, EF Core own convention default (with no OnDelete call at all) is already Cascade -- Restrict/SetNull are not valid defaults for a required FK, because leaving the FK non-null on the child after the parent disappears would violate NOT NULL. This means the explicit OnDelete(Cascade) on line 61 is not overriding an otherwise-different convention default; it is stating the convention default explicitly. That does not weaken the test: the experiment proves the configuration line is authoritative over the applied database behavior (flipping it to Restrict demonstrably changes runtime behavior), and it proves the database -- not EF's in-memory tracker -- is the one enforcing it. It does not, and cannot, prove that removing the line entirely would produce a different outcome, because removing it would leave the convention default in place, which is also Cascade for a required FK. This is a documentation nuance, not a defect: the explicit line makes intent visible in code review and is not redundant in any way that matters for correctness. PASS, with this nuance recorded for accuracy.

### WARNING (R8 no fuzzy name matching) -- reasoning independently re-derived, not deferred: AGREE, closed
Read TradeImportService.cs lines 26-96 directly. Attribution (step 3-4) is strategiesByMagic built from strategies with MagicNumber != null keyed by MagicNumber value, looked up via TryGetValue on the trade's MagicNumber -- an exact integer-keyed dictionary lookup, nothing approximate. Auto-assign (step 4.5, lines 58-93) runs only over the leftover orphan buckets that attribution could not place, and its match filters candidates whose MagicNumber is null AND whose trimmed Name equals the hint case-insensitively, then only commits when exactly one candidate exists.

This is exact string equality after trim/ordinal-ignore-case normalization -- not a similarity/fuzzy match (no edit distance, no substring, no tokenization). It also never re-points an already-magicked strategy and only commits on an unambiguous single match. This matches R8b as written in the corrected spec and matches the three new scenarios (single-candidate adopted, ambiguous hint stays orphan, already-set MagicNumber never re-pointed) exactly. R8 is not contradicted -- it was never about recovery of unattributable trades, only about the attribution step itself, which remains an exact integer match. CLOSED.

### WARNING (R11 DTO shape): CLOSED
Spec diff adds autoAssigned and availableStrategies to the documented TradeImportResultDto contract, matching the AutoAssignedStrategyDto fields populated in TradeImportService.cs lines 86-90. Additive, non-breaking, now documented. PASS.

## Spec-Merge Sanity Check (read-only -- no files touched)

Read all three documents in full:
- openspec/specs/strategy-model.md (flat) -- BatchStageId nullability, TradingAccountId FK, SetNull on BatchStage delete, orphan-state prevention. Four requirements, none named MagicNumber or R-M.
- openspec/specs/strategy-model/spec.md (directory) -- backtest-run strategy-scoping and cascade, plus a rollback-migration requirement. Two requirements with PINNED BY test references. No MagicNumber or R-M content.
- openspec/changes/import-mt-trades/specs/strategy-model.md -- R-M1 (nullable MagicNumber plus filtered unique index), R-M2 (AddStrategyModal input), R-M3 (StrategyDto field).

There is zero requirement-name or scenario-name overlap across the three documents -- three genuinely disjoint topics happen to share the capability name strategy-model, which is pre-existing debt from two unrelated commits, neither of which is part of this change. Merging R-M1/R-M2/R-M3 into the directory form as additional ADDED Requirements entries would not overwrite, shadow, or contradict anything currently in that file -- it only adds new, independently-testable requirement blocks. Promoting mt-trade-import as a brand-new capability at openspec/specs/mt-trade-import/spec.md is uncontested since no file exists at that path today. Leaving the flat strategy-model.md untouched defers, but does not worsen, the pre-existing duplicate-name debt -- that debt was created by unrelated changes and reconciling the flat vs directory split is not this change's responsibility.

Verdict: the plan is sound and loses nothing. The one residual risk it deliberately punts on -- two files answering to the same capability name -- already existed before this change and is not created or enlarged by executing this plan.

## Requirement-by-Requirement Verdict -- mt-trade-import.md

| Req | Verdict | Note |
|---|---|---|
| R1-R6, R9, R10, R12-R14 | PASS | Unchanged since prior pass; re-confirmed passing under the current 419/419 backend + 371/371 frontend run. |
| R7 | PASS | Corrected; see CRITICAL 2 above. |
| R8 | PASS | Exact match confirmed at TradeImportService.cs lines 33-56. |
| R8b (new) | PASS | Three scenarios each map to a distinct code path at TradeImportService.cs lines 58-93; behavior directly read, not inferred from test names alone. |
| R11 | PASS | DTO shape now documents autoAssigned and availableStrategies. |
| Edge: Cascade delete | PASS | Closed via StrategyTradeCascadeDeleteTests plus independent revert experiment above. |
| Edge: 404 / 400 / zero-trades | PASS | Unchanged, previously verified. |

## Requirement-by-Requirement Verdict -- strategy-model.md (delta, this change's own MagicNumber requirements)

| Req | Verdict | Note |
|---|---|---|
| R-M1 | Configuration matches spec exactly (StrategyConfiguration.cs, filtered unique index). Runtime enforcement against a real relational engine remains unverifiable in this pass -- see below. |
| R-M2 | PASS | add-strategy-modal.component.spec.ts. |
| R-M3 | PASS | StrategyDto plus controller tests. |

## Unverifiable (explicitly, not pass/fail)

- R-M1's filtered unique index enforcement on (TradingAccountId, MagicNumber) under a real SQL Server engine -- the EF in-memory/SQLite test providers used elsewhere in the suite do not exercise SQL Server's own filtered-index semantics, and connecting to a live database is prohibited. This was true in the prior pass and remains true; nothing in 8b5f3ea changes it.
- Manual/visual confirmation of the rendered ImportTradesModal/StrategyTradesGrid UI beyond the passing Vitest component specs.

## Remaining Findings

None CRITICAL. No new findings introduced by 8b5f3ea.

### SUGGESTION (carried over, not a blocker)
- When this change is archived, execute the spec-merge plan verified above (merge R-M1/R-M2/R-M3 into openspec/specs/strategy-model/spec.md, promote mt-trade-import as a new capability, leave the flat strategy-model.md as pre-existing debt) rather than a blind directory overwrite.

## Overall Verdict: PASS

All three CRITICAL findings from the prior pass are closed, verified independently rather than taken on the correction commit's own word: the spec now matches the shipped MapCloseReason, and the cascade-delete behavior is proven by a test that goes red when the production configuration line is reverted to Restrict and green again once restored -- an experiment run directly rather than accepting the commit message's claim. The one WARNING previously raised as an R8 contradiction does not hold up under direct code reading and is properly resolved by the new R8b requirement, not by a documentation trick. R11 documentation gap is closed. The only remaining CRITICAL-shaped item (never archived, spec never merged) is open by construction -- this pass is not the archive step -- and the archive plan it depends on has been independently sanity-checked here and found sound with no data loss or corruption risk beyond pre-existing, unrelated debt. R-M1's live-database enforcement remains explicitly unverifiable rather than assumed, per the no-DB constraint.

Recommendation: proceed to archive, executing the spec-merge plan described above.
