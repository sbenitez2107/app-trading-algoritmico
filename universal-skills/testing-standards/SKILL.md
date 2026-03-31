---
name: testing-standards
description: >
  Universal language-agnostic testing standards. 
  Covers AAA pattern, naming convention, testing pyramid, and FIRST principles.
  Trigger: When writing any type of test (Unit, Integration, E2E).
license: Apache-2.0
metadata:
  author: prizm-team
  version: "1.0"
---

## When to Use

Use this skill when:
- Writing new tests
- Reviewing PRs that include tests
- Naming test methods
- Structuring test code

---

## Critical Patterns

### Pattern 1: AAA Pattern (Arrange, Act, Assert)

ALL unit tests must strictly follow the AAA structure.

```csharp
[Fact]
public void Calculator_Add_ReturnsSum()
{
    // Arrange: Prepare data and dependencies
    var calculator = new Calculator();
    var a = 5;
    var b = 3;

    // Act: Execute the action under test
    var result = calculator.Add(a, b);

    // Assert: Verify the result
    result.Should().Be(8);
}
```

### Pattern 2: Naming Convention

Use `MethodName_Scenario_ExpectedResult`.

- **MethodName**: The method or unit being tested.
- **Scenario**: The specific condition or input.
- **ExpectedResult**: What should happen.

**Examples:**
- `GetById_UserExists_ReturnsUserDto`
- `Create_InvalidEmail_ThrowsValidationException`
- `Login_ValidCredentials_ReturnsJwtToken`

### Pattern 3: Testing Pyramid

Respect the test distribution to keep the suite fast and reliable.

1.  **Unit Tests (70%)**: Fast, isolated, test domain logic. Mocks allowed.
2.  **Integration Tests (20%)**: Test interaction between layers (API + DB). Real/in-memory database.
3.  **E2E Tests (10%)**: Complete user flows (UI + API + DB). Slow, fragile.

### Pattern 4: FIRST Principles

Tests must be:
- **F**ast: Execute quickly.
- **I**ndependent: Don't depend on other tests or execution order.
- **R**epeatable: Always give the same result (no random/time dependencies).
- **S**elf-validating: Pass or fail automatically (no manual inspection).
- **T**imely: Written alongside the code (or before, TDD).

### Pattern 5: One Assert Per Concept

Test a single logical concept per test. Multiple asserts are allowed if verifying the same behavior/result object.

```csharp
// ✅ Checking the same result (state object)
result.Should().NotBeNull();
result.IsSuccess.Should().BeTrue();
result.Value.Should().Be("Created");

// ❌ Checking unrelated things
result.Should().Be("Created");
repository.Verify(r => r.Add(), Times.Once); // Separate side effect
logger.Verify(bad); // Another side effect
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Logic in Tests** | `if`, `for`, `while` in tests hide bugs | Linear and simple tests |
| **Sleep/Delay** | Slow and flaky tests | Use pollers/awaiters |
| **Global State** | Interdependent tests that fail randomly | Clean setup/teardown |
| **Testing Implementation** | Fragile tests when refactoring | Test public behavior |
| **Mocking DTOs** | Unnecessary and verbose | Use real objects |
