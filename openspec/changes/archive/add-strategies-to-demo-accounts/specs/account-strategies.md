# Account Strategies Specification

## Purpose

Define behavior for managing strategies attached directly to a trading account,
bypassing the SQX pipeline. Covers listing, uploading, and displaying strategies
scoped to a specific Darwinex demo account.

---

## Requirements

### Requirement: List Strategies by Account

The system MUST expose an endpoint that returns all strategies associated with a
given trading account ID. The response MUST be a paginated array (may be empty).

#### Scenario: Account has strategies

- GIVEN a trading account with ID `acc-1` exists and has 3 strategies
- WHEN `GET api/trading-accounts/acc-1/strategies?page=1&pageSize=20` is called
- THEN the response is `200 OK` with an array of 3 `StrategyDto` objects

#### Scenario: Account exists but has no strategies

- GIVEN a trading account with ID `acc-2` exists and has no strategies attached
- WHEN `GET api/trading-accounts/acc-2/strategies?page=1&pageSize=20` is called
- THEN the response is `200 OK` with an empty array `[]`

#### Scenario: Account does not exist

- GIVEN no trading account with ID `acc-999` exists in the system
- WHEN `GET api/trading-accounts/acc-999/strategies` is called
- THEN the response is `404 Not Found`

---

### Requirement: Upload Strategy to Account

The system MUST accept a `POST` to `api/trading-accounts/{accountId}/strategies`
with a multipart body containing: `name` (string), `sqxFile` (.sqx), and
`htmlFile` (.html). Both files MUST be present. The system SHALL parse the HTML
report via `IHtmlReportParserService`, parse the `.sqx` content via
`ISqxParserService`, persist a new `Strategy` with `TradingAccountId` set and
`BatchStageId` null, and return the created `StrategyDto` with `201 Created`.

#### Scenario: Valid upload with both files

- GIVEN a demo account `acc-1` exists
- WHEN `POST api/trading-accounts/acc-1/strategies` is called with `name`, a valid `.sqx` file, and a valid `.html` report
- THEN the response is `201 Created` with the new `StrategyDto` containing parsed KPIs
- AND the persisted `Strategy` has `TradingAccountId = acc-1` and `BatchStageId = null`

#### Scenario: Missing HTML file

- GIVEN a demo account `acc-1` exists
- WHEN `POST api/trading-accounts/acc-1/strategies` is called with `name` and `.sqx` file only (no `htmlFile`)
- THEN the response is `400 Bad Request` with a validation error indicating both files are required

#### Scenario: Missing SQX file

- GIVEN a demo account `acc-1` exists
- WHEN `POST api/trading-accounts/acc-1/strategies` is called with `name` and `.html` file only (no `sqxFile`)
- THEN the response is `400 Bad Request` with a validation error indicating both files are required

#### Scenario: HTML file is unparseable

- GIVEN a demo account `acc-1` exists
- WHEN `POST api/trading-accounts/acc-1/strategies` is called with a valid `.sqx` and a malformed `.html` that causes `IHtmlReportParserService` to return null
- THEN the response is `400 Bad Request` with a message indicating the HTML report could not be parsed

#### Scenario: Account does not exist on upload

- GIVEN no trading account with ID `acc-999` exists
- WHEN `POST api/trading-accounts/acc-999/strategies` is called with valid files
- THEN the response is `404 Not Found`

---

### Requirement: Frontend Route for Demo Account Detail

The frontend MUST register a lazy-loaded child route `/darwinex/demo/:accountId`
that renders `AccountDetailComponent`. The component MUST use `OnPush` change
detection and Angular Signals for state.

#### Scenario: Navigate to a valid account

- GIVEN the user is on the demo accounts list and account `acc-1` exists
- WHEN the user clicks the `acc-1` row
- THEN the router navigates to `/darwinex/demo/acc-1`
- AND `AccountDetailComponent` loads and fetches strategies for `acc-1`

#### Scenario: Route with non-existent accountId

