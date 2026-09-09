# Base de Conocimiento IMOX - Módulo 10: Mentoría 1 a 1 — ⏳ PENDIENTE

> Registrado 2026-09-07.
>
> # 🚨 ESTE DOCUMENTO NO CONTIENE RESPUESTAS
>
> **La mentoría 1 a 1 todavía no se realizó.** Lo que sigue son **preguntas abiertas** del usuario y
> **su propio plan de arranque**, no doctrina de la academia.
>
> **Ningún agente debe tratar el contenido de este archivo como conocimiento validado.**
> Las preguntas marcan **exactamente dónde la KB tiene huecos**. El plan del usuario es una
> **intención declarada**, sujeta a cambio tras la mentoría.
>
> Cuando la mentoría se realice, este documento se reemplaza por las respuestas y se re-clasifica.

---

## 🎯 Para qué sirve este documento

Dos cosas, ambas útiles hoy:

1. **Es el inventario de huecos de la KB.** Varias de estas preguntas coinciden exactamente con los
   gaps que quedaron marcados al transcribir los módulos 6, 8 y 9.
2. **Contiene la estructura objetivo concreta del usuario** — qué cuentas, qué capital, qué activos y
   qué frecuencia de trades. Es el insumo de configuración más específico que existe para el
   simulador.

---

## ❓ Preguntas abiertas — sin responder

### Sobre portfolios y correlación *(coincide con el gap crítico del módulo 9)*

> **¿Cuál es la herramienta que nos ibas a mostrar para crear portfolios y gestionar el riesgo
> individual de cada estrategia? ¿Cuáles son los 4 tipos de correlaciones que hacés?**

> **¿Cómo medís el VaR?**

⚠️ **Estas dos son el hueco más consecuente de toda la KB.** El
[09_AlgoWizard_y_QA4.md](09_AlgoWizard_y_QA4.md) marca las secciones de *Portfolio Master* y
*Correlación/Descorrelación* como críticas y **no transcritas**, y remite los 4 tipos de correlación y
el VaR precisamente a esta mentoría. **Es el núcleo del simulador de esta aplicación.**

### Sobre diversificación de cuentas Darwinex

> **Quiero crear 2 Darwinex, ¿qué me recomendás para diversificar? ¿Cuántas estrategias y riesgo
> individual? ¿Estrategias mixtas y descorrelacionadas?**
>
> *Idea propia del usuario*: **un DZ con ORO/NQ/DAX/DIVISA en MT4 y el otro con BTC.**

⚠️ **BTC no figura en los activos objetivo de la academia.** El
[00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md) lista oro, Nasdaq, DAX y divisas.
**No hay doctrina de la KB sobre cripto** — ni configuración de instrumento, ni umbrales, ni perfil.

### Sobre timing de prop firms

> **¿Es mejor operar las prop firms (FTMO, TTP) cuando el mercado está dulce, o cuando viene de meses
> de caídas y lateralización?** Ya que están orientadas más al corto plazo.

*Contexto de la KB que roza el tema pero no lo responde*: el módulo 9 dice *"es mejor empezar a operar
en LIVE cuando el mercado te da ostias que empezar con mucho dinero"*, y el módulo 8 dice que hay
**épocas malas y buenas en el año según el activo**. Ninguna de las dos responde la pregunta para el
caso específico de un challenge.

### Sobre plataforma

> **Migración total a MT5, ¿qué recomendás? Todas mis EAs están en 136 con MT4.**

*Dato relacionado (módulo 7)*: **MT5 trae la tick data incorporada; MT4 no.**

### Sobre selección para challenges

> **¿Qué modelo de estrategias posee la probabilidad de pasar el desafío antes?**

