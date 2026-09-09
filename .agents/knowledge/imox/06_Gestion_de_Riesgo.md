# Base de Conocimiento IMOX - Módulo 6: Gestión de Riesgo

> Origen: módulo 6 de la academia + mentoría del 18/03/2026. Transcrito 2026-09-07.
> Reemplaza a `06_Money Management.md`, que cubría solo la configuración de cuentas grandes.
> **Es el módulo que implementa la doctrina del módulo 0**: la lógica es del activo, el MM es del
> servicio. Acá está el "MM del servicio".

---

## 🎯 Los dos modelos de Money Management

| Modelo | Cuándo se usa |
|---|---|
| **Fixed Size** | Construcciones que **no usan ATR** en SL/TP — modelo de pips, modelo de % |
| **Fixed Amount / ATR** | SL por ATR. **Es la que mejor performance da** |

---

## 1️⃣ MM Fixed Size

`Order size`: se coloca el tamaño del lote directamente.

| Valor | Nombre |
|---|---|
| `0,01` | microlote |
| `0,1` | minilote |
| `1` | lote estándar |

Con este modelo **el SL y el TP valen siempre lo que indicás — no varían.**

### Parámetros en MT4

```
smm            ---------- Money Management - Fixed size ----------
mmLots         0.01
mmMultiplier   1.0
```

---

## 2️⃣ MM ATR (Fixed Amount) — el modelo principal

Modelo de cantidad: permite trabajar el **SL por ATR**.

### Configuración por tamaño de cuenta

| Parámetro | **BASE / cuentas grandes** | **CUENTAS CHICAS** |
|---|---|---|
| `Initial capital` | **100.000** | **1.000** |
| `Risked Money` | **200** (0,20%) | **10** (1,0%) |
| **`Size decimals`** | **1** | **2** |
| `Size if no MM` (Lots) | **0,1** (minilote) | **0,01** (microlote) |
| `Maximum lots` | **10** | **10** |

### Parámetros en MT4

```
smm                  ---------- Money Management - Fixed Amount ----------
UseMoneyManagement   true
mmRiskedMoney        200.0
mmDecimals           2
mmMaxLots            10.0
mmLotsIfNoMM         0.1
mmMultiplier         1.0
```

> **`UseMoneyManagement = false`** anula la opción por defecto y **entra por lotes**.

---

## ⚠️ La trampa de los decimales — el punto técnico central del módulo

> *En este modelo hay trampas.* En CFDs y Forex el volumen va desde lote, minilote, microlote.
> Si le indico un tamaño de posición de 200, **¿cómo hace el cálculo del volumen** en base a esos 200
> USD y al valor del pip del activo?

**SQX lo calcula todo automáticamente**, pero el resultado depende de `Size decimals`:

| `Size decimals` | Consecuencia |
|---|---|
| **1** | Se **omite el segundo decimal** → el **SL queda variable**, con fluctuaciones tipo **158, 200, 300**. **No se pueden usar microlotes**: `0,01` no cabe en un decimal |
| **2** | **Clava el valor del SL.** Habilita microlotes |

> **Para clavar el valor del SL se pone `Size decimals` en 2.**

### Por fase

| Fase | Decimales | Ejemplos de tamaño resultante |
|---|---|---|
| **Builder** | **1** | 0,1 · 0,2 · 0,8 · 0,5 |
| **Testing final en AlgoWizard** | **2** | 0,01 · 0,21 · 0,09 · 0,36 |

El 1 decimal del Builder es deliberado — es un **filtro contra la falsa precisión** durante el minado.
El 2 decimales aparece cuando hay que medir de verdad.

---

## 🔢 `Maximum lots`

Máxima cantidad de lotes que se quiere que abra en la operación.

**Ejemplo trabajado:**

```
Cuenta          100.000
Tamaño          3.000
Size decimals   2
Coste por lote  250
→ 10 lotes × 250 = 2.500 < 3.000  ✔
→ abre 10 lotes, porque el máximo configurado era 10
```

El límite lo impuso `Maximum lots`, no el capital. **Se puede subir el parámetro a 20 o 30.**

---

## 🛟 `Size if no MM`

Es el **fallback**. Entra en juego cuando:

- se anula la operativa por defecto por **problemas de cálculo del tamaño de la posición**, o
- **por cálculo de ATR no puede comprar esa posición**.

Normalmente se trabaja con **0,1 o 0,01**.

> **El único error que podés tener acá**: que para cuentas pequeñas **no hayas ajustado la opción de
> microlote `0,01`** en `Size if no MM`.

Es un fallback silencioso: si queda en `0,1` en una cuenta de 1.000, cada vez que el sizing falla la
estrategia abre diez veces el tamaño que corresponde, sin avisar.

---

## 3️⃣ Backtest de MM en AlgoWizard

