---
description: Detener el proyecto Host (.NET)
---

// turbo-all

1. Detener procesos dotnet
```powershell
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
```

Host detenido.