- GIVEN no account with ID `acc-999` exists
- WHEN the user navigates directly to `/darwinex/demo/acc-999`
- THEN the component displays an error state (not a crash or blank screen)

---

### Requirement: Row Click Navigates to Account Detail

`AccountsListComponent` (demo context) MUST navigate to `/darwinex/demo/:accountId`
when a row is clicked. This MUST NOT trigger when clicking action buttons (edit,
toggle, delete).

#### Scenario: Row click on demo accounts list

- GIVEN the demo accounts list shows account `acc-1`
- WHEN the user clicks anywhere on the `acc-1` row (excluding action buttons)
- THEN the router navigates to `/darwinex/demo/acc-1`

---

### Requirement: Strategy Grid Exposes All KPIs with Column Management

`AccountDetailComponent` MUST render a grid built on `ag-grid-community` that
exposes EVERY KPI column from `StrategyDto` (Name, backtest metadata, and all
~50 KPI fields). The user MUST be able to:

- **Show/hide columns**: toggle any column's visibility via a column picker UI
  (custom sidebar or menu, since the ag-grid tool panel is an Enterprise
  feature). The initial default visible set SHOULD be: Name, TotalProfit,
  WinningPercentage, ProfitFactor, Drawdown, NumberOfTrades, SharpeRatio.
  All other columns default to hidden.
- **Reorder columns**: drag-and-drop column headers (ag-grid default behavior,
  `suppressMovableColumns` MUST NOT be set).
- **Filter per column**: apply column-level filters using ag-grid community
  filter types (`agTextColumnFilter` for strings, `agNumberColumnFilter` for
  numeric KPIs).

Column state (visibility, order, filter) is NOT required to persist across
sessions in this change; resets on reload are acceptable.

#### Scenario: Grid renders default visible KPIs

- GIVEN `acc-1` has 2 strategies with KPI data
- WHEN `AccountDetailComponent` finishes loading
- THEN the grid shows 2 rows with the default visible columns populated: Name, TotalProfit, WinningPercentage, ProfitFactor, Drawdown, NumberOfTrades, SharpeRatio
- AND null KPI values are displayed as an empty cell (no crash)

#### Scenario: Grid exposes all KPI columns

- GIVEN the strategy grid is loaded
- WHEN the user opens the column picker UI
- THEN every KPI field from `StrategyDto` is listed as a toggleable column (~50 columns)

#### Scenario: User toggles column visibility

- GIVEN the column picker is open and `YearlyAvgProfit` is hidden by default
- WHEN the user enables `YearlyAvgProfit` in the picker
- THEN the grid adds the `YearlyAvgProfit` column in its configured position
- AND values are populated for all rows (null → empty cell)

#### Scenario: User reorders columns via drag

- GIVEN the grid shows `Name | TotalProfit | WinningPercentage`
- WHEN the user drags the `WinningPercentage` header left of `TotalProfit`
- THEN the grid header order becomes `Name | WinningPercentage | TotalProfit`

#### Scenario: User filters a numeric column

- GIVEN the grid shows 10 strategies with varying `ProfitFactor`
- WHEN the user applies the ag-grid filter `ProfitFactor >= 1.5`
- THEN only rows with `ProfitFactor >= 1.5` remain visible
- AND the original data is preserved (filter can be cleared)

---

### Requirement: Add Strategy Modal Validates Both Files Before Submit

`AddStrategyModalComponent` MUST disable the submit button and SHOULD display a
validation message if either the `.sqx` or `.html` file is not selected. The
submit button MUST only be enabled when both files and a non-empty name are
present.

#### Scenario: Submit blocked with missing file

- GIVEN the Add Strategy modal is open
- WHEN the user selects only the `.sqx` file and leaves the `.html` file empty
- THEN the submit button is disabled
- AND a validation message is shown indicating both files are required

#### Scenario: Submit enabled with all inputs

- GIVEN the Add Strategy modal is open
- WHEN the user provides a name, selects a `.sqx` file, and selects a `.html` file
- THEN the submit button is enabled