1. `Edit → Strategy` y llevar la estrategia a testear al **AlgoWizard**.
2. En `Settings → Data` tienen que haberse cargado todos los parámetros.

**Qué probar acá:**

- Poner `Size decimals` en **2** para que **fije el precio del SL**, y el tamaño de la posición en 3000.
- Analizar el **Drawdown**.
- Revisar el **listado de trades** para ver el tamaño de los **SL** y los **TP**.
- Ver los **trailing stop** — cuál se activa más.

---

## 🎓 Mentoría 18/03/2026

### Diversificación en tres ejes

> Diversificación **de cuentas**, **de tipos de estrategias**, y de **RR = Average Win / Average Loss**.

**A Aritz le gustan las estrategias con RR > 2 o 3.**

Notar que el RR es un **eje de diversificación por sí mismo**: un portfolio de estrategias todas con
RR 3 se comporta distinto a uno mixto, aunque los activos sean los mismos.

> Si querés trabajar con un grupo de estrategias con **distintos RR**, **eso es a nivel portfolio.**

### Configuración por tamaño de cuenta *(resumen operativo)*

| Cuenta | Decimales | Lote |
|---|---|---|
| **Pequeña** | **2** | **0,01** microlote |
| **Grande** | — | **0,1** minilote |
| **Axi Select 5.000 / 10.000 / 20.000** *(dato de proveedor)* | — | **microlotes** — el mínimo volumen para entrar al mercado |

> **Todos los de la comunidad operan a un micro en Axi Select.**
> Y una advertencia: **las cuentas pequeñas a microlote a veces no funcionan por el riesgo.**

### 🔑 La doctrina de riesgo

> **LA CLAVE ES IR DE MENOS A MÁS. COMENZAR CON RIESGO ULTRA CONSERVADOR PARA LUEGO IR SUBIENDO SI ES
> NECESARIO.**

- **Cuidado con no entrar en contracorriente por miedo a quebrar la cuenta.**
- **El VaR te pilla de la noche a la mañana.**
- Al empezar con poco capital: **sistemas de 1 y como mucho 2 estrategias, no más**, por el riesgo de
  operación.
- **En cuentas pequeñas exponerse poco, muy conservador en RR.**
- **No podés empezar poniendo mucho porque no tenés margen.**

### VaR — y por qué el 6,5% NO es universal

> **En cuentas propias podés tener un VaR más alto: 15% o 10%**, y ganar un 30 o 40%. Luego de una
> buena subida podés **ajustar nuevamente al VaR de 6,5**.

⚠️ **Esto es importante y corrige una lectura ingenua.** El **6,5% es el estándar de Darwinex Zero**
—*dato de proveedor*, ver [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md)— **no un límite
de la academia.** En capital propio el VaR objetivo es una decisión, y la doctrina explícita es
**empezar bajo y subir después**, no arrancar en el techo.

Es la misma separación del módulo 0: **la lógica es del activo, el riesgo es del destino.**

### Portfolio

- **En portfolio usar VaR.**
- Existe una herramienta que indica **el RR de cada estrategia**.
- Existe una herramienta para **calcular el riesgo máximo que puede tener un portfolio**.
- **QuantAnalyzer 4 (QA4) permite tirar un Monte Carlo a todo el portfolio.**
- **¿Cómo se calcula el VaR de una cuenta?** → analizar qué métricas arrojan los trades.

> *"Si el día de mañana voy a trabajar un Darwinex Zero voy a tener una cuenta de 100.000."*
> El detalle queda para mentoría 1 a 1.

---

## 📌 Pendiente de revisar — mercados difíciles

> En los **últimos 10 minutos de la mentoría** explica cómo utilizar **mercados actuales difíciles**
> para generar lógicas y exploraciones que puedan vencerlos. **IMPORTANTE: verlo de nuevo.**
>
> *"Daréis gracias a mercados difíciles porque te permitirán sacar lógicas que puedan vencerlos"* —
> en el Builder, usar **OOS1, OOS2 y OOS3.**

⚠️ **`OOS1 / OOS2 / OOS3` no está documentado en [02_SQX_Builder.md](02_SQX_Builder.md)**, que registra
el split como IS / ISV / OOS. **Es una configuración distinta y falta su detalle.** Pendiente de
completar cuando se revise ese tramo de la mentoría.

---

## 🔗 Cómo encaja con el resto de la KB

| Concepto | Dónde vive |
|---|---|
| Por qué el MM es lo único que se re-parametriza por servicio | [00_Presentacion_y_Objetivos.md](00_Presentacion_y_Objetivos.md) |
| `Size decimals 1` durante el minado | [02_SQX_Builder.md](02_SQX_Builder.md) |
| El RR se lee de la lista de trades | [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) § 4 |
| El VaR de 6,5% y la banda 3,25-6,5% | [Darwinex_Zero_Risk_Model.md](Darwinex_Zero_Risk_Model.md) *(vendor rulebook)* |
