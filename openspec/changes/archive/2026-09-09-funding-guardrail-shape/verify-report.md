```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:46d47aaeaec4eef676e3f3829f659de45bfc15d0
verdict: pass
blockers: 0
critical_findings: 0
requirements: 8/8
scenarios: 22/22
test_command: dotnet test (from app.trading.algoritmico.api)
test_exit_code: 0
test_output_hash: sha256:d86b0b2eb303080007808dbffa37fa434381531ef9a6203ea606ffd2596ff875
build_command: dotnet build -warnaserror (from app.trading.algoritmico.api)
build_exit_code: 0
build_output_hash: sha256:8d047c8cc799cac9840cb00ba63a6fa9e5e221551a67e78092fa52b8d23123ff
```

## Verification Report

**Change**: funding-guardrail-shape
**Version**: N/A (single spec revision)
**Mode**: Strict TDD (re-run after correction)

### Context

This is a re-run after a correction transaction. The previous verify run returned FAIL on one
CRITICAL (spec scenario "DrawdownModel rejected on a StagedLossLimits payload" was unpinned and
contradicted by code, which silently normalized instead of rejecting). Correction plus one scoped
fix-delta validation (result: approve) landed; commit 46d47aa is on main, working tree clean. This
report verifies the committed code, not a dirty tree.

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 33 |
| Tasks complete | 33 |
| Tasks incomplete | 0 |

### Build and Tests Execution
**Build**: PASSED (dotnet build -warnaserror, 0 warnings, 0 errors)

**Backend Tests**: PASSED - 607 passed / 0 failed / 0 skipped (dotnet test, 9s)

**Frontend Tests**: PASSED - 393 passed / 0 failed (31 test files, pnpm --dir app.trading.algoritmico.web test, 29.6s)

**Coverage**: Not available - no coverage tool configured in this repo.

### Spec Compliance Matrix

#### ADDED Requirements

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| FundingService Enum Definition | Row carries a FundingService | Exercised implicitly by every UpsertAsync_ test (each DTO sets FundingService); no dedicated round-trip assertion (result.FundingService.Should().Be) exists | PARTIAL |
| Kind Bound to FundingService | Mismatched write rejected | RiskLimitsServiceTests.UpsertAsync_DarwinexZeroWithLossLimitsKind_Rejected, UpsertAsync_AxiWithLossLimitsKind_Rejected, UpsertAsync_FtmoWithVarTargetKind_Rejected | COMPLIANT |
| Kind Bound to FundingService | Legacy mismatched row flagged, not broken, on read | GetByBrokerAsync_LegacyAxiLossLimitsRow_FlagsMismatchWithoutThrowing | COMPLIANT |
| FtmoProduct Discriminator | OneStep with Static rejected | UpsertAsync_FtmoOneStepWithStatic_Rejected | COMPLIANT |
| FtmoProduct Discriminator | Ftmo payload without a product accepted | UpsertAsync_FtmoWithNullProduct_Accepted | COMPLIANT |
| FtmoProduct Discriminator | Ftmo row without a product flagged on read | GetByBrokerAsync_FtmoRowWithNullProduct_FlagsMismatch | COMPLIANT |
| FtmoProduct Discriminator | (companion) TwoStep+Static accepted, not mismatched | UpsertAsync_FtmoTwoStepWithStatic_Accepted (asserts RulebookMismatch == false) | COMPLIANT |
| StagedLossLimits Stage Rulebook | Six stage rows persisted | UpsertAsync_StagedLossLimitsSixStages_PersistsAllSix | COMPLIANT |
| StagedLossLimits Stage Rulebook | Duplicate stage ordinal rejected | UpsertAsync_StagedLossLimitsDuplicateOrdinal_Rejected | COMPLIANT |
| Closed-Trade Lower-Bound Disclosure | FTMO breach readout labelled a lower bound | PortfolioServiceRiskTests BreachBasis assertions plus PortfolioAnalyticsDto.BreachBasis computed property, portfolio-detail.component.spec.ts lower-bound label test | COMPLIANT |
| Closed-Trade Lower-Bound Disclosure | VarTarget carries no breach basis | PortfolioAnalyticsDto.BreachBasis computed switch returns null for VarTarget; covered by PortfolioServiceRiskTests | COMPLIANT |
| Non-Destructive Reshape Migration | Existing Axi LossLimits row survives unresolved | GetByBrokerAsync_LegacyAxiLossLimitsRow_FlagsMismatchWithoutThrowing; migration Up() inspected - schema-only DDL, zero data writes, confirming Down() 0-fill is safe by construction | COMPLIANT |

#### MODIFIED Requirements

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| GuardrailKind Discriminator | Creating a LossLimits/VarTarget/StagedLossLimits guardrail | UpsertAsync_StagedLossLimitsSixStages_PersistsAllSix plus existing LossLimits/VarTarget upsert tests | COMPLIANT |
| Kind Determines Valid Field Set | Loss fields rejected on a VarTarget payload | UpsertAsync_LossFieldsOnVarTargetPayload_Rejected | COMPLIANT |
| Kind Determines Valid Field Set | Var fields rejected on a LossLimits payload | UpsertAsync_VarFieldsOnLossLimitsPayload_Rejected | COMPLIANT |
| Kind Determines Valid Field Set | DrawdownModel rejected on a StagedLossLimits payload | UpsertAsync_DrawdownModelOnStagedLossLimitsPayload_Rejected - asserts ThrowAsync ArgumentException against StagedLossLimitsDto() with DrawdownModel = DrawdownModel.Static; code path confirmed at RiskLimitsService.ValidateKindFields line 134 (if DrawdownModel is not null, throw) | COMPLIANT - previously CRITICAL, now fixed and pinned |
| Kind Determines Valid Field Set | LossLimits payload without DrawdownModel rejected | UpsertAsync_LossLimitsWithoutDrawdownModel_Rejected - asserts rejection against LossLimitsDto() with DrawdownModel = null; code path confirmed at line 120-121 | COMPLIANT - previously WARNING (unpinned, inexpressible), now fixed and pinned |

