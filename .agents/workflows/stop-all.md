---
description: Detener ambos proyectos (Host + Web) — mata solo los procesos que escuchan en los puertos objetivo
---

// turbo-all

Ejecutar con la Bash tool (shell: bash). Mata **solo** los PIDs que escuchan en los puertos `5000`, `5001` (Host) y `4200` (Web). No toca otros procesos `dotnet`/`node` que puedan estar corriendo (VSCode, Docker Desktop, otras apps).

1. Detener Host + Web por puerto
```bash
powershell.exe -Command '$ports=4200,5000,5001; $procIds=Get-NetTCPConnection -LocalPort $ports -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique; if ($procIds) { $procIds | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }; Write-Output ("stopped PIDs: " + ($procIds -join ",")) } else { Write-Output "no listeners on 4200/5000/5001" }'
```

2. Verificar que los puertos quedaron libres
```bash
powershell.exe -Command '(Get-NetTCPConnection -LocalPort 4200,5000,5001 -State Listen -ErrorAction SilentlyContinue | Measure-Object).Count'
```
Debe imprimir `0`.

Notas de quoting:
- Comillas **simples** externas alrededor de `-Command '...'`. Si usás dobles, bash expande `$ports`, `$procIds`, etc. antes de que PowerShell las vea, y el script revienta.
- Evitá nombres de variable PowerShell que sean automáticas (`$PID`, `$args`, `$input`, `$host`). Por eso usamos `$procIds`, no `$pids`.
- `Stop-Process -Force` corta abruptamente: normal para dev servers, no apto para procesos con estado que deban persistir.
