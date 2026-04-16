# Testing Standards — Pyramid, AAA & FIRST

Consolidated from: `testing-standards` skill.
Applies to: ALL test files in both API and Web projects.

---

## AAA Pattern (Mandatory)

ALL tests must follow Arrange-Act-Assert:

```csharp
[Fact]
public void Calculator_Add_ReturnsSum()
{
    // Arrange
    var calculator = new Calculator();

    // Act
    var result = calculator.Add(5, 3);

    // Assert
    result.Should().Be(8);
}
```

## Naming Convention

`MethodName_Scenario_ExpectedResult`:

```
✅ GetById_UserExists_ReturnsUserDto
✅ Create_InvalidEmail_ThrowsValidationException
❌ TestLogin
❌ ShouldReturnTokenWhenValid
```

## Testing Pyramid

| Level | % | Scope | Speed |
|-------|---|-------|-------|
| Unit | 70% | Domain logic, isolated with mocks | Fast |
| Integration | 20% | Layer interaction (API + DB) | Medium |
| E2E | 10% | Full user flows (UI + API + DB) | Slow |

## FIRST Principles

- **F**ast — execute quickly
- **I**ndependent — no test depends on another
- **R**epeatable — same result every time
- **S**elf-validating — pass or fail automatically
- **T**imely — written alongside code

## One Assert Per Concept

Multiple asserts OK if verifying same result object:

```csharp
// ✅ Same result object
result.Should().NotBeNull();
result.IsSuccess.Should().BeTrue();

// ❌ Unrelated side effects
result.Should().Be("Created");
repository.Verify(r => r.Add(), Times.Once); // separate concern
```

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| Logic in tests (if/for/while) | Linear, simple tests |
| Sleep/Delay | Use pollers/awaiters |
| Global mutable state | Clean setup/teardown |
| Testing implementation details | Test public behavior |
| Mocking DTOs | Use real objects |
