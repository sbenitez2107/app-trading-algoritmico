# Módulo 2: Builder - Generación de Estrategias (Metodología IMOX)

Este documento centraliza la configuración técnica y lógica del Builder en StrategyQuant X, basada en la metodología de Aritz para la creación de estrategias robustas.

## 1. Configuración de Construcción (What to Build)
* **Strategy Type:** Simple Strategy (Se recomienda evitar Multi TF; diversificar por activos/timeframes independientes).
* **Trading Direction:** Se prefieren estrategias **Full Long** o **Full Short** por separado para mayor especialización.
* **Strategy Style:** * **Fuzzy Logic (Recomendado):** Más estricto y efectivo en las condiciones de entrada/salida.
    * **Old SQ3 Style:** Reservado solo para "Strategy for Template" (Modo Avanzado).
* **Build Mode:** Genetic Options (Evolución Genética).

## 2. Parámetros de Entrada y Salida
* **Conditions:** Entrada (min 1 - max 3) | Salida (min 1 - max 2).
* **Global Indicators Period:** 5 a 200 velas.
* **Lookback Period (Shift):** Máximo 1. **Prioridad IMOX:** Usar la barra anterior (1) para evitar el sesgo de la barra actual (0).
* **Stop Loss (SL) - Modelo ATR (Prioridad):**
    * **ATR Multiple:** 1.2 a 5.
    * **ATR Period:** 150 a 250.
* **Profit Target (PT) - Modelo ATR (Prioridad):**
    * **ATR Multiple:** 2 a 6.
    * **ATR Period:** 250 a 350.
* **Risk-Reward (RR):** No limitar. Permitir que el constructor encuentre el rango óptimo dinámicamente entre el SL y PT basados en ATR.

## 3. Configuración del Motor Genético (Genetic Options)
* **Estructura de Población:**
    * **Islands:** 5 a 8 (según capacidad del hardware).
    * **Population size:** 150 por isla.
    * **Max Generations:** 100.
* **Evolución:**
    * **Crossover (Cruce):** 80% (Alta probabilidad para mezclar ideas ganadoras).
    * **Mutation (Mutación):** 20% (Baja para mantener estabilidad lógica).
    * **Migration:** Migrar el 20% de la población cada 20 generaciones.
* **Initial Population:** Generar el doble (Decimation Coefficient: 2) para asegurar una base de alta calidad desde el inicio.
* **Gestión de Estancamiento:** Reiniciar evolución si el Fitness del IS no mejora tras 25 generaciones.

## 4. Trading Options y Datos (Data Settings)
* **Salida de Mercado:** Exit on Friday (20:30) para evitar Gaps de fin de semana y Swaps.
* **Rango Horario:** De 01:00 a 23:00 (Evitar el rollover/cambio de día).
* **Configuración de Datos:**
    * **Símbolo:** Clon con ajuste de Timeframe para el broker específico (ej. Darwinex).
    * **Precisión:** FASTER para el Builder (la precisión TICK se reserva para el Retester).
    * **Split de Datos:** * **IS (In Sample):** 70-80% (Entrenamiento).
        * **ISV (Validación):** 10-20% (Oculto en opciones genéticas, visible en Ranking).
        * **OOS (Out of Sample):** 20-30% (Prueba de examen final).

## 5. Money Management y Gestión de Riesgo
* **Capital Inicial:** $100,000 (Cuentas de 6 cifras).
* **Modelo:** Fixed Amount (Riesgo fijo por operación).
* **Riesgo por Trade:** $200 (0.20% de la cuenta - Perfil Conservador).
* **Decimales de Lote:** **1 decimal** (Evita la "falsa precisión"; el ajuste fino a 2 decimales se hace en fases posteriores).

## 6. Building Blocks y Tipos de Orden
* **Señales:** Indicadores clásicos (RSI, BB, MACD, EMA) combinados con acción de precio (Bar and times). No seleccionar todos los bloques; buscar eficiencia.
* **Order Types:**
    * **Enter at STOP (Recomendado):** Es el tipo de orden más robusto para estrategias de largo plazo.
    * **Enter at Market (MKT):** Evitar por la variabilidad del spread y latencia.
* **Exit Types:** SL, PT, y **Trailing Stop** (basado en ATR con holgura para no ser demasiado estricto).

## 7. Ranking y Filtros Globales
La estrategia debe pasar estos filtros mínimos para guardarse en el Results Databank:
1. **# of Trades (Full Data):** > 200.
2. **Winning % (Full Data):** > 40%.
3. **Ret/DD Ratio (Full Data):** > 8.
4. **Net Profit (OOS):** > 0 (Obligatorio: ganar dinero en el periodo de examen).

