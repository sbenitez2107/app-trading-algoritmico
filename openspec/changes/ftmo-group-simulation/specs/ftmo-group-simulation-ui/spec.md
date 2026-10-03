# FTMO Group Simulation UI Specification

## Purpose

`ftmo-group-simulation` answers, in the backend, whether a manually chosen group of strategies sharing one
FTMO account reaches the targets before a breach. This capability adds the screen that drives it: a
top-level Simulator route where the user picks members from ONE account, types the run parameters, runs
the group on explicit request, and reads the result as **elimination, not certification**, with the
group-specific diagnostics and disclosures.

It builds on, and does not restate, `ftmo-simulation-ui`: the reused run panel keeps every requirement
there (six outcome shares with counts, order-statistics table headlined from the funded start, whole-run
refusal reasons, months without a start, banned wording, exact-value enum mapping, EN/ES i18n). This
capability does not edit the single-strategy modal or its requirements.

## Requirements

### Requirement: The Simulator Route And Sidebar Entry

The app MUST expose the route `/simulator/ftmo`, outside the broker-account routes, and the sidebar MUST
show a "Simulator" group whose entry opens it. The sidebar group label and entry label MUST come from
i18n. The route MUST be reachable only to authenticated users like every other app route.

#### Scenario: The sidebar opens the screen
- GIVEN an authenticated user
- WHEN the user activates the Simulator group's FTMO entry
- THEN the app navigates to `/simulator/ftmo` and the screen is shown

#### Scenario: The route is not under the broker-account routes
- GIVEN the route table
- WHEN `/simulator/ftmo` is resolved
- THEN it is a top-level route, not a child of the broker-account FTMO routes

#### Scenario: Navigation labels are translated
- GIVEN the app in ES
- WHEN the sidebar is rendered
- THEN the Simulator group and entry labels show their ES text

### Requirement: The Picker Is Scoped To One Account And Offers Filters And Search

The picker MUST list the candidates of exactly ONE account at a time, read from
`GET api/ftmo-simulations/candidates?tradingAccountId=`. The account list comes from the trading accounts
read, and the default is the account named `SBDEMO2` when it exists, otherwise the first account. Switching
account MUST reload the candidates and clear the current selection. The picker MUST let the user filter by
symbol and search by name, and MUST allow multi-selection. Selection changes MUST NOT trigger a run request.

#### Scenario: SBDEMO2 is the default account
- GIVEN an account named SBDEMO2 exists
- WHEN the screen opens
- THEN SBDEMO2 is selected and its candidates are listed

#### Scenario: Without SBDEMO2 the first account is selected
- GIVEN no account is named SBDEMO2
- WHEN the screen opens
- THEN the first account is selected and its candidates are listed

#### Scenario: Only the selected account's strategies are listed
- GIVEN strategies on two accounts
- WHEN account X is selected
- THEN only X's strategies are listed

#### Scenario: Switching account clears the selection
- GIVEN three selected members on account X
- WHEN the user switches to account Y
- THEN the selection is empty

#### Scenario: Symbol filter and search narrow the list
- GIVEN candidates with several symbols
- WHEN the user filters by one symbol and types part of a name
- THEN only candidates matching both are listed, and members already selected stay selected

### Requirement: Each Candidate Shows Its Availability Flags

Each picker row MUST show: whether a Deploy run is held, whether an Evaluation run is held, the date range
of each held run (first and last date), whether an FTMO instrument spec and a usable calibration exist
for each held run's symbol, and whether the same name exists on another account. Missing availability MUST
be displayed as explicitly absent (never blank or `0`). The flags MUST inform the user without blocking
selection: a candidate lacking a kind, a spec or a calibration remains selectable, because the backend
reports the consequence as a member-level refusal of the affected panel.

#### Scenario: Presence and range are shown per kind
- GIVEN a candidate with a Deploy run and no Evaluation run
- WHEN the row is rendered
- THEN Deploy shows present with its date range and Evaluation shows absent

