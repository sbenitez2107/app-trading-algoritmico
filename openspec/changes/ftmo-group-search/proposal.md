# Proposal: FTMO group search (automatic 2..4 strategy combinations)

> Roadmap step 2 of 4 (`SIMULATOR_ROADMAP.md:236-238`). Exploration: Engram `sdd/ftmo-group-search/explore` (#2990).
> **`FtmoBreachEvaluator.cs`, `ComputeRun`, `FtmoGroupComputation.cs`, `FtmoMultiStartReadService.cs` and the
> tripwire's 18 slice files are never edited.** Shipped files touched (corrected 2026-10-04; the earlier note
> said "only"): `FtmoGroupSimulationReadService.cs` (member resolution extracted, behaviour-preserving,
> existing tests unedited), `DependencyInjection.cs` (new registrations), `simulator.routes.ts` (search route),
> `main-layout.component.html` (sidebar link), `en.json` / `es.json` (i18n keys) and the group-simulator page
> (deep-link params).

## Intent

The group simulator (step 1) answers "does THIS group pass FTMO?" for a hand-picked group. The user's real
question is "which 2..4 strategies of my pool pass with the lowest elimination risk, and how fast?".
Hand-picking cannot cover 12,926 combinations at P≈24, and nothing ranks them.

## Scope

### In Scope (PR slices)
| PR | Content | Rough size |
|---|---|---|
| 1 | Extract member resolution; pure funnel generator (eligibility, per-instrument cap, cached projections, proxy, shortlist, `ComputeGroup`, ranking); limit-headroom metric; identical Deploy/Eval detection; benchmark + proxy calibration; new tripwire tests | ~550 |
| 2 | Hosted service + in-memory job registry; `POST/GET/DELETE` search endpoints, DTOs, budget, cancellation, disclosures | ~450 |
| 3 | `/simulator/ftmo/search` page, sidebar link, form, progress/cancel, ranked table, EN/ES | ~500 |
| 4 | Scatter chart, frontier highlight, deep link into the group simulator (+ query-param support there) | ~350 |
| 5 (optional, later) | Re-run top N on a 50/75/100 risk grid | ~300 |

`400-line budget risk: High` -> chained PRs; `sdd-tasks` may split PR1 (resolution+generator / headroom+benchmark) and PR3.

### Out of Scope
- Risk grid (optional PR5); deterministic null model (later).
- Persistence, job history, "save as portfolio"; live data; Darwinex Zero / Axi.
- **Group-simulator charts** (equity curve with objective lines and toggle, daily P&L bars + calendar, headroom panel in the group simulator): the NEXT, already-agreed separate change `ftmo-group-simulator-charts`.

## Capabilities

### New Capabilities
- `ftmo-group-search`: funnel, eligibility, caps, proxy, shortlist, ranking, headroom, budget, job lifecycle, endpoints, disclosures.
- `ftmo-group-search-ui`: search page, form, progress/cancel, table, scatter, deep link, i18n.

Split, not merged: same precedent as `ftmo-group-simulation` / `-ui`; backend and UI verify and archive independently.

### Modified Capabilities
- `ftmo-group-simulation-ui`: the group simulator accepts deep-link query params preselecting members and risk.

## Decisions

