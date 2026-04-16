# Backend Testing & Automation

Consolidated from: `testing` + `dotnet-automation` skills.
Applies to: `tests/AppTradingAlgoritmico.UnitTests/`, `tests/AppTradingAlgoritmico.IntegrationTests/`.

---

## Tool Stack

- **Framework**: xUnit
- **Assertions**: FluentAssertions (never `Assert.Equal`)
- **Mocking**: Moq
- **Integration**: `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)
- **Coverage**: coverlet.collector

## AAA Pattern (mandatory)

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var mock = new Mock<IUserRepository>();
    mock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
    var sut = new UserService(mock.Object);

    // Act
    var result = await sut.GetByIdAsync(userId);

    // Assert
    result.Should().NotBeNull();
    result!.Email.Should().Be("test@test.com");
}
```

## Naming Convention

`MethodName_StateUnderTest_ExpectedBehavior`:
```
✅ LoginAsync_ValidCredentials_ReturnsToken
✅ LogoutAsync_UserNotFound_DoesNotThrow
❌ TestLogin
❌ ShouldReturnTokenWhenValid
```

## FluentAssertions (always)

```csharp
result.Should().Be(5);
collection.Should().HaveCount(3);
action.Should().ThrowAsync<ValidationException>().WithMessage("*Role*");
```

## Integration Tests — WebApplicationFactory

```csharp
public class UsersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public UsersControllerTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_Endpoints_ReturnSuccess()
    {
        var response = await _client.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();
    }
}
```

## Complete Isolation with Mocks

- NEVER use real dependencies in unit tests.
- ALWAYS mock: `IRepository`, `IService`, `DbContext`, `HttpClient`.
- Mock DbContext via Repository Pattern, not DbContext directly.

---

## Error Classification

### Compilation Errors (block execution)

| Code | Cause | Action |
|------|-------|--------|
| `CS0103` | Name not in context | Verify imports/usings |
| `CS0246` | Type not found | Add reference or using |
| `CS8602` | Possible null dereference | Add null check |
| `CS8618` | Non-nullable not initialized | Add `required` or initialize |
| `MSB3027` | File locked | Stop processes |

### Assertion Failures (test ran, logic wrong)

| Pattern | Cause | Action |
|---------|-------|--------|
| `Expected X, but found Y` | Value mismatch | Review logic in SUT |
| `Should().NotBeNull()` failed | Object null | Review data flow |
| `Should().Throw<T>()` failed | Exception not thrown | Add validation |

## Self-Healing Protocol

1. Classify error (compilation vs assertion vs runtime)
2. Read complete error message with file and line
3. Apply minimal fix — correct the CODE, not the test
4. Re-run test with `--filter "TestName"`
5. Max 2 automatic correction attempts, then report to user

## Commands

```bash
dotnet test                                                    # Run all
dotnet test --no-build                                         # Quick (after successful build)
dotnet test --filter "FullyQualifiedName~AuthServiceTests"     # Filtered
dotnet test /p:CollectCoverage=true                            # With coverage
```

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| Mocking DbContext directly | Use Repository Pattern + Mock Repo |
| Assert.Equal | FluentAssertions |
| Complex Callback in mocks | Simple `.Returns()` |
| Modifying test to make it pass | Fix the code, not the test |
| Infinite self-healing loop | Max 2 attempts |
