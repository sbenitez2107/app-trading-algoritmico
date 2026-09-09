# Base de Conocimiento IMOX - Módulo 9: AlgoWizard y Quant Analyzer 4

> Origen: módulo 9 de la academia + mentoría del 08/04/2026. Transcrito 2026-09-07.
> Último módulo de la formación grupal (queda la mentoría 1 a 1).
>
> ⚠️ **Es el módulo que más se solapa con el simulador de portfolios de esta aplicación**, y contiene
> **la crítica de la academia a la herramienta que hace exactamente eso**. Leerlo antes de diseñar
> cualquier ranking de grupos.

---

## 🚨 La advertencia central — QA4 sobreoptimiza portfolios

> **El Quant Analyzer sobreoptimiza mucho los portfolios. Los vas a ver que siempre ganan: una falsa
> impresión.**

Y reforzado en la sección de portfolios:

> **No creas que vas a tener años donde vences al mercado todos los meses. En eso la herramienta no es
> confiable, es un poco falso.**

Y el veredicto más duro:

> **Lo único que sirve de QA4 es el análisis de Monte Carlo — ver qué significa el 95%.**

**Por qué pasa esto.** Si se buscan combinaciones de estrategias y se elige la que mejor curva
produce, el resultado no mide la calidad del portfolio: mide **cuánto se buscó**. Es el mismo
fenómeno que el SPP detecta a nivel de parámetros (módulo 3) y que el Floating window ataca a nivel
de ventana (módulo 4) — pero **a nivel de composición de grupo, y sin defensa incorporada.**

> **Consecuencia de diseño**: un simulador que genere grupos y los ordene por resultado **reproduce
> este defecto por construcción.** Cualquier ranking de grupos necesita una defensa explícita contra
> la selección — no alcanza con calcular bien las métricas.

---

## 🎯 Por qué un portfolio y no una estrategia

> Cuando trabajás con **una** estrategia en el largo plazo tenés problemas: **dependés de la
> estrategia**. Si empieza a fallar —muchos SL, pérdidas, mal manejo del MM— **adiós a la trayectoria.**
>
> ¿Qué pasa si hacemos un portfolio de **15 estrategias** en Darwinex Zero con una cuenta de 100k?
> Esto hace que **la curva de equity sea más normal, estable**, sin tantas subidas y caídas.
> **Robustez en el largo plazo.**

> *"Esto es lo que vamos a intentar construir: un conjunto de estrategias simples combinadas en
> portfolio."*

---

## ⚠️ Tamaños de grupo que recomienda la academia

Los números de este módulo son **bastante más chicos** de lo que suele asumirse:

| Contexto | Cantidad |
|---|---|
| Filtrado con Portfolio Master | Quedarse con **5-10** de una remesa de **30-50** |
| Para reducir *consecutive losses* | Quitar las peores y quedarse con **5 o 6** |
| Darwinex Zero, cuenta de 100k | **15 estrategias** |
| **Axi Select y props** | **1 o 2** — *"poquitas"* |
| TTP | **4**: un oro, un NQ, un DAX y una divisa |
| Arranque con poco capital *(módulo 6)* | **1 y como mucho 2** |

> ⚠️ **Ninguna cifra de la academia llega a 20-30 estrategias por grupo.** El máximo citado es **15**,
> y solo para una cuenta de 100k en Darwinex Zero. Para servicios de fondeo el número es **1-4**.
> Si un diseño asume grupos de 20-30, esa premisa **no viene de este material** y conviene
> confirmarla antes de construir sobre ella.

**Recomendación de perfil:**

> **Portfolios de largo plazo en altas temporalidades (H4) y bajo `avg trades`.**

---

# 🧰 AlgoWizard

## 1. Para qué se usa

> Aritz usa AlgoWizard **principalmente para editar y backtestear** estrategias.

| Uso | Detalle |
|---|---|
| **BT rápido** | Hasta fechas de ayer |
| **MM** | Probar configuraciones |
| **Spread / Swap** | Ajustar y testear |
| **Timeframes** | Cambiarlos |
| **Editar SL y TP** | El uso principal |

Se puede tocar todo: `Trading signals`, `Long/Short entry`, `Long/Short exit`.

> **El único problema que podés tener en AlgoWizard**: que **no te detecte el clon del instrumento**
> en `Settings`.

**Permite guardar configuraciones de MM aplicadas** — por ejemplo, una cuenta de 2000 con 2 decimales.

## 2. Backtest

