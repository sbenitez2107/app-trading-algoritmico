# Proposal: FTMO multi-start replay — the challenge race plus a funded phase, from every monthly start

> Next step (i) and (iii) of roadmap layer 1 (`SIMULATOR_ROADMAP.md:210`). Backend only. No `explore.md`.
> **The single-start endpoint's output stays byte-identical for every input it already reports the same
> readout for.** `FtmoBreachEvaluator.cs` and the golden pin are not edited. PR0 fixes two bugs in
> `FtmoChallengeRace.cs` (below); it can change `FirstTargetTouchSourceClose`/`MinTradingDaysMetFtmoDay`
> on a `BreachedFirst` phase and phase 2's straddling-row composition, but never a phase's `Outcome` or
> breach classification.

## Intent

Every shipped result starts at the backtest's first trade (January 2016). "Starting in January 2016 both
targets are reached first" is not "the targets are reached first", and reaching them is half the goal:
at US$250 the account reaches both targets in ~51 days and breaks its daily limit on day 261, inside the
funded phase, which nothing measures today. The next change is a UI whose main view is this distribution.

## Scope

### In Scope
0. **PR0 — two race bug fixes in the SHIPPED `ftmo-challenge-race` capability**, ahead of the new
   multi-start work (orchestrator-added; RunPhase/RunChain are the shared handover this change's own
   funded phase and start slicing build on):
   - **Bug A**: `RunPhase`'s target scanner does not stop at the breach, so a `BreachedFirst` phase can
     report `FirstTargetTouchSourceClose`/`MinTradingDaysMetFtmoDay` from AFTER the account breached.
     Fix: report those two fields on a `BreachedFirst` phase only when they occur at or before the
     breach close; otherwise null. `Outcome` and the breach fields are unchanged.
   - **Bug B**: `RunChain`'s handover takes every row with `Close > T`, with no `Open` lower bound, so an
     `Unscalable` row straddling T (`Open < T`, `Close > T`) can land in the next phase and become its
     anchor, contradicting the "every row has `Open >= T`" invariant the design relies on. Fix: the next
     phase takes `Open >= T AND Close > T`; a zero-duration row at T stays in the current phase's group.
   - Both fixes are delta-specced against `ftmo-challenge-race` (see Capabilities); the golden pin and
     `FtmoBreachEvaluator.cs` stay untouched — this is race code, not the evaluator.
1. Start enumeration from the data, one per FTMO month.
2. Per start, the full chain: phase 1 → phase 2 → **funded phase** (new), built on the PR0-fixed handover.
3. Per-start rows plus an aggregate: outcome counts and shares, and order statistics of durations.
4. A new endpoint and DTO designed for the UI consumer, with disclosure.

### Out of Scope
- Any Angular change (the next change). Reward withdrawals, the Scaling Plan, swap, intraday equity,
  portfolio level. Any edit to the single-start endpoint's output.

## Capabilities

### New Capabilities
- `ftmo-multi-start`: start enumeration, funded phase, per-start chain, censoring, aggregates, disclosure.

