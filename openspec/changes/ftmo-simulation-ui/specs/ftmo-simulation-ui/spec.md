# FTMO Simulation UI Specification

## Purpose

The shipped `ftmo-breach-simulation`, `ftmo-challenge-race`, and `ftmo-multi-start` backend endpoints
answer "where does this strategy break a limit, and when", but are reachable only by hand-built HTTP
calls with 8+ required parameters. This capability adds `FtmoSimulationModalComponent`, opened from a
strategy row, that collects those inputs, calls the multi-start endpoint on explicit Run, and renders
the distribution as **elimination, not certification**. A second section (PR2) adds the single-start
detail (`ftmo-breach` + `ftmo-challenge-race`) additively, without changing the PR1 view.

**Non-goals**: the start-date timeline strip (one colour per month) and the cost-decomposition UI are
explicitly out of scope for this capability.

## Requirements

### Requirement: The Modal Opens From The Strategy Row

The modal MUST open from an action on a strategy row in the account detail view, the same interaction
pattern as the existing Analytics and Monthly Returns modals.

#### Scenario: Opening the modal from a strategy row
- GIVEN a strategy row in the account detail grid
- WHEN the user triggers the FTMO simulation action on that row
- THEN the modal opens scoped to that strategy

### Requirement: The Source Lot Grid Is Prefilled From The IMOX Retester Constant, Never From The FTMO Grid

The four source-grid fields (`sizeDecimals`, `step`, `minLot`, `maxLots`) MUST be prefilled, when the
modal opens, from a hardcoded frontend constant that mirrors `LotGrid.ImoxRetester` (sizeDecimals `2`,
step `0.01`, minLot `0.01`, maxLots `10`), and MUST be labelled in the UI as the backtest (IMOX retester)
lot grid — never as the FTMO grid. These fields MUST stay editable. `broker` MUST be prefilled with the
constant `FTMO` (user decision 2026-10-01: the strategies that have backtests can live in a non-FTMO
account, whose broker has no FTMO limits row) and stay editable. `initialCapital` MUST be prefilled to `10000` and stay editable. `targetRiskPerTrade`
MUST have no prefill; the user types it. `fxLow`/`fxHigh` MUST be optional. There is no client-side
lookup of the FTMO instrument spec: a missing spec, an undeclared FX rate, or an invalid FX band MUST
NOT be detected or pre-empted client-side, and Run MUST stay enabled regardless — each condition
surfaces only as the backend's own refusal (`InstrumentSpecMissing`, `FxRateNotDeclared`,
`InvalidFxBand`), shown as a run result with its own distinct i18n message.

#### Scenario: The source lot grid is prefilled from the IMOX retester constant
- GIVEN the modal opens for any strategy
- WHEN the source-grid fields are rendered
- THEN `sizeDecimals` shows `2`, `step` shows `0.01`, `minLot` shows `0.01`, `maxLots` shows `10`, each
  labelled as the backtest (IMOX retester) lot grid, `broker` shows `FTMO` whatever broker the opening account has, and `initialCapital` shows
  `10000`

#### Scenario: Broker is prefilled with FTMO, not with the account's broker
- GIVEN a strategy opened from an account whose broker is `Darwinex`
- WHEN the modal opens
- THEN `broker` shows `FTMO`, the field stays editable, and the request carries whatever value the user left in it

#### Scenario: The source-grid fields stay editable
- GIVEN the modal has opened with the prefilled source-grid values
- WHEN the user edits `maxLots`
- THEN the field accepts the new value and Run remains available

#### Scenario: A missing FTMO instrument spec surfaces only as the backend's refusal
- GIVEN a strategy whose symbol matches no FTMO instrument spec
- WHEN the user activates Run
- THEN the modal does not pre-empt the run client-side, and the backend's `InstrumentSpecMissing`
  refusal is shown as the run result with its own distinct message

### Requirement: Required Fields Are Validated Before Run

