# Verify Report: ftmo-group-search

Mode: hybrid (openspec + Engram). Strict TDD active. HEAD f0adffa.
Verdict: PASS WITH WARNINGS. CRITICAL 0, WARNING 8, SUGGESTION 3. Ready to archive: yes (no CRITICAL); artifact housekeeping recommended first.

## Evidence
- Focused backend run (this verify): dotnet test --filter FtmoGroupSearch, FtmoRaceSurrogate, FtmoLimitHeadroom, FtmoGroupSimulation, FtmoGroupResponseSnapshot, with -p:BaseOutputPath=bin-scratch/: exit 0, 370 passed, 0 failed, 4 skipped (3 benchmarks + calibration fact). bin-scratch deleted. Includes the tripwire (SHA pins), snapshot pin, interleave, limits, registry, worker, controller tests.
- Full suites from the commit gates (not re-run): backend 1613 passed / 12 skipped; frontend 1137 passed.
- Engine files: git diff 6ad91c0..HEAD shows NO change to FtmoBreachEvaluator, FtmoGroupComputation, FtmoGroupDiagnostics, FtmoGroupMerger, FtmoMultiStartReadService, FtmoSimulationsController. Only shipped backend files modified: FtmoGroupSimulationReadService.cs (D1 extraction) and DependencyInjection.cs (registrations), as designed. No existing backend test file modified. SHA-pin tripwire green.

## Completeness
Unchecked tasks: 1d.3.2, 1d.3.4, 1d.3.5, 2b.4, 5.1.
- 1d.3.2 (ask DB authorization): STALE checkbox. 1d.3.3 records a real SBDEMO2 calibration run, so authorization was obtained. WARNING (housekeeping).
- 1d.3.4 (1d gate): STALE checkbox. Its note says it was left open only until 1d.3.3; later gates ran with the full suite green. WARNING (housekeeping).
- 1d.3.5 (measure real per-group time, parallel runner): WARNING, NOT CRITICAL. Partially done: real Debug sequential runs are recorded in D5 (about 18 s and about 28 s per group) and the budget was rebalanced from them (1d.3.6, done). The parallel measurement belongs to the separately planned performance change (Release build, parallel full simulations). No spec scenario depends on the parallel number. Mark it partial and deferred.
- 2b.4 (optional runtime check): effectively satisfied by the real user runs. Optional.
- 5.1 (risk grid): optional, out of scope. Not counted.
All other tasks (including section 6, request in the job DTO) are checked and match the code.

## Spec compliance (backend 14 requirements, UI 11, delta 2)
All scenarios have passing covering tests except the items below.
- Endpoint contract, eligibility, per-instrument cap (default 2), identical Deploy/Eval, 1% rule, proxy on own window, determinism and ranking, shortlist-only full computation (parity vs SimulateAsync), lifecycle (409, cancel, terminal restart, 404), budget (incl. interleave), composition without change, disclosures, benchmark: COMPLIANT.
- Calibration: COMPLIANT for the recorded run. The run itself is a manual DB fact, skipped in CI by design; guard and recall logic are unit-tested.
- Limit headroom, scenario worse FX end used and reported: PARTIAL (W1).
- UI: route/sidebar, progress/cancel/409/404/leave/return, table + ceiling, fractions once, scatter/frontier, deep link from job.request, i18n/banned words, enum exact-value: COMPLIANT (frontend 1137 green at gate). Gaps: W2, W3.
- Delta group page deep link + picker default override: COMPLIANT.

## Calibration record (D9)
Recorded in design D9, spec, test doc comment (FtmoGroupSearchCalibrationTests.cs:21-23) and tasks 1d.3.3: SBDEMO2, 18 eligible, 4029 enumerated, 1719 survivors, ground truth of all 1719 groups, recall@10 = recall@25 = 1.000 for k=2,3,4, DEPTH d100 = K. Meets the 0.9 threshold and the spec top-10 criterion. Re-run at shortlist 75 (calib-5.log): same figures (W6 resolved).

## Design coherence
Matches: race surrogate (FtmoRaceSurrogate used in the engine ProxyOutcome), 75 shortlist interleaved by size (InterleaveBySize applied in Plan), 30 min budget, ceilings 500/3600, MaxPoolSize 24, request carried in the job DTO (FtmoGroupSearchJobDto.Request, set by the registry), sequential simulation, single-job registry, Unknown=0 enums.
Deviations (documented in tasks, design text stale): W5.

## Issues

### CRITICAL
None.

