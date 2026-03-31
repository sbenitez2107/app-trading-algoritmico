---
name: webapi-patterns
description: >
  Patterns for WebAPI in ASP.NET Core 10 for app-trading-algoritmico. Covers REST Controllers,
  GraphQL (HotChocolate), Middleware, and response conventions.
  Trigger: When creating REST endpoints, GraphQL resolvers, or middleware.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
---

## When to Use

Use this skill when:
- Creating new REST Controllers
- Implementing GraphQL Queries/Mutations
- Creating or modifying Middleware
- Defining HTTP response conventions

---

## Critical Patterns

### Pattern 1: Controller Decoration

ALL controllers MUST use these attributes.

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    // ...
}
```

### Pattern 2: Constructor Injection

Inject dependencies via constructor, DO NOT use `[FromServices]` in methods.

```csharp
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }
}
```

### Pattern 3: XML Documentation on Endpoints

ALL public endpoints MUST have XML documentation.

```csharp
/// <summary>
/// Login endpoint - Validates credentials and tenant
/// </summary>
/// <param name="request">Login credentials</param>
/// <returns>JWT token or 2FA requirement</returns>
[HttpPost("login")]
[AllowAnonymous]
public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
{
    // ...
}
```

### Pattern 4: ModelState Validation

Validate ModelState at the start of each endpoint with body.

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }
    // ...
}
```

### Pattern 5: Input Validation

Validate input using ModelState or FluentValidation at the service level.

```csharp
[HttpPost]
public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }
    var result = await _mediator.Send(new CreateUserCommand(dto));
    return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
}
```

### Pattern 6: Structured Logging

Use structured logging with placeholders, NEVER concatenation.

```csharp
// ❌ BAD
_logger.LogInformation("User " + user.Id + " created");

// ✅ GOOD
_logger.LogInformation("User {UserId} logged in from {IpAddress}", user.Id, ipAddress);
```

### Pattern 7: Response Conventions

Use the correct response methods according to the operation.

```csharp
// POST - Create resource
return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);

// GET - Resource not found
return NotFound();

// PUT/PATCH - Successful update
return Ok(result);

// DELETE - Successful deletion
return NoContent();

// Validation error
return BadRequest(new { message = "Error description" });

// Unauthorized
return Unauthorized(new { message = "Invalid credentials" });

// Forbidden
return Forbid();
```

### Pattern 8: Generic ActionResult

Use `ActionResult<T>` for endpoints that return data.

```csharp
// ✅ GOOD - Explicit type
[HttpGet("{id}")]
public async Task<ActionResult<UserDto>> GetById(string id)
{
    var user = await _repository.GetByIdAsync(id);
    if (user == null) return NotFound();
    return Ok(MapToDto(user));
}

// ❌ BAD - IActionResult without type
[HttpGet("{id}")]
public async Task<IActionResult> GetById(string id)
```

---

## GraphQL Patterns (HotChocolate)

### Pattern 9: Query Class Structure

Queries MUST use HotChocolate attributes.

```csharp
public class UserQueries
{
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ApplicationUser> GetUsers([Service] AppDbContext context)
    {
        return context.Users.AsNoTracking();
    }

    public async Task<ApplicationUser?> GetUserById(string id, [Service] AppDbContext context)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }
}
```

### Pattern 10: GraphQL Best Practices

| Practice | Description |
|----------|-------------|
| `[UseProjection]` | Automatic projection - selects only requested fields |
| `[UseFiltering]` | Allows dynamic filters in query |
| `[UseSorting]` | Allows dynamic sorting |
| `[Service]` | Dependency injection in resolver |
| `AsNoTracking()` | Performance - don't track read-only entities |
| `IQueryable<T>` | Return IQueryable, not IEnumerable |

---

## Middleware Patterns

### Pattern 11: Middleware Structure

```csharp
public class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolverMiddleware> _logger;
    
    // Routes that don't require validation
    private static readonly string[] ExcludedPaths = 
    {
        "/swagger",
        "/graphql",
        "/health"
    };

    public TenantResolverMiddleware(RequestDelegate next, ILogger<TenantResolverMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantAccessor tenantAccessor)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        
        // Skip excluded paths
        if (ExcludedPaths.Any(p => path.StartsWith(p)))
        {
            await _next(context);
            return;
        }

        // Validation logic...
        
        // Enrich OpenTelemetry
        Activity.Current?.SetTag("tenant.id", tenantId);
        
        await _next(context);
    }
}
```

### Pattern 12: Error Response in Middleware

```csharp
// For errors in middleware, use WriteAsJsonAsync
context.Response.StatusCode = StatusCodes.Status400BadRequest;
await context.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required" });
return;
```

---

## Decision Tree

```
What type of endpoint?
  │
  ├── Is it a Command (CREATE/UPDATE/DELETE)?
  │     └── REST Controller with [HttpPost/Put/Delete]
  │
  ├── Is it a Query (READ)?
  │     └── GraphQL Query with [UseProjection][UseFiltering]
  │
  └── Is it cross-cutting validation/security?
        └── Middleware
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **DbContext in Controller** | Violates clean architecture | Use IRepository<T> |
| **HttpGet in Controllers** | Mixes commands/queries | Move to GraphQL |
| **Manual Entity→DTO mapping** | Repetitive code | Consider AutoMapper |
| **String concatenation in logs** | Not structured | Use placeholders {Name} |
| **IActionResult without type** | No OpenAPI documentation | Use ActionResult<T> |
| **Try-catch in each method** | Repetitive code | Use exception middleware |

---

## Verification Commands

```bash
# Build and verify
dotnet build src/AppTradingAlgoritmico.WebAPI

# Run and test Swagger
dotnet run --project src/AppTradingAlgoritmico.WebAPI
# Navigate to: https://localhost:5001/swagger
```

