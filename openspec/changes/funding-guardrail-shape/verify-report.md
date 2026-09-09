# Verification Report: funding-guardrail-shape

**Verdict: FAIL** (1 CRITICAL, 3 WARNING, 1 SUGGESTION)

## Task completeness
All 33 checklist items (26 numbered tasks, phases 1-5) in tasks.md are checked `[x]`. Confirmed against code state -- no phantom checkmarks found; every referenced file/class exists.

## Build & test evidence (independently executed, not trusted from apply-report)
| Command | Result |
|---|---|
| `cd app.trading.algoritmico.api && dotnet build -warnaserror` | Build succeeded. 0 Warning(s), 0 Error(s). Exit 0. |
| `dotnet test` (full backend suite) | Passed! Failed: 0, Passed: 592, Skipped: 0, Total: 592. Exit 0. |
| `pnpm --dir app.trading.algoritmico.web test` | Test Files 31 passed (31); Tests 392 passed (392). Exit 0. |
| `dotnet ef migrations script 0 ReshapeFundingGuardrails --idempotent` | Generates cleanly (no DB touched); confirms Up() is schema-only DDL (CREATE TABLE, ADD COLUMN, ALTER COLUMN) with zero DML. |

All three apply-report claims (592/592, 392/392, 0 warnings/0 errors) are CONFIRMED by direct re-execution, not just trusted.

## Regression fence
`git status`/`git diff --stat` confirms the three named golden-regression files (`PortfolioAnalyticsCalculatorTests`, `PortfolioAnalyticsCalculatorLiveOutputRegressionTests`, `BacktestPortfolioAnalyticsAdapterTests`) do not appear in the changed-file list at all -- untouched. The three touched fence-adjacent test files (`BrokerRiskLimitsTests.cs`, `PortfolioServiceRiskTests.cs`, `RiskLimitsServiceTests.cs`) show exactly 299 insertions(+), 0 deletions(-) -- confirmed via `git diff --stat`. Read the actual diff for `BrokerRiskLimitsTests.cs`: the only change is one new `[Fact]` appended at the end of the class; no existing assertion body was altered. Additive-only confirmed.

## Load-bearing design invariants -- all CONFIRMED in delivered code
1. Migration `Up()` is schema-only. Read `20260909035101_ReshapeFundingGuardrails.cs`: `Up()` contains only `AlterColumn` (nullable), `AddColumn` (FtmoProduct), `CreateTable`/`CreateIndex` (FundingStageLimits) -- zero `Sql()`/DML calls. `Down()` runs `UPDATE ... SET DrawdownModel = 0 WHERE DrawdownModel IS NULL` before restoring `NOT NULL`, then drops `FtmoProduct` and `FundingStageLimits`. Confirmed via `dotnet ef migrations script` (idempotent, no DB touch) generating cleanly.
2. `RulebookMismatch` and `BreachBasis` are computed properties, not constructor/positional parameters -- confirmed in `BrokerRiskLimitsDto.cs` (`public bool RulebookMismatch { get { ... } }`) and `PortfolioAnalyticsDto.cs` (`public BreachBasis? BreachBasis => Kind switch { ... }`). Neither appears in either record's positional parameter list.
3. `BreachBasis` null for `VarTarget`, `ClosedTradeLowerBound` for `LossLimits`/`StagedLossLimits` -- confirmed by the switch expression and by `GetRiskAsync_VarTargetGuardrail_BreachBasisIsNull` / `GetRiskAsync_LossLimitsGuardrail_GoldenRegression...` / `GetRiskAsync_StagedLossLimitsGuardrail_EmitsNoDailyHeadroomOrBreach` tests, all passing.
4. Positional records append new members with defaults -- confirmed: `BrokerRiskLimitsDto`/`UpsertBrokerRiskLimitsDto` append `FtmoProduct? FtmoProduct = null, IReadOnlyList<...>? Stages = null` at the end of the parameter list; `ServiceGuardrailDto` adds zero new positional parameters (BreachBasis is 100% computed).
5. `DrawdownModel` on a `VarTarget` upsert is normalized to null, not rejected -- confirmed in `RiskLimitsService.UpsertAsync`: `normalizedDrawdownModel = dto.Kind == GuardrailKind.LossLimits ? dto.DrawdownModel : (DrawdownModel?)null;`, pinned by `UpsertAsync_VarTargetCarryingDrawdownModelTrailing_PersistsNull` (passing).
6. `FtmoProduct` optional on write; OneStep-requires-Trailing / TwoStep-requires-Static fires only when non-null -- confirmed in `GuardrailShape.IsFtmoProductConsistent`: `if (product is null) return true;`. Pinned by `GuardrailShapeTests.IsFtmoProductConsistent_NullProduct_AlwaysTrue` and `RiskLimitsServiceTests.UpsertAsync_FtmoWithNullProduct_Accepted`.

