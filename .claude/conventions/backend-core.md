# Backend Core — Clean Architecture & C# Standards

Consolidated from: `clean-architecture` + `csharp-dotnet` skills.
Applies to: ALL `.cs` files in `app.trading.algoritmico.api/`.

---

## Clean Architecture — Dependency Rule

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

### Placement Decision Tree

```
Where does this code go?
  ├── Entity or Value Object → Domain/Entities (inherits BaseEntity)
  ├── Service/repository interface → Application/Interfaces
  ├── DTO or Command/Query → Application/DTOs
  ├── Repository implementation → Infrastructure/Repositories
  ├── External service implementation → Infrastructure/Services
  ├── EF Core configuration → Infrastructure/Persistence
  ├── REST endpoint → WebAPI/Controllers
  └── GraphQL endpoint → WebAPI/GraphQL
```

### Mandatory Patterns

- ALL entities inherit from `BaseEntity`.
- Interfaces in Application, implementations in Infrastructure.
- DI via extension methods in `DependencyInjection.cs` per layer.
- NO business logic in Controllers/Resolvers — delegate to services.
- CQRS: REST Controllers for Commands (POST/PUT/DELETE), GraphQL for Queries (GET).
- Structured logs MUST include UserId and TraceId.
- AutoMapper for Entity ↔ DTO mapping.

---

## C# Standards (.NET 10, C# 12+)

### File-Scoped Namespaces (always)

```csharp
namespace AppTradingAlgoritmico.Domain.Entities;

public class TradingAccount : BaseEntity { ... }
```

### Nullable Reference Types (always respect)

```csharp
public string? GetUserName(User? user) => user?.Name;
public string GetUserNameOrDefault(User? user) => user?.Name ?? "Anonymous";
```

### Required Keyword for Mandatory Properties

```csharp
public class Tenant : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; } // Optional
}
```

### Async/Await with CancellationToken (always)

```csharp
public async Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    => await _dbSet.FindAsync(new object[] { id }, ct);
```

### Primary Constructors (C# 12+)

```csharp
public class ProductService(
    IRepository<Product> repository,
    ILogger<ProductService> logger)
{
    public async Task<Product?> GetAsync(string id, CancellationToken ct = default)
        => await repository.GetByIdAsync(id, ct);
}
```

### Null-Coalescing with Throw

```csharp
var jwtKey = _configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured");
```

### Expression-Bodied Members for Single-Expression Methods

```csharp
public IQueryable<User> GetUsers([Service] IApplicationDbContext context)
    => context.Users;
```

---

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `ApplicationUser` |
| Interfaces | IPascalCase | `IRepository<T>` |
| Methods | PascalCase + Async | `GetByIdAsync` |
| Properties | PascalCase | `IsActive` |
| Private fields | _camelCase | `_userManager` |
| Parameters | camelCase | `cancellationToken` |
| DTOs | PascalCaseDto | `LoginRequestDto` |

---

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| Circular dependencies | Use shared interfaces |
| God Services (>10 public methods) | Split by Feature/UseCase |
| Leaking DbContext to WebAPI | Use IRepository<T> |
| CQRS Violation (HttpGet in REST) | Move queries to GraphQL |
| Async void | Return Task |
| Magic strings | Use constants or nameof() |
| Ignoring nullability | Enable nullable reference types |
