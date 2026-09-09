# Base de Conocimiento IMOX - Módulo 2: SQX Builder

> Origen: mentorías del 18 y 19 de febrero de 2026, más los sets de Building Blocks.
> Transcrito 2026-09-07. Complementa `02_Mineria_y_Genetica.md` (teoría) con la **configuración
> operativa completa** del Builder.
>
> ⚠️ **Los umbrales de este módulo tienen variantes por timeframe y por clase de activo.** Ver la
> sección *Variantes* al final: los valores de la tabla de `INDEX.md` son los de H1 en índices y oro.

---

## 🧱 Los cuatro sets de Building Blocks

La clave del Builder no es la configuración base —que se comparte— sino **tener varios sets de BB y
generar lotes distintos con cada uno**. Es la herramienta principal de diversificación.

| Set | Enfoque | Idea |
|-----|---------|------|
| **BB1** | Base generalista | Amplio, exploratorio |
| **BB2** | Seguimiento de tendencia | Dirección e ímpetu |
| **BB3** | Volatilidad — "el explosivo" | Detectar la explosión del movimiento |
| **BB4** | Reversión / agotamiento | Fatiga y giro |

### BB1 — Base generalista

**Signals**: ADX (cambia dirección arriba, cruza nivel, mayor que nivel, is rising) · ATR (cambia
dirección arriba, is rising) · Volume is rising · Awesome Oscillator · Bollinger (abre bajo banda
inferior, abre bajo inferior tras abrir arriba, banda superior cayendo) · BullishEngulfing · CCI
(WFU Woodies Famir Up, WVD Woodies Vegas Down, falling, rising) · DeMarker (mayor/menor que nivel) ·
DI+ (falling, menor que DI−) · Highest/Lowest · HMA rising · Ichimoku (Kumo breakout bajista, Senkou
Span cross bajista) · KAMA · Kaufman Efficiency Ratio · Keltner · Laguerre RSI · Linear Regression ·
MACD · Momentum · Moving Average · OSMA · QQE · Reflex · RSI · Schaff Trend Cycle · SR Percent Rank ·
StdDev · Stochastic · Ulcer Index · Vortex · Williams %R.

**Indicators**: Bar Month · ADX, Aroon, Average Volume, Bears Power, CCI, Keltner, Kaufman
Efficiency Ratio, Linear Regression, MTATR, Reflex, ROC, Schaff Trend Cycle, SMA, Stochastic, Lowest
in range. **Prices**: Daily Close, Heiken Ashi Close, Low, Monthly Low, Open, Session High, Session
Low. **Operators**: Is Lower, Is Rising, Not.

**Stop/Limit Entry**: Bollinger, Daily Close/Open, EMA, GannHiLo, High, LinReg, Monthly Close/High/Low,
Session Close/High, SMA, Ichimoku.

**Order Types**: Enter at Stop. **Exits**: Profit Target, Stop Loss, Trailing Stop.

### BB2 — Seguimiento de tendencia

**Signals**: ADX mayor que nivel · Candle patterns (BullishEngulfing, Doji, Hammer) · HMA
(falling/rising) · Moving Average (falling/rising) · OSMA · SuperTrend (Up/Down Trend).

**Indicators**: Bar Hour · ADX, EMA, HMA, Linear Regression, SMA, SMMA, SuperTrend, TEMA.
**Prices**: Close, Daily Close, High, Heiken Ashi Close, Low, Open, Session High/Low/Open, Weekly
Close. **Operators**: Is Lower, Is Greater, Is Greater Or Equal, Crosses Above, Crosses Below, Is
Rising, Not.

**Stop/Limit Entry**: Close, EMA, High, Low, Open, SMA, SuperTrend, TEMA.
**Order Types**: Enter at Stop. **Exits**: PT, SL, TS.

### BB3 — Volatilidad / breakout

**Signals**: ADX is rising · ATR (cruza nivel, rising) · Bollinger (cierra sobre superior / bajo
inferior) · Keltner (cierra sobre superior / bajo inferior) · StdDev rising · Filtros de tiempo (Bar
Day Of Week, Bar Hour).