### Modified Capabilities
- `ftmo-challenge-race`: PR0 fixes bugs A and B (above) in two existing requirements
  ("Every Non-Refused Race Reports Full Per-Phase Timing And The Fixed Rules" and "Phase Two Starts
  Fresh At The First Trade Opened After The Phase-One Target Close"). Every other requirement, and both
  requirements' outcome/breach semantics, are unchanged. `ftmo-breach-simulation` keeps every requirement
  unchanged; this capability only reads it.

## Approach

| # | Decision |
|---|---|
| D1 | **Grain: monthly.** A start is the first scalable (non-Unscalable) trade opened in each FTMO (Berlin) month, from the first month with one to the last. A month with no scalable open has no start and is counted. At ~8 trades a month (~1,000 over ~128 months), weekly starts (~550) would sit ~2 trades apart and mostly replay identical chains: more rows, no more information, ~4× the cost. Every-trade starts (~1,000) are ~8× the cost with near-total duplication. Quarterly (~42) is too coarse for a distribution. Weekly and every-trade are recorded alternatives. |
| D2 | A start's series is every trade with `Open ≥ startOpen`. Phases 1 and 2 run the shipped `FtmoChallengeRace.RunPhase` unchanged. |
| D3 | **Funded phase**: it starts at the first trade opened after the phase-2 target close, on a fresh account at Initial Capital with the same limits, no target and no day minimum. The unedited evaluator supplies the first breach (earliest of daily/max, clean or contingent). Outcome ∈ `{BreachedFirst, NoBreachByEndOfData, NotStarted}`. Elapsed days are counted from the funded start and from the chain start. |
| D4 | **Chain outcome**, one per start: `Phase1Breached`, `Phase1UndecidedAtEndOfData`, `Phase2Breached`, `Phase2UndecidedAtEndOfData`, `FundedBreached`, `FundedNoBreachAtEndOfData`. The last three types the wording: "survived" is banned by both shipped specs, so it is not used. |
| D5 | **Censoring is its own category, never an exclusion.** Three of the six outcomes are right-censored and are counted and labelled as such. Shares use all starts as the denominator, with counts beside them. Each censored start carries its **runway** (days from its phase start to the last close), and the aggregate reports order statistics of that runway. A minimum-runway cutoff was rejected. Funded has no natural horizon, so any cutoff is either a fabricated constant or derived from the observed outcomes (circular). A cutoff would also drop the most recent starts. Kaplan–Meier was rejected: it assumes independent samples and reads as a probability. |
| D6 | **Aggregates**: counts and shares per chain outcome (zeros included), `FxRoundingSensitive` count, and order statistics (min, Q1, median, Q3, max, n) for days to both targets, funded days to breach, and censored runway. Nearest-rank, so every quantile is an observed start. No mean. No backend "within N days" bucket; N would be fabricated. |
| D7 | **FX band**: the race's whole-chain rule per start, extended to three phases: `… < (T,T, Funded Breached) < (T,T, Funded NoBreach)`, with an earlier funded breach less favourable. This is a new merge, because the shipped `MergeEnds` is two-phase and is not edited. A test pins that it equals `MergeEnds` on phases 1 and 2. A degenerate band (`fxLow == fxHigh`, every USD symbol) evaluates one end, since the projections are identical. |
| D8 | **Placement**: a new `IFtmoMultiStartReadService` and `GET api/strategies/{id}/ftmo-breach/multi-start`, taking the same request as the shipped endpoint. A mode flag on the existing endpoint was rejected because it risks the byte-identical promise and bloats its DTO. The guard chain and the projection are extracted from the shipped service unchanged, so both use one refusal path. |
| D9 | **DTO**: per run, `Starts[]` (start open, FTMO month, chain outcome, the three phase records, runway, FX end), `Summary` (D6), `MonthsWithoutStart`, `Grain = Monthly`, rules, disclosure, and `NotModelled`. |
| D10 | **Synchronous**, with cancellation checked per start. See Performance. |

**Disclosure** (every run): consecutive monthly starts share most of their trades, so N starts are not N
independent trials. The figures describe this backtest and are not probabilities or a forecast.
Unmodelled swap and closed-trade replay make targets easier and understate breaches. The funded
phase models no reward withdrawal or scaling, which keeps profit as cushion; that is also optimistic.

## Performance (estimated, not measured)

Basis: n = 1,000 trades, S = 125 starts, 2 runs, 2 FX ends, 3 phases, and a start's suffix averaging n/2.
- The evaluator overlap loop runs about S·n² = 125M iterations per run and end, **500M** in total. At an assumed 2–5 ns each, that is **1–2.5 s**. No overlap exists in current data, so the loop never exits early.
- **This loop is not the worst case.** `RunPhase`'s scanner calls `ElapsedDays` per close group, and that call re-attributes every prior open through `TimeZoneInfo`. A phase that ends undecided costs about m²/2 conversions, up to about **83M** in total, or **17–42 s** at an assumed 0.2–0.5 µs each. When a target arrives in ~31–45 days, the scanner cost is negligible.
- The degenerate-band shortcut (D7) halves every figure for XAUUSD.

Decision: the evaluator stays untouched. A benchmark on the real gold fixture is a design gate. If it
confirms the scanner worst case, optimise the race's own helpers (memoise per-trade open-day attribution
once per run) behind an equivalence test against the current implementation on the real series. The
7-trade golden pin alone cannot prove equivalence at this scale.

