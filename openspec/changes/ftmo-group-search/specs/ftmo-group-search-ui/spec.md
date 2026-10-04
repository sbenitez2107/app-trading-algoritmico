# FTMO Group Search UI Specification

## Purpose

`ftmo-group-search` finds and ranks 2..4-member groups on the backend. This capability is the screen that
drives it: the user sets the pool and constraints, starts a background search, watches progress or cancels,
reads the ranked groups as **elimination risk, not certification**, sees the speed-versus-elimination
tradeoff on a scatter, and opens any group in the group simulator through a deep link.

It builds on, and does not restate, `ftmo-group-simulation-ui` (sidebar group, i18n namespace practice,
banned wording, exact-value enum mapping) and `ftmo-simulation-ui`. It does not edit the group-simulator
charts, which belong to the next change.

## Requirements

### Requirement: The Route And Sidebar Entry

The app MUST expose `/simulator/ftmo/search`, a top-level route reachable only to authenticated users, and
the sidebar's "Simulator" group MUST contain a second entry that opens it. Labels MUST come from i18n.

#### Scenario: The sidebar opens the search page
- GIVEN an authenticated user
- WHEN the user activates the search entry under Simulator
- THEN the app navigates to `/simulator/ftmo/search`

#### Scenario: The existing group entry is unchanged
- GIVEN the sidebar
- WHEN rendered
- THEN the `/simulator/ftmo` entry still opens the group simulator

#### Scenario: Labels are translated
- GIVEN the app in ES
- WHEN the sidebar is rendered
- THEN the search entry shows its ES text

### Requirement: The Search Form And Its Defaults

The form MUST contain: the account (default SBDEMO2, else the first account); an optional symbol filter of
the pool; group size min and max (defaults 2 and 4, both bounded to [2, `MaxMembers`], with `MaxMembers`
taken from the candidates read and never hardcoded); `targetRiskPerTrade` (required, no prefill); max
strategies per instrument (default 1, minimum 1); exclude identical Deploy/Eval (default on); the academy 1%
rule (default off); the elimination ceiling (default 5%); `initialCapital` (default `10000`); FX band inputs
shown only when the pool contains a non-USD strategy; and the optional budget. Broker (`FTMO`) and the source
lot grid MUST come from the same constants as the group simulator and not be edited here. Start MUST be
disabled until a valid risk and capital hold, min <= max, and no job is running.

#### Scenario: Defaults are prefilled
- GIVEN the page opens
- WHEN the form is rendered
- THEN max per instrument shows 1, identical is excluded, the 1% rule is off, size is 2 to 4, the ceiling is
  5%, capital is `10000`, and risk is empty

#### Scenario: Start is disabled without a risk
- GIVEN an empty risk
- WHEN Start is viewed
- THEN Start is disabled

#### Scenario: The size bound follows the backend cap
- GIVEN the candidates read reports `MaxMembers = 4`
- WHEN the user enters a maximum of 5
- THEN it is not accepted and the bound is stated

#### Scenario: FX inputs are conditional
- GIVEN a pool with only USD strategies
- WHEN the form is rendered
- THEN no FX inputs are shown, and none are sent

### Requirement: Progress And Cancel Are Visible

After Start the screen MUST poll the job and show a progress bar and the funnel counts reported by the
backend (examined, removed at each stage, full simulations done and planned, elapsed). A Cancel control MUST
be shown while the job runs and MUST issue the cancel call. Terminal states MUST be shown distinctly with
translated text: completed, stopped at budget (naming the limit and how many were not computed), cancelled,
failed. Polling MUST stop at a terminal state and when the screen is left; leaving MUST NOT cancel the job, and on return the screen MUST re-attach through `GET group-search/current`. If a start answers 409, the screen
MUST attach to the running job and say so. A 404 on poll MUST be shown as a lost job (API restarted) with the
option to start again. Partial ranked results MUST be shown during a run and after a budget stop or cancel.

#### Scenario: Progress updates while running
- GIVEN a running job
- WHEN polls return increasing counts
- THEN the bar and counts update and Cancel is enabled

#### Scenario: Cancel stops polling and keeps partial results
- GIVEN a running job with partial results
- WHEN the user cancels
- THEN the state shows cancelled, polling stops, and the partial table remains

#### Scenario: A budget stop is labelled
- GIVEN a job in `StoppedAtBudget`
- WHEN rendered
- THEN a translated message names the limit hit and the not-computed count, and it is not shown as completed

