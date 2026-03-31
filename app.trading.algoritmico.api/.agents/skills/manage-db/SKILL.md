---
name: Manage DB
description: Handles Database migrations and updates safely.
---

# 🗄️ Skill: Manage Database

This skill ensures database operations are performed correctly with the right flags and checks.

## 📋 Usage
Use when users ask to "add migration", "update db", or "check database".

## ⚙️ Process

### 1. 🏗️ Build & Validate
Always build the project first to ensure the model snapshot can be compiled.
```powershell
dotnet clean src/AppTradingAlgoritmico.WebAPI
dotnet build src/AppTradingAlgoritmico.WebAPI
```
*🛑 CRITICAL: If build fails, STOP. Fix the code errors first.*

### 2. ➕ Add Migration
Execute the migration command.
```powershell
dotnet ef migrations add [MigrationName] --project src/AppTradingAlgoritmico.Infrastructure --startup-project src/AppTradingAlgoritmico.WebAPI --output-dir Persistence/Migrations
```

**🔍 Validation (MUST PERFORM):**
1.  **Check Output**: Look for "Build failed" or "Unable to create 'DbContext'".
    *   If **"Unable to create 'DbContext'"**: This usually happens because `ITenantAccessor` or other scoped services cannot be resolved at design time.
    *   **FIX**: Check if `src/AppTradingAlgoritmico.Infrastructure/Persistence/AppDbContextFactory.cs` exists. If not, create it implementing `IDesignTimeDbContextFactory<AppDbContext>`.
2.  **Verify File**: Ensure a new file was actually created in `src/AppTradingAlgoritmico.Infrastructure/Persistence/Migrations`. 
    *   *If no file appears, the migration FAILED silently or visibly.*

### 3. 🆙 Update Database (Apply)
Only proceed if step 2 was successful.
```powershell
dotnet ef database update --project src/AppTradingAlgoritmico.Infrastructure --startup-project src/AppTradingAlgoritmico.WebAPI
```
*Verify output says "Done."*

### 4. 📜 Generate Script (Production Safe)
If the user needs a SQL script for production:
```powershell
dotnet ef migrations script --project src/AppTradingAlgoritmico.Infrastructure --startup-project src/AppTradingAlgoritmico.WebAPI -o docs/migrations/[MigrationName].sql
```

## ⚠️ Safety Checklist
1.  **Environment**: Are you targeting the correct database? (Check `appsettings.Development.json`).
2.  **Context Factory**: Does `AppDbContextFactory` exist? This is often required for clean architecture projects to bypass runtime dependency injection issues during migration creation.


