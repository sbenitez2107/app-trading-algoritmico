---
description: Detener ambos proyectos (Host + Web)
---

// turbo-all

1. Detener procesos dotnet (Host)
```powershell
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
```

2. Detener procesos node (Web)
```powershell
Get-Process -Name "node" -ErrorAction SilentlyContinue | Stop-Process -Force
```

Todos los servicios detenidos.