#### Scenario: A second start attaches to the running job
- GIVEN the start answers 409 with a job id
- WHEN received
- THEN the screen shows that job's progress with a notice

#### Scenario: A lost job is explained
- GIVEN a poll answering 404
- WHEN received
- THEN a translated notice says the job was lost on restart and Start is enabled

#### Scenario: Leaving the screen stops polling and does not cancel
- GIVEN a running job
- WHEN the screen is destroyed
- THEN polling stops and no cancel is sent, and the job keeps running on the backend (user decision 2026-10-04)

#### Scenario: Returning re-attaches to the running job
- GIVEN a job still running after the user left the screen
- WHEN the screen is opened again
- THEN it reads `GET group-search/current`, shows that job's progress and results, and resumes polling

#### Scenario: Returning with no job shows the empty form
- GIVEN `GET group-search/current` answers 204
- WHEN the screen is opened
- THEN the form is shown with no progress and no table

### Requirement: The Ranked Table Shows Both Kinds And Highlights The Elimination Ceiling

The table MUST list candidates in the backend's rank order (the UI MUST NOT re-sort the default view). Per
row it MUST show, for Deploy and for Evaluation separately and never merged: breach share, headroom (worst
daily loss and worst drawdown, with the median drawdown), funded no-breach share, median days to both
targets; plus members and symbols and peak concurrency. A not-evaluated kind MUST show its refusal reason, not
blanks or `0`. Rows whose worse-of-kinds breach share is below the elimination ceiling MUST be highlighted,
and the user MUST be able to filter to them; the ceiling is editable and defaults to 5%. Rows flagged for
identical Deploy/Eval MUST show the flag. Each row MUST offer the deep link to the group simulator.

#### Scenario: Both kinds are shown per row
- GIVEN a ranked candidate valid on both kinds
- WHEN rendered
- THEN Deploy and Evaluation columns each show their own shares, headroom and median days

#### Scenario: The ceiling highlights by the worse kind
- GIVEN a row with Deploy breach share 0.03 and Evaluation 0.06, and a 5% ceiling
- WHEN rendered
- THEN the row is not highlighted, and with Evaluation 0.04 it is

#### Scenario: Changing the ceiling re-evaluates the highlight
- GIVEN a displayed table
- WHEN the user sets the ceiling to 10%
- THEN highlighting and the filter update without a request

#### Scenario: A refused kind shows its reason
- GIVEN a candidate whose Evaluation is refused
- WHEN rendered
- THEN the Evaluation cells show the translated refusal reason, not `0` or blank

#### Scenario: Null medians render as absent
- GIVEN a median days value of null
- WHEN rendered
- THEN an explicit absent marker is shown, not `0`

### Requirement: Fractions Are Converted To Percent At The Display Boundary

Shares, headroom values and the ceiling arrive and compare as fractions (0.05 = 5%). The UI MUST multiply
by 100 exactly once, at display, and MUST compare the ceiling against raw fractions. No value MUST be
shown as a percent twice or compared in mixed units.

#### Scenario: A fraction renders as a percent
- GIVEN a breach share of 0.045
- WHEN rendered
- THEN it shows 4.5%

#### Scenario: The ceiling comparison uses one unit
- GIVEN a ceiling input of 5 and a breach share of 0.049
- WHEN highlighting is computed
- THEN the row is highlighted

#### Scenario: Headroom is not scaled twice
- GIVEN worst drawdown 0.8 of the allowance
- WHEN rendered
- THEN it shows 80%, not 8000%

### Requirement: The Scatter Plots Speed Against Elimination And Opens A Group

The screen MUST render a scatter of the evaluated candidates with x = median days to both targets and y =
breach share (the worse-of-kinds value), in the same unit conventions as the table. Candidates on the
Pareto frontier (no other candidate is better on both axes) MUST be highlighted. Candidates with a null
median MUST NOT be plotted and their count MUST be stated. Clicking a point MUST open that group via the
deep link. The ceiling MUST be drawn or the points below it distinguished. Axis labels MUST be translated.

#### Scenario: Points are placed by the two metrics
- GIVEN a candidate with median 120 days and breach share 0.04
- WHEN plotted
- THEN it sits at x = 120 and y = 4%

