---
name: csharp-dotnet
description: >
  Best practices for C# and .NET 10 development for app-trading-algoritmico. Covers async patterns,
  nullable reference types, modern C# (12+) features, and coding conventions.
  Trigger: When writing or refactoring C# code in any layer.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
---

## When to Use

Use this skill when:
- Writing C# code in any project layer
- Defining interfaces, classes, or records
- Implementing async methods
- Working with nullable types
- Refactoring existing code

---

## Critical Patterns

The following patterns are **MANDATORY** in all C# code.

### Pattern 1: File-Scoped Namespaces

ALWAYS use file-scoped namespaces to reduce indentation.

```csharp
// ❌ BAD: Namespace with braces
namespace AppTradingAlgoritmico.Domain.Entities
{
    public class User
    {
        // ...
    }
}

// ✅ GOOD: File-scoped namespace
namespace AppTradingAlgoritmico.Domain.Entities;

public class User
{
    // ...
}
```

### Pattern 2: Nullable Reference Types

ALWAYS enable and respect nullable reference types. Use `?` for types that can be null.

```csharp
// ❌ BAD: Ignores nullability
public string GetUserName(User user)
{
    return user.Name; // Can be null
}

// ✅ GOOD: Respects nullability
public string? GetUserName(User? user)
{
    return user?.Name;
}

// ✅ GOOD: Guaranteed non-null return
public string GetUserNameOrDefault(User? user)
{
    return user?.Name ?? "Anonymous";
}
```

### Pattern 3: Required Keyword for Mandatory Properties

Use `required` for properties that MUST be initialized.

```csharp
// ❌ BAD: No initialization validation
public class Tenant
{
    public string Name { get; set; }
    public string ConnectionString { get; set; }
}

// ✅ GOOD: Required guarantees initialization
public class Tenant : BaseEntity
{
    public required string Name { get; set; }
    public required string ConnectionString { get; set; }
    public string? Description { get; set; } // Optional
}
```

### Pattern 4: Async/Await with CancellationToken

ALL async methods MUST accept `CancellationToken` with default value.

```csharp
// ❌ BAD: No CancellationToken
public async Task<User?> GetByIdAsync(string id)
{
    return await _dbSet.FindAsync(id);
}

// ✅ GOOD: With CancellationToken
public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
{
    return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
}
```

### Pattern 5: Null-Coalescing with Throw

Use `??` with throw for mandatory configuration validation.

```csharp
// ❌ BAD: Verbose validation
var jwtKey = _configuration["Jwt:Key"];
if (jwtKey == null)
{
    throw new InvalidOperationException("JWT Key not configured");
}

// ✅ GOOD: Null-coalescing throw
var jwtKey = _configuration["Jwt:Key"] 
    ?? throw new InvalidOperationException("JWT Key not configured");
```

### Pattern 6: Generic Constraints with BaseEntity

Generic repositories MUST use constraints with BaseEntity.

```csharp
// ❌ BAD: No constraint
public class Repository<T> : IRepository<T> where T : class
{
    // No access to BaseEntity properties
}

// ✅ GOOD: With BaseEntity constraint
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    public async Task<bool> ExistsAsync(string id, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(e => e.Id == id, ct); // Access to Id
    }
}
```

### Pattern 7: Expression-Bodied Members for Simple Methods

Use `=>` for single-expression methods.

```csharp
// ❌ BAD: Verbose
public IQueryable<User> GetUsers([Service] IApplicationDbContext context)
{
    return context.Users;
}

// ✅ GOOD: Expression-bodied
public IQueryable<User> GetUsers([Service] IApplicationDbContext context)
    => context.Users;
```

### Pattern 8: Object Initializers for DTOs

Use object initializers instead of constructors for DTOs.

```csharp
// ❌ BAD: Constructor with many parameters
return new LoginResponseDto(true, token, "Login successful", user);

// ✅ GOOD: Object initializer
return new LoginResponseDto
{
    Success = true,
    Token = token,
    Message = "Login successful",
    User = new UserInfoDto
    {
        Id = user.Id,
        Email = user.Email!,
        FullName = user.FullName
    }
};
```

### Pattern 9: Primary Constructors (C# 12+)

Use primary constructors for classes with simple dependency injection.

```csharp
// ❌ BAD: Traditional verbose constructor
public class ProductService
{
    private readonly IRepository<Product> _repository;
    private readonly ILogger<ProductService> _logger;
    
    public ProductService(IRepository<Product> repository, ILogger<ProductService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}

// ✅ GOOD: Primary constructor
public class ProductService(
    IRepository<Product> repository,
    ILogger<ProductService> logger)
{
    public async Task<Product?> GetAsync(string id, CancellationToken ct = default)
        => await repository.GetByIdAsync(id, ct);
}
```

---

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `ApplicationUser` |
| Interfaces | IPascalCase | `IRepository<T>` |
| Methods | PascalCase | `GetByIdAsync` |
| Properties | PascalCase | `IsActive` |
| Private fields | _camelCase | `_userManager` |
| Parameters | camelCase | `cancellationToken` |
| Async suffix | Async | `LoginAsync`, `GetAllAsync` |
| DTOs | PascalCaseDto | `LoginRequestDto` |

---

## Decision Tree

```
What type should I use?
  │
  ├── Can the data be null?
  │     └── Use nullable type: string?, User?
  │
  ├── Is it a mandatory property?
  │     └── Use required: required string Name
  │
  ├── Is it an async method?
  │     └── Add CancellationToken = default
  │
  ├── Is it a mandatory configuration?
  │     └── Use ?? throw new InvalidOperationException()
  │
  └── Is it a one-line method?
        └── Use expression-bodied: => expression
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Ignoring nullability** | NullReferenceException at runtime | Enable nullable reference types |
| **Async without CancellationToken** | Cannot cancel operations | Add `CancellationToken ct = default` |
| **Throw without context** | Errors hard to diagnose | Include descriptive message |
| **Public fields** | Breaks encapsulation | Use properties |
| **Magic strings** | Hard to refactor | Use constants or nameof() |
| **Using on each line** | Verbose files | Use global usings |
| **Async void** | Unhandleable exceptions | Return Task |

---

## Verification Commands

```bash
# Build with warnings as errors
dotnet build --warnaserror

# Verify nullable reference types
dotnet build -p:TreatWarningsAsErrors=true -p:Nullable=enable

# Run code analyzer
dotnet format --verify-no-changes
```

