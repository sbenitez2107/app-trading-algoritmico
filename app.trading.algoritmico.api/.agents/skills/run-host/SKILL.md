---
name: run-host
description: Builds and runs the app.trading.algoritmico.api (WebAPI) project.
---

# 🚀 Skill: Run Host

This skill handles the process of building and running the backend API project.

## 📋 Usage
Trigger this skill when the user asks to:
- "Run the backend"
- "Start the API"
- "Build and run host"
- "Launch the server"

## ⚙️ Process

### 1. 🧹 Cleaning (Optional but Recommended)
If the user reports strange build errors, start with a clean:
```powershell
dotnet clean src/AppTradingAlgoritmico.WebAPI
```

### 2. 🏗️ Build
Attempt to build the project to catch compilation errors before running.
```powershell
dotnet build src/AppTradingAlgoritmico.WebAPI
```
*If the build fails, Analyze the error output, Fix the code, and Retry.*

### 3. 🏃 Run
Once built successfully, launch the application using the desired launch profile.

**Development profile (default):**
```powershell
dotnet run --project src/AppTradingAlgoritmico.WebAPI --launch-profile Development
```

**Local profile (`appsettings.Local.json`):**
```powershell
dotnet run --project src/AppTradingAlgoritmico.WebAPI --launch-profile Local
```

### 4. 🩺 Readiness Check
After the console indicates the app is running (e.g., "Now listening on..."):
- Verify the Swagger UI is accessible at `https://localhost:5001/swagger`.
- Verify the Health Check endpoint: `https://localhost:5001/health`.

## ⚠️ Standards & Rules
- **Non-Blocking**: If running in a terminal that blocks (like `dotnet run`), ensure the user knows it will occupy that terminal session. In an Agent context, usually, we run it in the background or ask the user to run it. 
- **Port Conflicts**: If port 5001 is in use, identify the process or suggest using a different port.
- **Database**: Ensure database migrations are applied (using `manage-db` skill) *before* running if there are pending model changes.