#### Scenario: A missing spec is flagged but selectable
- GIVEN a candidate whose symbol has no FTMO instrument spec
- WHEN the row is rendered
- THEN the spec flag shows absent and the row can still be selected

#### Scenario: A name that exists on another account is flagged
- GIVEN a candidate whose name also exists on another account
- WHEN the row is rendered
- THEN a translated flag marks it

### Requirement: The Member Cap Is Enforced In The UI

The UI MUST NOT allow more than `MaxMembers` selected members, using the cap value supplied by the
candidates read (never a separately hardcoded number that can drift). When the cap is reached, the remaining
unselected rows MUST become non-selectable and a translated message MUST state the cap. Deselecting a
member re-enables selection.

#### Scenario: Selection stops at the cap
- GIVEN `MaxMembers` selected members
- WHEN the user tries to select another
- THEN it is not selected and the cap message is shown

#### Scenario: Deselecting frees a slot
- GIVEN the cap is reached
- WHEN the user deselects one member
- THEN the other rows become selectable again

### Requirement: The Parameter Form And Its Prefills

The form MUST contain: `initialCapital` prefilled to `10000`; `targetRiskPerTrade` with no prefill; the
four source-grid fields prefilled from the same frontend constant mirroring `LotGrid.ImoxRetester` as
`ftmo-simulation-ui` (sizeDecimals `2`, step `0.01`, minLot `0.01`, maxLots `10`) and labelled as the
backtest (IMOX retester) lot grid, never the FTMO grid; and `broker` prefilled with `FTMO`. All fields
MUST stay editable. Run MUST be disabled until at least one member is selected and all required values
(`broker`, `initialCapital`, `targetRiskPerTrade`, the four grid fields; `sizeDecimals = 0` legal) hold a
value the backend request can accept. There MUST be one global risk field and no per-member risk input.

#### Scenario: Defaults are prefilled
- GIVEN the screen opens
- WHEN the form is rendered
- THEN capital shows `10000`, the grid shows `2`/`0.01`/`0.01`/`10` labelled as the backtest grid, and
  risk is empty

#### Scenario: Run is disabled without members or risk
- GIVEN no selected member, or members selected but empty risk
- WHEN Run is viewed
- THEN Run is disabled

#### Scenario: Run is enabled once members and required fields are present
- GIVEN at least one member and every required field filled
- WHEN Run is viewed
- THEN Run is enabled

#### Scenario: There is no per-member risk input
- GIVEN three selected members
- WHEN the form is rendered
- THEN a single risk field applies to all of them

### Requirement: FX Inputs Appear Only When A Selected Member Settles In A Non-USD Currency

The FX band inputs (`fxLow`, `fxHigh`) MUST be shown only while at least one selected member's instrument
settles in a non-USD currency (the candidates read's `needsFxBand` of its held runs), and hidden otherwise (hidden values
MUST NOT be sent). Run MUST NOT require them: a missing band is surfaced only as the backend's own
`FxRateNotDeclared` refusal.

#### Scenario: FX inputs are hidden for an all-USD selection
- GIVEN selected members that all settle in USD
- WHEN the form is rendered
- THEN no FX inputs are shown

#### Scenario: Selecting a non-USD member reveals the FX inputs
- GIVEN an all-USD selection
- WHEN the user adds a EUR-settling member
- THEN the FX inputs appear

#### Scenario: Run stays enabled without a band
- GIVEN a non-USD member and empty FX inputs
- WHEN Run is viewed
- THEN Run is enabled and any consequence arrives as the backend's refusal

### Requirement: The Worst-Case Readout Compares k x Risk With Both Limits

With `k` selected members and a valid risk, the form MUST show the worst simultaneous risk `k x risk`
both as an amount and as a percentage of `initialCapital`, beside two reference lines: the academy 1%
group criterion and the FTMO daily limit. The daily limit is the response's echoed `DailyLossLimitPct`,
falling back to 5% before the first run. The figure is computed by the client from `k` and the typed risk
(the response carries no risk echo). After a run, the readout MUST also show the observed
`PeakConcurrentOpen x risk` from the diagnostics. When a figure exceeds a reference, the readout MUST mark
that comparison with neutral wording (for example "above the 1% criterion"). The readout is informational:
it MUST NOT block Run and MUST NOT use banned wording or imply the group is acceptable.