**Indicators**: ADX, ATR, Bollinger, Keltner, Standard Deviation. **Estructura**: Highest in range,
Lowest in range. **Operators**: Is Lower, Is Greater, Is Falling, Is Rising, Not Equals.

**Stop/Limit Entry**: Bollinger, Keltner, Highest, Lowest, Highest/Lowest in range. **Offsets**: ATR,
BarRange — permiten que la orden STOP se mueva dinámicamente según la volatilidad actual.

**Order Types**: Enter at Stop — mantiene la coherencia de entrar solo cuando el precio ya tiene
fuerza. **Exits**: PT, SL, TS, todos gestionados dinámicamente por ATR.

### BB4 — Reversión / agotamiento

Se eligen bloques de fatiga y giro que **no están en BB1, BB2 ni BB3**.

**Signals**: Aroon (Up rises from bottom, Down falls from top) · Bulls Power falling (agotamiento de
compradores) / Bears Power rising (agotamiento de vendedores) · Candle patterns (DarkCloud,
PiercingLine, ShootingStar) · DI− (cambia dirección) · Fractal (bullish/bearish) · ROC · CCI ·
DeMarker · Williams %R.

**Indicators**: Bulls Power, ROC, Ulcer Index, Fractal. **Precios estructurales**: Weekly High/Low,
Monthly High, Daily High/Low. **Volatilidad**: TrueRange.

**Operators — y acá está la clave del set**:
- `Is lower` / `Is greater`: comparar contra niveles de sobrecompra/sobreventa (CCI > 100).
- `Crosses Above` / `Crosses Below`: **críticos para el trigger** — entrar justo cuando el indicador
  sale de la zona de agotamiento (Williams %R cruzando hacia arriba el −80).
- `Is falling` / `Is rising`: confirmar que tras el agotamiento el impulso ya cambió de dirección.
- **`Is lower for X bars` / `Is greater for X bars`** — *el ingrediente secreto de BB4*. En mean
  reversion se busca **estrés** en el precio: *"si el precio estuvo por debajo del Low semanal
  durante más de 3 velas, prepárate para el giro"*.

**Stop/Limit Entry**: Pivots, Fibo, Fractal, Weekly High/Low, Daily High/Low. **Offset**: BarRange
(colocar el stop un poco por encima/debajo de la vela de señal).

**Order Types**: Enter at Stop — al usar STOP el Builder busca niveles que deben ser cruzados antes
de entrar, **asegurando que la reversión tenga momentum inicial**.

**Exits**: PT, SL, TS. **Recomendación IMOX**: mantener el SL basado en ATR (coeficiente 1–3) para
protegerse de continuaciones inesperadas de la tendencia previa.

---

## 🎯 Tipos de estrategia por activo

| Activo | Enfoque |
|--------|---------|
| Índices | Seguimiento de tendencia |
| Oro | Reversiones · **y trend following como "la reina"** |
| EURUSD | Seguimiento de tendencia **y** reversión a la media |
| GBP/JPY | Tendencia |

**Direcciones de trabajo**: Oro largos · NQ largos · DAX largos · Dow Jones largos · Libra/Yen largos
y cortos · Dólar/Yen largos y cortos.

### El Oro en detalle

1. **Trend following — la reina.** Activo macroeconómico de ciclos largos. Lo que mejor funciona son
   **Buy Stop en rupturas de máximos recientes**. Configuración IMOX: **solo largos**, porque el sesgo
   alcista histórico hace que los cortos sean ruidosos y menos robustos a largo plazo.
2. **Mean reversion en tendencia (pullbacks).** Identificar tendencia alcista en H4 y buscar entrada
   barata en H1 tras un retroceso (RSI sobrevendido o toque de banda inferior). **Ventaja**: SL más
   ajustado y Sharpe superior a 1,2.
3. **Breakout de volatilidad.** Aprovechar la apertura de Londres o Nueva York, donde el oro tiene
   picos de participación institucional. Entrar tras compresión de bandas (squeeze) da los trades más
   rápidos y limpios.

---

## ❌ Los 5 errores que matan estrategias en SQX

1. **Usar pocos datos o un solo conjunto.** Historiales de 10 a 20 años, probar distintos mercados y
   timeframes.
