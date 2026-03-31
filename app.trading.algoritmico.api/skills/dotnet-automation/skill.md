---
name: dotnet-automation
description: >
  .NET CLI command automation for compilation, testing, and self-healing in app-trading-algoritmico.
  Enables the agent to execute `dotnet build` and `dotnet test`, interpret results,
  distinguish between syntax errors and logic failures, and apply automatic corrections.
  Trigger: When executing builds, tests, validating .NET code, or performing self-healing.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
  capabilities:
    - build-execution
    - test-execution
    - output-parsing
    - self-healing
  related_skills:
    - universal-skills/testing-standards/skill.md
---

## When to Use

Use this skill when:
- Building the Host project (`dotnet build`)
- Executing unit or integration tests (`dotnet test`)
- Verifying that changes don't break the build
- Interpreting and classifying execution errors
- **Self-healing**: Automatically correcting code when a test fails

---

## Error Interpretation Tables

### Table 1: Syntax/Compilation Errors (CS/MSB)

These errors prevent test execution. The agent must correct the source code.

| Code | Type | Common Cause | Agent Action |
|------|------|--------------|--------------|
| `CS0103` | Syntax | Name does not exist in context | Verify imports/usings |
| `CS0246` | Syntax | Type or namespace not found | Add reference or using |
| `CS0029` | Syntax | Invalid implicit conversion | Fix types |
| `CS1061` | Syntax | Member does not exist on type | Verify method/property name |
| `CS8602` | Nullable | Possible null dereference | Add null check or `!` |
| `CS8618` | Nullable | Non-nullable property not initialized | Add `required` or initialize |
| `MSB3027` | Build | File locked | Stop processes with `@[/stop-all]` |
| `MSB1009` | Build | Project file does not exist | Verify path |

### Table 2: Logic/Assertion Failures (FluentAssertions/xUnit)

These failures indicate the test executed but the logic is incorrect.

| Output Pattern | Failure Type | Probable Cause | Agent Action |
|----------------|--------------|----------------|--------------|
| `Expected X, but found Y` | Assertion | Incorrect result | Review logic in SUT |
| `Should().Be()` failed | Assertion | Value mismatch | Verify calculations/transformations |
| `Should().NotBeNull()` failed | Null | Object not initialized | Review data flow |
| `Should().Throw<T>()` failed | Exception | Expected exception not thrown | Add validation |
| `System.NullReferenceException` | Runtime | Bug in code under test | Add null checks |
| `System.InvalidOperationException` | Runtime | Invalid state | Review preconditions |

---

## Business Rules (Mandatory)

### Rule 1: AAA Pattern Required

Every test MUST follow the **Arrange-Act-Assert** pattern:

```csharp
[Fact]
public async Task Method_Scenario_ExpectedResult()
{
    // Arrange - Prepare data and mocks
    var mock = new Mock<IDependency>();
    var sut = new ServiceUnderTest(mock.Object);

    // Act - Execute the action
    var result = await sut.DoSomething();

    // Assert - Verify result
    result.Should().NotBeNull();
}
```

> **Reference**: See [`universal-skills/testing-standards/skill.md`](../../universal-skills/testing-standards/skill.md) for complete standards.

### Rule 2: Complete Isolation with Mocks

- **NEVER** use real dependencies in unit tests
- **ALWAYS** mock: `IRepository`, `IService`, `DbContext`, `HttpClient`
- **USE** `Mock<T>` from Moq for all dependencies

```csharp
// ✅ CORRECT: Mocked dependency
var repoMock = new Mock<IUserRepository>();
repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
    .ReturnsAsync(new User { Id = Guid.NewGuid() });

// ❌ INCORRECT: Real dependency
var realRepo = new UserRepository(dbContext); // NOT in unit tests
```

### Rule 3: Naming Convention

Use the `MethodName_StateUnderTest_ExpectedBehavior` pattern:

```
✅ LoginAsync_ValidCredentials_ReturnsToken
✅ LogoutAsync_UserNotFound_DoesNotThrow
❌ TestLogin
❌ ShouldReturnTokenWhenValid
```

---

## Health Protocol

### Quick Health Check (Standard)

For quick validations within the Host health workflow:

```bash
# STANDARD: Validate without recompiling (faster)
dotnet test app.trading.algoritmico.api/tests/AppTradingAlgoritmico.Tests --no-build --verbosity minimal
```

### Full Health Check

For complete validation after significant changes:

```bash
# COMPLETE: Build + Test
dotnet build app.trading.algoritmico.api/AppTradingAlgoritmico.sln
dotnet test app.trading.algoritmico.api/tests/AppTradingAlgoritmico.Tests --verbosity normal
```

---

## Self-Healing Protocol

When a unit test fails, the agent MAY attempt to automatically correct:

### Step 1: Classify the Error

```
Is it a compilation error (CS/MSB)?
├─ Yes → Correct source code, NOT the test
└─ No → Continue to Step 2

Is it an assertion failure?
├─ Yes, in the test → Verify if the test is correct
├─ Yes, in the code → Correct the code under test (SUT)
└─ It's a runtime exception → Add validations/null checks
```

### Step 2: Apply Correction

1. **Read the complete error message**
2. **Identify the file and line** (`at Namespace.Class.Method() in File.cs:line X`)
3. **Apply minimal fix** following the pattern from the corresponding table
4. **Re-run test** with `dotnet test --no-build --filter "TestName"`

### Step 3: Validate Correction

If the test passes after correction:
- ✅ Report success
- Run complete suite to verify no regression

If the test still fails:
- ❌ Report to user with complete context
- DO NOT attempt more than 2 automatic corrections

---

## Commands Reference

```bash
# Full build
dotnet build app.trading.algoritmico.api/AppTradingAlgoritmico.sln

# Quick health check (STANDARD)
dotnet test app.trading.algoritmico.api/tests/AppTradingAlgoritmico.Tests --no-build --verbosity minimal

# Full test with output
dotnet test app.trading.algoritmico.api/tests/AppTradingAlgoritmico.Tests --verbosity normal

# Filtered test (for self-healing)
dotnet test --no-build --filter "FullyQualifiedName~AuthServiceTests"

# Specific test
dotnet test --no-build --filter "LoginAsync_ValidCredentials_ReturnsSuccessWithToken"
```

---

## Anti-Patterns

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Test without prior Build** | Doesn't distinguish compilation errors | Use `--no-build` only after successful build |
| **Ignoring CS8... warnings** | Can cause NullReferenceException | Treat as potential errors |
| **Infinite self-healing** | Loop of failed corrections | Maximum 2 attempts, then report |
| **Modifying test to pass** | Hides real bugs | Correct the code, NOT the test |
| **--verbosity quiet** | Hides critical information | Use `minimal` or `normal` |