*Lo más cercano en la KB*: módulo 5 (*"las estrategias con mucho `Avg Trades Month` sirven para pasar
challenges de fondeo"*) y módulo 8 (la carrera de doble objetivo). Pero **no hay un "modelo" definido.**

### Sobre copy trading

> **¿Cómo usar el copiador de trading? Por ejemplo `trader connect`.**

*Pendiente heredado del módulo 8*: *"VER COPY TRADING EN AXI: `fxblue` y `trader connect` es
espectacular."*

### Sobre nomenclatura

> **¿Nombres de las EAs en live que recomendás? Nomenclatura.**

📌 **Nota de la aplicación**: ya existe una convención de nombres para los archivos de importación de
backtest (`1-DEPLOY__…`, `2-EVAL__…`, `3-WFEXPORT__…`). **No es lo mismo** —esa nombra archivos de
importación, esta nombraría EAs desplegadas— pero conviene que las dos convenciones se decidan juntas
para que compartan los identificadores de estrategia y versión.

### Sobre la comparación demo vs backtest *(ya está en el plan del simulador)*

> **¿Cómo comparamos las estrategias de SQX con las de demo en el tiempo que comparten? ¿Por KPIs,
> lista de trades?**

⚠️ **Esta es exactamente la funcionalidad que el simulador tiene planificada.** El módulo 7 menciona
el bucle (*"al año y 3 meses comparás las estrategias que pusiste en el mercado vs lo que saca SQX"*)
pero **no dice el método**. La pregunta de si se compara por KPIs o por lista de trades **está sin
responder y es una decisión de diseño**, no un detalle.

### Sobre mantenimiento del portfolio

> **Recambio de estrategias en un portfolio: ¿cómo puedo identificar aquellas que no están rindiendo
> según el BT? En DZ, ¿cada cuánto conviene hacer revisión del portfolio y cambiar las estrategias o
> modificar el MM?**

⚠️ **El ciclo de vida del portfolio no está en ninguna parte de la KB.** Todos los módulos tratan la
**construcción** de un grupo; ninguno su **mantenimiento**. Es una dimensión faltante del simulador.

*Dato relacionado (módulo 5)*: la WF Matrix produce una **cadencia de reoptimización** (ej. cada ~349
días). Podría ser el punto de partida, pero aplica a una estrategia, no a un portfolio.

### Sobre FTMO y repetición de estrategias

> **En FTMO no deberíamos repetir estrategias en distintos challenges, ¿cierto? Por temas de copy.**

⚠️ Si se confirma, es una **restricción dura del generador de grupos**: dos challenges simultáneos no
pueden compartir estrategias, lo que convierte el problema en un **reparto sin reposición** sobre el
pool, no en selecciones independientes.

---

## 📋 Requisitos y logística de la mentoría *(esto sí está confirmado, del módulo 9)*

**Qué es**: una mentoría privada, **sin límite de tiempo**, con requisitos mínimos, que se solicita en
privado y **no tiene fecha límite**.

**Temas**: escalar a los mercados reales · de virtual a real · Props Firms / DZero / Capital Propio ·
toma de decisiones · dudas.

### Requisitos mínimos

1. **Seguimientos de test terminados**
2. **Academia terminada**
3. **Preparar la estructura que va a real**

### Qué presentar en la mentoría — y las respuestas del usuario

| Pregunta de la mentoría | Respuesta del usuario |
|---|---|
| ¿Cuántos desafíos querés comenzar por mes en prop? | **1 desafío por mes en FTMO, quizás de 10.000** |
| Axi Select: ¿patrimonio de inicio? ¿Poseés divisas? | **1.000 USD. Solo tengo USDJPY** |
| ¿Deseás comenzar con DZero patrimonio propio? ¿H4 terminado? | **Por el momento no**; solo crear un Darwin y más adelante otro con MT5, para ver la diversificación |
| Activos realizados: ORO/NQ/DAX/Divisa | **Sí, todos en H1 y H4: Oro, NQ y DAX. En pips: ORO y NQ** |
| ¿Se puede comenzar con 2 activos? | *(Respuesta de la academia)*: **Sí, pero tendrás estructura pequeña** |

**Advertencia de la academia sobre cuándo tomarla** *(módulo 9)*:

> **Solo tomarla cuando estés seguro de escalar a mercado real.** Si estás muy verde y arrancás con
> una estructura pobre, podés sentirte mal en comparación con otros compañeros.
> **Escalar pronto no es sinónimo de éxito.**
> **Darwinex Zero no la requiere**: es una decisión de largo plazo que podés hacer a tu tiempo.

---

## 🏗️ ESTRUCTURA PENSADA PARA COMENZAR

> **Intención declarada del usuario**, previa a la mentoría. Sujeta a cambio.

| # | Componente | Detalle |
|---|---|---|
| 1 | **FTMO** | **1 challenge de 10k mensual** |
| 2 | **Darwinex Zero** | **1 DZ con MT4**, estrategias **mixtas de ORO/NQ/DAX en H1 y H4** |
| 3 | **Darwinex Zero (futuro)** | **Más adelante, otro DZ con MT5** |
| 4 | **Axi Select** | **1.000 de capital** |

**Criterios de selección declarados:**

> - **Buscar EAs que abran poco y seguro.**
> - **Tener estrategias H1 o H4 que abran 3 o 4 trades al mes.**

### ✅ Coherencia con la doctrina de la KB

Estos dos criterios **coinciden exactamente** con:

- **Módulo 5, paso 7**: `Avg. Trades Month` bajo.
- **Módulo 9**: *"portfolios de largo plazo en altas temporalidades (H4) y bajo avg trades"*.
- **Módulo 0**: H4 son **2 o 3 trades por mes** y produce estrategias extremadamente robustas.

### ⚠️ Tensiones a resolver en la mentoría

| Tensión | Detalle |
|---|---|
| **Tamaño del challenge FTMO** | El usuario planea **10k**; el módulo 8 dice que **recomienda 20k o 50k**, con los que se puedan ganar 200-300 USD |
| **"Abrir poco" vs Axi Select** | Los módulos 5 y 8 dicen que las estrategias de **muchos trades** son las que sirven para challenges y para cumplir **el conteo de trades de Axi en 60 días**. Un pool de 3-4 trades/mes puede **no alcanzar la cuota** |
| **Divisas disponibles** | Solo **USDJPY**. El módulo 8 dice que las divisas a microlote son **el relleno legítimo** de la cuota de trades de Axi. Con una sola divisa el margen es estrecho |
| **BTC** | Sin doctrina en la KB |
| **Capital de Axi** | 1.000 USD; el módulo 8 recomienda **500 para aprender**, y el módulo 9 advierte que **con 1.000 el DD puede llegar al 92%** en backtest |

---

## 🔴 Gap estructural nuevo — estrategias en SHORT

*Planteado por el usuario 2026-09-07. **No hay doctrina de la academia sobre esto.***

> *"Algo a tener en cuenta que no hemos trabajado: **minar y crear estrategias en short** para
> compensar y trabajar cuando el mercado está lateral o bajista."*

### Por qué es un hueco real y no un detalle

**Toda la KB asume long-only.** La evidencia:

| Dónde | Qué dice |
|---|---|
| [XAUUSD_Profile.md](XAUUSD_Profile.md) | *"bullish bias (**Long-Only**)"* |
| [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) | *"Estrategias **full largos**, solo veo si el 2022 quedó positivo, ya que fue bear market"* |
| [MEASURED_FTMO_Demo_Baseline.md](MEASURED_FTMO_Demo_Baseline.md) | **48 de 48 trades fueron BUY.** Cero shorts en datos reales |

### ✅ No contradice la doctrina — la completa

[01_SQX_Data.md](01_SQX_Data.md) prohíbe operar **contra-tendencia**. **Un short en tendencia bajista
no es contra-tendencia: es seguir la tendencia.** El sesgo long-only de la academia viene de los
activos elegidos (oro e índices tienen deriva alcista estructural), no de una prohibición de vender.

### Tres razones por las que esto importa más de lo que parece

1. **Es la respuesta al test de 2022.** El módulo 5 valida una cartera long-only preguntando si
   sobrevivió el último bear market. Con shorts en la cartera, **ese test cambia de naturaleza**: ya
   no se trata de sobrevivir un mercado bajista sino de aprovecharlo.
2. ⭐ **Puede ser la herramienta de descorrelación más potente disponible.**
   [SERVICE_Darwinex_Zero.md](SERVICE_Darwinex_Zero.md) impone **correlación < 0.5 entre tus propios
   DARWINs** como condición de cobro. Una cartera long-only sobre oro, NQ, DAX y BTC — todos activos
   *risk-on* — tiende a correlacionar alto justo cuando importa. **Un componente short es
   estructuralmente descorrelacionado**, no por casualidad estadística sino por construcción.
3. **Cubre el hueco de régimen.** La cartera actual no tiene ninguna respuesta a un mercado lateral o
   bajista salvo no operar.

### ✅ Lo que la KB YA responde — verificado 2026-09-08

Al revisar el material antes de tratar esto como un hueco entero, **dos preguntas ya estaban
contestadas**:

#### 1. El Building Block set existe: **BB4**

[02_SQX_Builder.md](02_SQX_Builder.md) § *BB4 — Reversión / agotamiento* es exactamente la lógica de
*"corrección después del impulso"*:

> *"En mean reversion se busca **estrés** en el precio: si el precio estuvo por debajo del Low semanal
> durante más de 3 velas, prepárate para el giro."*

Y trae **la reconciliación con la doctrina incorporada**, en dos mecanismos:

| Mecanismo de BB4 | Qué resuelve |
|---|---|
| **`Enter at Stop`** — *"al usar STOP el Builder busca niveles que deben ser cruzados antes de entrar, **asegurando que la reversión tenga momentum inicial**"* | No entra contra el movimiento: **espera que el giro se confirme** |
| **SL por ATR (coeficiente 1-3)** — *"para protegerse de **continuaciones inesperadas de la tendencia previa**"* | Reconoce el riesgo contra-tendencia y lo acota explícitamente |

> 🔑 **BB4 no es contra-tendencia ciega — es un giro que exige confirmación.** Por eso convive con la
> doctrina del módulo 1 sin contradecirla.

#### 2. El swap ya estaba respondido, y **favorece al short**

[01_SQX_Data.md](01_SQX_Data.md) lo dice literal:

> *"Con el oro en máximos, abrir un long es carísimo en swap y **abrir un short te pagan**, porque vas
> contratendencia."*

**El short cobra rollover cuando el activo está en tendencia alcista.** Es un viento de cola económico,
no solo una cobertura de régimen. Confirmado en los datos reales:
[MEASURED_FTMO_Demo_Baseline.md](MEASURED_FTMO_Demo_Baseline.md) muestra swaps **negativos** (−2,00 /
−1,29) en las posiciones largas de BTCUSD sostenidas de un día al otro — un short habría estado del
lado que cobra.

⚠️ Y esto **reabre la decisión de no configurar swap en el Builder**, que se tomó en parte por el bug
de la v136. En FTMO el swap entra dentro del equity que leen los dos límites de drawdown; en una
cartera con shorts deja de ser un costo uniforme y pasa a ser **una asimetría entre direcciones**.

### 🔻 La doctrina es más angosta de lo que se había registrado

El módulo 1 no dice "no operes short". Dice:

> *"No se buscan sistemas en contratendencia. Ejemplo: **Nasdaq bajista → prefiere no operar**."*

**La doctrina prescribe NO OPERAR en un mercado bajista.** Eso no es una prohibición del short: es una
**limitación reconocida** — la academia deja el capital parado porque su herramienta es long-only.
**Una cartera con shorts es la respuesta a esa limitación, no una violación de la regla.**

### ❓ Qué sigue faltando

- ¿Los umbrales de selección cambian para short? El módulo 2 documenta variantes por timeframe y por
  clase de activo, **ninguna por dirección**. Con BB4 el `# of trades` y el `Win %` podrían comportarse
  muy distinto — mean reversion suele dar win rate alto y RR bajo, justo lo que el filtro de RR > 2-3
  castiga.
- ¿Cómo se compone una cartera mixta? ¿Pares long/short del mismo activo? ¿Shorts solo en ciertos
  activos?
- ¿El 2022 pasa a ser **muestra de entrenamiento** en vez de test?
- ¿BB4 se mina igual en short que en long, o hay asimetría? (Las caídas suelen ser más rápidas y
  volátiles que las subidas — el SL por ATR puede necesitar otro coeficiente.)

### 📍 Hipótesis del usuario: **BTC es el mejor activo para el short**

*Planteada 2026-09-08.* > *"Donde veo que pueden funcionar bien las estrategias con short es en BTC,
ya que es un activo volátil y por ende se pueden aprovechar los mercados bajistas y las correcciones
luego de los impulsos grandes."*

**Contiene DOS trades distintos que conviene separar antes de minar:**

| | Lógica | Doctrina | Set |
|---|---|---|---|
| **a) Aprovechar mercados bajistas** | Short en tendencia bajista = **seguir la tendencia** | ✅ Consistente | BB2 (tendencia) |
| **b) Correcciones tras impulsos grandes** | Short después de una subida = **mean reversion** | ⚠️ Es lo que el módulo 1 llama contra-tendencia — **válido solo con el `Enter at Stop` de BB4** | BB4 (agotamiento) |