2. **Ignorar la robustez.** Enamorarse de un backtest perfecto y omitir Monte Carlo o Walk Forward
   Matrix. Hay que someter las estrategias a estrés y sacudirlas.
3. **Complicar en exceso las reglas.** Cuantas más reglas, más te atás al pasado en lugar de predecir
   el futuro. **Las mejores estrategias son sencillas**: una o dos condiciones, lógicas fáciles de
   entender, robustas en varios mercados. La complejidad no es sinónimo de solidez.
4. **Configuración incorrecta del broker o los datos.** Spread, comisiones y horario deben coincidir
   con el broker real.
5. **Perseguir el santo grial.** No existe, y una estrategia nunca será suficiente. Pensar como
   inversor: **portfolio, diversificando entre instrumentos, timeframes y enfoques**, para que cuando
   uno falle los otros compensen.

---

## ⚙️ Configuración del Builder, paso a paso

### 1. Primeros pasos
Views/perspectiva, last generation, portfolio. Progreso: genetic evolution info, fitness del
databank, cargar constructores. Usar plantillas por defecto. Fitness de evolución en cada isla.

### 2. What to Build

**Strategy Type**
- **Simple strategy** ← el que se usa
- *Multi TF*: estrategias que usan más de un timeframe. **IMOX no las usa** — se separa por TF.
- *Strategy for template*: partir de un edge observado. Modo avanzado.
- *Improve existing strategy*: importar una estrategia para sacarle hijos. Modo avanzado.

**Additional build config**
- **Trading direction**: Long / Short / Both / Entry-Exit Symmetry. Las `Both` se usan más para
  acompañar; **se prefieren Full Long o Full Short**. La simetría coloca Buy Stop y Sell Stop a
  distancia simétrica — Aritz la probó y **no le gusta**.
- **Strategy Style**: *SQX Signals Style* (no tan estricta) · *con Fuzzy Logic* (más estricta, trabaja
  por porcentaje) · *Old SQ3 Style* (muy estricta, la usa para strategy-for-template).
- **Build Mode**: Genetic Option o Random.

**Condiciones y períodos**

| Parámetro | Valor |
|---|---|
| Conditions in entry rule | min 1 — max 3 |
| Conditions in exit rule | min 1 — max 2 (SL, TP, trailing) — las suele activar |
| Global indicators period | min 5 — max 200 |
| Global lookback (shift) | max 1 — **le gusta la barra anterior, no la actual (0)**; se puede llevar hasta 5 |

**Stop Loss**

| Modelo | Rango |
|---|---|
| Fixed Pips | 50 – 300 |
| Percent | 1 – 10 |
| **ATR based** ← *el que más le gusta hoy* | **ATR Multiple 1,2 – 5** · **ATR Period 150 – 250** |

**Profit Target**

| Modelo | Rango |
|---|---|
| Fixed Pips | 50 – 500 |
| Percent | 1 – 10 |
| **ATR based** ← *el que más le gusta hoy* | **ATR Multiple 2 – 6** · **ATR Period 250 – 350** |

**Por qué ATR.** Mide la volatilidad pasada y acciona SL o TP en consecuencia, lo que hace que **la
estrategia se acomode al mercado actual**. Casi todo lo que tiene va por ATR: son las más robustas y
**permiten una diversificación enorme**.

**No se limita el Risk-Reward.** Pueden salir estrategias 2:1, 2,5:1 o 3:1. Hay que dejar que el
Builder analice, en base al mínimo y máximo del ATR, cuál es el rango que mejor se comporta — que sea
dinámico.

> **DALE ANCHO DE BANDA AL CONSTRUCTOR.** No lo limites, porque si no se bloquea y no encuentra nada.

*Use indicator levels*: Aritz no le ha dado uso.

### 3–4. Generación genética y Genetic Options

**Teoría.** Primero se **rellenan las islas** con la población inicial de forma aleatoria. El primer
filtrado es el de genetic options — básico, para partir de una población inicial. Recién ahí empieza
la **evolución genética**: cruce, mutación, generaciones de hijos, migración.

