---
name: multitenancy
description: >
  Patterns for multitenancy implementation. Covers TenantResolverMiddleware,
  ITenantAccessor, Query Filters, and TenantId propagation across the stack.
  Trigger: When working with tenant data isolation or X-Tenant-Id validation.
license: Apache-2.0
metadata:
  author: prizm-team
  version: "1.0"
---

## When to Use

Use this skill when:
- Implementing X-Tenant-Id validation
- Filtering data by tenant
- Propagating TenantId between layers
- Configuring Query Filters in EF Core
- Validating tenant in JWT vs header

---

## Critical Patterns

### Pattern 1: X-Tenant-Id Header

ALL requests (except public routes) MUST include the `X-Tenant-Id` header.

```http
POST /api/users HTTP/1.1
Host: api.example.com
Authorization: Bearer eyJhbGciOiJIUzI1NiIsIn...
X-Tenant-Id: tenant-123
Content-Type: application/json
```

### Pattern 2: ITenantAccessor Interface

Interface to access TenantId in any layer.

```csharp
// Application/Interfaces/ITenantAccessor.cs
public interface ITenantAccessor
{
    string GetTenantId();
    bool TryGetTenantId(out string tenantId);
    void SetTenantId(string tenantId);
}

// Infrastructure/Services/TenantAccessor.cs
public class TenantAccessor : ITenantAccessor
{
    private string? _tenantId;

    public string GetTenantId()
    {
        return _tenantId ?? throw new InvalidOperationException("TenantId not set");
    }

    public bool TryGetTenantId(out string tenantId)
    {
        tenantId = _tenantId ?? string.Empty;
        return !string.IsNullOrEmpty(_tenantId);
    }

    public void SetTenantId(string tenantId)
    {
        _tenantId = tenantId;
    }
}
```

### Pattern 3: TenantResolverMiddleware

Middleware that validates and sets the TenantId for each request.

```csharp
// WebAPI/Middleware/TenantResolverMiddleware.cs
public class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolverMiddleware> _logger;
    
    // Routes that DO NOT require tenant
    private static readonly string[] ExcludedPaths = 
    {
        "/swagger",
        "/graphql",
        "/health",
        "/index.html"
    };

    public async Task InvokeAsync(HttpContext context, ITenantAccessor tenantAccessor)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        
        // Skip public routes
        if (ExcludedPaths.Any(p => path.StartsWith(p)))
        {
            await _next(context);
            return;
        }

        var jwtTenant = context.User.FindFirst("tenant_id")?.Value;
        var headerTenant = context.Request.Headers["X-Tenant-Id"].ToString();

        // Validate that at least one exists
        if (string.IsNullOrEmpty(headerTenant) && string.IsNullOrEmpty(jwtTenant))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required" });
            return;
        }

        // Validate no mismatch
        if (!string.IsNullOrEmpty(jwtTenant) && !string.IsNullOrEmpty(headerTenant) 
            && headerTenant != jwtTenant)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant ID mismatch" });
            return;
        }

        var tenantId = jwtTenant ?? headerTenant;
        tenantAccessor.SetTenantId(tenantId);
        
        // Enrich telemetry
        Activity.Current?.SetTag("tenant.id", tenantId);
        
        await _next(context);
    }
}
```

### Pattern 4: Register Middleware

```csharp
// Program.cs - BEFORE Authorization
app.UseAuthentication();
app.UseMiddleware<TenantResolverMiddleware>();  // After Authentication
app.UseAuthorization();
```

### Pattern 5: HasQueryFilter in EF Core

ALL entities with TenantId MUST have automatic filtering.

```csharp
// Infrastructure/Persistence/AppDbContext.cs
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantAccessor _tenantAccessor;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatic filter for Users
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasQueryFilter(u => u.TenantId == _tenantAccessor.GetTenantId());
        });

        // Filter for Settings
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasQueryFilter(s => s.TenantId == _tenantAccessor.GetTenantId());
        });
        
        // Apply to ALL entities with TenantId
    }
}
```

### Pattern 6: IMustHaveTenant Interface

Interface for entities that MUST have TenantId.

```csharp
// Domain/Interfaces/IMustHaveTenant.cs
public interface IMustHaveTenant
{
    string TenantId { get; set; }
}

// Domain/Entities/Setting.cs
public class Setting : BaseEntity, IMustHaveTenant
{
    public required string TenantId { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
}
```

### Pattern 7: Auto-Assign TenantId in Interceptor

```csharp
// Infrastructure/Persistence/Interceptors/AuditInterceptor.cs
private void UpdateAuditableEntities(DbContext context)
{
    foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
    {
        if (entry.State == EntityState.Added)
        {
            // Auto-assign TenantId if empty
            if (entry.Entity is IMustHaveTenant tenantEntity 
                && string.IsNullOrEmpty(tenantEntity.TenantId))
            {
                tenantEntity.TenantId = _tenantAccessor.GetTenantId();
            }
        }
    }
}
```

### Pattern 8: Propagate TenantId to External Services

```csharp
// Infrastructure/Services/TenantHeaderHandler.cs
public class TenantHeaderHandler : DelegatingHandler
{
    private readonly ITenantAccessor _tenantAccessor;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TryGetTenantId(out var tenantId))
        {
            request.Headers.Add("X-Tenant-Id", tenantId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### Pattern 9: Include TenantId in JWT Claims

```csharp
// Infrastructure/Services/AuthService.cs
private string GenerateJwtToken(ApplicationUser user)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Email, user.Email!),
        new("tenant_id", user.TenantId)  // ALWAYS include tenant
    };
    
    // Generate token...
}
```

### Pattern 10: Validate Tenant in Controllers

```csharp
// For critical operations, validate explicitly
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
{
    if (!_tenantAccessor.TryGetTenantId(out var tenantId))
    {
        return BadRequest(new { message = "X-Tenant-Id is required" });
    }

    // Use tenantId...
}
```

---

## Complete Flow

```
Request with X-Tenant-Id
       ↓
┌─────────────────────────────────┐
│  TenantResolverMiddleware       │
│  - Validates header             │
│  - Compares with JWT            │
│  - Sets ITenantAccessor         │
│  - Enriches OpenTelemetry       │
└─────────────────────────────────┘
       ↓
┌─────────────────────────────────┐
│  Controller/GraphQL             │
│  - Uses ITenantAccessor         │
└─────────────────────────────────┘
       ↓
┌─────────────────────────────────┐
│  DbContext                      │
│  - Automatic HasQueryFilter     │
│  - Filters ALL queries          │
└─────────────────────────────────┘
       ↓
┌─────────────────────────────────┐
│  Interceptor                    │
│  - Auto-assign TenantId         │
│  - Audit log with TenantId      │
└─────────────────────────────────┘
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **No HasQueryFilter** | Data leak between tenants | Add to all entities |
| **Hardcoded TenantId** | Not scalable | Use ITenantAccessor |
| **Validate only in Controller** | Can be bypassed | Use middleware |
| **JWT without tenant_id** | Cannot validate ownership | Include claim |
| **IgnoreQueryFilters() unprotected** | Exposes data | Only in admin/seeding |
| **TenantAccessor Singleton** | Mixed data | Scoped lifetime |

---

## Verification Commands

```bash
# Test: Verify request without X-Tenant-Id fails
curl -X GET https://localhost:5001/api/users
# Expected: 400 Bad Request

# Test: Verify filter by tenant
curl -X GET https://localhost:5001/api/users \
  -H "X-Tenant-Id: tenant-123" \
  -H "Authorization: Bearer ..."
# Expected: Only users from tenant-123
```
