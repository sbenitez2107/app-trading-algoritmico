---
description: Restart Host - Stop and restart the .NET Host API
---
// turbo-all

1. Detener procesos dotnet
```powershell
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
```

2. Ejecutar Host en modo desarrollo
```bash
dotnet run --project app.trading.algoritmico.api/src/AppTradingAlgoritmico.WebAPI
```

El servidor estará disponible en:
- Swagger: https://localhost:56496/swagger
- GraphQL: https://localhost:56496/graphql

Para detener, usar /stop-host o presionar Ctrl+C