## Spec scenario coverage matrix (every scenario in the delta spec)

### FundingService Enum Definition
- Row carries a FundingService -- UNPINNED (no test asserts `result.FundingService` after a round-trip upsert; only incidentally exercised). SUGGESTION -- low risk, trivial persistence of an already-required column.

### Kind Bound to FundingService
- Mismatched write rejected -- `UpsertAsync_DarwinexZeroWithLossLimitsKind_Rejected`, `UpsertAsync_AxiWithLossLimitsKind_Rejected`, `UpsertAsync_FtmoWithVarTargetKind_Rejected` -- PASS
- Legacy mismatched row flagged, not broken, on read -- `GetByBrokerAsync_LegacyAxiLossLimitsRow_FlagsMismatchWithoutThrowing` -- PASS

### FtmoProduct Discriminator (explicitly requested scrutiny)
- OneStep with Static rejected -- `UpsertAsync_FtmoOneStepWithStatic_Rejected` -- PASS, real assertion (`ThrowAsync<ArgumentException>`)
- Ftmo payload without a product accepted -- `UpsertAsync_FtmoWithNullProduct_Accepted` -- PASS, real assertion (`NotThrowAsync`)
- Ftmo row without a product flagged on read -- `GetByBrokerAsync_FtmoRowWithNullProduct_FlagsMismatch` -- PASS, real assertion (`RulebookMismatch.Should().BeTrue()`)

All three pinned by non-trivial, behavior-exercising tests. No issue found here.

### StagedLossLimits Stage Rulebook
- Six stage rows persisted -- `UpsertAsync_StagedLossLimitsSixStages_PersistsAllSix` -- PASS
- Duplicate stage ordinal rejected -- `UpsertAsync_StagedLossLimitsDuplicateOrdinal_Rejected` -- PASS

### Closed-Trade Lower-Bound Disclosure
- FTMO breach readout labelled a lower bound -- `GetRiskAsync_LossLimitsGuardrail_GoldenRegression_HeadroomAndBreachUnchanged` asserts `guard.BreachBasis.Should().Be(BreachBasis.ClosedTradeLowerBound)` -- PASS
- VarTarget carries no breach basis -- `GetRiskAsync_VarTargetGuardrail_BreachBasisIsNull` -- PASS