#### Scenario: The readout shows both comparisons
- GIVEN 4 members, risk 25, capital 10000
- WHEN the readout is rendered
- THEN it shows 100 (1.00%) against the 1% criterion and the 5% daily limit

#### Scenario: Exceeding the academy criterion is marked, not blocked
- GIVEN 4 members, risk 50, capital 10000 (2%)
- WHEN the readout is rendered
- THEN the 1% comparison is marked as exceeded, the 5% comparison is not, and Run stays enabled

#### Scenario: The daily reference follows the echoed limit
- GIVEN a completed run that echoed a `DailyLossLimitPct`
- WHEN the readout is rendered
- THEN the daily reference is the echoed value, and before the first run it is 5%

#### Scenario: The observed peak is shown after a run
- GIVEN a completed run whose diagnostics report a peak of 3 concurrent positions and risk 25
- WHEN the readout is rendered
- THEN it also shows the observed 75 (peak x risk) beside the `k x risk` figure

#### Scenario: The readout updates without a request
- GIVEN a displayed readout
- WHEN the user adds a member or edits the risk
- THEN the readout recomputes and no request is sent

### Requirement: A Run Is Explicit And Blocked While One Is In Flight

A request MUST be sent only when the user activates Run; editing selection or any field MUST NOT send
one. While a request is in flight Run MUST be disabled so a second concurrent run cannot start. An HTTP
error, network failure or 400 MUST show an explicit translated error state, re-enable Run, and not render
a stale or partial result as the new outcome. Leaving the screen MUST cancel an in-flight request. The
request body carries `memberStrategyIds` (the deduplicated selected ids), broker, capital, risk, the four
grid fields and the FX band when shown.

#### Scenario: Editing does not run
- GIVEN a completed result displayed
- WHEN the user edits the risk
- THEN no request is sent until Run is activated

#### Scenario: A second Run is blocked in flight
- GIVEN a Run that has not resolved
- WHEN the user activates Run again
- THEN no second request is sent and Run stays disabled until the first resolves

#### Scenario: Leaving the screen cancels the request
- GIVEN a Run that has not resolved
- WHEN the screen is destroyed
- THEN the request is cancelled

#### Scenario: A 400 shows an explicit error
- GIVEN a Run that resolves with HTTP 400
- WHEN the response is received
- THEN an explicit error state is shown, Run is re-enabled, and no partial result is rendered

### Requirement: Deploy And Eval Render Side By Side Through The Reused Run Panel

A successful result MUST render the Deploy and the Evaluation results in the reused `FtmoRunPanelComponent`,
side by side in two distinct, labelled panels (stacking on narrow widths), never merged; no figure from
one kind may appear as belonging to the other. Before the first Run a single translated hint replaces the
empty panels. A kind carrying a refusal MUST render its refusal in that kind's slot while the other kind
still renders its result. A group-wide refusal is the exception: it is shown once above the panels and
no panel shows findings.

#### Scenario: Two panels, no shared figures
- GIVEN a response valid on both kinds
- WHEN it is rendered
- THEN two separately labelled panels are shown

#### Scenario: One refused kind does not hide the other
- GIVEN Deploy valid and Evaluation refused `MemberMissingKind`
- WHEN it is rendered
- THEN the Deploy panel shows results and the Evaluation slot shows the refusal

#### Scenario: A hint is shown before the first Run
- GIVEN no Run has been requested
- WHEN the screen is rendered
- THEN one translated hint is shown and no empty-slot marker

### Requirement: Member-Level Refusals Show In The Affected Panel, Group-Wide Refusals Show Once Above

