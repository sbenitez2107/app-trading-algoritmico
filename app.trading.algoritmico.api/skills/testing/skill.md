---
name: testing
description: >
  Testing patterns for .NET 10 in app-trading-algoritmico. Covers xUnit, FluentAssertions, Moq,
  WebApplicationFactory, and backend-specific conventions.
  Trigger: When creating test projects or writing unit/integration tests in the API.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
---

## When to Use

Use this skill when:
- Creating Unit Tests for Services, Domain, or Utils
- Creating Integration Tests for Controllers/API
- Configuring mocks for dependencies
- Using WebApplicationFactory

---

## Critical Patterns

### Pattern 1: Tool Stack

The official testing stack for the backend is:

- **Framework**: `xUnit`
- **Assertions**: `FluentAssertions`
- **Mocking**: `Moq`
- **Integration**: `Microsoft.AspNetCore.Mvc.Testing`

### Pattern 2: Unit Test Mocking (Moq)

Use `Mock<T>` to isolate the unit under test.

```csharp
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly UserService _sut; // System Under Test

    public UserServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _sut = new UserService(_userRepoMock.Object);
    }

    [Fact]
    public async Task GetById_UserExists_ReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId.ToString(), Email = "test@test.com" };

        _userRepoMock.Setup(x => x.GetByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetByIdAsync(userId.ToString());

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@test.com");
    }
}
```

### Pattern 3: FluentAssertions

Always use fluent syntax for readability.

```csharp
// ✅ GOOD
result.Should().Be(5);
result.Should().StartWith("Error");
collection.Should().HaveCount(3);
action.Should().ThrowAsync<ValidationException>();

// ❌ BAD
Assert.Equal(5, result);
Assert.True(result.StartsWith("Error"));
```

### Pattern 4: WebApplicationFactory (Integration Tests)

For testing complete endpoints using an in-memory API instance.

```csharp
public class UsersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UsersControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Endpoints_ReturnSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        response.Content.Headers.ContentType.ToString()
            .Should().Be("application/json; charset=utf-8");
    }
}
```

### Pattern 5: Testing Exceptions

Use `Func<Task>` to test async methods that throw exceptions.

```csharp
[Fact]
public async Task Create_InvalidRole_ThrowsException()
{
    // Arrange
    var cmd = new CreateUserCommand { Role = "Invalid" };

    // Act
    Func<Task> act = async () => await _sut.ExecuteAsync(cmd);

    // Assert
    await act.Should().ThrowAsync<ValidationException>()
        .WithMessage("*Role*");
}
```

---

## Test Project Structure

```
tests/AppTradingAlgoritmico.UnitTests/
├── Domain/
├── Application/
└── Infrastructure/

tests/AppTradingAlgoritmico.IntegrationTests/
├── Controllers/
└── Persistence/
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Mocking DbContext** | Difficult and error-prone | Use Repository Pattern + Mock Repo |
| **Assert.Equal** | Poor error messages | Use FluentAssertions |
| **Logic in Mocks** | Complex `Callback(...)` | Simple mocks `.Returns(...)` |
| **Coupled tests** | One test breaks another | Instantiate SUT in constructor |

---

## Verification Commands

```bash
# Run all tests
dotnet test

# Run tests from a specific project
dotnet test tests/AppTradingAlgoritmico.UnitTests

# Run with coverage (if coverlet installed)
dotnet test /p:CollectCoverage=true
```

