# Base de Conocimiento IMOX - Módulo 5: Análisis de Resultados

> Origen: módulo 5 de la academia + mentoría del 11/03/2026. Transcrito 2026-09-07.
> **Es el módulo más operativo de la KB**: contiene el **procedimiento de decisión de 7 pasos** para
> elegir una estrategia para Backtest o Demo. Los módulos 2-4 producen candidatas; este las elige.

> **Advertencia de calibración del propio módulo:**
> *"El día de mañana vas a meter más capas de precisión. Ser más estricto, más cirujano."*
> Los umbrales de acá son **más estrictos que los del pipeline** a propósito: el pipeline filtra para
> no perder buenas, esta etapa filtra para no llevar malas a producción.

---

## ⭐ El procedimiento de decisión — elegir estrategia para BT o Demo

Este es el corazón del módulo. Es una **secuencia ordenada**, no una lista de criterios sueltos.

| # | Filtro | Criterio |
|---|---|---|
| **1** | **SPP** | Se descartan las estrategias cuya **mediana se aleje más del 50%** del Ret/DD Ratio de la original |
| **2** | **WF Matrix** | Analizar matrices **en verde o con zonas estables**; si no, descartar |
| **3** | **Zona estable** | Dentro de cada `Run/OOS` de la matriz, ubicar la zona estable y **validar que en el gráfico 3D Surface también sea zona estable**. ⚠️ **No necesariamente el Ret/DD Ratio más alto** |
| **4** | **Todos los años positivos** | Filtro anual completo |
| **5** | **Agrupación estructural** | Agrupar estrategias con los **mismos parámetros `price` / `Entry`** y quedarse con **2** |
| **6** | **KPIs** | Profit Factor > 1,6 · Sharpe Ratio > 1,3 · Ret/DD Ratio > 12 · Winning % > 48% · %DD < 2% · Stagnation < 365 · max avg loses < 10 |
| **7** | **Avg. Trades Month bajo** | *"No vas a querer que las estrategias entren mucho a mercado"* |

**El paso 3 es el más contraintuitivo y el más importante.** Explícitamente dice que el objetivo NO es
el Ret/DD más alto — es la **posición dentro de una zona estable**. Elegir el pico es exactamente el
error que todo el pipeline viene combatiendo.

**El paso 5 es una regla de diversificación estructural**, no de retornos: dos estrategias con los
mismos bloques de entrada son la misma apuesta aunque sus curvas difieran. Aparece también en la
mentoría como *"elegir por bloques distintos, by stop distintos, entradas distintas"* y como
*"analizar en el databank de optimización los grupos de parámetros, indicadores y señales de compra,
para agrupar y quedarme con 1 o 2 de cada grupo"*.

### ⚠️ La excepción del paso 7 — depende del servicio destino

> **Las estrategias que tienen mucho `Avg Trades Month` sirven para pasar challenges de fondeo.**
> Son buenas cuando hacés lectura del DD y pillás las tendencias.

Es decir: **el paso 7 se invierte según el destino**. Pocas operaciones para capital propio y
track record largo; muchas operaciones para superar un challenge de prop firm. Conecta directo con la
doctrina del módulo 0 — *una lógica, muchos servicios, adaptando el MM y el riesgo*— y con el mínimo
de operaciones que exige Axi Select.

---

## 1️⃣ Primeros pasos

- **Hacer backup** de las estrategias de WF generadas en la optimización.
- Se puede crear una **vista específica para el WF Matrix**.

---

## 2️⃣ WFM Results

Ejemplo de lectura de un resultado `Passed`:

```
Number of Walk-Forward Optimizations that passed: 52 of 54
Best group of combinations: 9 of 9 around cell [WF 6 runs : 22% OOS]
Recommended combination: WF 6 runs, 22% OOS
  → reoptimizing every 349 days on history of 1583 days
```

La *recommended combination* se traduce a una **cadencia de reoptimización**: cada ~349 días sobre un
histórico de ~1583 días. Ese es el dato operativo que sale de la matriz.

### 3D Surface

Se usa para **ver zonas de estabilidad**:

1. En un rectángulo **3×3** se analiza el **fitness (Ret/DD Ratio)**.
2. Se verifica que **no haya una dispersión del 50%**.
3. En la matriz de WF se buscan **zonas verdes** — lo ideal es que **casi toda la matriz esté verde**.