### WARNING
Resolution update (post-verify): W1, W2, W3, W4, W5, W6 and W8 RESOLVED. W7 remains OPEN.
- W1: spec amended; the worse FX end is used but not reported per kind (decision, tasks.md 2a.1.2).
- W2: UI spec amended; the symbol filter was omitted because the backend request has no symbols field.
- W3: the search page now renders the group simulator disclosures (concurrent breaches, same-close ordering, eligibility) with the existing SIMULATOR.FTMO_GROUP.RESULT.DISCLOSURE keys, once a job/result is shown. Spec: ftmo-group-search-page.disclosures.spec.ts.
- W4: tasks.md updated (1d.3.2, 1d.3.4, 2b.4 done; 1d.3.5 partial and deferred; 5.1 out of scope).
- W5: design.md updated (D3 score, budget check, D9 open item, Interfaces, constants).
- W8: design.md corrected to record the two edited web specs (assertions unchanged).
- W6: calibration re-run at the production shortlist 75 (quota 25 per size) against the cached full truth (`TRUTH source=file`, truth-full.json, 1719 entries, k4 stride 1), calib-5.log 2026-10-06: recall@10 and recall@25 = 1.000 for k=2, 3 and 4; DEPTH d100 = K; 45 s. Recall at 75 is now measured, not inferred. Recorded in design D9 and Open Questions.
- W7: OPEN, noted in design Open Questions; deferred to the separate performance change.

| ID | Location | Issue |
|----|----------|-------|
| W1 | specs/ftmo-group-search/spec.md (Limit Headroom: FX end MUST be reported); api/src/AppTradingAlgoritmico.Application/DTOs/Backtests/FtmoGroupSearchDto.cs:101 | Per-kind headroom DTO has no FX end used. Worse end is used but not reported. Deviation noted at tasks.md:228, spec never amended. Amend spec or add the field. |
| W2 | specs/ftmo-group-search-ui/spec.md:38; web/src/app/features/simulator/ftmo-search-form/ftmo-search-form.component.ts:54-58 | No optional symbol filter in the form (no backend field either). Spec MUST not met, not amended. |
| W3 | specs/ftmo-group-search-ui/spec.md:225-231; ftmo-search-progress.component.html:92-100; en.json FTMO_SEARCH keys | Disclosures show N, selection bias, elimination risk, restart loss, but not the group simulator disclosures (concurrent breaches, same-close ordering, eligibility rules not modelled). Server disclosures are intentionally not rendered, so these appear nowhere on the page. |
| W4 | tasks.md:216, 218, 220, 249 | Stale/partial task state (see Completeness). |
| W5 | design.md:83-84 (D3 proxy score, now the race surrogate); :172 (budget every 256 proxies, not implemented, see tasks.md:170); :220 (D9 open item, 9.4 s x 150); :279-287 (Interfaces differ from built DTOs); :315 (constants 150/15 min vs 75/30 min) | Design text stale vs the built system. No spec break. |
| W6 | api/src/AppTradingAlgoritmico.Infrastructure/Services/FtmoGroupSearchLimits.cs:14-20; tasks.md:219 | Calibration recorded with shortlist 150; production is 75 (quota 25 per size). Recall@25 at 75 is inferred from DEPTH d100 = 25 (zero margin) on one pool (18 eligible, 2 instruments, risk 50, one FX band), not re-run at 75. |
| W7 | spec.md:415-418 (budget follows measurement); FtmoGroupSearchLimits.cs:24-25 | Default budget fits only in Release (75 x 3.5 s = 262 s). In Debug a real run stopped at 66/75 (about 28 s per group). The 30 min comment cites 18 s, contradicted by the second run. Known; performance change planned. |
| W8 | web simulator.result.i18n.spec.ts, simulator.routes.spec.ts (commit f0adffa); design.md:196 | Two pre-existing specs edited in the stabilization commit. Assertions unchanged (behaviour-preserving) but contradicts the design claim of no edited existing tests. |

### SUGGESTION
- S1: RESILIENCE-004 (pool assumed single source zone; runner uses first resolved zone) remains unfixed; every spec is Asia/Jerusalem today.
- S2: Pool-size gate margin at P=24 is thin (single sample 60.9 s). Use a cheaper proxy or pre-filter before raising MaxPoolSize.
- S3: Re-populating the search form on re-attach was skipped (tasks 6.2).

## Next
Archive-ready (no CRITICAL). Before archive, update tasks.md checkboxes and the stale design/spec text (W1-W5), or amend specs for W1-W3 explicitly.
