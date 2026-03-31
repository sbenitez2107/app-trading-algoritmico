---
description: Detener el proyecto Web (Angular)
---

// turbo-all

1. Detener procesos node
```powershell
Get-Process -Name "node" -ErrorAction SilentlyContinue | Stop-Process -Force
```

Web detenido.
