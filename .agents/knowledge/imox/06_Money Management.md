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