Es el mismo criterio 3×3 del filtro del módulo 4, pero mirado sobre la superficie de fitness en lugar
de sobre el score de robustez: la matriz dice *cuántas celdas pasan*, la 3D Surface dice *cuán parecido
es el resultado entre celdas vecinas*.

### Aplicar parámetros

En la sección `Results for…` → **`Apply params to strategy`** se aplican los parámetros optimizados de
un run concreto. (Contrastar con los crosschecks del Retester, donde **nunca** se aplican.)

---

## 3️⃣ Overview

Qué mirar:

- **Malas rachas de 2 a 4 meses.**
- **Conocimiento del activo**: noticias, actualidad, situaciones geopolíticas.
- **Si después de una racha perdedora recupera rápido la pérdida.**
- **Mercados bajistas**: 2022 fue el último año bajista — mirar drawdowns máximos y demás métricas ahí.

> **Para estrategias full largos**: solo se mira si **2022 quedó positivo**, ya que fue bear market.
> Lo anterior no se mira.

Es un test de régimen, no de rendimiento: una estrategia long-only que gana en un mercado alcista no
demostró nada.

---

## 4️⃣ Lista de trades

- Tener en cuenta la **columna de swap y comisiones**.
- **Analizar el tamaño de los SL**: ver si son variables o se mantienen cerca de 200.
- **Ordenar por close time.**
- **Analizar los TP** y deducir el **RR de la estrategia** — ejemplo, 2:1.
- **Media de los trades en tiempo.**

---

## 5️⃣ Sys. Param Permutation

> **Punto principal**: la métrica **Ret/DD Ratio Frequency** no puede estar muy separada de la media
> de la original.

**Nota**: se pueden llevar a demo ciertas estrategias candidatas que **no cumplen** con el Ret/DD
Ratio. El SPP es un filtro de sobreoptimización, no de rentabilidad — una estrategia poco rentable
pero no sobreajustada sigue siendo información válida.

---

## 6️⃣ Equity Chart

Analizar la curva de equity de **distintos runs del Walk Forward**.

**Subcharts a habilitar:**
- Equity
- **Stagnation** — marcarlo
- **Max Drawdown duration**

Analizar el drawdown y **ver qué fechas cayeron en ese período**.

> **Recomendado: el stagnation y el max drawdown deben estar ANTES de 2022.**

Es decir, el peor momento de la estrategia debería quedar en el pasado conocido, no en el período
reciente — si el peor drawdown es el más nuevo, la estrategia se está degradando.

---

## 7️⃣ Source Code

*(Sección presente en el temario, sin contenido registrado en esta transcripción.)*

---

## 🎓 Mentoría 11/03/2026 — criterios de filtrado avanzados

> *A medida que vayas teniendo más estrategias y quieras ser más estricto, vas a usar otros KPIs
> para hacer el filtrado.*

### El SQN / STR y su relación con la cantidad de trades

En SQX el SQN se llama **STR (System Quality Number)**.

> El SQN está **correlacionado con la cantidad de trades**. En una tendencia alcista, las estrategias
> que más ganan son **las que tienen mayor número de trades** — pero **también son las que más pierden
> cuando la tendencia ya no es alcista.**
>
> Y **psicológicamente son las que peor se llevan**. Según **Van K. Tharp**, el peso de la psicología
> en todo el trading es del **60%**.

Esta es la justificación de fondo del paso 7 del procedimiento de decisión: preferir bajo
`Avg. Trades Month` no es una preferencia estética, es evitar una métrica de calidad inflada por
exposición direccional.

### La regla del 50% — aparece dos veces

| Contexto | Regla |
|---|---|
| **Original vs Optimizada** | *"De una estrategia original a una optimizada no te podés separar más del **50%** en Ret/DD Ratio"* → señal de **sobreoptimización** |
| **Entre bloques de la matriz** | *"…y tampoco entre bloques de la matriz"* |

Y su corolario operativo:

> Si hay **mucha diferencia** entre el Ret/DD Ratio de la estrategia optimizada por el WF y la
> original, hay que hacer **backtesting en MetaTrader** para ver si está sobreoptimizada.

### Qué buscar