The Run action MUST be disabled until all 8 required fields — `broker`, `sqxSymbol`, `initialCapital`,
`targetRiskPerTrade`, `sizeDecimals`, `step`, `minLot`, and `maxLots` — each hold a value the backend's
`FtmoBreachSimulationRequest` shape can accept (non-null, and numeric fields parseable as numbers;
`sizeDecimals = 0` is a legal value, not treated as missing). `fxLow`/`fxHigh` MUST NOT be required for
Run to be enabled. The controller rejects the request with HTTP 400 unless all 8 required fields are
present.

#### Scenario: Run is disabled while targetRiskPerTrade is empty
- GIVEN a modal with every other required field filled but `targetRiskPerTrade` empty
- WHEN the user views the Run action
- THEN Run is disabled

#### Scenario: Run is enabled once all 8 required fields are filled
- GIVEN a modal with `broker`, `sqxSymbol`, `initialCapital`, `targetRiskPerTrade`, `sizeDecimals`,
  `step`, `minLot`, and `maxLots` filled and `fxLow`/`fxHigh` left empty
- WHEN the user views the Run action
- THEN Run is enabled

#### Scenario: A zero sizeDecimals value does not disable Run
- GIVEN a modal with every required field filled and `sizeDecimals = 0`
- WHEN the user views the Run action
- THEN Run is enabled, because `0` is a legal value for `sizeDecimals`

### Requirement: A Simulation Runs Only On Explicit Run, And A Second Run Is Blocked While One Is In Flight

Editing any input MUST NOT trigger a request. A request MUST be sent only when the user activates Run.
While a request is in flight, the Run action MUST be disabled so a second Run cannot be started
concurrently.

#### Scenario: Editing an input does not trigger a request
- GIVEN a modal with a previously completed run displayed
- WHEN the user edits `targetRiskPerTrade`
- THEN no new request is sent until Run is activated again

#### Scenario: A second Run is blocked while one is in flight
- GIVEN a Run request that has not yet resolved
- WHEN the user activates Run again
- THEN no second request is sent, and Run remains disabled until the first request resolves

### Requirement: Deploy And Eval Render Side By Side, Never Merged

The Deploy run and the Eval run MUST each render in their own clearly labelled panel, side by side. On
narrow widths the panels MAY stack vertically, but MUST remain two distinct panels. No figure from one
run MUST be combined, averaged, or displayed as if it belonged to the other run.

#### Scenario: Deploy and Eval render as separate panels
- GIVEN a multi-start result with both a Deploy run and an Eval run
- WHEN the result is rendered
- THEN two separately labelled panels are shown, one per run, with no combined or shared figures

#### Scenario: A response with zero runs shows one explicit message
- GIVEN a completed multi-start response with zero runs (the strategy has no imported backtests)
- WHEN the result is rendered
- THEN exactly one message is shown: "This strategy has no imported backtests. Import its Deploy and
  Evaluation backtests with the Import backtests action on the strategy row." (EN and ES), and neither
  "No run held for this slot" nor any panel is shown

#### Scenario: Before the first Run a single hint replaces the empty slots
- GIVEN the modal has opened and no Run has been requested
- WHEN the modal is rendered
- THEN one hint is shown ("Enter the risk per trade and press Run to see results." / "Ingrese el riesgo
  por operación y presione Ejecutar para ver los resultados.", consistent with the Run button label) and
  no "No run held for this slot" slot is rendered

#### Scenario: Exactly one missing kind still shows the slot marker
- GIVEN a response holding a Deploy run but no Evaluation run
- WHEN the result is rendered
- THEN the Deploy panel is shown and the Evaluation slot reads "No run held for this slot", with no
  zero-runs message

#### Scenario: Narrow widths stack but do not merge the panels
- GIVEN a viewport narrow enough that side-by-side panels do not fit
- WHEN the result is rendered
- THEN the two panels stack vertically, each still separately labelled, and remain distinct

### Requirement: The Six Outcome Shares Render With Counts, Zeros Included

Each run's summary MUST render all six chain-outcome shares (`Phase1Breached`,
`Phase1UndecidedAtEndOfData`, `Phase2Breached`, `Phase2UndecidedAtEndOfData`, `FundedBreached`,
`FundedNoBreachAtEndOfData`), each showing its share and its count, even when the count is zero. No
outcome MUST be omitted from the rendered set.