Se puede backtestear en **cualquier timeframe** y con **toda la data disponible**, incluido el OOS y
**el último año que nunca se le dio** al Builder ni al Retester.

### El balance cambia el drawdown

| Cuenta | DD |
|---|---|
| **1.000** | **92%** |
| **10.000** | **12%** |

> **El balance es paralelo al riesgo que asumís por operación.** Si arriesgo 0,01 lotes —algo así como
> 13 o 14 USD— **el total es el mismo, lo que cambia es el tamaño del balance.** No es lo mismo 500 USD
> de balance que 100.000: en la cuenta de 500 el DD será mayor **porque el balance es menor, se
> arriesga más.**

⚠️ **Una misma estrategia puede ser inviable en cuenta chica y cómoda en cuenta grande sin cambiar
nada.** El DD porcentual no es una propiedad de la estrategia — es una propiedad del par
(estrategia, balance). Conecta con la advertencia del módulo 6: *"las cuentas pequeñas a microlote a
veces no funcionan por el riesgo"*.

También se puede backtestear en períodos concretos, por ejemplo **2022-2026**.

## 3. Editar estrategias

- El **SL y el TP se cambian en `long entry`**, ajustando los **pips fixed values** — por ejemplo, de
  **64 a 30**.
- **La edición de SL y TP en AW funciona muy bien**: hay que ajustar los pips para **clavar el máximo
  SL y TP que quieras**, por ejemplo 12 USD.
- Ejemplo de configuración: **MM Fixed size = 0,01 con Initial capital 4000** — como la **fase 3
  aceleration de Axi Select**.
- **Backtestear en otras temporalidades**: *"sería una coña que funcione en otras"*.

> 🐛 **Bug conocido de SQX v136**: si en el **Source Code** ves que los parámetros que cambiaste en
> AlgoWizard (`long entry` → SL, por ejemplo) **no están bien reflejados**, es un **bug de SQX 136**.

*(Segundo bug documentado de la v136 — el primero es el del swap, en el módulo 1.)*

---

# 📊 Quant Analyzer 4

## 1. Secciones

| Sección | Para qué |
|---|---|
| **Analyze** | Analítica interna: overview, MC, lista de trades de un portfolio en conjunto |
| **Monte Carlo** | A una estrategia o a un portfolio completo |
| **Portfolio Master** | Construcción por descorrelación |
| **Money Management** | Simular distintas configuraciones de MM, con más o menos capital, y comparar el equity y el DD generados |

**Prerrequisito:**

> Para empezar a usar QA4 hay que tener listas **las estrategias del Walk Forward Matrix,
> seleccionadas con el RUN elegido.**

## 2. Portfolios

Combina estrategias finales — por ejemplo **1 EA de ORO, 1 de NQ, 1 de DAX y 1 de divisa** —
seleccionarlas todas y crear el portfolio.

**Qué mirar primero:**

> Analizar la **lista de trades**: hay que ver **variedad** de trades, ganancias, pérdidas, máximo SL.

**Lo que más le gusta a Aritz de la creación de portfolios:**

- **Portfolio Correlation** — correlación entre las estrategias.
- **Trades per month** — cuántas veces entra a mercado en el mes; analizar los últimos meses para
  saber **la frecuencia con la que entran** las estrategias.
- **Monte Carlo** — al 95% de confianza, cuánto es el **Max DD**.
- **Equity chart** — la evolución **individual de cada estrategia** y la **global del portfolio**.
- **Jugar con la composición**: ver cuál es la mejor estrategia en un activo, o **quitar un activo** y
  quedarse solo con ORO y NQ para ver cómo va.
- **Llevar el portfolio a un prop o a Axi Select** y ver **cómo se comporta el MM en períodos
  bajistas.**
- Template **`SQ with Portfolio`**.

> **Ideal para arrancar a evaluar cuentas pequeñas de 3000 o menos.**

## 3. Analyze

- Seleccionar **Template**: `SQ Default`, `SQ with Portfolio`, `TS Overview`.
- ⚠️ **No permite editar el MM**, ni cambiar el size o los decimales del lotaje. **Eso se hace en
  AlgoWizard**, se guarda, y recién ahí se importa en QA4.
- **Equity chart**: a Aritz le gusta la **vista de DD en %**, para buscar **ese 5% de max DD que piden
  las props**.
- **Trades analysis**: ver **duración de los trades** y **trades por mes**.

## 4. Monte Carlo

