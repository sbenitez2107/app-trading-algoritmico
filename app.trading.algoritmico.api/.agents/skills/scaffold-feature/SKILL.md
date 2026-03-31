---
name: Scaffold Feature
description: Generates a complete vertical feature (Entity, DTOs, Service, Controller) following Clean Architecture rules.
---

# 🏗️ Skill: Scaffold Feature

This skill guides the AI to generate a complete feature stack for the app-trading-algoritmico application.

## 📋 usage
When the user asks to "create a new feature for [Entity]" or "scaffold [Entity]", follow these steps strictly.

## ⚙️ Process

### 1. 🔍 Context Gathering
Ask the user for:
- **Entity Name** (e.g., `Product`)
- **Properties** (e.g., `Name:string`, `Price:decimal`)
- **Is it Tenant-Aware?** (Usually YES, so add `IMustHaveTenant`)

### 2. 💎 Domain Layer (First)
Create file: `src/AppTradingAlgoritmico.Domain/Entities/[Entity].cs`
```csharp
namespace AppTradingAlgoritmico.Domain.Entities;

public class [Entity] : AuditableEntity, IMustHaveTenant // or just AuditableEntity if shared
{
    public string TenantId { get; set; } // Only if IMustHaveTenant
    
    // Properties with private setters
    public [Type] [PropName] { get; private set; }

    public [Entity]([Type] [propName])
    {
        // Constructor logic
    }
    
    public void Update(...) { ... }
}
```

### 3. 🧠 Application Layer
**A. DTOs**
Create folder: `src/AppTradingAlgoritmico.Application/DTOs/[Entity]/`
Create files:
- `[Entity]Dto.cs`
- `Create[Entity]Request.cs`
- `Update[Entity]Request.cs`

**B. Interface**
Create file: `src/AppTradingAlgoritmico.Application/Interfaces/I[Entity]Service.cs`

### 4. 🏗️ Infrastructure Layer
**A. Configuration**
Create file: `src/AppTradingAlgoritmico.Infrastructure/Persistence/Configurations/[Entity]Configuration.cs`
- **Crucial**: Implement `builder.HasQueryFilter(x => x.TenantId == _tenantProvider.TenantId);` if it's IMustHaveTenant.

**B. Implementation**
Create file: `src/AppTradingAlgoritmico.Infrastructure/Services/[Entity]Service.cs`
- Implement `I[Entity]Service`.

### 5. 🌐 WebAPI Layer
Create file: `src/AppTradingAlgoritmico.WebAPI/Controllers/[Entity]Controller.cs`
- Inherit `BaseController`.
- Eject `I[Entity]Service`.
- Create Endpoints: `Get`, `GetById`, `Create`, `Update`, `Delete`.

### 6. 🔗 Final Wiring
- Remind the user (or do it if asked) to register the service in `src/AppTradingAlgoritmico.Infrastructure/DependencyInjection.cs`.
- Remind to run `dotnet ef migrations add` via the `manage-db` skill.


