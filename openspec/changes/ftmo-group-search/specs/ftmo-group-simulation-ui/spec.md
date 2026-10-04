# Delta for FTMO Group Simulation UI

## ADDED Requirements

### Requirement: The Group Page Accepts A Deep Link That Preselects Members And Risk

The route `/simulator/ftmo` MUST read these optional query parameters on load: `account` (a trading account
id), `members` (comma-separated strategy ids), `risk` (the `targetRiskPerTrade` amount, a plain decimal
number in the same currency units as the form field), and optionally `capital`, `fxLow` and `fxHigh`. When
`account` resolves to a known account, the page MUST select it instead of the default; when `members` are
present they MUST be preselected within that account's candidates; when `risk` is a valid positive number it
MUST fill the risk field; `capital` and the FX band fill their fields under the same validity rule. The page
MUST NOT send a run request because of a deep link: Run stays an explicit user action, and the preselected
state is only a form state the user can edit. Ids are matched against the candidates of the selected account
only. Handling of bad input:

- an unknown or malformed id MUST be ignored, the valid ids kept, and a translated notice MUST name the
  count ignored;
- more ids than `MaxMembers` (the cap from the candidates read, never a hardcoded number) MUST keep the
  first `MaxMembers` in link order, ignore the rest, and show a translated notice stating the cap;
- repeated ids MUST count once;
- an unknown, malformed or missing `account` MUST fall back to the default account, and `members` MUST then
  be ignored with a translated notice (ids of another account are never selected);
- a risk that is missing, non-numeric, zero or negative MUST leave the field empty, and a notice MUST say
  the risk was not applied;
- an empty `members` MUST leave the selection empty.

Deep-link parameters are applied once on load; later user edits MUST NOT be overwritten.

#### Scenario: A valid link preselects and does not run
- GIVEN the link `/simulator/ftmo?account=X&members=A,B,C&risk=25` with A, B, C candidates of account X
- WHEN the page opens
- THEN account X is selected, A, B and C are selected, risk shows 25, and no run request is sent

#### Scenario: Unknown ids are ignored with a notice
- GIVEN `members=A,ZZZ` where ZZZ is not a candidate of the account
- WHEN the page opens
- THEN only A is selected and a notice states one id was ignored

#### Scenario: A list over the cap is truncated visibly
- GIVEN `members` listing `MaxMembers + 2` valid ids
- WHEN the page opens
- THEN the first `MaxMembers` ids are selected and a notice states the cap

#### Scenario: An unknown account falls back and drops members
- GIVEN `account` that matches no account and `members` listing ids
- WHEN the page opens
- THEN the default account is selected, the selection is empty, and a notice explains why

#### Scenario: An invalid risk is not applied
- GIVEN `risk=-5` or `risk=abc`
- WHEN the page opens
- THEN the risk field is empty, Run stays disabled until the user types a risk, and a notice says it was not applied

#### Scenario: Repeated ids count once
- GIVEN `members=A,A,B`
- WHEN the page opens
- THEN A and B are selected and k is 2

#### Scenario: Editing after a deep link is respected
- GIVEN a page opened from a deep link
- WHEN the user deselects a member and changes the risk
- THEN the edits stand and the link parameters are not reapplied

## MODIFIED Requirements

### Requirement: The Picker Is Scoped To One Account And Offers Filters And Search

The picker MUST list the candidates of exactly ONE account at a time, read from
`GET api/ftmo-simulations/candidates?tradingAccountId=`. The account list comes from the trading accounts
read, and the default is the account named `SBDEMO2` when it exists, otherwise the first account, unless a
valid deep-link `account` parameter selects another (see the deep-link requirement). Switching
account MUST reload the candidates and clear the current selection. The picker MUST let the user filter by
symbol and search by name, and MUST allow multi-selection. Selection changes MUST NOT trigger a run request.
(Previously: the default account was always SBDEMO2 or the first account, with no deep-link override.)

#### Scenario: SBDEMO2 is the default account
- GIVEN an account named SBDEMO2 exists and no deep-link account
- WHEN the screen opens
- THEN SBDEMO2 is selected and its candidates are listed

#### Scenario: Without SBDEMO2 the first account is selected
- GIVEN no account is named SBDEMO2 and no deep-link account
- WHEN the screen opens
- THEN the first account is selected and its candidates are listed

#### Scenario: A deep-link account overrides the default
- GIVEN SBDEMO2 exists and the link names another valid account
- WHEN the screen opens
- THEN the linked account is selected, not SBDEMO2

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

## Non-Goals

Auto-running from a deep link, persisting the link's state, and the group-simulator charts (the next change
`ftmo-group-simulator-charts`).

## Open Questions

- Whether the group page should also accept the source lot-grid in the link; this spec fixes it to the
  shared `LotGrid.ImoxRetester` constant on both pages.