> Alterar tu propia estrategia a **situaciones de estrés**, poco probables, y ver esas simulaciones.
> *¿Qué pasa si a nuestra estrategia —que tiene una década y pasó por todas las fases de aceptación—
> la llevo a una situación de estrés? ¿Y a un portfolio?*

### Los umbrales de Max % DD

| Nivel de confianza | Tolerancia |
|---|---|
| **95%** | **como mucho un 2× del DD de la original** |
| **100%** | **un 3× o 4× de la original es aceptable** |

> **Es una ayuda psicológica**: si el día de mañana caés un 2× del DD original, **ya lo tenías
> contemplado.**

Esto **cuantifica** la pregunta que el módulo 5 dejaba abierta —*"¿la estrategia es capaz de hacer un
×2 o ×3 de ese drawdown?"*— y le pone nivel de confianza.

*(Nota sin resolver del material: "trades que no se ejecutan, puede ser por spread — ver por qué dice
esto Aritz".)*

## 5. Portfolio Master

**Es una de las dos maneras de crear portfolios en QA4:**

> **Trabajar el portfolio por descorrelación de `profit/loss by day`.**
> Sirve para **filtrar las mejores 5-10 de una remesa de 30-50 estrategias** — por ejemplo, ORO, NQ,
> DAX.

- La opción **`subequity only`** permite ver **la curva de equity de cada estrategia por separado** y
  quitar las menos rentables.
- Alternativa: llevar las estrategias al **AlgoWizard** y analizar **los últimos 4 años** de cada una.
- Para tener **menos *consecutive losses***, quitar las peores y quedarse con **5 o 6**.

> 📌 **`Aritz trabaja 4 tipos de correlaciones`** — el detalle queda para la **mentoría 1 a 1**.
> El **VaR** también.

> ⚠️ **SECCIÓN MARCADA COMO CRÍTICA EN EL MATERIAL Y NO TRANSCRITA**:
> *"5) Portfolio master — MUY IMPORTANTE VER"* y *"6) Correlación/Descorrelación de estrategias"*.
> **Estas dos secciones son exactamente el núcleo del simulador de esta aplicación y su contenido
> falta.** Recuperarlas antes de fijar el algoritmo de agrupación.

---

## 🎓 Mentoría 1 a 1 — requisitos

> **Solo tomarla cuando estés seguro de escalar a mercado real.** Si estás muy verde y arrancás con
> una estructura pobre, podés sentirte mal en comparación con otros compañeros.

- Implica **otro tipo de formación**: horas y horas de trabajo para tomar decisiones hacia los
  mercados reales.
- Es **un traje a medida**, y da herramientas para el escalado a la comunidad y a real.
- **Escalar pronto no es sinónimo de éxito.**
- **Darwinex Zero no requiere la 1 a 1**: es una decisión de largo plazo que podés hacer a tu tiempo.

**¿Cuántas EAs en H1/H4 hay que tener listas?**

> **Poquitas**: 1 o 2 estrategias para Axi Select y props. **Darwinex Zero en H4 es lo más rentable**,
> con lógicas de **distintos indicadores**.
> *"Podés operar un TTP con un oro, un NQ, un DAX y una divisa y reventarla."*

---

## 💡 Sobre el paso a Live

> **Es mejor empezar a operar en LIVE cuando el mercado te da ostias (pérdidas) que empezar con mucho
> dinero.**

Cuarta formulación del mismo principio (módulos 5 y 7): **el momento de entrar no es el bueno, es el
malo** — porque es el único que te da información.

---

## 🔗 Qué significa esto para el simulador de esta aplicación

| Hallazgo | Consecuencia |
|---|---|
| **QA4 sobreoptimiza portfolios y "siempre ganan"** | Un ranking de grupos sin defensa contra la selección **reproduce el defecto**. Es el argumento más fuerte a favor del benchmark de retornos barajados |
| **La academia usa 5-15 estrategias, no 20-30** | Revisar la premisa del tamaño de grupo |
| **Portfolio Master descorrelaciona por `profit/loss by day`** | Hay una definición concreta y diaria de la correlación a usar |
| **`Trades per month` es criterio de primera clase** | Ya sabíamos que el conteo de trades es la restricción de Axi (módulo 8); acá es una métrica de portfolio |
| **El DD % depende del balance, no solo de la estrategia** | El simulador debe evaluar grupos **contra un balance concreto**, no en abstracto |
| **MC: 2× el DD al 95%, 3-4× al 100%** | Umbral concreto y cuantificado para validar un grupo |
| **El MM se edita en AlgoWizard, no en QA4** | Separación de responsabilidades del pipeline real |