## 8. Selección para el Retester (Módulo 3)
De la remesa generada, seleccionar un bloque de **300 a 400 estrategias** bajo estos criterios:
* Top 100 por **Ret/DD Ratio Full**.
* Top 100 por **Net Profit OOS**.
* Top 100 por **Ret/DD Ratio OOS**.

# Módulo 3: Retester - Pruebas de Robustez (Metodología IMOX)

Este documento establece el protocolo de estrés y validación técnica en StrategyQuant X para asegurar que una estrategia tiene una ventaja estadística real y no está sobreoptimizada.

## 1. Configuración de Flujo y Databanks
* **Source:** Crear un Databank nuevo para cargar las estrategias del Builder.
* **Target:** Crear un Databank nuevo para las estrategias aprobadas tras el retest.
* **Crosschecks:** Activar "Check Run all crosschecks independently" (para ver fallos incluso si uno falla) y "Retest only selected".

## 2. Higher Backtest Precision (Filtro de Calidad)
Aumentamos la precisión para eliminar el "ruido" de la fase de generación.
* **Precisión:** 1 minute data tick simulation (slow).
* **Filtros de Ranking (Más livianos por aumento de precisión):**
    * **# of Trades Full:** > 200.
    * **Winning % Full:** > 35%.
    * **Net Profit OOS:** > 0 (Obligatorio).
    * **Ret/DD Ratio Full:** Oro (10), Nasdaq (8), Divisas/Dax (5).

## 3. Monte Carlo: Trades Manipulation (Estrés de Ejecución)
Simula fallos operativos y cambios en el orden de las operaciones.
* **Configuración:** 1000 simulaciones. **No Full Sample** (usa aprox. 80% de la muestra aleatoria) + **Exact** (mantiene la lógica original).
* **Randomly Skip Trades:** 10% de probabilidad (Simula órdenes no ejecutadas por latencia/volatilidad).
* **Filtros de Aceptación:**
    * **Ret/DD Ratio (Conf. 100%):** Debe ser >= 0 (El peor caso no puede ser pérdida).
    * **Net Profit (Conf. 95%):** >= 50% del Net Profit promedio (Conf. 50%).
    * **Drawdown (Conf. 95%):** <= 250% del Drawdown promedio (Conf. 50%).

## 4. Monte Carlo: Retest Methods (Robustez de Parámetros y Datos)
Evalúa la sensibilidad del bot ante cambios en el entorno del mercado.
* **Configuración:** 1000 simulaciones. Uso de **Full Sample** y precisión **1 minute data tick**.
* **Checks Obligatorios (Aritz Base):**
    1. **Randomize History Data (Tick):** Probabilidad 20%, cambio máximo ATR 10%.
    2. **Randomize Spread:** De 1 a 3 (Simula spreads triples).
    3. **Randomize Strategy Parameters:** Probabilidad 10-20%, cambio máximo 20%.
* **Filtros:** Iguales a los de Trades Manipulation (Ret/DD 100% > 0).

## 5. Sequential Optimization (Zonas Estables)
Analiza si la estrategia depende de parámetros "quirúrgicos" o si tiene una zona de trabajo estable.
* **Settings:** Distribución de valor +/- 30%. Step del 40%.
* **Stability Check:** Se busca un área estable del 15% con un rango de fitness del 5% al 8%.
* **Objetivo:** Que al menos el **80% o 100%** de los parámetros pasen la prueba de estabilidad.

## 6. SPP - System Parameter Permutation (Anti-Sobreoptimización)
Determina si el bot está "ajustado a mano" para el histórico o si tiene lógica real.
* **Settings:** 15,000 a 20,000 permutaciones. Parámetros recomendados.
* **Filtros de Optimización:**
    * **% Profitable Optimizations:** > 95%.
    * **Uniform Distribution:** Menos de 5 cambios de signo (positivo a negativo).
    * **Median Check:** El Ret/DD Ratio (Mediana) debe estar entre el **70% y el 130%** del valor original.

## 7. What If & Additional Markets
* **What If:** Excluir el 5% de los mejores y peores trades para ver si la curva sigue siendo estable sin los "picos" de suerte.
* **Additional Markets:** Testear en activos similares (ej. XAUUSD vs XAGUSD). Ayuda a validar si la lógica es generalista.

