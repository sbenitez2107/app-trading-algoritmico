# Base de Conocimiento IMOX - Módulo 1: SQX Data

> Origen: mentoría 1 de la academia IMOX (11/02/2026), transcrito 2026-09-07.
> **Doctrina** = metodología de la academia, estable. **Dato de proveedor** = especificaciones de
> Darwinex / Axi / CME, que cambian sin aviso y deben re-verificarse antes de usarse en una decisión.
> Este módulo es el que más datos de proveedor contiene: la tabla de instrumentos está fechada.

---

## 📚 Temario

Añadir registro y descarga de datos · Analizar la data · Clon del registro (TimeZone) · Punto de
valor en $ · Pip/Tick Size · Pip/Tick Step · Spread · Comisiones · Swap · Configuración completa ·
Backup de data (importar al SQX virgen) · Cuestionario Data Manager · Configurar XAUUSD ·
Configurar índices · Configurar una divisa.

---

## 🧭 Doctrina de mercado

- **No se buscan sistemas en contratendencia.** Ejemplo: Nasdaq bajista → prefiere no operar.
- **Los CFDs son un caos**: mercado descentralizado, cada broker pinta lo que quiere. Consecuencia
  directa: **no se puede bajar a M1 o M15 y ser robusto a largo plazo**.
- **Los futuros tienen data centralizada**, pero es un mercado muy manipulado.
- **El swing es lo más fácil a nivel de encontrar estrategias**, y más fácil psicológicamente que
  el day trading. **Por eso los brokers inflan el swap**: para que el trader swing no se beneficie.
  De ahí que la elección de broker sea parte de la estrategia, no un detalle administrativo.

### Darwinex vs Axi *(dato de proveedor)*

| | Darwinex | Axi |
|---|---|---|
| ¿Te lleva a mercado? | **Sí** | No |
| CFD del Nasdaq | Operable | Lote mínimo 1 (no micro) → te empuja al futuro del CME |
| Spread en índices / oro / DAX | Bajo (< 1 en índices) | — |
| Swap | Muy bajo | Sin swap en NQ ni DAX |
| Punto débil | Swing a 30 días te masacra en comisiones; scalping parecido | Data mala o muy manipulada |

Aritz plantea la pregunta sin responderla: *¿por qué Darwinex tiene la mayoría de su operativa en
CFDs y no en futuros? ¿Por qué Axi ofrece al minorista el Nasdaq a lote mínimo en vez de micro y te
obliga al futuro del CME?* Su lectura es que **el CME manipula mucho más que otros proveedores de
liquidez**. En Axi, en cuentas de 500k **no se puede operar el Nasdaq** — probablemente por el swap.

**Ningún broker sirve para todo.** Axi conviene para swing en índices al futuro (por swap y
rollover); Darwinex conviene para intradía en CFDs.

---

## 🏗️ Regla estructural: Darwinex es la configuración de referencia

> **Construimos todo en base a la configuración de instrumentos de Darwinex.** En MT4:
> símbolo = especificaciones. Luego cada sistema se va acomodando a cada broker.

Y su corolario, que repite la doctrina del módulo 0:

> **No se trabajan estrategias para props específicas: se adaptan.**

**Filosofía IMOX de construcción.** Tomar la mejor data, la mejor configuración y el mejor spread,
crear con eso las mejores estrategias posibles, y **recién después** adaptarlas a los distintos
brokers — incluidos los que tienen data mala o manipulada. SQX es una herramienta compleja a la que
hay que buscarle huecos y probar cosas que no operan los traders de libro, para lograr lógicas
distintas a las del rebaño.

---

## 💾 Data

| Aspecto | Definición |
|---|---|
| **Profundidad** | **Últimos 10 años** para construir |
| **Granularidad para minar** | **M1 alcanza** en un principio |
| **Tick data** | Darwinex desde **2017**; requiere SQX de pago |
| **Cuándo hace falta tick** | **No** para H1/H4. **Sí** para M5/M15 |
| **Cuándo agregar tick** | Conviene dejarlo **para el final, en el Optimizer** |
| **Time zone** | **UTC+02** (Darwinex trabaja en UTC+2, hay que convertir) |
| **DST** | ±1 hora según horario de verano o invierno |

**Qué aporta el tick.** Con tick tenés el **spread real**. En M1 tenés el open/close de la vela de
1 minuto — eso *es* el spread.

