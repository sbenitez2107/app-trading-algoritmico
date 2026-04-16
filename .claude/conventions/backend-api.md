# Backend API — REST, GraphQL, Security & Auditing

Consolidated from: `webapi-patterns` + `security` + `auditing` skills.
Applies to: `WebAPI/Controllers/`, `WebAPI/GraphQL/`, `WebAPI/Middleware/`, `Program.cs`.

---

## REST Controllers (Commands — Write Operations)

### Mandatory Attributes

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase { ... }
```

### XML Documentation on ALL Endpoints

```csharp
/// <summary>Login endpoint - Validates credentials</summary>
[HttpPost("login")]
[AllowAnonymous]
public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request) { ... }
```

### Response Conventions

```csharp
// POST → CreatedAtAction(nameof(GetById), new { id }, result)
// GET not found → NotFound()
// PUT/PATCH → Ok(result)
// DELETE → NoContent()
// Validation → BadRequest(new { message = "..." })
// Auth failure → Unauthorized() / Forbid()
```

### Use ActionResult<T> (not IActionResult)

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<UserDto>> GetById(string id) { ... }
```

---

## GraphQL Queries (Read Operations — HotChocolate)

```csharp
public class UserQueries
{
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ApplicationUser> GetUsers([Service] AppDbContext context)
        => context.Users.AsNoTracking();
}
```

| Attribute | Purpose |
|-----------|---------|
| `[UseProjection]` | Selects only requested fields |
| `[UseFiltering]` | Dynamic filters |
| `[UseSorting]` | Dynamic sorting |
| `AsNoTracking()` | Performance for read-only |
| Return `IQueryable<T>` | Never `IEnumerable` |

---

## Security

### JWT Authentication

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };
    });
```

### Policy-Based Authorization (not hardcoded roles)

```csharp
options.AddPolicy("RequireAdmin", policy => policy.RequireRole(Roles.Admin));
options.AddPolicy("CanEditProducts", policy => policy.RequireClaim("Permission", "Products.Edit"));
options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
```

### CORS — NEVER AllowAnyOrigin in production

```csharp
policy.WithOrigins(allowedOrigins!).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
```

### Rate Limiting

100 requests per minute per user, with `PartitionedRateLimiter`.

### SQL Injection Prevention

Always use LINQ or parameterized SQL. NEVER string concatenation in queries.

### Secrets

NEVER commit secrets. Use User Secrets in development, environment variables in production.

---

## HTTP Audit Logging

### Rule 1: Skip GET/HEAD/OPTIONS (read-only, no state change)

```csharp
if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(...) || HttpMethods.IsOptions(...))
{
    await _next(context); return; // Skip audit
}
```

### Rule 2: Mask Sensitive Data

Keys: `password`, `token`, `secret`, `apikey`, `authorization`, `credentials`, `key` → replaced with `"***"`.

### Rule 3: Truncate to 1024 Characters

```csharp
auditLog.Parameters = requestBody.MaskSensitiveData().TruncateWithPostfix(1024);
auditLog.ReturnValue = responseBody.MaskSensitiveData().TruncateWithPostfix(1024);
```

### AuditLog Entity Fields

`UserId`, `ServiceName`, `MethodName`, `Parameters` (masked), `ReturnValue` (masked), `ExecutionDuration` (ms), `ClientIpAddress`, `BrowserInfo`, `HttpMethod`, `Url`, `HttpStatusCode`, `ExceptionMessage`, `AuditDate` (UTC).

---

## Structured Logging (always)

```csharp
// ✅ Placeholders
_logger.LogInformation("User {UserId} logged in from {IpAddress}", user.Id, ipAddress);
// ❌ NEVER concatenation
_logger.LogInformation("User " + user.Id + " created");
```

---

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| DbContext in Controller | Use IRepository<T> |
| HttpGet in REST Controllers | Move to GraphQL |
| IActionResult without type | Use ActionResult<T> |
| Secrets in appsettings.json | User Secrets / Env Vars |
| Authorize(Roles="Admin") | Use Policy-based |
| Auditing GET requests | Filter by HTTP method |
| Unlimited audit payloads | Truncate to 1024 chars |
