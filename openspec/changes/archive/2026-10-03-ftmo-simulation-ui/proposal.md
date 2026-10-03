# Proposal: FTMO simulation UI — the start-date distribution (per strategy)

> Roadmap "Next: the UI track" (`SIMULATOR_ROADMAP.md:217-223`). Consumes the shipped
> `GET api/strategies/{strategyId}/ftmo-breach` and `.../ftmo-breach/multi-start`. Exploration: Engram
> `sdd/ftmo-simulation-ui/explore`.

## Intent

The user is preparing FTMO 2-Step 10k challenges. The multi-start backend answers "from which start
months does this strategy break a limit, and when", but it is only reachable by hand-built HTTP calls
with 8 required parameters. The user needs to see the distribution per strategy, framed as
**elimination, not certification**.

## Scope

### In Scope
- **PR1 (web)**: `FtmoSimulationModalComponent` with the multi-start view (D2–D4).

### Deferred (2026-10-03)
- **PR2 (web, slices 2a/2b)**: the single-start detail section in the same modal (D5). **Deferred, not
  shipping in this change.**
  - **Reason**: the user's real goal is a separate portfolio-simulation screen — groups of 2..n
    strategies, for FTMO, Darwinex Zero and Axi Select, on backtest and later live data. The per-strategy
    single-start detail loses priority against it.
  - **Agreed next order**: (1) FTMO group simulation, on its own screen; (2) automatic combinations;
    (3) live data; (4) Darwinex Zero and Axi.
  - D5 below is kept as the record of what was designed; it is not part of this change's scope, tasks or
    success criteria. If the section is wanted later, it needs its own change.

### Out of Scope
- The start-date timeline strip, one colour per month. It is deferred until the user has seen real data.
- The cost-decomposition UI, which is a separate later change.
- Any change to simulation semantics, to either FTMO endpoint's output, or to a migration.
- **A read-only instrument-spec endpoint (dropped PR0).** There is no `GET api/ftmo-instrument-specs`
  and no backend change in this change. See "Dropped: PR0" below.

## Capabilities

### New Capabilities
- `ftmo-simulation-ui`: the modal, its inputs, the multi-start view, the refusal and disclosure display, and the i18n wording rules. (The single-start detail is deferred, see "Deferred".)

### Modified Capabilities
- None.

### Dropped: PR0 (`ftmo-instrument-specs`)

The original PR0 proposed `GET api/ftmo-instrument-specs` so the UI could prefill the four source-grid
query parameters (`sizeDecimals`, `step`, `minLot`, `maxLots`) from the FTMO instrument spec. That idea
was verified wrong: those four parameters describe the SOURCE (SQX backtest) lot grid, consumed by
`TradeRiskNormalizer` (`FtmoSimulationInputs.cs:162,204`). The FTMO grid is a separate thing, read by the
backend itself from `FtmoInstrumentSpec` (`FtmoSimulationInputs.cs:111`) — sending it as the source grid
would be wrong. There is therefore no reason left for a spec-listing endpoint in this change:
- Missing specs and non-account currencies are already surfaced by the backend's own refusals
  (`InstrumentSpecMissing`, `FxRateNotDeclared`, `InvalidFxBand` — `Domain/Enums/FtmoSimulationRefusal.cs`),
  which the UI shows with distinct i18n messages.
- The source-grid fields are prefilled instead from a frontend constant (D2, revised).

`ftmo-instrument-specs` is removed as a capability. Its delta spec file is deleted.

## Decisions