**Son arquetipos opuestos y se minan por separado.** Mezclarlos en un mismo Builder produce híbridos
que no pasan ningún crosscheck.

#### A favor de BTC

- ✅ **Volátil de verdad**: en los datos medidos, distancias de SL de **700-900 puntos ≈ 1% del precio**.
- ✅ **El short cobra swap** mientras el largo lo paga (§ arriba).
- ⭐ **Descorrelación estructural**: un short de BTC contra un largo de NQ está **opuesto por
  construcción**. Es el argumento más fuerte, y ataca directo el gate de **correlación < 0.5** de
  [SERVICE_Darwinex_Zero.md](SERVICE_Darwinex_Zero.md).
- ✅ **BTCUSD es elegible en los tres servicios** (en Axi es uno de solo dos cryptos permitidos).
- ✅ Los drawdowns de BTC son mucho más profundos que los de los índices — el lado corto tiene más
  recorrido que en oro o NQ.

#### ⚠️ Advertencias honestas

1. 🔴 **Los datos medidos NO respaldan esta hipótesis — y es fácil creer que sí.** En la ventana
   medida BTC osciló entre ~77.600 y ~81.300 **sin tendencia**, y el sistema long-only sangró. Eso es
   evidencia de que **una estrategia direccional pierde en lateral**, no de que el short habría
   ganado. **En lateral el short sangra igual, simétricamente.** La inferencia "el largo perdió, luego
   el corto gana" no se sostiene.
