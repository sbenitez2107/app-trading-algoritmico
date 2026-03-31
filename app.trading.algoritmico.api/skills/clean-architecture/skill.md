---
name: clean-architecture
description: >
  Clean Architecture rules for .NET 10 in app-trading-algoritmico. Ensures layer separation,
  correct dependency direction, and standardized patterns for trading algorithm systems.
  Trigger: When creating/modifying code in Domain, Application, Infrastructure, or WebAPI.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
---

## When to Use

Use this skill when:
- Creating new domain entities (User, Role, TradingAccount, etc.)
- Implementing services or repositories
- Adding new endpoints (REST Controllers or GraphQL Resolvers)
- Reviewing PRs for architectural violations
- Deciding where to place business logic

---

## Critical Patterns

The following patterns are **MANDATORY**. Violations are architectural bugs.

### Pattern 1: Dependency Rule

Dependencies MUST point inward. The core (Domain) MUST NOT know about outer layers.

```
┌─────────────────────────────────────────────────────────┐
│                      WebAPI                             │
│   Controllers, GraphQL Resolvers, Middleware            │
├─────────────────────────────────────────────────────────┤
│                   Infrastructure                        │
│   EF Core, Repositories, External Services              │
├─────────────────────────────────────────────────────────┤
│                    Application                          │
│   Interfaces, DTOs, Services, Validators                │
├─────────────────────────────────────────────────────────┤
│                      Domain                             │
│   Entities, Value Objects, Enums                        │
└─────────────────────────────────────────────────────────┘
         ↑ Dependencies always point DOWNWARD
```

### Pattern 2: Entities Inherit from BaseEntity

ALL domain entities MUST inherit from `BaseEntity`.

```csharp
// ❌ BAD: Entity without BaseEntity
public class TradingAccount
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

// ✅ GOOD: Inherits from BaseEntity
public class TradingAccount : BaseEntity
{
    public required string Name { get; set; }
    public decimal Balance { get; set; }
}
```

### Pattern 3: Interfaces in Application, Implementations in Infrastructure

Service interfaces are defined in **Application**, concrete implementations in **Infrastructure**.

```csharp
// Application/Interfaces/IUserRepository.cs
public interface IUserRepository : IRepository<ApplicationUser>
{
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default);
}

// Infrastructure/Repositories/UserRepository.cs
public class UserRepository : Repository<ApplicationUser>, IUserRepository
{
    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email, ct);
    }
}
```

### Pattern 4: Dependency Injection via Extension Methods

Each layer registers its services via extension methods in `DependencyInjection.cs`.

```csharp
// Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        return services;
    }
}

// Infrastructure/DependencyInjection.cs
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    services.AddDbContext<AppDbContext>(...);
    services.AddScoped<IUserRepository, UserRepository>();
    return services;
}

// Program.cs
services.AddApplication();
services.AddInfrastructure(configuration);
```

### Pattern 5: No Business Logic in Controllers/Resolvers

Controllers and GraphQL Resolvers MUST ONLY delegate to services. NEVER contain business logic.

```csharp
// ❌ BAD: Logic in Controller
[HttpPost]
public async Task<IActionResult> Login(LoginRequestDto dto)
{
    if (dto.Password.Length < 8) // Business logic leaked
    {
        return BadRequest("Password too short");
    }
    return Ok();
}

// ✅ GOOD: Delegate to service via MediatR
[HttpPost("login")]
public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
{
    var result = await _mediator.Send(new LoginCommand(request.Email, request.Password));
    return result.IsSuccess ? Ok(result.Value) : Unauthorized(result.Error);
}
```

### Pattern 6: CQRS — REST for Commands, GraphQL for Queries

The API follows the CQRS pattern with separation of responsibilities:

```
┌─────────────────────────────────────────────────────────┐
│  REST Controllers (Commands - Write)                    │
│  POST, PUT, DELETE → Modify state                       │
│  Example: AuthController, UsersController               │
├─────────────────────────────────────────────────────────┤
│  GraphQL Queries (Queries - Read)                       │
│  GET with dynamic filters → Read only                   │
│  Example: UserQueries, TradingAccountQueries            │
└─────────────────────────────────────────────────────────┘
```

### Pattern 7: Observability - Structured Logs

ALL logs MUST include UserId and TraceId for complete traceability.

```csharp
_logger.LogInformation(
    "User {UserId} logged in from {IpAddress}",
    user.Id,
    context.Connection.RemoteIpAddress);
```

### Pattern 8: AutoMapper for Entity ↔ DTO Mapping

Use AutoMapper for mapping between Entities and DTOs. NEVER map manually.

```csharp
// Application/Mappings/MappingProfile.cs
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ApplicationUser, UserDto>();
        CreateMap<CreateUserDto, ApplicationUser>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
    }
}
```

---

## Decision Tree

```
Where does this code go?
  │
  ├── Is it an Entity or Value Object?
  │     └── Domain/Entities (inherits from BaseEntity)
  │
  ├── Is it a service/repository interface?
  │     └── Application/Interfaces
  │
  ├── Is it a DTO or Command/Query?
  │     └── Application/DTOs
  │
  ├── Is it a repository implementation?
  │     └── Infrastructure/Repositories
  │
  ├── Is it an external service implementation?
  │     └── Infrastructure/Services
  │
  ├── Is it EF Core configuration?
  │     └── Infrastructure/Persistence
  │
  ├── Is it a REST endpoint?
  │     └── WebAPI/Controllers
  │
  └── Is it a GraphQL endpoint?
        └── WebAPI/GraphQL
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Circular dependencies** | Layers import each other | Use shared interfaces |
| **God Services** | Services with >10 public methods | Split by Feature/UseCase |
| **Leaking DbContext** | DbContext used directly in WebAPI | Use IRepository<T> |
| **Anemic entities** | Entities without behavior, only properties | Encapsulate rules in entity |
| **CQRS Violation** | HttpGet in REST Controllers | Move queries to GraphQL |
| **Logs without context** | Logs without UserId/TraceId | Enrich with Serilog |

---

## Project Structure

```
src/
├── AppTradingAlgoritmico.Domain/
│   ├── Common/
│   │   └── BaseEntity.cs
│   ├── Entities/
│   │   ├── ApplicationUser.cs
│   │   └── ApplicationRole.cs
│   └── Interfaces/
│       └── (Only Domain Service interfaces)
│
├── AppTradingAlgoritmico.Application/
│   ├── Interfaces/
│   │   ├── IRepository.cs
│   │   ├── IAuthService.cs
│   │   └── IApplicationDbContext.cs
│   ├── DTOs/
│   │   ├── Auth/
│   │   └── Users/
│   ├── Features/
│   │   └── Auth/
│   │       ├── Commands/
│   │       └── Queries/
│   └── DependencyInjection.cs
│
├── AppTradingAlgoritmico.Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   └── Configurations/
│   ├── Repositories/
│   ├── Services/
│   └── DependencyInjection.cs
│
└── AppTradingAlgoritmico.WebAPI/
    ├── Controllers/
    │   ├── AuthController.cs
    │   └── UsersController.cs
    ├── GraphQL/
    ├── Middleware/
    └── Program.cs
```

---

## Verification Commands

```bash
# Build solution to verify dependencies
dotnet build src/AppTradingAlgoritmico.Domain
dotnet build src/AppTradingAlgoritmico.Application
dotnet build src/AppTradingAlgoritmico.Infrastructure
dotnet build src/AppTradingAlgoritmico.WebAPI

# Run tests
dotnet test
```