| # | Decision |
|---|---|
| D2 | **(Revised)** The four source-grid fields (`sizeDecimals`, `step`, `minLot`, `maxLots`) are prefilled from the frontend constant that mirrors `LotGrid.ImoxRetester` (`Domain/Backtests/LotGrid.cs:71`: sizeDecimals 2, step 0.01, minLot 0.01, maxLots 10), labelled in the UI as the **backtest (IMOX retester) lot grid** — never as the FTMO grid. The fields stay editable. A missing `FtmoInstrumentSpec` row, or a non-account profit currency, is not detected client-side; it surfaces as the backend's own refusal (`InstrumentSpecMissing`, `FxRateNotDeclared`, or `InvalidFxBand`), each shown with its own distinct i18n message when the run result renders. |
| D3 | **PR1** is a container modal, opened from the strategy-row actions in `account-detail.component.ts`, the same way as the Analytics and Monthly modals. It holds signals and calls multi-start. It shows **Deploy and Eval separately and never merged**. Each run shows: six BEM/CSS share bars (zeros included, with the count beside each share), and a plain semantic `<table>` of order statistics (N/Min/Q1/Median/Q3/Max). The Prizm grid is built for server-side data grids and is out of proportion for six rows. |
| D4 | **Refusals and disclosure**: a whole-run `Refused` shows its reason, with `InstrumentSpecMissing`, `FxRateNotDeclared`, and `InvalidFxBand` each rendering their own distinct i18n message. `ProfitTargetMismatch` shows its own message with the stored value. `MonthsWithoutStart` is listed with its count and is never dropped. `Start1DiffersFromSingleStartAnchor` is disclosed. The `Disclosure` text is always visible, as data rather than a translated string (see the design's disclosure resolution). |
| D5 | **DEFERRED (2026-10-03), not part of this change.** **PR2** would call `ftmo-breach` and show the verdict and findings, the first-breach timing, and the challenge race (the phase 1 and phase 2 outcomes and dates). It only adds to the modal: the PR1 view is unchanged. |
| D6 | **Wording**: every string is an i18n key in both EN and ES. There is no pass-style or survival wording: "passed", "safe", "survived", "would have passed", "aprobado", "aprobó", "seguro", "sobrevivió" and "habría aprobado" are all excluded. A target reached is labelled optimistic, and a breach before the target is labelled a strong result. The UI says that overlapping starts are not independent samples, and never uses odds or probability wording. |
| D7 | **Enum zero-default hazard (verified)**. Every FTMO enum has a zero member: `FtmoSimulationStatus.Refused`, `FtmoSimulationRefusal.InvalidRequest`, `FtmoChallengeRaceRefusal.ProfitTargetMismatch`, `FtmoChainOutcome.Phase1UndecidedAtEndOfData`, and the `NotStarted` members. No `JsonStringEnumConverter` is registered, so these enums arrive as integers. The label mappers therefore switch on exact values, and presence checks use `!== null`, following the `breachBasisLabel` precedent in `portfolio-detail.component.ts:701-709`. |

## Defaults to confirm

1. `broker` comes from the account context. The FTMO route injects `'FTMO'`.
2. `sqxSymbol` is `StrategyDto.symbol`, used verbatim.
3. `initialCapital` is prefilled to `10000` and stays editable.
4. The user types `targetRiskPerTrade`. It has no prefill.
5. `fxLow` and `fxHigh` are optional.
6. A run starts only on an explicit **Run** action. Editing an input does not re-run the simulation.

## Proposal question round (blocked, needs the user)

1. Funded duration: should the UI headline it from the funded start or from the chain start? The multi-start proposal (resolution 3) deferred this choice to the UI change.
2. Deploy and Eval: tabs or side-by-side? The modal's width favours tabs.
3. Does `StrategyDto.symbol` hold the verbatim SQX symbol, for example `XAUUSD_M1_UTC02`, for the user's FTMO strategies?

## Resolved

1. All six "Defaults to confirm" above are CONFIRMED as written: broker from account context (`'FTMO'`); `sqxSymbol` = `StrategyDto.symbol` verbatim; `initialCapital` prefilled to `10000`, editable; the user types `targetRiskPerTrade`, no prefill; `fxLow`/`fxHigh` optional; a run starts only on explicit **Run**, and a second Run is blocked while one is in flight.
2. Symbol match (question 3) is CONFIRMED by a read-only query of the local DB on 2026-09-28: all 171 strategies have a non-null `Symbol`, and each exactly matches a seeded `FtmoInstrumentSpec.SqxSymbol` — `XAUUSD_M1_UTC02` ×91, `USATECHIDXUSD_M1_UTC02` ×49, `BTCUSD_M1_UTC02` ×21, `DEUIDXEUR_M1_UTC02` ×10. This confirms the symbols the backend will match against for its own spec lookup; it does not affect the frontend's source-grid prefill (D2, revised), which is now a hardcoded constant, not derived from the spec.
3. The `AddFtmoInstrumentSpecs` migration is CONFIRMED APPLIED: it is present in `__EFMigrationsHistory` and all 4 seeded rows exist. `openspec/SIMULATOR_ROADMAP.md` (layer 1, "Outstanding") wrongly states it has not been applied and that the endpoint refuses every run with `InstrumentSpecMissing` until then — that line is corrected as part of this change.
4. Funded duration (question 1) is RESOLVED: the headline is elapsed days from the FUNDED start (`FtmoFundedPhaseDto.FtmoTradingDaysFromFundedStart`/`CalendarDaysFromFundedStart`). The days elapsed from the chain (phase-1) start (`...FromChainStart`) is shown as secondary information, not the headline.
5. Deploy vs Eval layout (question 2) is RESOLVED: side by side, so the two runs can be compared and are never merged into one view. On narrow widths the two panels MAY stack vertically, but MUST remain two distinct, separately-labelled panels — never a single merged panel.
6. **PR0 is DROPPED (2026-09-28)**: the instrument-spec endpoint is not needed. The source-grid fields are
   prefilled from a frontend constant mirroring `LotGrid.ImoxRetester`, not from a spec fetch. Missing
   specs and non-account currencies are already surfaced by the backend's own refusals.

## Affected Areas

| Area | Impact |
|---|---|
| `web/.../features/broker-accounts/ftmo-simulation-modal/` and `core/services` (FTMO simulation client) | New |
| `account-detail.component.ts` (row action) | Modified |
| `public/assets/i18n/{en,es}.json` | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Shares are read as pass odds | High | The disclosure is always visible, counts sit beside the shares, and a banned-word test runs over the FTMO i18n keys. The test is scoped to those keys because the SQX pipeline keys legitimately use "survived". |
| A zero enum is treated as falsy, so a refusal disappears silently | High | D7 mappers, plus tests with value `0`. |
| The backend refuses with `InstrumentSpecMissing`, `FxRateNotDeclared`, or `InvalidFxBand` and the UI does not render a distinct message for each | Med | D4 requires a distinct i18n message per refusal reason, pinned by a test. |
| Multi-start is slow on long series, per the estimates in the ftmo-multi-start proposal | Med | Only an explicit Run starts it, the loading state is visible, and a second Run is blocked while one is in flight. |

## Rollback Plan

Revert the PR or PRs. Every change is additive, with no migration and no persisted state.

## Sizing

| PR | Content | Estimate |
|---|---|---|
| PR1 | The client, models, mappers, i18n, and the modal, in four slices | ~1,900 lines total: 1a (client and models), 1b (mappers and i18n), 1c (panels), 1d (container and wiring) |
| PR2 | DEFERRED (2026-10-03): the single-start detail, slices 2a and 2b | not part of this change (previously 2a ~400, 2b ~450) |

`400-line budget risk: High` for PR1. These are chained PRs; see design.md's per-slice estimates.

## Success Criteria

- [ ] Deploy and Eval render separately. All six outcomes render, including those with a zero count.
- [ ] Every refusal path renders its reason, including those whose enum value is `0`, and `InstrumentSpecMissing`/`FxRateNotDeclared`/`InvalidFxBand` each render a distinct message.
- [ ] Every run shows the disclosure, `MonthsWithoutStart`, and the Start-1 flag.
- [ ] The banned-word test passes on the EN and ES FTMO keys.
- [ ] The PR1 view ships as designed with no single-start section (PR2 is deferred, so there is no
  "PR2 leaves PR1 unchanged" criterion in this change).