## Affected Areas

| Path | Impact |
|---|---|
| `Infrastructure/Services/FtmoMultiStart*.cs` (service, funded phase, 3-phase merge, order statistics) | New |
| `Infrastructure/Services/FtmoBreachSimulationReadService.cs` | Modified: guard/projection extraction, behaviour-preserving |
| `Application/DTOs/Backtests/`, `Application/Interfaces/`, `Domain/Enums/` | Additive |
| `WebAPI/Controllers/StrategyBacktestsController.cs`, DI registration | Additive |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Shares read as pass probabilities | High | Disclosure on every run; counts beside shares; no forecast wording |
| Extraction changes shipped output | Med | Golden pin and shipped suites green, unedited |
| Scanner worst case is slow | Med | Benchmark gate; D7 shortcut; optimise outside the evaluator |
| Start 1 differs from the shipped anchor when the first trade is Unscalable | Low | Documented; cross-check only when it is scalable |

## Rollback Plan

Revert the PR(s). The change is additive, with no migration and no persisted state.

## Sizing

Floor ~1,300 lines (tests included), realistic 1,700+. `400-line budget risk: High`. This is five chained
PRs: **PR0**, the two race bug fixes (small, RED-first); PR1, the funded phase and the 3-phase merge
(pure); PR2, start enumeration and order statistics (pure); PR3, the guard/projection extraction
(refactor only); PR4, the service, DTO and endpoint.

## Success Criteria

- [ ] PR0's two bug fixes are RED-first, land before PR1, and touch zero existing assertions (reported in
      design.md; none of the existing `FtmoChallengeRaceTests` assert the buggy post-breach-touch or
      straddling-row behaviour).
- [ ] Shipped Ftmo suites and the golden pin are green, unedited; `FtmoBreachEvaluator.cs` is unchanged.
- [ ] When the first trade is scalable, start 1's phases 1 and 2 equal the single-start `ChallengeRace`.
- [ ] Every start lands in exactly one chain outcome, and the counts sum to the start count.
- [ ] The benchmark on the real fixture is recorded before the endpoint ships.

## Proposal question round (blocked, needs a human)

1. **Q1**: Is modelling no funded reward withdrawal and no scaling acceptable, as an optimistic
   assumption that is disclosed (assumed)? Modelling a withdrawal cadence would need a value that has no source.
2. **Q2**: Confirm `FundedNoBreachAtEndOfData` instead of "survived". The settled wording contradicts
   the no-survival rule that both shipped specs enforce.
3. **Q3**: Should funded duration be headlined from the funded start or from the chain start? Both are reported;
   the choice matters for the UI.

## Question round — resolved by the orchestrator (2026-09-27)

1. **Funded reward withdrawals and the Scaling Plan are not modelled.** Modelling them would require inventing when and how much the user withdraws. The omission is optimistic — withdrawing profit lowers the balance towards the static floor — and the disclosure says so.
2. **`FundedNoBreachAtEndOfData`** is the name, replacing "survived", which both shipped specs ban.
3. **Funded duration is reported both from the funded start and from the chain start.** Which one the UI headlines is decided in the UI change.
4. **Start 1 versus the single-start anchor.** When the backtest's first trade is Unscalable, start 1 (first scalable trade of the first month) differs from the single-start anchor (which includes Unscalable rows). Accepted and disclosed; the single-start result is not changed.
5. **The performance figures are estimates.** The first design task is a benchmark on the real gold strategy (`WF_6_22_XAUUSD_H1_SMA_BB_2.48.334`) before any optimisation is committed to.
