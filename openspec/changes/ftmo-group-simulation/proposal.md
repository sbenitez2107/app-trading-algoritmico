# Proposal: FTMO group simulation, on its own Simulator screen

> Roadmap step 1 of 4 (`SIMULATOR_ROADMAP.md:232-235`). Exploration: Engram `sdd/ftmo-group-simulation/explore` (#2979).
> **No shipped engine file (race, funded phase, enumerator, sweep, calendar, `ComputeRun`) is edited.
> `FtmoBreachEvaluator.cs` is never edited.** The only shipped file touched is `FtmoSimulationInputs`
> (input resolution), via the behaviour-preserving B1 refactor.

## Intent

Every FTMO result today is one strategy on one account. The user's real question is whether a manually
chosen group of SBDEMO2 strategies, sharing ONE FTMO account, reaches the targets before a breach.
Nothing answers it: group analytics are VaR/correlation only, and worst-case simultaneous risk is
computed nowhere (roadmap :46-50).

## Scope

### In Scope (PR slices)
| PR | Content | Rough size |
|---|---|---|
| B1 | Split `FtmoSimulationInputs.ResolveSharedAsync` into limits + per-symbol parts, pinned by existing tests (PR3 precedent); pure merger (sort, renumber, `RowIndex` map, intersection) | ~350 |
| B2 | `IFtmoGroupSimulationReadService`, DTO, `POST api/ftmo-simulations/group`, refusals/guards, disclosures, **group benchmark** that sets the cap | ~450 |
| B3 | Group diagnostics; picker candidates read (Deploy/Eval presence, date range, spec/calibration flags, account scope) | ~350 |
| F1 | `/simulator/ftmo` route, sidebar "Simulator" group, page shell | ~150 |
| F2 | Strategy picker (one account, default SBDEMO2; symbol filter, search, flags) | ~350 |
| F3 | Parameter form, reused run panels, group POST in service, EN/ES i18n | ~400 |
| F4 | Diagnostics panel | ~250 |

Total ~2,300 lines. `400-line budget risk: High` -> seven chained PRs.

### Out of Scope
Automatic combinations (step 2), live data (step 3), Darwinex Zero/Axi (step 4), per-member risk,
correlation, `Portfolio` persistence, eligibility-rule modelling, any edit to the single-strategy modal or endpoints.

## Capabilities

### New Capabilities
- `ftmo-group-simulation`: merge, window, kind pairing, group-wide vs per-panel refusals, diagnostics, disclosures, candidates read, endpoint.
- `ftmo-group-simulation-ui`: Simulator route/sidebar, picker, form, reused panels, diagnostics panel, i18n.

Split, not merged: follows the `ftmo-multi-start` / `ftmo-simulation-ui` precedent, lets B* and F* be verified and archived independently, and keeps the backend contract reusable by step 2 without UI requirements.

### Modified Capabilities
None. B1's refactor is behaviour-preserving; reused UI components keep their requirements.

## Decisions

1. **Approach A**: reuse `FtmoMultiStartReadService.ComputeRun` on a merged group projection via a NEW service and `POST api/ftmo-simulations/group`. No shipped engine file edited.
2. **Merge**: sort by `(Open, memberOrder, origRow)` (`memberOrder` = ascending `StrategyId`, so the result does not depend on picker order), renumber `RowIndex` globally, keep merged `RowIndex -> (member, origRow)`. Mandatory: otherwise `AttributeOpenDays` and `HasConcurrentOpenPosition` silently go wrong.
3. **Window**: intersection of member date ranges; empty -> refusal. Response echoes window and each member's first/last date. `NoCommonWindow` is evaluated **per kind** (orchestrator decision 2026-10-03, consistent with the per-panel rule of D11): an empty Deploy window refuses only the Deploy panel and never hides a valid Eval result, and vice versa.
4. **Kinds**: Deploy with Deploy, Eval with Eval. A member missing a kind -> that panel Refused listing the members; the other kind still runs.
5. **Risk**: one global per-member target risk. UI shows worst simultaneous k x risk vs academy 1% group criterion and FTMO 5% daily. Overrides deferred.
6. **Cap**: `MaxMembers` = min(8, largest k whose group benchmark passes its gate); B2's benchmark sets the final value.
7. **No persistence** (`Portfolio` contradicts itself, `Portfolio.cs:7` vs `:18-22`). Request stays serialisable for a later "save as portfolio".
8. **Screen**: top-level `/simulator/ftmo`, separate from broker-account routes. Picker, form (capital 10000, risk, lot grid prefilled from `IMOX_RETESTER_LOT_GRID`, FX inputs only when a member settles non-USD), reused panels, EN/ES.
9. **Diagnostics**: per-member net and counts, first-breach member via the `RowIndex` map (aggregated across starts), peak concurrent open positions. Correlation deferred.
10. **Disclosures** (added): contingent breaches dominate groups, so Clean/Contingent loses meaning; same-close ordering is a modelling choice that can create or remove a breach; eligibility rules (e.g. martingale/grid ban) not modelled.
11. **Refusals/guards** (amended by user decision, 2026-10-03; it replaces the earlier "refuse the whole group" rule).
    - **Member-level problems refuse only the affected kind's panel**, listing the failing members with their own reason: `RiskNotEstimable`, `PointValueNotCalibrated`, `InstrumentSpecMissing`. When the problem belongs to the symbol (spec or calibration missing, and the FX refusals that derive from the spec), the symbol fails for Deploy and Eval alike, so both panels are refused. `RiskNotEstimable` belongs to one run, so it refuses only its own kind.
    - **Reason**: Deploy and Eval are different backtests, so a member can be estimable in one and not the other. This is consistent with D4, where a missing kind refuses only that panel.
    - **Group-wide refusals stay group-wide**: invalid request, mixed source time zones, over the member cap (HTTP 400), and the broker limits refusals. An empty date window blames no member: it is a property of the group's ranges, reported with every member's coverage.
    - Duplicate ids are deduped server-side; duplicate names across accounts warn.
12. **Extensibility**: pure `ComputeGroup(members, params)` that a step-2 generator can compose; that generator must cross the analytics tripwire deliberately.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Silent `RowIndex` corruption | High if skipped | D2; merger tests with overlapping members and duplicate per-run indices |
| k^2 evaluator cost unmeasured | Med | B2 benchmark before endpoint ships; cap from evidence |
| Intersection shrinks to a short window | Med | Echo window and coverage; empty -> refusal |
| Results read as optimistic forecasts | High | Existing + D10 disclosures; counts beside shares |

## Rollback Plan

Revert the PR(s). Additive: new files, route and sidebar entry; no migration, no persisted state. B1 revert restores the original `ResolveSharedAsync`.

## Success Criteria

- [ ] Shipped Ftmo suites green and unedited; `FtmoBreachEvaluator.cs` unchanged.
- [ ] A one-member group equals the single-strategy multi-start result on every field except `RunId`, whenever no same-close tie disagrees with open order (test fixture has no such ties).
- [ ] Merger tests prove globally unique `RowIndex` and a correct reverse map.
- [ ] Group benchmark recorded; cap set from it.
- [ ] User runs a SBDEMO2 group end-to-end at `/simulator/ftmo` in EN and ES.