A kind refused for a member-level problem MUST render, in that kind's panel only, the kind's failing
members, each by strategy name with its own reason (`RiskNotEstimable`, `PointValueNotCalibrated`,
`InstrumentSpecMissing`, `FxRateNotDeclared`, `InvalidFxBand`, or the kind-level reasons
`MemberMissingKind` and `MemberHasNoTradesInWindow`). The other kind's panel MUST be unaffected. A
symbol-level problem appears in both panels, because both kinds are refused. `NoCommonWindow` MUST render
in its kind's panel with every member's coverage and no blamed member.

A group-wide refusal (`InvalidRequest`, `SharedInputsRefused` with its `SharedRefusal` reason,
`MemberNotFound`, `MixedSourceTimeZones` showing each member with its zone) MUST be rendered ONCE, above
the panels, and the panels MUST show no findings. Each of these reasons, and each member-level reason,
MUST have its own distinct translated message; no two share rendered text. A refused kind MUST render no
shares or order statistics.

#### Scenario: Per-member reasons are listed in the panel
- GIVEN a Deploy refusal listing B with `PointValueNotCalibrated` and C with `RiskNotEstimable`
- WHEN rendered
- THEN the Deploy panel lists B and C, each with its own distinct reason text

#### Scenario: A member refused in one kind does not touch the other panel
- GIVEN B refused `RiskNotEstimable` in Deploy only
- WHEN rendered
- THEN the Deploy panel lists B with that reason, and the Evaluation panel renders its results

#### Scenario: A symbol-level refusal appears in both panels
- GIVEN B refused `InstrumentSpecMissing` in both kinds
- WHEN rendered
- THEN each panel lists B with that reason

#### Scenario: Missing-kind members are listed
- GIVEN `MemberMissingKind` listing two members
- WHEN rendered
- THEN both members are named

#### Scenario: A group-wide refusal is shown once, above the panels
- GIVEN a `MixedSourceTimeZones` refusal
- WHEN rendered
- THEN the refusal is shown once above the panels, each member is listed with its zone, and neither panel
  shows findings

#### Scenario: Each refusal has a distinct message
- GIVEN otherwise identical refusals of each reason
- WHEN each is rendered
- THEN no two render the same text

### Requirement: The Window And Each Member's Coverage Are Shown

For each kind, the screen MUST show the replay window (start and end) and, per member, its first open,
its last close and its number of in-window trades. When a member's range is wider than the window, the
screen MUST state that the result describes the shared window only. On a `NoCommonWindow` refusal the
coverage MUST still be shown.

#### Scenario: The window and coverage are shown
- GIVEN a successful kind with two members
- WHEN rendered
- THEN the window and a coverage row per member are shown

#### Scenario: A shortened window is disclosed
- GIVEN a member whose range is longer than the window
- WHEN rendered
- THEN a translated note states the result covers the shared window only

#### Scenario: Coverage is shown on an empty window
- GIVEN a `NoCommonWindow` refusal
- WHEN rendered
- THEN each member's range is shown

### Requirement: The Diagnostics Panel Shows Contribution, Attribution And Peak Concurrency

