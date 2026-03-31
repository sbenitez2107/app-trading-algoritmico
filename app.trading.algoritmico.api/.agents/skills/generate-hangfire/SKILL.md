---
name: Generate Hangfire
description: Sets up background jobs and recurring tasks using Hangfire, handling Tenant Context.
---

# 🕰️ Skill: Generate Hangfire

This skill guides the implementation of background processing jobs using Hangfire in the Clean Architecture solution.

## 📋 Usage
Use this when the user asks to "create a background job", "setup hangfire", or "schedule a recurring task".

## ⚙️ Process

### 1. 📦 Dependencies Check
Ensure the following packages are installed:

- **AppTradingAlgoritmico.Infrastructure**:
  - `Hangfire.Core`
  - `Hangfire.SqlServer`
  - `Hangfire.AspNetCore` (Required for `AddHangfire` extensions in DI)
- **AppTradingAlgoritmico.WebAPI**:
  - `Hangfire.AspNetCore` (Required for Dashboard Middleware)

### 2. 🧠 Application Layer (Defintion)
Background jobs are just method calls. Define the contract.
Create/Update: `src/AppTradingAlgoritmico.Application/Interfaces/IBackgroundJobService.cs`
(Or specific interfaces like `IEmailJob`).

```csharp
public interface IEmailJob
{
    Task SendWelcomeEmailAsync(string userId, string tenantId);
}
```

### 3. 🏗️ Infrastructure Layer (Implementation)
Implement the logic.
**CRITICAL**: Background jobs DO NOT have an HTTP Context. You must manually inject or handle the `TenantId`.
Create file: `src/AppTradingAlgoritmico.Infrastructure/Jobs/[JobName].cs`

```csharp
public class EmailJob : IEmailJob
{
    private readonly ITenantProvider _tenantProvider; // You might need a way to SET this
    
    public EmailJob(ITenantProvider tenantProvider) 
    {
        _tenantProvider = tenantProvider;
    }

    public async Task SendWelcomeEmailAsync(string userId, string tenantId)
    {
        // 🛡️ Set Tenant Context manually for this scope
        _tenantProvider.SetCurrentTenant(tenantId); 
        
        // ... Business Logic ...
    }
}
```

### 4. 🌐 WebAPI Layer (Enqueueing)
Inject `IBackgroundJobClient` in your Controller/Service.

```csharp
public class UserController : BaseController 
{
    private readonly IBackgroundJobClient _jobClient;

    public async Task<IActionResult> Register(...)
    {
        // ... Logic ...
        
        // 🔥 Fire-and-forget
        _jobClient.Enqueue<IEmailJob>(x => x.SendWelcomeEmailAsync(user.Id, _tenantProvider.TenantId));
    }
}
```

### 5. ⚙️ Configuration (If first time)
Ensure `Program.cs` or `Infrastructure/DependencyInjection.cs` has:
```csharp
services.AddHangfire(config => ...);
services.AddHangfireServer();
```
And the Middleware:
```csharp
app.UseHangfireDashboard();
```


### 6. ✅ Verification
1. **Build**: Run `dotnet build` to ensure all references are correct.
2. **Dashboard**: Start the host and navigate to `/hangfire` (e.g., `https://localhost:5001/hangfire`).
   - You should see the Hangfire Dashboard.
   - If 404, check if `app.UseHangfireDashboard()` is called in `Program.cs`.
   - If 401/403, check authorization filters (by default local requests are allowed).
3. **Database**: Connect to the SQL DB and verify tables starting with `Hangfire.` (e.g., `Hangfire.Job`, `Hangfire.Schema`) have been created.

## ⚠️ Standards & Rules
1.  **Primitives only**: Pass IDs (strings/ints) to jobs, NOT full Entities. Entities might change before the job runs.
2.  **Idempotency**: Jobs might retry. Ensure the logic can run multiple times without side effects (or handle it gracefully).
3.  **Tenant Context**: NEVER assume `_tenantProvider` has a value in a background job. Always pass `tenantId` as an argument to the job method.


