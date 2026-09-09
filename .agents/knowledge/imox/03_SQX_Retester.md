# Base de Conocimiento IMOX - Módulo 3: SQX Retester

> Origen: mentoría del 25 de febrero de 2026. Transcrito 2026-09-07.
> Complementa `03_Validacion_y_Stress_Test.md`. Es el módulo del **arsenal anti-sobreajuste**:
> cada crosscheck existe para romper una estrategia de una forma distinta.

---

## ⚠️ Precondiciones — antes de correr nada

| Ajuste | Valor | Por qué |
|---|---|---|
| `Data → Test parameters → Precision` | **tick** | Los crosschecks IMOX lo requieren |
| `Ranking → Custom filters` | **todos deshabilitados** | Los crosschecks del Retester **ya son bastante estrictos**; sumarles los filtros del Builder es ser estricto dos veces |

Los **filtros automáticos** del ranking sí quedan activos. Los custom del Builder, no.

---

## 🧭 Setup

**Databanks**: crear uno **Source** para cargar las estrategias a retestear y uno **Target** para
guardar las analizadas. Opción `retest only selected` para procesar solo las elegidas.

**`Run all crosschecks independently from crosschecks filter failure`** ← activado. Ejecuta todos los
crosschecks **aunque alguno falle**, para ver el cuadro completo en lugar de detenerse en el primero.

### Los tres niveles, y cuáles usa IMOX

| Nivel | Crosscheck | ¿Se usa? |
|---|---|---|
| **BASIC (fast)** | What-If simulations | ✅ puntual |
| | Monte Carlo trades manipulation | ✅ **sí** |
| | Higher backtest precision | ✅ **sí** |
| **STANDARD (slow)** | Backtest on additional markets | ⬜ opcional |
| | Monte Carlo retest method | ✅ **sí** |
| | Sequential optimization | ✅ **sí** |
| **EXTENSIVE (slowest)** | Opt. Profile / **Sys. Param. Permutation** | ✅ **sí** |
| | Walk-Forward Optimization | → módulo 4 |
| | Walk-Forward Matrix | → módulo 4 |

---

## 1️⃣ What-If

Simulación de **algo concreto** que se quiera testear sobre una estrategia.

**Ejemplo**: una estrategia en H4 donde solo se testean días hábiles sin fin de semana — porque en H4
se deja correr el fin de semana. `Trade only in days` y se seleccionan días específicos.

Filtros extra interesantes:
- `Exclude 5% trades with biggest profit` y `Exclude 5% trades with lowest profit` — **quitan los
  picos y los valles** y muestran si la curva de rentabilidad se mantiene estable sin ellos.
- `Use 0.1 fixed lots` — probar lotajes distintos.
- `Take maximum 2 trades per day`.
- **`swap (long: 10, short: −10)`** — testear con un swap distinto para sacar conclusiones. Este es el
  lugar donde el swap entra al análisis, ya que **en el Builder no se configura** (ver módulo 1).

---

## 2️⃣ Monte Carlo — teoría

Permite **evaluar el peor caso**: aplicar a una estrategia niveles de estrés que no se pueden aplicar
a mano, y ver qué drawdown y qué net profit aparecen.

**Lectura visual**: eje Y = equity, eje X = trades. La **línea azul** es la estrategia original; el
resto son las 1000 simulaciones. **Si la mayoría de las simulaciones se asemejan a la original,
estamos ante un buen Monte Carlo.**

Cada métrica se reporta como *Original | 95%*: Net profit, Drawdown, Ret/DD ratio, Expectancy.

**El peor caso es el 100% de confianza.** Ahí sabés cuál es tu peor Ret/DD (puede ser 3× el original)
o tu peor net profit. Y la pregunta que hay que hacerse:

> Si el peor caso sale negativo, ¿te quedás con esta estrategia? ¿Y si ese peor caso ocurre **el
> primer día**?

---

## 3️⃣ Monte Carlo — Trades Manipulation

**Qué hace**: toma el orden de los trades y lo desordena. Si en la primera quincena del mes venías
ganando, al reordenar quizá ya no — **altera el comportamiento de la estrategia sin cambiar sus trades**.

### Settings

| Ajuste | Valor | Nota |
|---|---|---|
| Number of simulations | **1000** | Buen valor; también se puede hacer MC a portfolios |
| **Use Full Sample** | **NO** ← recomendado | Con Full Sample usa **todos** los trades históricos en cada simulación (mucho más lento). Sin él, usa una muestra aleatoria (~80%) |
| Randomize trades order | **Exact** | *Exact* mantiene la configuración igual al origen. *Resampling* es aleatoriedad **con reemplazo** — variaciones más agresivas y escenarios muy distintos; **para Aritz es demasiado agresivo** |
| **Randomly skip trades** | **probabilidad 10%** | Simula lo que pasa de verdad: una orden pendiente que no se activó, un trade perdido en sesiones de mucha volatilidad |

