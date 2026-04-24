# Delta for Strategy Model

## ADDED Requirements

### Requirement: R-M1 — MagicNumber Field on Strategy

The system MUST add a nullable `int? MagicNumber` field to the `Strategy` entity.
A filtered unique index on `(TradingAccountId, MagicNumber)` WHERE both columns are non-null
MUST be applied so the same magic number is allowed across different accounts but is unique
within one account. A `null` value is valid — it means the strategy does not yet participate
in trade imports.

#### Scenario: Two strategies on the same account cannot share a MagicNumber

- GIVEN two strategies belong to the same `TradingAccountId`
- WHEN both are assigned `MagicNumber=2333376`
- THEN the second save raises a unique-constraint violation

#### Scenario: Same MagicNumber is allowed across different accounts

- GIVEN strategy A on account 1 and strategy B on account 2, both with `MagicNumber=2333376`
- WHEN both are persisted
- THEN no constraint violation occurs

#### Scenario: Multiple strategies can have MagicNumber=null on the same account

- GIVEN three strategies on the same account all have `MagicNumber=null`
- WHEN all are persisted
- THEN no constraint violation occurs (null is excluded from the unique index)

---

### Requirement: R-M2 — MagicNumber Input in AddStrategyModal

The `AddStrategyModal` frontend component MUST include an optional numeric input for `MagicNumber`.
The field MUST accept only positive integers or be left empty. An empty field MUST be submitted
as `null` — not as `0`.

#### Scenario: User enters a valid magic number

- GIVEN the user types `2333376` into the MagicNumber field and submits
- WHEN the form is submitted
- THEN the strategy is created with `magicNumber: 2333376`

#### Scenario: User leaves MagicNumber blank

- GIVEN the user leaves the MagicNumber field empty and submits
- WHEN the form is submitted
- THEN the strategy is created with `magicNumber: null`

#### Scenario: User enters a non-numeric value

- GIVEN the user types `abc` into the MagicNumber field
- WHEN the form is submitted
- THEN the field shows a validation error and submission is blocked

---

### Requirement: R-M3 — MagicNumber in StrategyDto Response

The `StrategyDto` MUST include a `magicNumber: int | null` field in all endpoints that return
strategy data.

#### Scenario: Strategy with MagicNumber set is reflected in DTO

- GIVEN a strategy with `MagicNumber=7112021`
- WHEN any endpoint returns this strategy as `StrategyDto`
- THEN the response includes `"magicNumber": 7112021`

#### Scenario: Strategy with null MagicNumber is reflected in DTO

- GIVEN a strategy with `MagicNumber=null`
- WHEN any endpoint returns this strategy as `StrategyDto`
- THEN the response includes `"magicNumber": null`