- **Cruce** (ej. 80%): padre RSI 14 × madre RSI 12 → hija, como un árbol genealógico.
- **Mutación** (ej. 20%): cambiar un parámetro interno (período de un indicador, grados de cruce).
- El fin de ambos es **encontrar estrategias diversificadas**.
- Se repite tantas veces como generaciones se configuren.
- Si la evolución **se estanca**, SQX puede reiniciar la isla desde cero, o **migrar** cada X
  generaciones un % de estrategias entre islas.

**Configuración**

| Genetic Options | |
|---|---|
| Max # of generations | 100 |
| Population size (por isla) | 150 |
| Crossover probability | **80%** |
| Mutation probability | **20%** |

| Island options | |
|---|---|
| Islas (evolución separada) | 8 |
| Migrar cada X generaciones | 20 |
| Tasa de migración | 20% |

| Initial population | |
|---|---|
| Size | 1200 |
| **Generated decimation coefficient** | **2** — el doble de estrategias por isla quedándose con las mejores. **Obligatorio según Aritz**: aumenta muchísimo la calidad, aunque es más lento |

**Filter generated initial population** — *ser poco estricto*: filtros todos por IS, sobre # of
trades, winning percentage y Ret/DD ratio.

**Fresh blood**: detectar estrategias iguales y reemplazarlas · reemplazar el 10% de las más débiles
cada 2 generaciones · mostrar el databank de la última generación de la isla 1.

**Evolution management**: empezar de nuevo al terminar · **reiniciar si el fitness del IS se estanca
25 generaciones** (importante si se usan más de 100 generaciones).

**Nota de hardware.** Aritz trabaja con 16 núcleos y 128 GB de RAM, 5 o 6 islas. Si no vas a crear
muchas generaciones, no hace falta migrar. **Poca probabilidad de mutación, alta de cruce.** La
población siempre por encima de las generaciones. **La configuración va atada al hardware.**

### 5. Trading Options

| | |
|---|---|
| Exit on Friday | 20:30 |
| Time Range | 01:00 – 23:00 |
| Realistic gaps handling | activo |

Se sale los viernes para **evitar gaps de apertura del fin de semana** y el swap del tiempo sostenido.
El rango horario evita entrar en cambios de día. La idea general es **eliminar variables
impredecibles del mercado**.

### 6. Data

- **Engine**: MT4 / MT5.
- **Symbol**: seleccionar **el clon con el ajuste de timezone para Darwinex**.
- **Timeframe**: H1, H4, M5, M15.
- **End day**: dejar un colchón — **guardarse el último año** para backtestear después. Son 10 años
  de backtest.
- **Precision**: `FASTER` para el Builder, `TICK` para el Retester.
- Validar que el símbolo esté asociado al instrumento correcto, o los valores de test saldrán mal.

### 7–8. IS / ISV / OOS

| Segmento | Proporción | Rol |
|---|---|---|
| **IS** (in-sample) | 70–80% | Entrenamiento |
| **ISV** (validación) | 10–20% del IS | Oculto en *genetic options*, **visible en el ranking**. Va **antes** del OOS |
| **OOS** (out-of-sample) | 20–30% | La prueba de examen |

**Qué es el OOS.** Preguntas de examen que **no** formaron parte del entrenamiento. Ejemplo: 8 años
de IS puro y 2 de validación. La idea es que la estrategia **se comporte parecido en IS y OOS** — eso
es robustez, no rentabilidad.

**Dónde poner el OOS.** Conviene **siempre en bear markets**, para ver cómo se comporta. El objetivo
es sacar estrategias que **venzan el bear market sin usar cortos, solo largos**, ya que el oro es
tendencial. Con `Show chart` se ven las caídas del activo y se segmentan los OOS sobre esos períodos.

Si estás en long, poner en el ranking `OOS1 net profit > 0` **y** `OOS2 net profit > 0`, para validar
que ambos son positivos.

### 9. Money Management

**Initial capital**: 100.000 (cuenta de 6 cifras).

**Fixed amount** (con SL basado en ATR) — el modelo por defecto:

| | |
|---|---|
| Risked money | **200** (0,20% de 100.000 — conservador) |
| **Size decimals** | **1** |
| Size if no MM | 0,1 |
| Maximum lots | 10 |

