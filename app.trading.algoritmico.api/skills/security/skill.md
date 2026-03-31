---
name: security
description: >
  Security patterns for .NET 10 in app-trading-algoritmico. Covers JWT Authentication,
  Policy-Based Authorization, CORS, Secret Management, and OWASP basics.
  Trigger: When configuring security, headers, authentication, or authorization.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
---

## When to Use

Use this skill when:
- Configuring authentication (JWT)
- Defining access policies (RBAC/CBAC)
- Configuring CORS and security headers
- Managing secrets and connection strings
- Protecting against common vulnerabilities (SQLi, XSS in API)

---

## Critical Patterns

### Pattern 1: JWT Authentication Configuration

Standard configuration for validating signed JWT tokens.

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero // Strict expiration
        };
    });
```

### Pattern 2: Policy-Based Authorization

Use policies instead of hardcoded roles.

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    // ✅ GOOD: Policy based on claims/roles
    options.AddPolicy("RequireAdmin", policy => 
        policy.RequireRole(Roles.Admin));
        
    options.AddPolicy("CanEditProducts", policy =>
        policy.RequireClaim("Permission", "Products.Edit"));

    // Default policy: Authenticated users
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Usage in Controllers
[Authorize(Policy = "RequireAdmin")]
public class AdminController : ControllerBase { ... }
```

### Pattern 3: Secure CORS

NEVER use `AllowAnyOrigin` in production.

```csharp
// Program.cs
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors", policy =>
    {
        policy.WithOrigins(allowedOrigins!)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // If using cookies/auth headers
    });
});

app.UseCors("ProductionCors");
```

### Pattern 4: Secrets Management (User Secrets)

NEVER commit secrets. Use User Secrets in development.

```bash
# Initialize user secrets
dotnet user-secrets init --project src/AppTradingAlgoritmico.WebAPI

# Set secret
dotnet user-secrets set "Jwt:Key" "super_secret_local_key_12345" --project src/AppTradingAlgoritmico.WebAPI
```

### Pattern 5: Rate Limiting

Protect public endpoints against abuse.

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

app.UseRateLimiter();
```

### Pattern 6: SQL Injection Prevention (EF Core)

Always use LINQ or parameterized SQL.

```csharp
// ✅ GOOD: LINQ (automatically parameterized)
var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

// ✅ GOOD: Raw SQL with parameters
var user = await context.Users
    .FromSql($"SELECT * FROM Users WHERE Email = {email}") 
    .FirstOrDefaultAsync();

// ❌ BAD: Concatenation (VULNERABLE)
var user = await context.Users
    .FromSqlRaw("SELECT * FROM Users WHERE Email = '" + email + "'")
    .FirstOrDefaultAsync();
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Secrets in appsettings.json** | Credentials exposed in git | Use User Secrets / Env Vars |
| **Authorize(Roles="Admin")** | Hardcoded role, hard to change | Use `[Authorize(Policy="...")]` |
| **CORS AllowAll (*)** | Allows CSRF/XSS cross-origin attacks | List explicit origins |
| **Expose Stack Traces** | Reveals internal info to attacker | Only in `IsDevelopment()` |
| **Log Sensitive Data** | PII/Passwords in logs | Use `.Redact()` or don't log |

---

## Verification Commands

```bash
# Scan for vulnerable packages
dotnet list src/AppTradingAlgoritmico.WebAPI/AppTradingAlgoritmico.WebAPI.csproj package --vulnerable

# Verify User Secrets
dotnet user-secrets list --project src/AppTradingAlgoritmico.WebAPI
```

