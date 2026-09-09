# Base de Conocimiento IMOX - Módulo 4: SQX Optimizer

> Origen: módulo 4 de la academia. Transcrito 2026-09-07.
> Es la **última etapa de filtrado** del pipeline y la que produce la evidencia más fuerte sobre
> estabilidad. Reemplaza la sección de Optimizer de `03_Validacion_y_Stress_Test.md`.

---

## 🎯 Qué busca el Optimizer

> **Buscamos obtener una combinación de parámetros óptima Y robusta.**

Y la aclaración que define toda la etapa:

> **No nos queremos quedar con la mejor estrategia sino con lo más estable y robusto.**
> Esta es **la piedra fundamental de la academia.**

El Optimizer corre **una simulación por vez** en la carga. Permite ver de un golpe de vista qué
estrategias quedan optimizadas y robustas, y si aplican o no a ciertos patrones.

---

## ⚙️ Configuración base (común a todos los tipos)

### 1. Choose strategy to optimize

**`All strategies in databank`** ← se optimiza todo el databank actual.
La alternativa (`Select strategy file`, con una ruta tipo `…/Strategy 1.10.230.sqx`) sirve para
correr una optimización específica sobre una estrategia puntual.

### 2. What to parametrize

**`Recommended parameters`**. Solo se parametrizan los parámetros significativos: períodos,
configuraciones de entrada/salida y multiplicadores.

`Optimize also trading options` → **desactivado**.

### 3. Parameters — automatic settings

| Ajuste | Valor | Nota |
|---|---|---|
| Parameter settings | **Automatic** | |
| Max optimizations | **15.000** | Rango recomendado por SQX: 5.000 – 20.000 |
| Value distribution — Up | **20** | % desde el valor original |
| Value distribution — Down | **20** | |
| **Steps** | **8** | ⚠️ **No subirlo mucho: se entra en sobreoptimización** |