## 8. Protocolo de Selección IMOX
Para optimizar el tiempo de proceso en VDS (3-4 días), seleccionar remesas de **300-400 estrategias** del Builder:
1. Top 100 por **Ret/DD Ratio Full**.
2. Top 100 por **Net Profit OOS**.
3. Top 100 por **Ret/DD Ratio OOS**.
4. Remanente por Profit Factor o Sharpe Ratio.

# Módulo 4: Optimizer - Optimización y Robustez Temporal (Metodología IMOX)

Este documento detalla el uso del Optimizer en StrategyQuant X para encontrar parámetros estables y validar la vigencia de las estrategias en el mercado real (último año oculto).

## 1. Optimización Simple (Búsqueda de Estabilidad)
A diferencia de la optimización tradicional que busca el "máximo beneficio", en IMOX buscamos la **estabilidad**.
* **Objetivo:** Encontrar una combinación de parámetros que se adapte al mercado actual sin caer en el sobreajuste.
* **Configuración Recomendada:**
    * **Tipo:** Simple Optimization / Store only best optimization.
    * **Parámetros:** Recommended Parameters (periodos, multiplicadores, niveles).
    * **Rango (Value Distribution):** Up 20% / Down 20%.
    * **Steps:** 8 (No subir este valor para evitar la sobreoptimización).
    * **Máximo de Optimizaciones:** 15,000.

> **Piedra Angular:** No nos quedamos con la estrategia que más gana, sino con la más **estable y robusta**.

## 2. Teoría Walk Forward (El Examen Final)
El Walk Forward (WF) es el backtest de optimización más importante. Simula cómo se habría comportado el bot si lo hubiéramos re-optimizado periódicamente en el pasado.
* **Modelo Floating:** La ventana de tiempo se desplaza hacia el futuro. Permite segmentar el histórico y evaluar el comportamiento en el mercado actual (el año que ocultamos en el Builder y Retester).
* **Configuración Exacta:**
    * **WF Type:** Exact IS, Exact OOS, slow (El más preciso y real).
    * **Period Type:** Floating / Percent.
    * **In Sample / Out of Sample:** Se recomienda usar el mismo ratio que en el Builder (ej. 70% IS / 30% OOS).

## 3. Walk-Forward Optimization (WFO)
Se usa para una corrida específica (ej. 10 años con 10 runs).
* **Filtros de Robustez (Score > 80% para PASSED):**
    * **WF Win % (OOS):** Debe ser >= 70% del Win % en el In-Sample (IS).
    * **WF Stability of Net Profit:** >= 60% (Consistencia en la ganancia).
    * **Profitable Runs:** > 70% de los periodos deben ser positivos.
    * **Max Profit in one run:** < 50% del total (Evita que el éxito dependa de un solo golpe de suerte).
    * **Stagnation:** < 365 días (No puede estar más de un año sin nuevos máximos).
    * **Ret/DD Ratio:** >= 10.

## 4. Walk-Forward Matrix (WFM)
La Matrix es una rejilla de múltiples optimizaciones WF con diferentes configuraciones de OOS y Runs. Busca "zonas de estabilidad".
* **Configuración IMOX:**
    * **OOS %:** Start 20% | Stop 36% | Step 2%.
    * **Runs:** Start 5 | Stop 10 | Step 1.
* **Filtro de la Matrix:**
    * Se busca un área de **3x3 celdas** donde al menos **7 resultados** tengan un Robustness Score >= 80%.
    * Si la estrategia solo pasa en una celda aislada ("isla de rentabilidad"), se descarta por fragilidad.

## 5. Métricas Especiales de Análisis
Para decidir si una estrategia es apta, la Gema debe evaluar estas métricas en el reporte del Optimizer:

| Métrica | Umbral / Objetivo | Significado |
| :--- | :--- | :--- |
| **Max Profit in one run %** | < 40-50% | Si es muy alto, el bot depende de un evento único (suerte). |
| **Percentage of Profitable Runs** | > 70% | Mide la consistencia a lo largo de los años. |
| **Max Stagnation %** | Bajo | Indica cuánto tiempo pasa la estrategia en "drawdown" temporal. |
| **Min trades in one run** | > 0 | Asegura que cada periodo tenga suficiente muestra estadística. |
| **WF Score** | > 80% | Índice global de calidad de la optimización. |

## 6. Filosofía de Aplicación
1. Se realizan **2 tipos de optimizaciones** WF distintas para validar la robustez total.
2. **Nunca** se aplican los parámetros optimizados automáticamente a la estrategia original dentro de los crosschecks; el objetivo es **validar la lógica**, no "tunear" el bot para el pasado.