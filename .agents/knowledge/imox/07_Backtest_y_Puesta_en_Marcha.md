# Base de Conocimiento IMOX - Módulo 7: Backtest y Puesta en Marcha

> Origen: módulo 7 de la academia + mentorías del 04/12/2025 y 01/04/2026. Transcrito 2026-09-07.
> Primer módulo **de infraestructura y operación**, no de selección. Cubre el backtest de
> confirmación en MT4 y el despliegue real de EAs en VPS.
> ⚠️ Los precios y capacidades de proveedor están **fechados** y deben re-verificarse.

---

## 1️⃣ Backtest en MT4 — un paso de confirmación, no obligatorio

> **No es obligatorio este tipo de pruebas**, pero es **un paso más de validación**: sirve para ver
> si la estrategia del SQX es **correlativa** con la del MT4, y así tener más confirmaciones.

Es decir: el backtest de MT4 no valida la estrategia — **valida la herramienta**. Si SQX y MT4
divergen mucho sobre la misma estrategia, el problema está en el modelado, no en la lógica.

### Datos tick

| Plataforma | Tick data |
|---|---|
| **MT4** | ❌ **No la tiene integrada** — hay que exportarla desde SQX |
| **MT5** | ✅ **Viene incorporada por defecto** — solo hace falta descargarla |

**Reglas de descarga:**

- **Tener espacio de disco** para guardar las descargas.
- **Hacer las descargas en la PC local**, que suele tener más espacio (no en el VPS).
- **¿Cuántos años?** → **los últimos 2 años.**
- **Descargar solo el marco temporal que necesites.**

### Fuente de datos, por precisión

> **Mayor precisión: tick data de Darwinex. Si no, Dukascopy.**

Coherente con el módulo 1, donde **Darwinex es la configuración de instrumentos de referencia**.

**¿Se descarga con la licencia full de SQX?** ✅ **Respondido por el usuario (2026-09-09): sí** — las
fuentes de datos de Darwinex solo se pueden descargar con la versión full/de pago de SQX. Corrobora
[01_SQX_Data.md:72](01_SQX_Data.md) — *"Tick data: Darwinex desde 2017; requiere SQX de pago"*.

⚠️ **Por qué importa en la práctica**: cuando no se tiene esa licencia (o simplemente se elige el
fallback), la instrucción de arriba dice ir a Dukascopy — y ese fallback tiene un costo medido, no
teórico. [MEASURED_Demo_vs_Backtest_Divergence.md](MEASURED_Demo_vs_Backtest_Divergence.md) documenta
un caso real donde la especificación del instrumento quedó en Darwinex (`NDX_DARWINEX`) pero la serie
de precios efectivamente usada era Dukascopy (`USATECHIDXUSD_M1_UTC02`): un offset de precio
sistemático y creciente (22 a 41 puntos) entre demo y backtest, sobre las 24 operaciones pareadas por
minuto de apertura.

### Exportar tick data de SQX a MT4

*(El procedimiento paso a paso está en el material original en formato visual y no quedó transcrito.
Pendiente de completar.)*

---

## 2️⃣ Infraestructura VPS *(datos de proveedor — 04/12/2025)*

> **Recomienda usar VPS de empresas que se dediquen al trading automático**: tienen mejor soporte y
> especialización en el mercado.

**Proveedor citado**: `fxvps.pro/pricing`

| Plan | Precio | Recursos | Capacidad |
|---|---|---|---|
| **Professional** | **~14 USD/mes** *(11/2025)* | **2 core CPU · 3 GB RAM** | **6-7 instancias de MT4**, las soporta bien |

**Ejemplo de reparto de 7 instancias**: Axi Select, cuenta de patrimonio, Darwinex, demo…

---

## 3️⃣ Organización de instancias — las reglas de capacidad

| Regla | Valor |
|---|---|
| **Instancias de MT4 por VPS** | **6-7** |
| **Sistemas (EAs) por instancia** | **no superar 20** |
| **Instancia por activo** | ✅ recomendado — ORO Instancia 1, Nasdaq Instancia 2, DAX Instancia 3 |
| **Cuentas demo por instancia** | Muchas instancias pueden usar **la misma cuenta demo** |
| **Cuentas demo de Darwinex** *(dato de proveedor)* | ❌ **Corregido: son ILIMITADAS.** *"You may open as many demo trading accounts as you need"* (verificado 2026-09-07). El "hasta 10" del transcript no coincide con ningún límite publicado; el número más cercano es el tope de **1 demo + 5 live** de las *carteras de inversión* demo, que es otra cosa. Ver [SERVICE_Darwinex_Zero.md](SERVICE_Darwinex_Zero.md) |