**Por qué 1 decimal y no 2.** No quiere clavar el SL de 200 en todo lo que testea, **porque es falso**:
en el pasado casi nunca saliste exactamente en 200 — hay slippages que te sacan en 230, 250, 260. Con
un decimal se introduce esa imprecisión a propósito. **Con 2 decimales se trabaja más adelante**,
cuando se busca precisión.

Así, el día de mañana puede combinar todas las estrategias a 200 de SL y analizarlas en conjunto.

**Fixed size** (si se trabaja con SL en fixed pips o percent): order size 0,1.

Referencia: 1 = lote · 0,1 = minilote · 0,01 = microlote.

### 10. Building Blocks — cómo usarlos

En el Builder se tiene **una configuración base** y se van creando **distintos sets de BB** para
generar lotes de estrategias y diversificar. También se cambian los Stop/Limit entre blocks.

- **No seleccionar todo**: el Builder se vuelve abarcativo, crea *estrategias engendro* y tarda
  muchísimo. No es eficiente.
- **Los indicadores típicos de toda la vida son los que mejor rendimiento tienen a largo plazo.**
- Se pueden customizar bloques: *parameter sets*, *parameter values* y **weight** (por defecto 1; si
  subís unos sobre otros, les das prioridad).
- **Calibrate indicators**: acotar mínimo, máximo y step. Es alta precisión — **aconsejable para una
  etapa avanzada**; Aritz no ha necesitado calibrar.

**Order types**

| Tipo | Veredicto |
|---|---|
| (MKT) Enter at market | ❌ Influye mucho el spread: en volatilidad el precio es distinto, da incoherencias a largo plazo |
| **(STOP) Enter at Stop (Buy Stop)** | ✅ **Base** — el que mejor resultados le ha dado en robustez a largo plazo |
| (LMT) Enter at limit | ❌ No le gustan: estás en el precio, cae como un cuchillo y te saca |

**Exit types**: Profit Target Required ✅ · Stop Loss Required ✅ · **Trailing Stop Required ✅** — por
ATR con mínimo y máximo con holgura, para no ser tan estricto. También existen *Exit after bars*
(rango 5–50, interesante en divisas), *Move SL to BE*, *Trailing activation*, *Exit rule*.

### 11. Ranking

Filtra qué estrategias se guardan en el results databank. **Son las preguntas de examen sobre el OOS.**

- **Maximum top strategies to store**: 500 (depende de la velocidad de la máquina).

**Strategy Quality ranking (fitness)** — computar desde:
- Net Profit
- **Ret/DD ← el que recomienda Aritz.** *El concepto más importante para evaluar estrategias; lo
  sencillo funciona en los mercados.*
- R Expectancy (Van Tharp)
- Annual Return % / Max DD %
- Weighted Fitness (multiple goals)

**Automatic filters** — Aritz los pone **todos** (10): no trades · too many ambiguous trades · too
many open trades · no filled trades · outlier trade · zero P/L o zero duration · unfinished trades ·
too many little trades · too many trades closing at the same time.

**Custom filters — los mínimos, y son tres**

| Filtro | Umbral | Alcance |
|---|---|---|
| Winning percent | **> 40%** | FULL + main data |
| **Ret/DD Ratio** | **> 8** | FULL + main data |
| # of trades | **> 200** | FULL + main data |

Opcional: `Net profit (OOS + main data) > 0` — la pregunta de examen no vista en generación debe ser
positiva.

> **El principio que ordena todo el Builder**: **menos estricto en Genetic Options → más estricto en
> Ranking.**
>
> Ser muy estricto en las condiciones de construcción **no implica** tener una buena estrategia.
> Computar por muchas variables con distintos weights buscando la perfección **es sobreoptimización**:
> vas a encontrar la estrategia ideal que no pierde y que no cae, y **eso es falso**, porque en
> trading se pierde. **Alta precisión significa sobreoptimización.**

### 12. Cargar configuración base — y no caer en copy trading

Para no repetir lo que hace todo el mundo:
- Modificar un poco el filtrado en genetic options (más Ret/DD, distinta cantidad de generaciones).
- **Modificar los Building Blocks**: añadir unos, quitar otros.
- Modificar los parámetros del ranking.

### 13. Filtrar estrategias en el databank

Seleccionar al menos **300 estrategias** para el Retester — eso tarda 3 o 4 días. Más de 300–400 te
lleva la semana entera.