### Los tres niveles de exigencia

| Nivel | Configuración |
|---|---|
| Estricto medio | Full Sample + Exact |
| **Estricto alto** ← **recomendado** | **No Full Sample + Exact** |
| Estricto muy alto | No Full Sample + Resample |

### Filtros

```
Ret/DD Ratio (MC trades, Conf 100%)  >= 0
Net Profit   (MC trades, Conf 95%)   >= 50%  of Net Profit (Conf 50%)
Drawdown     (MC trades, Conf 95%)   <= 250% of Drawdown   (Conf 50%)
```

El primero es el más importante: **si el peor caso queda negativo, la estrategia se descarta.**

---

## 4️⃣ Monte Carlo — Retest Methods

Este MC permite **bajar a simulación por tick data** (lento). Donde el anterior desordena *trades*,
este perturba *los datos y los parámetros*.

| Ajuste | Valor |
|---|---|
| Number of simulations | 1000 |
| Use Full Sample | ✅ toda la muestra histórica |
| Backtest precision | 1 minute data tick simulation |

### Las tres opciones que Aritz activa

1. **`Randomize history data (by tick)`** — probabilidad 20% arriba / 20% abajo, cambio máximo de
   precio del 10% del ATR.
2. **`Randomize spread from 1 to 3`** — le gusta configurar un rango de spread **hasta el triple**.
   *Generalmente una estrategia que aguanta spread alto no cambia mucho de ahí.*
3. **`Randomize strategy parameters`** — probabilidad 10%, cambio máximo 20%. Con cambio simétrico, un
   parámetro en 100 se mueve entre 80 y 120; sin él, entre 100 y 120. Se puede subir la probabilidad a
   15% o 20%. **Cambios moderados**, para que la estrategia sea robusta ante simulaciones distintas.

**Advertencia sobre no pasarse.** Con valores más grandes estarías probando en un mercado mucho más
errático — tipo los aranceles de Trump. **Para evaluar spikes este no es el lugar.** *No es bueno
cargarse una estrategia por haber sido demasiado estricto acá. Ni mucho ni muy poco.*

Otras disponibles, no usadas por defecto: modified randomize history data (40%), randomize OHLC,
randomize min distance (0–10), randomize slippage (0–5), randomize starting bar (max 100).

### Filtros — **idénticos a los del MC de trades**

```
Net Profit   (MC retest, Conf 95%)   >= 50%  of Net Profit (Conf 50%)
Ret/DD Ratio (MC retest, Conf 100%)  >= 0
Drawdown     (MC retest, Conf 95%)   <= 250% of Drawdown   (Conf 50%)
```

> **En resumen, con los dos Monte Carlo estás tocando cinco cosas**: el spread, los parámetros de la
> estrategia, la data a nivel tick, el orden de los trades y un porcentaje de trades salteados.
> **Con esas cinco alcanza.**

---

## 5️⃣ Higher Backtest Precision

Backtest de precisión: más lento, más estricto en calidad de data. **Se va a perder calidad de
estrategia al aumentar la precisión** — y de ahí sale la regla de los filtros.

**Settings**. Symbol, timeframe, main test precision y main test spread vienen de la data del Builder
y **no se pueden cambiar**. Lo que sí se elige es `Crosscheck precision` y `Crosscheck spread` — Aritz
recomienda **usar el mismo spread que en el Builder**, aunque se podría poner el triple.

### Filtros — más livianos que los del Builder, y ese es el punto

| Filtro | Builder | **Higher Precision** |
|---|---|---|
| # of trades (FULL) | > 200 | **> 200** |
| Winning percent (FULL) | > 40% | **> 35%** |
| Net Profit OOS | > 0 | **> 0** |
| **Ret/DD Ratio (FULL)** | > 8 | **> 5 · por activo** |

**El Ret/DD varía por activo** *(y esto confirma que los umbrales no son universales)*:

| Activo | Ret/DD en Higher Precision |
|---|---|
| XAUUSD (oro) | **10** |
| NQ | **8** |
| DAX, SP500, divisas — *activos complicados* | **5** |

Opcional: `Drawdown OOS < 150% Drawdown IS`.

> **Ojo**: acá pueden caerse **más del 50% de las estrategias** del Builder si fuiste muy exigente.
> Como este crosscheck **baja la calidad** al subir la precisión, los filtros no pueden ser estrictos
> — si no, filtrás un porcentaje enorme.

---

## 6️⃣ Backtest on Additional Markets