#### Scenario: The frontier is highlighted
- GIVEN candidates (100 d, 0.05), (120 d, 0.03) and (130 d, 0.04)
- WHEN plotted
- THEN the first two are frontier points and the third is not

#### Scenario: A null-median candidate is not plotted
- GIVEN a candidate with a null median
- WHEN plotted
- THEN it is omitted and the omitted count is shown

#### Scenario: Clicking a point opens the group simulator
- GIVEN a plotted candidate
- WHEN the user clicks its point
- THEN the app navigates to `/simulator/ftmo` with that group's deep link

### Requirement: The Deep Link Carries The Group Into The Group Simulator

Opening a group from a row or a point MUST navigate to `/simulator/ftmo` with query parameters `account`,
`members` (comma-separated ids in ascending `StrategyId` order), `risk` (the search's risk), and `capital`
and the FX band when set, as accepted by `ftmo-group-simulation-ui`. Navigation MUST NOT run the simulation.

#### Scenario: The link carries the search inputs
- GIVEN a result for members A, B at risk 25 and capital 10000
- WHEN its link is opened
- THEN the URL has `account`, `members=A,B`, `risk=25` and `capital=10000`

#### Scenario: Opening does not run
- GIVEN the group page opened from the link
- WHEN it loads
- THEN members and risk are preselected and no run request is sent

### Requirement: Disclosures Are Visible And Translated

A translated disclosure block MUST be visible at all times and, once a result exists, MUST state: "N groups
examined" with N from the response, and that choosing the best of many backtests is optimistic (selection
bias); the figures are elimination risk on closed-trade daily aggregates, not certification or forecast;
the group disclosures of the group simulator; and that results are lost on API restart. Server disclosure
text MUST NOT be rendered. The excluded-strategy counts by reason MUST be shown.

#### Scenario: The disclosure is visible before any search
- GIVEN no search has run
- WHEN the page is rendered
- THEN the short disclosure is visible

#### Scenario: N and selection bias appear with a result
- GIVEN a response that examined 12,926 groups
- WHEN rendered in EN or ES
- THEN the disclosure shows 12,926 and the selection-bias statement, once

#### Scenario: Exclusions are listed by reason
- GIVEN exclusions of two reasons
- WHEN rendered
- THEN each reason and its count is shown

### Requirement: Every String Is In i18n EN/ES With No Banned Wording

Every user-facing string MUST come from a key present in both `public/assets/i18n/en.json` and `es.json`,
in a `SIMULATOR.FTMO_SEARCH.*` namespace (plus `SIMULATOR.NAV.*`), with no hardcoded string. Tests MUST load
the real files and assert rendered text. No key, EN or ES, MUST contain "passed", "safe", "survived", "would
have passed", "aprobado", "aprobo", "seguro", "sobrevivio" or "habria aprobado", and the funded no-breach
share MUST NOT be labelled survival ("supervivencia").

#### Scenario: Keys exist in both languages
- GIVEN the screen's key set
- WHEN EN and ES are compared
- THEN every key exists in both

#### Scenario: Banned wording is absent
- GIVEN every EN and ES text of the screen
- WHEN inspected
- THEN none contains a banned word

### Requirement: Every Enum Is Mapped By Exact Value, Never By Truthiness

The job status, stop reason, exclusion reason and every reused refusal enum MUST be mapped by switching on the
exact numeric value, with `!== null` for presence. A zero-valued member MUST render its own label and never
read as completed or absent. An unknown value MUST render an UNKNOWN label with the raw value, never a raw
key or a `{{` placeholder.

#### Scenario: A zero-valued status renders its label
- GIVEN a job status whose value is `0`
- WHEN rendered
- THEN its own translated label is shown and it is not treated as completed or absent

#### Scenario: An unknown value shows the raw value
- GIVEN a status or reason with no mapping
- WHEN rendered
- THEN the UNKNOWN label shows the raw value and no `{{` is visible

## Non-Goals

The risk grid (50/75/100 re-run); a null model; persistence, history or "save as portfolio"; live data;
Darwinex Zero and Axi Select; per-member risk; editing the broker or lot grid; the group-simulator charts
(equity curve, daily P&L, headroom panel), which are the next change `ftmo-group-simulator-charts`.

## Open Questions

- The scatter technology (`lightweight-charts`, SVG or CSS) is a design decision; the spec fixes only the
  observable axes, frontier highlight and click behaviour.
- Poll interval and the table's page size are design decisions.