- **Estabilidad en Ret/DD**: mismos valores en la original que en la optimizada.
- **Zonas estables de 3×3** en la matriz de WF.
- **Comparar si la original es vencida por algún WF.**
- **A futuro**: tener estrategias con **menos `Avg. Trades Month` es bueno**.

### KPIs mencionados en el análisis

| KPI | Umbral citado en la narrativa |
|---|---|
| **STR** | **> 1.6** |
| **Profit Factor** | **> 1.5 / 1.4** |

> ⚠️ **Tensión interna sin resolver** — ver la sección de discrepancias al final.

### Drawdown: la pregunta correcta

> Ver el drawdown — ejemplo, **1647**. La pregunta es: **¿la estrategia es capaz de hacer un ×2 o ×3
> de ese drawdown?**

No *"cuánto cayó"*, sino *"cuánto podría llegar a caer"*. Es la misma lógica que el peor caso del
Monte Carlo del módulo 3.

### Estrategias dudosas

> Las estrategias dudosas las vas a poner en una **instancia de MT4** y las vas a dejar operando, para
> ver cómo se van comportando.

---

## ⏳ ¿Cuánto tiempo tener una estrategia en Demo antes de ponerla en una cuenta?

> **Mínimo, si querés confirmación buena**: **verla caer en DD y luego recuperarse por encima** —
> es decir, **vencerse a sí misma** por lo menos en un mal momento de mercado.

No es un plazo en días: es un **evento**. La estrategia sale de demo cuando atravesó y superó un
drawdown real, no cuando pasaron N meses.

**Contexto de la mentoría (marzo 2026)**: *"Ahora es prueba de fuego. En 2 jornadas hemos recuperado
la gran caída de marzo. Hemos vuelto a realizar gran parte de aranceles de la misma manera. Todavía
queda confirmar pero muchas estrategias lo han recuperado ya."*

---

## 💰 Nota sobre rendimientos de Darwinex *(dato de proveedor / fiscal)*

> ¿Qué hacer con los rendimientos de Darwinex? Ojalá pudieran invertirse en INDX; **si se sacan, hay
> que tributar.**

*No es doctrina de trading y no debe influir en decisiones técnicas. Verificar con la normativa fiscal
vigente antes de usarlo para cualquier cosa.*

---

## ⚠️ Discrepancias detectadas — pendientes de confirmar con el usuario

Estas contradicciones **están en el material de origen**. Se registran sin resolver: usar el valor
equivocado es tan malo como inventar uno.

### 1. Profit Factor — dos valores en el mismo módulo

| Fuente | Valor |
|---|---|
| Narrativa de KPIs (mentoría) | `> 1.5 / 1.4` |
| **Procedimiento de decisión, paso 6** | **`> 1.6`** |

**Interpretación provisoria** (a confirmar): la narrativa describe el KPI durante el análisis
exploratorio; el paso 6 es el **gate final** de selección para Demo, y por eso es más estricto.
**Ante la duda, usar el del paso 6.**

### 2. El umbral del SPP — 50% acá vs 70-130% en el Retester

| Fuente | Regla | Equivale a |
|---|---|---|
| [03_SQX_Retester.md](03_SQX_Retester.md) — filtro configurado en SQX | Median entre **70% y 130%** del Higher | **±30%** |
| **Este módulo** — regla de análisis manual | Se descarta si la mediana se aleja **más del 50%** | **±50%** |

**No son el mismo número.** La lectura más razonable es que el ±30% es el filtro automático que
configura el Retester, y el ±50% es la regla de descarte que aplica el analista al revisar resultados
—más permisiva porque llega después de que otros filtros ya actuaron. **Confirmar cuál gobierna.**

### 3. KPIs del paso 6 vs los umbrales por defecto de la KB

| KPI | Default de la KB (`INDEX.md`) | **Paso 6 de este módulo** |
|---|---|---|
| Sharpe Ratio | > 1.2 | **> 1.3** |
| Profit Factor | > 1.3 | **> 1.6** |
| Ret/DD Ratio | > 8-10 | **> 12** |
| Winning % | > 40% | **> 48%** |

**No es una contradicción: es un tier distinto.** Los defaults filtran el pipeline; estos filtran la
**decisión final de llevar a Demo**. Registrados como tal en la tabla de umbrales del índice.