**No es obligatorio**, pero da un extra: probar la estrategia en un activo similar.

Ejemplo: estrategia de XAUUSD probada en **XAGUSD (plata)** en H1, comisión 0,005% del equity, sin
swap. Se pueden agregar varios backtests adicionales, en otros timeframes u otros activos.

Con `Turn on detailed configuration` se puede backtestear con **datos tick de un período específico** y
elegir en `Test parameters → Precision` real tick con spread custom o el del broker.

**Para qué sirve realmente**: cuando ya tengas 10 estrategias buenas y seas rentable en un activo,
preguntarte *"¿cómo van estas 10 a spread real del broker en el último año?"* — y quedarte con la mejor.

---

## 7️⃣ Sequential Optimization

**Qué pregunta**: ¿somos muy dependientes de los parámetros?

Optimiza **parámetro por parámetro manteniendo los otros fijos**, buscando una **zona estable de
resultados**. A medida que se es más estricto, el fitness ronda cerca de 1.

### Settings

| Value distribution (% del valor original) | |
|---|---|
| Up | 30 |
| Down | 30 |
| **Step** | **40** — en 100 sería alta precisión y más estricto; **no es necesario**, y evaluarías muchos más parámetros |

**`Apply the optimized parameters to the strategy`: NO.** Ninguna optimización hecha en los
crosschecks se aplica a la estrategia ni le cambia sus valores por los óptimos.

**What to parametrize**: `Recommended parameters`.

### Filtros

| | Estándar | Menos estricto |
|---|---|---|
| % de parámetros que deben pasar el test de estabilidad | **100%** | 80% |
| Number of results in stable area | **15%** | 10% |
| Fitness stability range | **5%** | 8% |

> **En divisas vas a ver muchísima inestabilidad.** En oro y NQ —activos sencillos— no vas a tener
> problemas. Es la razón práctica detrás de la variante de umbrales para divisas.

---

## 8️⃣ System Parameter Permutation (SPP) — el detector de sobreoptimización

**Es el crosscheck conceptualmente más importante del módulo**, porque responde una pregunta que
ningún backtest responde: *¿esta estrategia está sobreoptimizada?*

**Cómo funciona.** Tenés un Ret/DD de 10 que salió del Builder y estás contento con los parámetros.
Lo que no sabés es si ese 10 es real o es el pico de suerte de una búsqueda. El SPP **optimiza esos
parámetros y compara el resultado original contra la mediana** de todas las permutaciones.

> **Si la mediana sale muy dispar del valor original, la estrategia está sobreoptimizada.** En el
> ejemplo, si la media da 2 contra un original de 10, está muy alejada.

Es exactamente la lógica de *"el mejor de N se ve mejor que la media por selección"*, aplicada a los
parámetros de una estrategia — y el mismo razonamiento que hace falta al rankear **grupos** de
estrategias.

### Settings

| | |
|---|---|
| Maximum tests | **15000** permutaciones (el límite es 20000) |
| What to parametrize | `Recommended parameters` — siempre |

### Filtros

**Optimization Profile conditions** — el crosscheck falla si alguna falla:

| Condición | Valor | Nota |
|---|---|---|
| ✅ % of Profitable Optimizations | **> 95%** | La gran mayoría de las optimizaciones deben ser positivas |
| ⬜ Average profit of all optimizations | — | No se usa: el fitness no está asociado al net profit |
| ✅ **Uniform distribution** — cambios de positivo a negativo | **< 5** | Estricto sería 3; **5 es moderado** |
| ⬜ Best optimization profit < StDev of average profit | — | Filtro muy estricto |

**System Parameter Permutation conditions**:

```
Ret/DD Ratio (Median) >= 70%  of Ret/DD Ratio (Higher)
Ret/DD Ratio (Median) <= 130% of Ret/DD Ratio (Higher)
```

Si no cumple, la estrategia queda en **fail**.

---

## 🔑 El patrón que atraviesa todo el módulo

Cada crosscheck rompe la estrategia **por un eje distinto**, y por eso se usan juntos:

| Crosscheck | Qué perturba |
|---|---|
| MC Trades | El **orden** de los trades, y saltea un 10% |
| MC Retest | La **data** (tick), el **spread** y los **parámetros** |
| Higher Precision | La **calidad de la data** |
| Sequential Opt. | Un **parámetro por vez**, buscando zona estable |
| **SPP** | **Todos los parámetros a la vez**, comparando original contra mediana |
| What-If | Una **hipótesis concreta** (swap, días, lotaje) |

Y la regla de calibración que se repite: **ni mucho ni muy poco**. Ser demasiado estricto en un
crosscheck no produce mejores estrategias — produce menos estrategias, y descarta buenas por ruido.