2. **BTC tiene deriva alcista estructural.** El short pelea contra la tasa base, que es exactamente
   por qué la academia es long-only en oro e índices.
3. **La volatilidad no favorece ninguna dirección por sí sola.** Da movimientos más grandes a favor
   y en contra. Los propios datos lo muestran: **76% de win rate y aun así pérdida neta**.
4. **Darwinex requiere una cuenta Crypto CFD (MT5) aparte** para BTC — subscripción adicional de 45€/mes.
   Coincide con el plan de dos DZ, pero hay que presupuestarlo.

**Prioridad alta para la 1 a 1**: es el único hueco identificado que **agrega una capacidad nueva** en
lugar de refinar una existente.

---

## 🔗 Índice de gaps que esta mentoría debería cerrar

| Gap | Origen |
|---|---|
| **Los 4 tipos de correlación** | [09](09_AlgoWizard_y_QA4.md) — **crítico para el simulador** |
| **Cómo se mide el VaR** | [09](09_AlgoWizard_y_QA4.md), [06](06_Gestion_de_Riesgo.md) |
| **Portfolio Master en detalle** | [09](09_AlgoWizard_y_QA4.md) — no transcrito |
| **Método de comparación demo vs backtest** | [07](07_Backtest_y_Puesta_en_Marcha.md) — **decisión de diseño pendiente** |
| **Ciclo de vida / recambio del portfolio** | Ausente de toda la KB |
| **`OOS1 / OOS2 / OOS3` para mercados difíciles** | [06](06_Gestion_de_Riesgo.md) |
| **Métodos de backtest en MT4** | [07](07_Backtest_y_Puesta_en_Marcha.md) |
| **El tip para operar el oro en Axi** | [08](08_Servicios_de_Fondeo.md) |
| **Copy trading (`fxblue`, `trader connect`)** | [08](08_Servicios_de_Fondeo.md) |
| **Nomenclatura de EAs en live** | Esta mentoría |
| 🔴 **Estrategias en SHORT** — minado, umbrales, composición mixta, swap invertido | Esta mentoría — **el único gap que agrega una capacidad nueva**; ver la sección de arriba |
