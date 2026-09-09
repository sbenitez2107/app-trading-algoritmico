# 06_Money Management.md — ⚠️ SUPERSEDED

> **No leer para decidir.** Reemplazado por
> **[06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md)** (módulo 6 completo + mentoría 18/03/2026).
>
> Este resumen cubría **solo la columna de cuentas grandes** y omitía por completo la configuración
> de cuentas chicas (`Risked Money 10`, `Size decimals 2`, `Size if no MM 0,01`), que es donde está
> el único error de configuración que el módulo señala explícitamente.
>
> El nombre de archivo se conserva porque lo citan
> `openspec/changes/archive/2026-09-01-strategy-portfolio-simulator/design.md:305`,
> `openspec/changes/archive/2026-09-03-trade-risk-normalization/design.md:138` y
> `Darwinex_Zero_Risk_Model.md`. Sus afirmaciones **siguen siendo correctas** para cuentas grandes
> — el problema era el alcance, no el contenido.

---

## Contenido original (conservado por trazabilidad)

\# 💰 IMOX Academy - Protocolo de Money Management



\## 📌 Filosofía de Gestión

El riesgo no se mide en puntos, se mide en \*\*volatilidad (ATR)\*\*. El objetivo es que cada trade arriesgue una cantidad fija de capital, adaptando el tamaño de la posición a la "respiración" del mercado.



\## ⚙️ Configuración del Builder (Fase de Minado)

\* \*\*Method:\*\* Fixed Amount.

\* \*\*Initial Capital:\*\* $100,000.

\* \*\*Risked Money:\*\* $200 (0.20%).

\* \*\*Size Decimals:\*\* 1 (Filtro contra falsa precisión).

\* \*\*Size if no MM:\*\* 0.1 (Minilote).

\* \*\*Maximum Lots:\*\* 10.



\## 🧮 Conceptos Técnicos Clave

1\. \*\*Lote Standard (1.0):\*\* $10 por pip aprox. (en activos con Point Value 100,000).

2\. \*\*Minilote (0.1):\*\* $1 por pip aprox.

3\. \*\*Microlote (0.01):\*\* $0.10 por pip aprox. (Solo usar 2 decimales en fase de Retester/Live).



\## ⚠️ Reglas de Autoridad IMOX

\- \*\*SL/PT Basados en ATR:\*\* Obligatorio para que la estrategia sea dinámica\[cite: 4].

\- \*\*Slippage Simulado:\*\* Siempre asumir que la salida no será exacta.

\- \*\*Capitalización:\*\* No usar interés compuesto en el Builder; distorsiona las métricas de robustez.