**Calidad de datos — matiz importante de Aritz.** Data con muchos gaps o que parece de mala calidad
**también sirve** para analizar estrategias y dejarlas en demo a verificar. **No hay que descartarlas
de primeras** cuando no generan confianza: hay que darles oportunidad si no están tan mal. También
se pueden **obviar esos períodos** de mala calidad.

**Análisis previo**: `Tools → View and Analyses`.

**Compatibilidad**: conviene trabajar datos, estrategias y demás **por versión de SQX**.

---

## ⚙️ Configuración de instrumentos en SQX

| Campo | Valor / regla |
|---|---|
| **Data Type** | `Forex` o `CFD` (no ve diferencia para commodities) · **divisas → Forex** · **índices americanos → Index** |
| **Order Size Multiplier** | `1` — no se toca |
| **Default Slippage Pips** | `0` — no se toca |
| **Order Size Step** | `0` — no se toca (no existe en la v136) |
| **Point Value $** | El valor en USD de un movimiento de **1.0 en el precio** operando 1 lote estándar |
| **Pip/Tick Size** | En **índices siempre 1**. Sale de la *precision* de MT4 |
| **Pip/Tick Step** | El **mínimo movimiento** del precio del activo |
| **Default Spread Pips** | Bajo. Poner spread alto es tirarse piedras sobre el propio tejado: aumenta la dificultad de la minería |

### Point Value en $ — la explicación larga, porque se malinterpreta

**No es el tamaño del contrato**, aunque a veces coincida. Es literalmente **el dinero en USD que
ganás o perdés por cada movimiento de 1.0 en el precio con 1 lote estándar**.

- **1 lote long en EURUSD**, el precio pasa de 1,18 a 2,18 → ganás **100.000 $**. Point value = 100000.
- **1 lote long en USDJPY**, el precio pasa de 150 a 151 → ganás **100.000 ¥**. Convertidos a USD
  dan ≈ **652 $**. Point value ≈ 654,86.

En SQX y MT4 un **punto** no es un **pip**: el punto es un cambio de 1.0 en el precio. Como en los
pares con yen un pip es 0,01, **un punto equivale a 100 pips**.

Cuando la segunda divisa no es dólar hay que convertir: dividir 100000 por la cotización de esa
divisa contra el USD.

### El corrimiento de coma en el spread

Cuando **step y size difieren en un decimal**, el spread que muestra MT4 hay que correrlo una coma
de izquierda a derecha.

- **USDJPY**: precision 3 → size 0,01; step 0,001. Difieren en un decimal → si MT4 dice spread 25,
  el spread real es **2,5 pips**.
- Aplica igual a **oro, Nasdaq, DAX, GBPJPY**.
- **WS30**: precision 0 → size 1; step 1. **No difieren** → lo que aparece es el valor real: si dice
  2, son 2 pips, no 0,2.

> ⚠️ **La especificación del instrumento y la serie de precios son dos bindings separados** en el Data
> Manager de SQX, y pueden venir de brokers distintos: nada impide dejar la especificación en
> Darwinex y que el dato de precio efectivamente cargado sea de otro proveedor (p. ej. Dukascopy). Ver
> [MEASURED_Demo_vs_Backtest_Divergence.md](MEASURED_Demo_vs_Backtest_Divergence.md) para un caso
> medido: especificación Darwinex, datos Dukascopy, offset de precio sistemático de 22 a 41 puntos.

---

## 📋 Tabla de instrumentos — configuración Darwinex

> **Dato de proveedor, no doctrina.** Especificaciones de Darwinex tomadas de MT4 al 11/02/2026.
> Fuente: <https://www.darwinex.com/forex-cfds/forex> · tick data: <https://www.darwinex.com/tick-data>
> **Re-verificar contra MT4 antes de usar en una decisión de riesgo.**