### Non-Destructive Reshape Migration
- Existing Axi LossLimits row survives unresolved -- PARTIALLY PINNED. No test literally executes `Up()`/`Down()` against a real database (EF InMemory provider does not run migrations, and the project's own rule forbids unauthorized DB connections). The equivalent post-migration row shape is exercised by `GetByBrokerAsync_LegacyAxiLossLimitsRow_FlagsMismatchWithoutThrowing`, and `Up()` was independently confirmed schema-only by source read + `dotnet ef migrations script`. Reported as WARNING (structurally sound, not literally migration-executed) rather than CRITICAL, given the environment constraint is a hard project rule, not a shortcut.

### GuardrailKind Discriminator (MODIFIED)
- Creating a LossLimits guardrail -- `UpsertAsync_ValidLossLimitsPayload_PersistsUnchanged` -- PASS
- Creating a VarTarget guardrail -- `UpsertAsync_ValidVarTargetPair_PersistsUnchanged` -- PASS
- Creating a StagedLossLimits guardrail -- `UpsertAsync_StagedLossLimitsSixStages_PersistsAllSix` -- PASS

### Kind Determines Valid Field Set (MODIFIED)
- Loss fields rejected on a VarTarget payload -- `UpsertAsync_LossFieldsOnVarTargetPayload_Rejected` -- PASS
- Var fields rejected on a LossLimits payload -- `UpsertAsync_VarFieldsOnLossLimitsPayload_Rejected` -- PASS
- DrawdownModel rejected on a StagedLossLimits payload -- CRITICAL, UNPINNED AND CONTRADICTED. `RiskLimitsService.ValidateKindFields`'s `StagedLossLimits` branch checks only `DailyLossLimitPct`/`MaxLossLimitPct`/`ProfitTargetPct`/`TargetVarPct`/`VarFloorPct` for non-null -- it never inspects `DrawdownModel`. `UpsertAsync`'s normalization line (`dto.Kind == GuardrailKind.LossLimits ? dto.DrawdownModel : null`) applies the same "normalize to null" treatment used for `VarTarget` (D4) to `StagedLossLimits` as well, silently discarding any `DrawdownModel` value instead of rejecting the payload. The spec's explicit scenario requires REJECTION for this case; no test exercises it (a passing test would have caught the contradiction). Design.md's D4 rationale addresses only `VarTarget` (to keep `UpsertAsync_ValidVarTargetPair_PersistsUnchanged` green) and never discusses extending that normalization to `StagedLossLimits`, so this is not a documented, deliberate scope decision -- it is a gap. Recommend: either (a) explicitly reject non-default `DrawdownModel` on `StagedLossLimits` payloads and add a covering test, or (b) if normalization is intentionally extended to `StagedLossLimits` too, update design.md's D4 to say so explicitly and correct/annotate the spec scenario rather than leaving a silent contradiction.
- LossLimits payload without DrawdownModel rejected -- WARNING, unpinned by an executable test. `UpsertBrokerRiskLimitsDto.DrawdownModel` is a required non-nullable positional parameter (no default), so omission is only enforced by the C# compiler / JSON model binder, not by any unit test in `RiskLimitsServiceTests`. Structurally sound but nothing in the test suite exercises the WebAPI/model-binding rejection path for a payload literally missing the field.

## TDD deviation assessment (Phase 3 batching)
Apply self-reported that Phase 3 (Application/Infrastructure) wrote GREEN code together with its RED test in the same edit batch, rather than strict red-first sequencing used elsewhere.

Assessment: does not materially weaken the tests. Every Phase-3 test depends on symbols that did not exist in the pre-change baseline (`GuardrailKind.StagedLossLimits`, `FtmoProduct`, `BreachBasis`, `GuardrailShape`, nullable `BrokerRiskLimits.DrawdownModel`, `BrokerRiskLimits.Stages`, `BrokerRiskLimitsDto.RulebookMismatch`, `ServiceGuardrailDto.BreachBasis`). Checked the two candidates most likely to "accidentally pass" against pre-change code:
- `UpsertAsync_VarTargetCarryingDrawdownModelTrailing_PersistsNull` -- pre-change `RiskLimitsService` persisted `DrawdownModel` unchanged (no normalization existed); this test would FAIL against pre-change code, so it genuinely pins new behavior.
- `UpsertAsync_DarwinexZeroWithLossLimitsKind_Rejected` -- pre-change service had no Kind-to-FundingService binding check at all; this test would FAIL (not throw) against pre-change code.

All remaining Phase-3 tests reference DTO/entity members that simply did not exist pre-change and would fail to compile. A test that cannot compile against the pre-change implementation cannot "accidentally pass" -- this is a stronger pinning guarantee than ordinary red-first, even though the ceremony (writing the failing test first, observing red, then writing minimal code) was skipped. Conclusion: procedural/ceremony deviation only, not a substantive test-quality issue. WARNING at most, and only for process discipline, not correctness risk. Included as SUGGESTION-level in the findings list below since it carries no runtime risk.

## Findings summary
| # | Severity | Finding |
|---|---|---|
| 1 | CRITICAL | "DrawdownModel rejected on a StagedLossLimits payload" spec scenario is unpinned by any test and contradicted by `RiskLimitsService` (silently normalizes to null instead of rejecting) |
| 2 | WARNING | "LossLimits payload without DrawdownModel rejected" relies solely on C#/JSON required-field enforcement; no executable test exercises it |
| 3 | WARNING | "Existing Axi LossLimits row survives unresolved" (migration scenario) not literally exercised by running `Up()`/`Down()`; only structurally corroborated (source read + EF script generation + equivalent read-path test) -- acceptable given the project's no-unauthorized-DB rule, but still a coverage gap |
| 4 | SUGGESTION | "Row carries a FundingService" scenario has no dedicated round-trip assertion |
| 5 | SUGGESTION | Phase 3 TDD-ordering deviation (RED+GREEN same batch) -- assessed as ceremony-only, no test found that would pass against pre-change code |

## Verdict
FAIL -- one CRITICAL (unpinned + spec-contradicting behavior) blocks a clean archive. All build/test evidence is otherwise green and independently reconfirmed; the regression fence is intact; five of six load-bearing design invariants are unconditionally confirmed and the sixth (DrawdownModel-on-StagedLossLimits) is exactly where the CRITICAL was found.