**Compliance summary**: 21/22 scenarios COMPLIANT, 1/22 PARTIAL (non-blocking). 0 CRITICAL, 0 WARNING, 1 SUGGESTION.

### Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| VarTarget normalizes (not rejects) DrawdownModel | Confirmed | RiskLimitsService.UpsertAsync line 57: normalizedDrawdownModel = Kind == LossLimits ? dto.DrawdownModel : null. This is the deliberate asymmetry vs StagedLossLimits, documented inline (lines 50-56) and pinned by UpsertAsync_VarTargetCarryingDrawdownModelTrailing_PersistsNull, whose VarTargetDto() fixture defaults DrawdownModel: DrawdownModel.Static - confirms the fence is load-bearing. This asymmetry still holds and was NOT converted to a rejection. |
| BreachBasis and RulebookMismatch are computed properties | Confirmed | PortfolioAnalyticsDto.BreachBasis is a get-only computed switch expression (not a constructor param); BrokerRiskLimitsDto.RulebookMismatch is a get-only property computed from GuardrailShape.IsShapeConsistent plus the Ftmo/product check - neither appears in either records positional parameter list |
| BreachBasis is null for VarTarget | Confirmed | Switch expression in PortfolioAnalyticsDto.cs maps LossLimits/StagedLossLimits to ClosedTradeLowerBound, all other kinds (including VarTarget) fall through to null |
| Positional records append with defaults | Confirmed | BrokerRiskLimitsDto and UpsertBrokerRiskLimitsDto: FtmoProduct? FtmoProduct = null and IReadOnlyList FundingStageLimitDto Stages = null are appended as the last two positional parameters, both defaulted - preserves existing call-site compatibility |
| FtmoProduct optional on write | Confirmed | UpsertAsync_FtmoWithNullProduct_Accepted proves no rejection when omitted |
| Migration Up() writes zero data | Confirmed | Up() is pure DDL (AlterColumn, AddColumn, CreateTable, CreateIndex) - no Sql()/UpdateData() calls. This is what makes Down() UPDATE ... SET DrawdownModel = 0 WHERE NULL safe: no pre-migration row could have been NULL under the old NOT NULL constraint |
| Stage bounds (0, 1] for MaxLossLimitPct and ProfitTargetPct, null ProfitTargetPct legal | Confirmed | ValidateKindFields lines 145-148; UpsertAsync_StageMaxLossOutsideValidRange_Rejected, UpsertAsync_StageProfitTargetOutsideValidRange_Rejected, UpsertAsync_StageProfitTargetNull_Accepted |

### Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| DrawdownModel widened to nullable on the upsert DTO | Yes | Matches the specs own MODIFIED-requirement note; no spec amendment needed |
| Axi frontend payload no longer sends DrawdownModel.Static on submit | Yes | risk-limits-modal.component.ts lines 213-214: submitted StagedLossLimits payload sets drawdownModel: null with an inline comment referencing the API rejection. The DrawdownModel.Static values at lines 78/128 are form-control display defaults for the reactive form (never submitted for Axi), not a regression |
| Regression fence additive-only (previously established) | Not independently re-verified | Single squash commit 46d47aa contains the entire change; the prior claim (455 insertions, 0 deletions across three fence files) describes the corrections incremental diff against pre-correction apply state, which is no longer isolable from this single commit. Not re-litigated per instructions - flagged here only for transparency, not as a finding |

### Issues Found

**CRITICAL**: None

**WARNING**: None

**SUGGESTION**:
1. "Row carries a FundingService" (ADDED requirement "FundingService Enum Definition") has no dedicated round-trip assertion - every UpsertAsync_ test sets a FundingService on its DTO and several downstream tests depend on it indirectly (e.g. kind-binding tests), but no test asserts result.FundingService.Should().Be(expectedService) after an upsert/read round trip. Low risk: the field is a plain scalar with no transformation logic, and its binding to Kind is separately and thoroughly pinned. Same status as the previous verify run - unchanged, not a regression.

### Verdict

**PASS**

Both previously-blocking findings are now fixed and pinned by real, non-tautological tests that
exercise production code (RiskLimitsService.UpsertAsync via ValidateKindFields), confirmed by
direct source inspection of the guard clauses at lines 120-121 and 134-135. All three FtmoProduct
Discriminator scenarios and the FundingService round-trip requirement were re-checked: the round-trip
requirement remains a non-blocking SUGGESTION carried over unchanged from the prior run. Backend
build is warning-free, 607/607 backend tests and 393/393 frontend tests pass on the currently
committed tree (46d47aa), which is clean. sdd-archive is unblocked.
