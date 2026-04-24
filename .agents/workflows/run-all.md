---
description: Run both projects (Host + Web)
---

// turbo-all

Run both commands using the Bash tool with `run_in_background: true`. Do NOT wrap them in PowerShell — the shell is bash.

1. Run Host (Development profile) in background
```bash
dotnet run --project app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI --launch-profile Development
```

Optional for Local profile:
```bash
dotnet run --project app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI --launch-profile Local
```

2. Run Web in background
```bash
pnpm --dir app.trading.algoritmico.web start --port 4200
```

Available services:
- Host API: https://localhost:5001/swagger
- Host GraphQL: https://localhost:5001/graphql
- Web App: http://localhost:4200