El procedimiento usa **tres variables distintas** para no seleccionar tres veces sobre el mismo eje:

1. Filtrar por **Ret/DD ratio FULL** → quedarse con ~100, guardar, borrar.
2. Filtrar por **net profit OOS** → otras ~100, guardar, borrar.
3. Filtrar por **Ret/DD ratio OOS** → otras ~100, guardar, borrar.

Con el remanente se puede ordenar por profit factor o Sharpe y quedarse con 10–50 más.

---

## 🔀 Variantes por timeframe y clase de activo

> **Los umbrales del Builder NO son universales.** `INDEX.md` los presenta como criterios únicos; en
> realidad los de la tabla corresponden a **H1 en índices y oro**. Estas son las variantes documentadas.

### H4

| Parámetro | H1 | **H4** |
|---|---|---|
| # of trades (Builder) | > 200 | **> 150** |
| Exit on Friday | activo | **desactivado** (opcional) |

- Se trabaja **con o sin trailing stop**.
- **Hay que tener en cuenta el swap** — es lo que hace H4 inviable en props.
- En H4 el efecto del MM se nota más, simplemente porque **los SL tienden a estar más lejos que en H1**.
- **La estrategia debe aguantar ambos MM**: backtestear en MM 1 y MM 2 decimales. Aritz tiene oro/NQ/DAX
  en H4 desde 2025 en mercado, con MM a 1, y lleva pleno en supervivencia.
- H4 es muy bueno para largo plazo: **bueno en Darwinex Zero y capital propio, no en prop firms, y no
  en Axi Select porque te come el score**.

### Divisas

| Fase | Parámetro | Estándar | **Divisas** |
|---|---|---|---|
| Builder | Salidas por barras | — | avanzado |
| Retester | MonteCarlo filtro Ret/DD (MC retest, conf. 100%) | activo | **desactivado** |
| Optimizer | WF Ret/DD | 8–10 | **5** |
| Optimizer | WF Stagnation | 365 | **500** |

Preguntas abiertas del módulo: ¿los mismos multiplicadores ATR son válidos? ¿largos y cortos por
separado?

### Axi Select — modelo de pips

Para **cuentas pequeñas** (USD 1.000, el NQ) se usa **modelo de pips**, no ATR:

- **SL 40–80 pips · PT 100–250 pips.**
- **El mínimo del PT debe ser superior al máximo del SL.**
- Con estrategias de pips, el MM debe ser **Fixed size** — minilotes 0,1 o microlotes 0,01.
- **Por encima de USD 3.000 ya se puede usar ATR**, que es lo más robusto a largo plazo.

**Sobre el período del ATR**: períodos largos (100–250) van mejor en mercados volátiles; los cortos
son 1–50. *No hacer caso a lo que dice la IA*, que generalmente sugiere entre 1 y 40 — **dejar
períodos más largos**.

---

## 🧭 Doctrina de diversificación

- **La perfección no está en las cuentas, está en la diversificación de las estrategias**, que tienen
  distintos RR.
- **El tiempo hay que invertirlo en los Building Blocks**, tratando de diversificar por activo a gran
  escala: diversificar por indicadores, I+D, preparar bloques, explorar. En generación **no seas
  estricto** — necesitás ver que salgan estrategias primero — y después analizás y clasificás.
- Cambiar modelos de Buy/Stop para diversificar: poner **4 a 6 sobre los mismos BB** e irlos cambiando.
- Después de ver en demo qué estrategias funcionan mejor, **se puede investigar la lógica** de por qué
  funcionan y buscar por esos bloques o indicadores.
- **Filtros muy extremos llevan a sobreoptimización** — por ejemplo PF > 2.
- El **fitness es un KPI muy importante** que se usa en el resto del flujo de creación y validación.

---

## ❓ Pendientes que este módulo deja abiertos

- ¿Qué es y cómo funciona el modelo ATR en detalle? ¿Y el modelo de pips?
- ¿Qué es el Sharpe Ratio, con ejemplos?
- ¿Cómo funciona el trailing stop, que hoy es obligatorio?
- ¿Los mismos multiplicadores ATR valen para divisas? ¿Largos y cortos por separado?