1. **Funnel, new capability.** Composes pure `ComputeGroup` per candidate; never loops inside the one-group capability. Stages: (1) eligibility (held Deploy+Eval, spec, calibration, same zone, FX band); (2) max-per-instrument cap; (3) cheap proxy on cached per-member projections: worst summed day vs daily limit and peak concurrency, **computed on the candidate's actual common window** (window-shrink gotcha); (4) deterministic shortlist of bounded size; (5) full `ComputeGroup` on the shortlist only; (6) deterministic ranking. No RNG; enumerate in `StrategyId` order. Deviates from roadmap layer 6 ("exhaustive whenever k ≤ 4") because full simulation is 0.5–3.8 s per group.
2. **Group size** 2..4, bounded by `MaxMembers`.
3. **Ranking, lexicographic:** (1) Deploy and Eval both evaluated, no refusal; (2) lowest breach share (P1+P2+funded), worse of Deploy/Eval; (3) highest limit headroom (D9); (4) highest funded no-breach share (right-censored; the word "survival" is banned in every artifact and text); (5) lowest median days to both targets; (6) fewer members, lower peak concurrency, then `StrategyId`s. Visible elimination ceiling (default < 5%) as filter/highlight; both kinds' metrics shown.
4. **Per instrument:** at most 2 by default, user-adjustable (minimum 1). USER DECISION 2026-10-04, changed from 1: the first real calibration (SBDEMO2: 130 strategies, 18 eligible, 4029 enumerated, 72 survivors, all pairs, shortlist 72) covered only 2 instruments (gold, NQ) under a cap of 1, so its recall of 1.0 was vacuous.
5. **Identical Deploy/Eval** (equal projected series): excluded by default; opt-in includes and flags them.
6. **Risk:** one user-chosen risk per search. Risk grid = optional PR5.
7. **Academy 1% rule:** optional constraint, off by default.
8. **Execution:** in-memory background job (hosted service + registry). `POST` starts, `GET` polls progress/results, `DELETE` cancels. Budget = max full simulations + wall-clock, with visible "stopped at budget". Cancellation token through `ComputeGroup`. Per-job projection cache discarded afterwards. No persistence; jobs lost on API restart (disclosed). One job at a time; second-start behaviour decided in design (proposed: 409 with the running job id).
9. **Limit headroom** (MetriX-inspired), per kind, computed outside the evaluator: (a) worst daily loss as % of the daily allowance (5% of initial capital), the daily loss measured from the previous CE(S)T-midnight balance as `FtmoBreachEvaluator` does; (b) worst drawdown from initial capital as % of the max-loss allowance (10%). Design fixes exact definitions, including worst vs median across starts for (b). Shown in results; tie-breaker in D3. Fixed (2026-10-04): headroom = 1 − max(daily used, worst drawdown used), taken on the worse of the two kinds; all shares and headroom values travel as fractions on the wire and are converted to percent once, at display.
10. **UI:** `/simulator/ftmo/search`, sidebar link under Simulator. Form: pool filter (account, symbols), k range, risk, max per instrument, exclude identical, 1% rule, budget. Progress bar, cancel. Ranked table per kind: breach share, headroom (worst day %, worst drawdown %), funded no-breach share, median days to both targets, peak concurrency, members/symbols. Scatter x = median days to both targets, y = breach share, frontier highlighted; clicking a point/row deep-links to the group simulator. The deep link carries `account`, `members`, `risk`, `capital`, `fxLow` and `fxHigh` (broker and lot grid are the shared constants, not link parameters). Leaving the page does NOT cancel the job (user decision 2026-10-04): it only stops polling, `GET current` re-attaches on return, and explicit cancel stays on the Cancel button. `lightweight-charts` if it fits a scatter, else SVG/CSS; no new chart library without user decision. EN/ES everywhere.
11. **Disclosures** (mandatory, translated): "N groups examined; choosing the best of many backtests is optimistic (selection bias)"; elimination risk on closed-trade daily aggregates, not certification; plus existing group disclosures.
12. **Out of scope** as listed above.

### Design decisions left open
- Whether `ComputeGroup` gains a diagnostics opt-out (wasted per candidate).
- Shortlist size M and default budget, set from the PR1 benchmark.

## Facts

- Full group: ~0.5–3.8 s. C(24,2..4) = 12,926 (1.8–13.6 h); C(120,2..4) ≈ 8.5M. Exhaustive full search is infeasible; shortlist is bounded.
- `ComputeRun` already saturates cores; outer parallelism adds little.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Selection bias read as forecast | High | D11 disclosure with N; null model later |
| Proxy over-prunes good groups | Med | Calibrate vs full simulation at P≈24 before trusting |
| Window shrink distorts proxy | Med | Proxy on each candidate's common window |
| Compute blow-up | High | Funnel, bounded shortlist, budget, cancel |
| Data quality (spec/calibration/FX gaps) | Med | Eligibility stage; refusals visible |
| Job lost on restart | Low | Disclosed; rerun is cheap to start |

## Rollback Plan

Revert the PR(s). Additive: new service, endpoints, route, sidebar entry; no migration or persisted state. Reverting PR1 restores the inline member resolution; reverting PR4 removes the deep-link params.

## Success Criteria

- [ ] Shipped Ftmo suites and the 18 tripwire slice files unedited and green; `FtmoBreachEvaluator.cs` unchanged.
- [ ] Proxy calibrated vs full simulation on the P≈24 pool; calibration result recorded before the shortlist is trusted.
- [ ] Two runs with the same inputs return identical rankings (no RNG).
- [ ] Every shortlisted group's metrics equal a direct group-simulator run of the same members and risk.
- [ ] Budget stop and cancel are visible and leave no running work.
- [ ] User runs a SBDEMO2 search end-to-end, opens a result via deep link, in EN and ES.