**Ejemplo trabajado:**

```
60 EAs  →  3 instancias de MT4  →  todas apuntando a la cuenta DEMO1
```

> ⚠️ **Consecuencia para un pool grande de estrategias.** Con el techo de **20 EAs por instancia** y
> **6-7 instancias por VPS**, un solo VPS Professional sostiene del orden de **120-140 EAs**. Un pool
> de ~80 estrategias elegibles entra en **4 instancias** y cabe en un VPS — pero **agrupadas por
> activo**, que es la recomendación, el reparto no es libre: la cantidad de instancias la fija el
> número de activos, no el de estrategias.

### Backups y renombrado

- Se pueden hacer **backups de las instancias de MT4** y llevarlas a otro servidor.
- ⚠️ **Cuando se renombra una instancia copiada, dejar el primer nombre que le pusiste.**

---

## 4️⃣ Money Management en despliegue

> **MM en demo/live: siempre 2 decimales. En testing y building: 1 decimal.**

Confirma exactamente lo del [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md): el 1 decimal es un
filtro anti-falsa-precisión del minado, y **todo lo que toca mercado real va a 2 decimales.**

---

## 5️⃣ El protocolo de Demo — cómo se filtra de verdad

> **TIP**: Aritz deja **un buen rato** las EAs en demo, **a la espera de un período malo, lateral o
> bajista**, y ve **si recuperan o no**. Así filtra las que funcionan de las que no.

Y la regla que lo formaliza *(mentoría 01/04/2026)*:

> **Para saber si una estrategia realmente es buena, debe haber tenido una caída antes de ir a live.**

Es la **tercera aparición del mismo criterio**, y ya es doctrina firme:

| Dónde | Formulación |
|---|---|
| [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) | *"Verla caer en DD y luego recuperarse por encima — vencerse a sí misma"* |
| Este módulo, TIP | Esperar un período malo, lateral o bajista, y ver si recupera |
| Este módulo, 01/04/2026 | **Debe haber tenido una caída antes de ir a live** |

**La salida de Demo no es un plazo: es un evento.** Y el evento lo provee el mercado, no el calendario
— por eso "un buen rato" es una espera, no una duración.

---

## 6️⃣ Operación de EAs en producción *(mentoría 01/04/2026)*

- **Guardar los presets de las EAs** cuando se configuran en MT4 — **sobre todo las que están en Live.**
- **Poner las EAs en mercado cerrado** para que **no hagan falsas entradas.**

---

## 📊 La comparación diferida — herramienta vs mercado

> Cuando pasa **1 año y 3 meses**, vas a **comparar las estrategias que pusiste en el mercado a 200 USD
> contra lo que saca SQX**: comparación de **la herramienta contra el mercado**.
> *(Se explicará en la mentoría 1 a 1.)*

Es el bucle de calibración de todo el método: no valida una estrategia, valida **si el backtest
predice**. Es exactamente la comparación **demo vs backtest** que ya está en el plan del simulador.

---

## ❓ Preguntas abiertas y pendientes

| Estado | Ítem |
|---|---|
| 📌 **Pendiente de revisar** | Los **últimos minutos de la mentoría 01/04/2026** explican **muchas formas de hacer backtest** para determinar si la estrategia es similar a la de SQX. **Verlo de nuevo.** |
| 📌 **Pendiente de transcribir** | El procedimiento visual de **exportar tick data de SQX a MT4** |
| 🔧 **TODO de la plataforma** | **Actualizar la vista de WFM con los campos que se muestran** |

---

## 🔗 Cómo encaja con el resto de la KB

| Concepto | Dónde vive |
|---|---|
| Por qué Darwinex es la referencia de datos | [01_SQX_Data.md](01_SQX_Data.md) |
| La decisión de llevar a Demo (7 pasos) | [05_Analisis_de_Resultados.md](05_Analisis_de_Resultados.md) |
| Los decimales del MM y por qué cambian por fase | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| Cuántas estrategias correr con poco capital (1-2) | [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) |
| Costo medido de usar Dukascopy en vez de tick data de Darwinex | [MEASURED_Demo_vs_Backtest_Divergence.md](MEASURED_Demo_vs_Backtest_Divergence.md) |