#### Scenario: A zero-count outcome is still rendered
- GIVEN a summary where `FundedBreached` has count 0 and share 0
- WHEN the summary is rendered
- THEN `FundedBreached` is shown with a 0 count and a 0% share, not omitted

#### Scenario: All six outcomes are always present
- GIVEN any produced summary
- WHEN the summary is rendered
- THEN exactly six outcome rows are shown, one per chain outcome

### Requirement: The Order-Statistics Table Renders With The Funded-Duration Headline From The Funded Start

Each run's summary MUST render a plain semantic `<table>` of order statistics (N, Min, Q1, Median, Q3,
Max) for days to phase-1 target, days to phase-2 target, and funded days to breach. The funded-duration
figure MUST be headlined using `FundedDaysToBreachFromFundedStart`; the chain-start-based figure
(`FundedDaysToBreachFromChainStart`) MUST be shown as secondary information, not the headline. An
order-statistics row with `N = 0` MUST render every quantile as explicitly absent (for example "—"),
never as `0`.

#### Scenario: Funded duration headlines from the funded start
- GIVEN a summary whose `FundedDaysToBreachFromFundedStart` order statistics differ from
  `FundedDaysToBreachFromChainStart`
- WHEN the order-statistics table is rendered
- THEN the funded-duration figure headlined is `FundedDaysToBreachFromFundedStart`, with the
  chain-start figure shown as secondary

#### Scenario: A zero-observation quantile row renders as absent, not zero
- GIVEN an order-statistics input with `N = 0`
- WHEN the table is rendered
- THEN Min/Q1/Median/Q3/Max render as explicitly absent, not as `0`

### Requirement: A Whole-Run Refusal Shows Its Reason

> Post-PR1 fix (2026-10-01): `LimitsNotConfigured` and `ProductNotTwoStep` are properties of the
> broker's risk-limit configuration, so their copy MUST name the broker of the submitted query (for
> example "The broker 'Darwinex' has no FTMO limits configured.") and MUST NOT blame the account. The
> broker is the one captured at Run, so later edits of the field do not change a rendered refusal.

When a run's `Status` is `Refused`, the panel MUST show the refusal reason (`Refusal`) and MUST NOT
render any outcome shares or order statistics for that run. `InstrumentSpecMissing`, `FxRateNotDeclared`,
and `InvalidFxBand` are the backend's own refusals for a missing FTMO instrument spec, an undeclared FX
rate for a non-account profit currency, and an invalid FX band respectively; each MUST render its own
distinct i18n message, not a shared generic refusal message.

#### Scenario: A refused run shows its reason, not empty results
- GIVEN a run with `Status = Refused` and `Refusal = InstrumentSpecMissing`
- WHEN the panel is rendered
- THEN the refusal reason `InstrumentSpecMissing` is shown, and no outcome shares or order-statistics
  table are rendered for that run

#### Scenario: InstrumentSpecMissing, FxRateNotDeclared, and InvalidFxBand each render a distinct message
- GIVEN three otherwise-identical refused runs, one with `Refusal = InstrumentSpecMissing`, one with
  `Refusal = FxRateNotDeclared`, and one with `Refusal = InvalidFxBand`
- WHEN each panel is rendered
- THEN each shows its own distinct message, and no two of the three refusals share the same rendered
  text

### Requirement: A Race Refusal Of ProfitTargetMismatch Shows Its Stored Value

When a run's `RaceRefusal` is `ProfitTargetMismatch`, the panel MUST show that refusal alongside the
run's `StoredProfitTargetPct` value, even though the run itself is not refused.

