# Backend Data — Entity Framework & Data Access

Consolidated from: `entity-framework` + `multitenancy` skills.
Applies to: `Infrastructure/Persistence/`, `Domain/Entities/`, migrations.

---

## DbContext Structure

Inherits `IdentityDbContext` and implements `IApplicationDbContext`:

```csharp
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}
```

## Fluent API Configuration (mandatory — NO Data Annotations)

ALL entity configuration goes in `OnModelCreating`:

```csharp
modelBuilder.Entity<Tenant>(entity =>
{
    entity.ToTable("Tenants");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
    entity.HasIndex(e => e.Name).IsUnique();
});
```

## Relationships — Explicit DeleteBehavior

```csharp
entity.HasOne(e => e.Tenant)
    .WithMany(t => t.Users)
    .HasForeignKey(e => e.TenantId)
    .OnDelete(DeleteBehavior.Restrict); // Restrict by default, Cascade only when explicit
```

## SaveChangesInterceptor for Auditing

Auto-sets `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on save:

```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
    {
        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy = userId;
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

## Data Seeding

```csharp
public class DataSeeder : IDataSeeder
{
    public async Task SeedAsync()
    {
        await _context.Database.MigrateAsync();
        // Seed roles: Admin, Trader, Viewer
        // Seed admin user
    }
}
```

## Migrations Commands

```bash
# Create
dotnet ef migrations add <Name> -p src/AppTradingAlgoritmico.Infrastructure -s src/AppTradingAlgoritmico.WebAPI

# Apply
dotnet ef database update -p src/AppTradingAlgoritmico.Infrastructure -s src/AppTradingAlgoritmico.WebAPI

# Revert
dotnet ef migrations remove -p src/AppTradingAlgoritmico.Infrastructure -s src/AppTradingAlgoritmico.WebAPI

# Generate SQL script
dotnet ef migrations script -p src/AppTradingAlgoritmico.Infrastructure -s src/AppTradingAlgoritmico.WebAPI
```

---

## Multitenancy (when applicable)

> **Note**: This project is currently single-tenant. These patterns apply if multitenancy is enabled in the future.

### X-Tenant-Id Header

All requests (except public routes) must include `X-Tenant-Id`.

### ITenantAccessor — access TenantId in any layer

```csharp
public interface ITenantAccessor
{
    string GetTenantId();
    bool TryGetTenantId(out string tenantId);
    void SetTenantId(string tenantId);
}
```

### HasQueryFilter — automatic tenant isolation

```csharp
modelBuilder.Entity<ApplicationUser>(entity =>
{
    entity.HasQueryFilter(u => u.TenantId == _tenantAccessor.GetTenantId());
});
```

### IMustHaveTenant — interface for tenant-aware entities

```csharp
public interface IMustHaveTenant
{
    string TenantId { get; set; }
}
```

### TenantResolverMiddleware — validates header vs JWT `tenant_id` claim

Register AFTER `UseAuthentication()`, BEFORE `UseAuthorization()`.

---

## Anti-Patterns

| Anti-Pattern | Solution |
|--------------|----------|
| Data Annotations on entities | Use Fluent API |
| Cascade Delete by default | Use Restrict, Cascade only when explicit |
| SaveChanges without Async | Always SaveChangesAsync |
| DbContext as Singleton | Scoped lifetime |
| IgnoreQueryFilters() unprotected | Only in admin/seeding contexts |
