---
name: entity-framework
description: >
  Patterns for Entity Framework Core 10 in app-trading-algoritmico. Covers DbContext,
  Migrations, Fluent API, Interceptors, and Query configurations.
  Trigger: When working with database, migrations, or entity configuration.
license: Apache-2.0
metadata:
  author: imox-team
  project: app-trading-algoritmico
  version: "2.0"
---

## When to Use

Use this skill when:
- Configuring DbContext and DbSets
- Creating migrations
- Using Fluent API for entity configuration
- Implementing interceptors (auditing)
- Configuring indexes and constraints

---

## Critical Patterns

### Pattern 1: DbContext Structure

The DbContext MUST inherit from `IdentityDbContext` (if using Identity) and implement `IApplicationDbContext`.

```csharp
// Infrastructure/Persistence/AppDbContext.cs
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // DbSets with expression-bodied members
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}
```

### Pattern 2: Fluent API Configuration

ALL entity configuration goes in `OnModelCreating`. DO NOT use Data Annotations.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Tenant>(entity =>
    {
        entity.ToTable("Tenants");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasMaxLength(100);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.HasIndex(e => e.Name).IsUnique();
    });
}
```

### Pattern 3: Indexes and Constraints

```csharp
modelBuilder.Entity<ApplicationUser>(entity =>
{
    entity.ToTable("Users");
    entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
    entity.HasIndex(e => e.Email).IsUnique();
});
```

### Pattern 5: Relationships and DeleteBehavior

```csharp
modelBuilder.Entity<ApplicationUser>(entity =>
{
    // Relationship with explicit FK
    entity.HasOne(e => e.Tenant)
        .WithMany(t => t.Users)
        .HasForeignKey(e => e.TenantId)
        .HasPrincipalKey(t => t.Id)
        .OnDelete(DeleteBehavior.Restrict); // NO cascade delete
});

modelBuilder.Entity<ApplicationRolePermission>(entity =>
{
    // Composite key
    entity.HasKey(e => new { e.RoleId, e.PermissionId });
    
    // Cascade delete allowed
    entity.HasOne(e => e.Role)
        .WithMany(r => r.RolePermissions)
        .HasForeignKey(e => e.RoleId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

### Pattern 6: SaveChangesInterceptor for Auditing

```csharp
// Infrastructure/Persistence/Interceptors/AuditInterceptor.cs
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public AuditInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy = userId;
                
                // Auto-assign TenantId
                if (entry.Entity is IMustHaveTenant tenantEntity)
                {
                    tenantEntity.TenantId = tenantId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedBy = userId;
            }
        }
    }
}
```

### Pattern 7: Register Interceptor

```csharp
// Infrastructure/DependencyInjection.cs
services.AddSingleton<AuditInterceptor>();

services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var auditInterceptor = serviceProvider.GetRequiredService<AuditInterceptor>();
    
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    })
    .AddInterceptors(auditInterceptor);
});
```

### Pattern 8: Migrations

```bash
# Create migration
dotnet ef migrations add InitialSchema \
  -p src/AppTradingAlgoritmico.Infrastructure \
  -s src/AppTradingAlgoritmico.WebAPI

# Apply migration
dotnet ef database update \
  -p src/AppTradingAlgoritmico.Infrastructure \
  -s src/AppTradingAlgoritmico.WebAPI

# Revert migration
dotnet ef migrations remove \
  -p src/AppTradingAlgoritmico.Infrastructure \
  -s src/AppTradingAlgoritmico.WebAPI
```

### Pattern 9: Data Seeding

```csharp
// Infrastructure/Services/DataSeeder.cs
public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public async Task SeedAsync()
    {
        await _context.Database.MigrateAsync();

        // Seed default roles
        foreach (var role in new[] { "Admin", "Trader", "Viewer" })
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
        }

        // Seed admin user
        if (await _userManager.FindByEmailAsync("admin@trading.local") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@trading.local",
                Email = "admin@trading.local"
            };
            await _userManager.CreateAsync(admin, "Admin@123!");
            await _userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
```

---

## Decision Tree

```
How to configure the entity?
  │
  ├── Needs unique constraint?
  │     └── HasIndex(...).IsUnique()
  │
  ├── Relationship with another entity?
  │     └── Use HasOne/HasMany with explicit DeleteBehavior
  │
  ├── Needs simple index?
  │     └── HasIndex(e => e.Property)
  │
  └── Audit fields?
        └── Use AuditInterceptor (automatic)
```

---

## Anti-Patterns (Guardrails)

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| **Data Annotations** | Mixes Domain with Infrastructure | Use Fluent API |
| **Cascade Delete by default** | Deletes data unexpectedly | Use Restrict by default |
| **SaveChanges without Async** | Blocks threads | Always SaveChangesAsync |
| **DbContext as Singleton** | Memory leaks, stale data | Scoped lifetime |
| **Manual migrations** | Inconsistencies | Use dotnet ef |

---

## Verification Commands

```bash
# Verify model
dotnet ef dbcontext info \
  -p src/AppTradingAlgoritmico.Infrastructure \
  -s src/AppTradingAlgoritmico.WebAPI

# Generate SQL script
dotnet ef migrations script \
  -p src/AppTradingAlgoritmico.Infrastructure \
  -s src/AppTradingAlgoritmico.WebAPI

# List migrations
dotnet ef migrations list \
  -p src/AppTradingAlgoritmico.Infrastructure \
  -s src/AppTradingAlgoritmico.WebAPI
```