#### Scenario: ProfitTargetMismatch is shown with the stored value
- GIVEN a run with `RaceRefusal = ProfitTargetMismatch` and `StoredProfitTargetPct = 0.08`
- WHEN the panel is rendered
- THEN the race refusal is shown together with the stored value `0.08`

### Requirement: Months Without A Start Are Disclosed

When a run's `MonthsWithoutStart` list is non-empty, the panel MUST display those months and their
count. This list MUST NOT be silently dropped when non-empty.

#### Scenario: Months without a start are listed with their count
- GIVEN a run with 3 entries in `MonthsWithoutStart`
- WHEN the panel is rendered
- THEN those 3 months are listed together with the count 3

### Requirement: The Start1DiffersFromSingleStartAnchor Flag Is Disclosed

When a run's `Start1DiffersFromSingleStartAnchor` is `true`, the panel MUST display an explicit
disclosure that start 1 differs from the single-start endpoint's anchor.

#### Scenario: A divergent start 1 is disclosed
- GIVEN a run with `Start1DiffersFromSingleStartAnchor = true`
- WHEN the panel is rendered
- THEN an explicit disclosure that start 1 diverges from the single-start anchor is shown

### Requirement: The Disclosure Is Always Visible, And Is Translated (EN And ES)

**Amended by user decision 2026-10-03** (reason: the user wants a Spanish UI; every text in the modal
MUST be available in Spanish). Previously the server's `Disclosure` and `NotModelled` text was shown
verbatim as data. The UI MUST NOT render the server's `Disclosure` text any more.

An i18n-keyed short disclosure block MUST be visible at all times, even before any run has completed.
Once a result with at least one run is shown, ONE i18n disclosure (`DISCLOSURE_RESULT`), rendered once
for the whole result and never once per panel, MUST be visible without any extra interaction (no
collapsed-by-default accordion). It MUST faithfully cover the three points of the server text:

1. consecutive monthly starts share most trades, so they are not independent trials, and the figures
   describe this backtest, not probabilities or a forecast;
2. unmodelled swap and closed-trade replay make targets easier to reach and understate breaches;
3. the funded phase models no reward withdrawal and no Scaling Plan, which keeps profit as cushion and
   is also optimistic.

The server's `NotModelled` values MUST be mapped by exact string to i18n labels (`Swap`,
`FtmoCommission`, `IntradayEquity`), merged and deduplicated across runs, and rendered once next to the
disclosure. An unrecognised value MUST render through an explicit UNKNOWN label that shows the raw value
through a `{{value}}` param, and MUST never leak a placeholder.

#### Scenario: The disclosure block is visible before any run
- GIVEN the modal has just opened and no run has completed
- WHEN the modal is rendered
- THEN the i18n-keyed short disclosure block is visible without further interaction

#### Scenario: The translated disclosure is shown once for the whole result
- GIVEN a result with a Deploy run and an Evaluation run
- WHEN the result is rendered in EN or ES
- THEN the `DISCLOSURE_RESULT` text is shown exactly once, no panel renders its own disclosure, and the
  server's `Disclosure` text appears nowhere

#### Scenario: NotModelled values render as translated labels, once
- GIVEN runs whose `NotModelled` is `Swap`, `FtmoCommission`, `IntradayEquity`
- WHEN the result is rendered in ES
- THEN one list shows "Swap", "Comisión de FTMO", "Equity intradía"

#### Scenario: An unknown NotModelled value shows the raw value
- GIVEN a `NotModelled` value that is not one of the three known strings
- WHEN the result is rendered
- THEN the UNKNOWN label shows the raw value and no `{{` is visible

### Requirement: An HTTP Error Or 400 Response Is Shown, Never Silently Swallowed

When the Run request fails (network error, non-2xx, or a 400 validation response), the modal MUST show
an explicit error state and MUST re-enable Run. It MUST NOT render a stale or partial result as if it
were the new run's outcome.

#### Scenario: A 400 response shows an explicit error
- GIVEN a Run request that resolves with HTTP 400
- WHEN the response is received
- THEN the modal shows an explicit error state, re-enables Run, and does not render a partial result

