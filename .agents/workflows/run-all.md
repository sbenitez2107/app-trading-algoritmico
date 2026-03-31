---
description: Run both projects (Host + Web)
---

// turbo-all

1. Run Host in background (Development profile)
```bash
Start-Process -NoNewWindow powershell -ArgumentList "dotnet run --project app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI --launch-profile Development"
```

Optional for Local profile:
```bash
Start-Process -NoNewWindow powershell -ArgumentList "dotnet run --project app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI --launch-profile Local"
```

2. Run Web
```bash
pnpm --dir app.trading.algoritmico.web start --port 4200
```

Available services:
- Host API: https://localhost:5001/swagger
- Host GraphQL: https://localhost:5001/graphql
- Web App: http://localhost:4200