Compará esto con el Sequential Optimization del Retester (30/30/**40**): allí el step es grueso a
propósito para no evaluar demasiados parámetros; acá es fino pero acotado. En ambos casos la razón es
la misma — **más precisión es más sobreajuste**, no más calidad.

**Sobre qué período se optimiza.** Estamos buscando algo que se adapte al **mercado actual**, y para
eso se usa **el último año**.

---

## 1️⃣ Simple Optimization

| Ajuste | Valor |
|---|---|
| Optimization type | `Simple optimization` |
| Almacenamiento | `Store only best optimization` |

**Es una optimización muy básica.** Sirve como primer paso, no como criterio de selección.

Los otros tipos disponibles: `Sequential` (se usa en el **Retester**, ver módulo 3), `Walk-Forward` y
`Walk-Forward Matrix`.

---

## 2️⃣ Walk Forward — teoría

> Es uno de los backtests de optimización **más importantes**, el más complejo en analítica, el más
> preciso y el más complicado de analizar — y **el que ofrece la mejor información** para decidir si
> una estrategia es buena y estable.

**Qué hace.** Evalúa constantemente entrenamientos (IS) y pruebas fuera de muestra (OOS), y —lo más
importante— **deslizamientos**: las ventanas de prueba se mueven en el tiempo.

### Floating vs Fixed — la decisión clave

| Modo | Comportamiento |
|---|---|
| **Fixed** | Toma **todo el rango de datos siempre desde el inicio**; solo cambia el OOS de cada rango |
| **Floating** ← el que se usa | La misma ventana (ej. 5 años: 4 IS + 1 OOS) **se va moviendo hacia la derecha** 5 veces, cubriendo los 10 años sin empezar siempre desde el origen |

**Por qué Floating, y este es el punto conceptual del módulo:**

> Querés que haya **determinadas ventanas que sean más complicadas** de pasar el test de robustez.
> Eso es lo que da el flotante.

Si se segmenta un período a partir de 2021 como IS y se configura el **último año** como OOS —un año
que **nunca se le mostró al Builder ni al Retester**— se obtiene una ventana **bastante complicada**
de pasar, porque además acá también se aplica filtrado.

Es una inversión deliberada del incentivo habitual: no se busca la ventana donde la estrategia se
luce, se busca la que la puede romper.

### Qué mirar en el resultado

El **Net Profit OOS** del año oculto: si es positivo o negativo, y **compararlo contra el
comportamiento real del mercado en ese año** — si fue alcista, bajista o lateral. Una estrategia
long-only que gana en un año alcista no probó nada; una que sobrevive un lateral, sí.

### Walk-Forward Type

| Opción | Uso |
|---|---|
| Simulated IS, Simulated OOS, fastest | ⬜ |
| Simulated IS, Exact OOS, slower | ⬜ |
| **Exact IS, Exact OOS, slow** | ✅ **el que se usa** — queremos simular pruebas reales de mercado |

---

## 3️⃣ Walk Forward Optimization — configuración

| Ajuste | Valor |
|---|---|
| Walk-Forward Type | **Exact IS, Exact OOS, slow** |
| Period Type | **Percent** + **Floating** |
| **Out of Sample %** | **30** — el mismo que se usó en el Builder |
| **Walk-Forward runs** | **10** — uno por cada año |

**¿Para qué un WF solo y no la matriz?** Para una corrida específica: 10 años, 10 runs sobre toda la
data hasta la fecha más reciente posible, y ver los resultados. Este optimizador da **muchas más
analíticas** para evaluar estabilidad y beneficio.

### Filtros — `Walk-Forward Optimization Filter`

Esto es lo que hace que la optimización quede en **PASSED** o **FAILED**.

```
Robustness score must be > 80% to pass
```

| Condición | Umbral |
|---|---|
| WF Winning Percent (OOS) | **>= 70% del WF Winning Percent (IS)** |
| WF Stability of Net Profit | **>= 60%** |
| WF Special — Percentage of profitable runs | **> 70%** |
| WF Special — Max profit in one run as % of total | **< 50%** |
| WF stagnation | **< 365** (un año) |
| **WF Ret/DD Ratio** | **>= 10** |

> ⚠️ **Variante documentada**: en **divisas** el `WF Ret/DD` baja a **5** y el `WF stagnation` sube a
> **500**. Ver módulo 2 § *Variantes*.

La condición de Winning Percent es relativa, no absoluta: no exige que la estrategia gane mucho fuera
de muestra, exige que **no se degrade más del 30% respecto de lo que hacía dentro de muestra**. Es una
prueba de consistencia, no de rendimiento.

---

## 4️⃣ Walk Forward Matrix

**Qué implica.** El resultado es una **matriz de optimizaciones**, donde cada celda es un test WF
distinto con su propio % de PASSED. En el eje **X** el `OOS %`, en el eje **Y** los `runs`.

| Ajuste | Valor |
|---|---|
| Walk-Forward Type | **Exact IS, Exact OOS, slow** |
| Period Type | **Floating** |

### Configuración típica IMOX

| Parámetro | Start | Stop | Step | Nota |
|---|---|---|---|---|
| **Out of Sample %** | 20 | 36 | 2 | Muchos runs de evaluación; **lo normal son 30 de OOS** |
| **Walk-Forward runs** | 5 | 10 | 1 | **El Start es la mitad del Stop** |

**Orden de ejecución**: incrementa el OOS primero y recién ahí avanza al próximo run.

```
Run 5  - 20 OOS
Run 5  - 22 OOS
Run 5  - 24 OOS
…
Run 5  - 36 OOS
Run 6  - 20 OOS
…
Run 10 - 36 OOS
```

> **La potencia del WF Matrix reside en todas las evaluaciones distintas que puede realizar sobre los
> mismos datos**, para ver si la estrategia tiene **zonas de estabilidad y robustez marcadas ante
> pequeños cambios de los parámetros.**

Es el mismo criterio que el Sequential Optimization del Retester —buscar una **zona** estable, no un
punto óptimo— pero aplicado a la geometría de la ventana en lugar de a los parámetros.

### Filtro — `Walk-Forward matrix filter`

> *WF matrix produces a table of X rows and Y columns, where each cell is a different WF optimization test.*

```
Filter passes when it finds an area of [3] rows and [3] columns
where at least [7] results have robustness score >= [80]%
```

No se exige que **toda** la matriz pase: se exige que exista un **vecindario contiguo de 3×3 donde 7
de 9 celdas pasen**. Eso es literalmente una zona de estabilidad — si los aprobados están dispersos
por la matriz, son suerte; si están agrupados, es robustez.

> **Es un filtro no tan estricto a propósito.** Cuanto más se sube el porcentaje del score, más
> estricto es el filtro y **podría sacarte varias estrategias buenas.**

Las seis condiciones de ranking son **idénticas a las del WF Optimization** (ver tabla de arriba).

### Recomendación de cierre

> **Pasarle 2 tipos de optimizaciones a una estrategia para validar robustez.**

---

## 📊 Diccionario de métricas `WF Special`

| Métrica | Qué mide | Para qué sirve |
|---|---|---|
| **Max Drawdown in one run** | El mayor drawdown en dinero absoluto en cualquier ejecución individual del WFO | Detecta si alguna ejecución tuvo una pérdida extrema |
| **Max % Drawdown in one run** | El mayor drawdown porcentual (sobre capital) en cualquier run | Evalúa si en algún ciclo hubo una caída peligrosa para el riesgo |
| **Max profit in one run** | La mayor ganancia obtenida en una sola ejecución | Si es mucho mayor que el resto, puede indicar un resultado anómalo o suerte puntual |
| **Max profit in one run as % of total** | Qué porcentaje del beneficio total se generó en un solo run | **Si es muy alto (>40–50%), la estrategia depende demasiado de un único período** |
| **Max Stagnation in %** | Cuánto tiempo (en %) estuvo la estrategia sin hacer nuevos máximos en cualquier ejecución | Alto estancamiento indica poca consistencia y puede ser señal de debilidad |
| **Min trades in one run** | El número mínimo de operaciones ejecutadas en un run | Asegura que cada ejecución tenga muestra suficiente para que las métricas sean válidas |
| **Percentage of profitable runs** | El porcentaje de ejecuciones del WFO que terminaron con ganancias netas | Mide la consistencia general a lo largo del tiempo |
| **WF Score** | Índice compuesto interno de StrategyQuant que resume la calidad de la estrategia en el WFO | Útil como indicador global, pero **menos transparente** que las métricas individuales |

---

## 🔑 El hilo que atraviesa el módulo

Tres mecanismos distintos, una sola idea:

| Mecanismo | Cómo busca robustez |
|---|---|
| **Floating window** | Elige deliberadamente ventanas **difíciles** en lugar de favorables |
| **Max profit in one run as % of total < 50%** | Rechaza el beneficio **concentrado en un período** |
| **Área de 3×3 en la matriz** | Exige que los aprobados estén **agrupados**, no dispersos |

Los tres castigan lo mismo: **un resultado que depende de una circunstancia particular.** Y los tres
son el argumento contra quedarse con "la mejor" — porque la mejor de una búsqueda grande es,
casi siempre, la más afortunada.
