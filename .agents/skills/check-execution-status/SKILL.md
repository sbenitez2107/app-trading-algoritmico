---
name: check-execution-status
description: Checks if both the Backend (Host) and Frontend (Web) projects are currently running.
---

# 🔍 Skill: Check Execution Status

This skill is used to verify the health and availability of both the backend and frontend development servers.

## 📋 Usage
Trigger this skill when the user asks:
- "Are the projects running?"
- "Check if the app is alive"
- "System health check"
- "Verify execution status"

## ⚙️ Process

### 1. 🖥️ Backend (Host) Check
The backend is expected to run on `https://localhost:5001`.

```powershell
# Check health endpoint
curl -k https://localhost:5001/health
```
*Expected Output*: A JSON indicating `Healthy`.

### 2. 🌐 Frontend (Web) Check
The frontend is expected to run on `http://localhost:4200`.

```powershell
# Check if the port is listening
Test-NetConnection -ComputerName localhost -Port 4200
```
*Alternatively, try to fetch the index page:*
```powershell
curl http://localhost:4200
```

### 3. 🧪 Summary Report
- If both are responding: Reports "All systems operational 🚀".
- If one is down: Suggests using the corresponding `run-host` or `run-web` skill.

## ⚠️ Standards & Rules
- **Security**: Always use `-k` with `curl` for the backend check to bypass self-signed certificate warnings in development.
- **Port Specificity**: If ports have been customized in `launchSettings.json` or `angular.json`, adjust the check accordingly.