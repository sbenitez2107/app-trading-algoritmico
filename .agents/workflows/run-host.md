---
description: Run Host project (.NET 9)
---

// turbo-all

1. Run with Development profile
```bash
dotnet run --project app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI --launch-profile Development
```

2. Run with Local profile (loads appsettings.Local.json)
```bash
dotnet run --project app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI --launch-profile Local
```

Server endpoints:
- Swagger: https://localhost:5001/swagger
- GraphQL: https://localhost:5001/graphql

3. To stop, press Ctrl+C