#### Scenario: A network failure shows an explicit error
- GIVEN a Run request that fails with a network error
- WHEN the failure is received
- THEN the modal shows an explicit error state and re-enables Run

### Requirement: The PR2 Single-Start Detail Section Is Additive, Never Changing The PR1 View

A single-start detail section (verdict and findings from `ftmo-breach`, first-breach timing, and the
challenge-race phases) MUST be addable to the modal without altering any PR1-rendered element, markup,
or behaviour for the multi-start view.

#### Scenario: Adding the single-start section leaves the multi-start view unchanged
- GIVEN a modal already rendering the PR1 multi-start view
- WHEN the PR2 single-start detail section is added
- THEN every PR1-rendered element and behaviour is unchanged, and the single-start section appears
  as an addition

#### Scenario: The single-start section shows verdict, findings, first-breach timing, and race phases
- GIVEN a single-start (`ftmo-breach` + `ftmo-challenge-race`) result for the strategy
- WHEN the single-start detail section is rendered
- THEN it shows the verdict and findings, the first-breach timing, and the challenge-race phase 1/phase
  2 outcomes and dates

### Requirement: Banned Wording Is Excluded From The FTMO i18n Keys

No FTMO-scoped i18n key (EN or ES) MUST contain: "passed", "safe", "survived", "would have passed",
"aprobado", "aprobó", "seguro", "sobrevivió", or "habría aprobado". This check is scoped to the FTMO
i18n keys only — the SQX pipeline keys, which legitimately use "survived" for an unrelated meaning, are
out of scope for this rule.

#### Scenario: A banned-wording test passes over the FTMO keys
- GIVEN the EN and ES FTMO-scoped i18n key sets
- WHEN each key's text is inspected
- THEN none contains "passed", "safe", "survived", "would have passed", "aprobado", "aprobó",
  "seguro", "sobrevivió", or "habría aprobado"

#### Scenario: SQX pipeline keys using "survived" are out of scope for this rule
- GIVEN an SQX pipeline i18n key that legitimately uses "survived"
- WHEN the banned-wording check runs
- THEN that key is not scoped by the check and does not fail it

### Requirement: Every FTMO Enum Is Mapped By Exact Value, Never By Truthiness

Every FTMO enum label mapper (for example `FtmoSimulationStatus`, `FtmoSimulationRefusal`,
`FtmoChallengeRaceRefusal`, `FtmoChainOutcome`) MUST switch on the exact numeric value and MUST use
`!== null` for presence checks, never a truthy check that would treat a zero-valued member as absent. A
refusal or outcome whose enum value is `0` MUST render its correct label, not disappear.

#### Scenario: A zero-valued refusal renders its label
- GIVEN a run with `RaceRefusal = ProfitTargetMismatch` (value `0`)
- WHEN the label is rendered
- THEN the label for `ProfitTargetMismatch` is shown, not treated as absent

#### Scenario: A zero-valued status renders its label
- GIVEN a run with `Status = Refused` (value `0`) and `Refusal = InvalidRequest` (value `0`)
- WHEN the labels are rendered
- THEN both `Refused` and `InvalidRequest` render their correct labels, neither treated as absent by a
  truthy check

### Requirement: Every Visible String Comes From i18n, In EN And ES

Every user-facing string produced by this capability (labels, disclosures, refusal reasons, error
states, the not-modelled labels) MUST be sourced from an i18n key present in both
`public/assets/i18n/en.json` and `public/assets/i18n/es.json`. No hardcoded user-facing string MUST
appear in the component templates or TypeScript. The earlier exception for the server's `Disclosure` and
`NotModelled` text was removed by user decision 2026-10-03: that text is no longer rendered.

#### Scenario: Every rendered string resolves to an i18n key in both languages
- GIVEN the modal rendered in each of the EN and ES locales
- WHEN every visible string is inspected
- THEN each one resolves to a key present in both `en.json` and `es.json`, with no hardcoded string