| # | MT4 Darwinex | Símbolo SQX | Global | Data Type | Point Value $ | Pip/Tick Size | Precision MT4 | Pip/Tick Step | Decimales | Default Spread | Slippage | Order Size Mult. | Comisión | Swap |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | XAUUSD | XAUUSD | ORO | Forex | 100 | 0,1 | 2 | 0,01 | 2 | 1 | 0 | 1 | 0,0050% | NO |
| 2 | NDX | USATECHIDUSD | NASDAQ | Index | 10 | 1 | 1 | 0,1 | 1 | 1 | 0 | 1 | 5,5 | NO |
| 3 | GDAXI | DEUIXEUR | DAX | Index | 10 | 1 | 1 | 0,1 | 1 | 1 | 0 | 1 | 6,53 | NO |
| 4 | WS30 | USA30IXUSD | DOW JONES | Index | 1 | 1 | 0 | 1 | 0 | 1 | 0 | 1 | 0,84 | NO |
| 5 | SP500 | USA500IXUSD | SP500 | Index | 10 | 1 | 1 | 0,1 | 1 | 1 | 0 | 1 | 0,55 | NO |
| 6 | GBPJPY | GBPJPY | LIBRA/YEN | Forex | 654,86 | 0,01 | 3 | 0,001 | 3 | 2 | 0 | 1 | 3,41 | NO |
| 7 | USDJPY | USDJPY | DOLAR/YEN | Forex | 654,86 | 0,01 | 3 | 0,001 | 3 | 1,4 | 0 | 1 | 5 | NO |
| 8 | EURJPY | EURJPY | EURO/YEN | Forex | 654,86 | 0,01 | 3 | 0,001 | 3 | 1,4 | 0 | 1 | 5,94 | NO |
| 9 | EURUSD | EURUSD | EURO/DOLAR | Forex | 100000 | 0,0001 | 5 | 0,00001 | 5 | 1 | 0 | 1 | 5,94 | NO |
| 10 | XAGUSD | XAGUSD | PLATA | Forex | 5000 | 0,01 | 3 | 0,001 | 3 | 1 | 0 | 1 | 0,0050% | NO |

**Validación independiente registrada**: la aplicación **derivó** el point value del Nasdaq desde
los MAE de los trades importados y obtuvo **10** con 183 muestras (rango 9,995579–10,007951), que
coincide exactamente con el 10 que esta tabla especifica por configuración. Dos caminos
independientes, mismo número.

---

## 💸 Spread, comisiones y swap

### Spread *(doctrina de construcción)*

Es una **comisión totalmente variable** de la que se beneficia el broker. Se calcula en MT4 como la
diferencia entre Bid y Ask.

**Se construye con el spread tirando para abajo**, y recién después se backtestea con intervalos
reales. No conviene ser estricto al principio: un spread alto en minería reduce la cantidad de
estrategias que salen. Vale la pena **probar con spread alto y bajo** para medir esa diferencia.

- Oro e índices: **1** en construcción, backtest en ~2
- Divisas: **1 a 3** en construcción, backtest en 2–3

### Comisiones

- **Futuros** suelen tener comisión **fija** (per trade).
- `None` cuando no hay comisión — Axi tipo *Standard* no tiene comisión; la *Pro* tiene menos
  comisión y un poco de spread *(dato de proveedor)*.
- `Size based`: comisión por orden **× 2** (entrada y salida).
- Si la comisión no está en dólares, hay que convertirla.

### Swap — **no se configura, y hay dos razones**

1. **Técnica**: en la **v136 está bugueado** — lo suma en positivo. Es una variable que en esa
   versión conviene no configurar, para meter menos ruido.
2. **Metodológica, y es la que importa**: usar los **swaps de hoy**, con lo inflados que pueden
   estar, **aplicados a datos del pasado** es un gran error que mete ruido en las estrategias, y el
   broker se beneficia de eso.

**Cómo tratarlo entonces.** El swap depende mucho del estado del activo en el momento: con el oro en
máximos, abrir un long es carísimo en swap y abrir un short **te pagan**, porque vas contratendencia.
La forma correcta es aplicarlo **en el Retester como crosscheck (WhatIf)** a un swap concreto o a un
rango, según sea H1 o H4, para ver si la estrategia pasa. Después se pueden backtestear rangos de
spread.

*(Pendiente del módulo: qué es el triple swap.)*

---

## 🔭 Observación sobre props y riesgo de fin de semana

Recomienda los podcasts de **Javi Colón (CTO de Darwinex)**. Su argumento: si él tuviera una prop
bien hecha, **no dejaría a los traders mantener operaciones abiertas los fines de semana**. Que una
prop lo permita es indicio de que **no está operando en real** — una posición abierta durante el fin
de semana puede encontrarse un evento que el lunes queme una cuenta con riesgo abierto no controlado.

---

## ✅ Pendientes que este módulo deja abiertos

- **Buscar swaps, spreads y comisiones de cada activo** en Darwinex, Axi Select y demás, para
  guardarlos como instrumento por broker.
- Definir **qué es el triple swap**.
- Verificar el símbolo del Nasdaq: la tabla dice `USATECHIDUSD` y los datos importados en la
  aplicación traen `USATECHIDXUSD_M1_UTC02`. Difieren en una letra; no está determinado cuál es el
  correcto.