For each successful kind the screen MUST render a diagnostics panel with: a per-member table (in-window
and scalable trades, net contribution at the low and the high end, raised-to-minimum, capped and
Unscalable counts); the first-breach attribution aggregated across starts (per member, the starts credited
by phase and its sole-contributor starts, plus the kind's shared-close starts), with counts beside shares;
and the peak concurrent open positions with the members at the peak. Missing values MUST render as
explicitly absent, never `0`. Shared-close attribution MUST be labelled as tied. The panel MUST NOT show
correlation.

#### Scenario: The per-member table is shown
- GIVEN diagnostics for three members
- WHEN rendered
- THEN three rows show trades, net, raised and capped counts

#### Scenario: Breach attribution shows counts beside shares
- GIVEN an aggregate with member sole-contributor counts and a shared-close (tied) count
- WHEN rendered
- THEN each member's count and share is shown, plus the tied count labelled as tied

#### Scenario: Peak concurrency is shown
- GIVEN peak concurrent positions of 3 reached with members A, B and C
- WHEN rendered
- THEN the value 3 is shown with its label and the three members are named

### Requirement: Disclosures Are Visible And Translated

A translated disclosure block MUST be visible at all times. Once a result is shown, ONE translated group
disclosure, rendered once for the whole result, MUST state that: concurrent-position breaches dominate in
a group so the Clean/Contingent split loses its usual meaning; same-close ordering is a modelling choice
that can create or remove a breach; funding-programme eligibility rules are not modelled. The reused
`DISCLOSURE_RESULT` and `NotModelled` rendering of `ftmo-simulation-ui` MUST also appear once. Server
disclosure text (the envelope's `Disclosures`) MUST NOT be rendered. The `DuplicateNameWarnings` MUST be
shown, naming the members.

#### Scenario: The disclosure is visible before any run
- GIVEN no run has completed
- WHEN the screen is rendered
- THEN the short disclosure block is visible without interaction

#### Scenario: The group disclosure covers three points, once
- GIVEN a result
- WHEN rendered in EN or ES
- THEN the group disclosure appears once and mentions concurrent breaches, same-close ordering and
  unmodelled eligibility rules

#### Scenario: A duplicate-name warning is shown
- GIVEN a response warning two members share a name
- WHEN rendered
- THEN the warning names both members

### Requirement: Every String Is In i18n EN/ES With No Banned Wording

Every user-facing string of this screen (labels, hints, refusal reasons, errors, disclosures, diagnostics
headings, readout text, sidebar labels) MUST come from an i18n key present in both
`public/assets/i18n/en.json` and `public/assets/i18n/es.json`, with no hardcoded user-facing string in
templates or TypeScript. The screen's keys MUST live in the new `SIMULATOR` namespace (`SIMULATOR.NAV.*`,
`SIMULATOR.FTMO_GROUP.*`); the reused panels keep their `FTMO_SIMULATION.*` keys. Tests MUST load the real
`en.json` and `es.json` and assert rendered text, never echoed keys. No key of this capability, EN or ES, MUST contain "passed", "safe", "survived",
"would have passed", "aprobado", "aprobó", "seguro", "sobrevivió" or "habría aprobado".

#### Scenario: Keys exist in both languages
- GIVEN the screen's key set
- WHEN EN and ES files are compared
- THEN every key exists in both

#### Scenario: A banned-wording test passes over the screen's keys
- GIVEN the EN and ES key sets of this screen
- WHEN each text is inspected
- THEN none contains a banned word

#### Scenario: No hardcoded visible string
- GIVEN the screen rendered in EN and ES
- WHEN visible strings are inspected
- THEN each resolves to an i18n key

### Requirement: Every Enum Is Mapped By Exact Value, Never By Truthiness

Every enum label mapper of this screen (`FtmoGroupRefusal`, `FtmoSimulationRefusal`,
`FtmoSimulationStatus`, `FtmoChainOutcome`, breach limit and any new group enum) MUST switch on the exact
numeric value (the TS `FtmoGroupRefusal` declares explicit numbers matching the backend) and use
`!== null` for presence, never a truthy check. View-model state uses string discriminants, not enum values. A zero-valued member MUST render its
correct label. An unrecognised value MUST render an explicit UNKNOWN label showing the raw value, never a
raw key or a `{{` placeholder.

#### Scenario: A zero-valued refusal renders
- GIVEN a refusal whose enum value is `0` (for example `InvalidRequest`)
- WHEN rendered
- THEN its label is shown, not treated as absent

#### Scenario: An unknown value shows the raw value
- GIVEN an enum value with no mapping
- WHEN rendered
- THEN the UNKNOWN label shows the raw value and no `{{` is visible

## Non-Goals

Automatic combination generation or ranking, live data, Darwinex Zero or Axi Select rules, per-member
risk, persistence or "save as portfolio", correlation, eligibility-rule modelling, and any change to the
single-strategy FTMO modal.

## Open Questions

- How the account `SBDEMO2` is identified in the picker (by account name) is assumed, not verified.
