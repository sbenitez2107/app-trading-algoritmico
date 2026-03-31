---
name: run-web
description: Builds and runs the App.Trading.Algoritmico.Web (Angular) project.
---

# 🌐 Skill: Run Web

This skill handles the process of initializing and serving the Angular frontend application.

## 📋 Usage
Trigger this skill when the user asks to:
- "Run the frontend"
- "Start the web app"
- "Launch Angular"
- "Open the UI"

## ⚙️ Process

### 1. 📦 Install/Restore Dependencies
If this is the first run or if `package.json` has changed, ensure dependencies are up to date.
*Note: The project uses `pnpm`.*

```powershell
cd app.trading.algoritmico.web
pnpm install
```

### 2. 🚀 Start Development Server
Launch the Angular development server.

```powershell
cd app.trading.algoritmico.web
pnpm start
```
*This command runs `ng serve` internally.*

### 3. 🩺 Readiness Check
The standard Angular output will indicate when the server is ready, typically on port 4200.
- URL: `http://localhost:4200`

## ⚠️ Standards & Rules
- **Terminal Occupation**: `pnpm start` is a blocking command. It must be left running for the UI to work.
- **Port 4200**: By default, Angular uses port 4200. If it's busy, Angular usually asks to use a different port.
- **Backend Connection**: The frontend relies on the backend (API) running on `https://localhost:5001`. Ensure the `run-host` skill has been executed or the backend is active.
