# Base de Conocimiento IMOX - Módulo 0: Presentación y Objetivos

> Origen: módulo 0 de la academia IMOX, transcrito 2026-09-07.
> Distingue **doctrina** (metodología de la academia, estable) de **datos de proveedor**
> (características de Axi / Darwinex / props, que cambian sin aviso). Los segundos están
> marcados como tales y deben re-verificarse contra el servicio antes de usarse en una decisión.

---

## 🎯 Los cuatro destinos de capital

El pipeline no produce estrategias en abstracto: produce estrategias para colocar en uno de
cuatro destinos, y **el destino condiciona el timeframe y el money management**, no la lógica.

| # | Destino | Notas |
|---|---------|-------|
| 1 | **Capital propio** | Sin restricciones externas de riesgo |
| 2 | **Axi Select** | Objetivo $1.000.000. Escalado por fases: F1 $500 → …, F2 $1.000 → 2.000, F1 $2.000 → 4.000 |
| 3 | **Darwinex Zero** | Horizonte largo plazo. Se busca *track record* e *inversores*. Coste **45 € / mes** *(verificado 2026-09-07 contra la página de precios oficial; ver [SERVICE_Darwinex_Zero.md](SERVICE_Darwinex_Zero.md) § 7)* |
| 4 | **Prop firms** | FTMO, TTP |

**Contexto de mercado (doctrina).** Se espera **más volatilidad a futuro** por el aumento del uso
de trading algorítmico: a mayor adopción, mayor incentivo del mercado a barrer estos sistemas.
Es un argumento a favor de la robustez sobre el ajuste fino.

---

## 📈 Activos objetivo y sus timeframes

| Activo | Timeframes | Nota |
|--------|-----------|------|
| **Oro** (XAUUSD) | H1 y H4 | Poco tiempo de exposición |
| **Nasdaq** (US100) | H1 y H4 | Poco tiempo de exposición |
| **DAX** (GDAXI) | H1 y H4 | |
| **Divisas** | H1 | GBPJPY, USDJPY, EURUSD, AUDUSD |

**Por qué divisas.** Permiten ejecutar **muchos trades**, y eso importa porque
**Axi Select exige un número mínimo de operaciones** *(dato de proveedor — verificar el mínimo
vigente antes de usarlo como filtro)*.

### Composición de portfolio objetivo (H1 y H4)

Un portfolio se arma cubriendo las cuatro familias, no acumulando la mejor:

- Oro
- Nasdaq / SP500
- DAX
- Una divisa

---

## ⏱️ Matriz timeframe × servicio

Esta es la regla de elegibilidad más operativa del módulo: **el mismo timeframe es excelente o
inviable según el destino**, y el motivo casi siempre es el coste de financiación.

| Timeframe | Destino | Veredicto | Motivo |
|-----------|---------|-----------|--------|
| **H4** | Props (FTMO, TTP) | ❌ No recomendado | El swap se come el resultado *(dato de proveedor)* |
| **H4** | Axi Select | ❌ No se puede | Por el **score** del servicio *(dato de proveedor)* |
| **H4** | Capital propio | ✅ **El mejor de todos** | Swing trading |
| **H4** | Darwinex Zero | ✅ Muy bueno | Swap muy bajo; se puede operar en cuentas del broker **Darwinex Classic** *(dato de proveedor)* |
| **H1** | Todos | ✅ | Es el timeframe de trabajo habitual y el que habilita props y Axi |

**Por qué H4 es el más robusto (doctrina).** No tiene nada de ruido y entra poco al mercado —
**2 o 3 trades por mes**. De ahí salen estrategias extremadamente robustas y estables a largo
plazo. El problema nunca es la estrategia: es que **para props el swap la revienta**.

**Axi y capital propio.** Axi también sirve para patrimonio propio porque **no cobra swap en NQ
ni en DAX** *(dato de proveedor)*.

---

## 🔑 Doctrina central: una lógica, muchos servicios

> **No se crean estrategias para cada broker.** La lógica de la estrategia debe servir para
> cualquier servicio, **adaptando el Money Management y el riesgo**.

Es el principio con mayor consecuencia arquitectónica de todo el módulo, y define qué es
configurable y qué no:

- **La lógica de entrada y salida es del activo**, no del servicio. Se construye una vez.
- **El riesgo por operación y el MM son del servicio.** Es lo único que se re-parametriza.
- Por lo tanto **el mismo backtest puede evaluarse contra varios servicios** cambiando el
  objetivo de riesgo, sin re-minar ni re-optimizar nada.

Ver [06_Gestion_de_Riesgo.md](06_Gestion_de_Riesgo.md) para el protocolo de MM que implementa esta separación — incluida la aclaración de que **el VaR de 6,5% es de Darwinex, no de la academia**: en capital propio se puede correr 10-15% y ajustar después.

---

## ⚠️ Qué NO afirma este módulo

- **No dice que H4 sea inviable en props por la estrategia** — es por el coste de swap, que es un
  parámetro del proveedor y puede cambiar.
- **No fija el mínimo de operaciones de Axi Select.** Dice que existe y que las divisas ayudan a
  cumplirlo; el número hay que buscarlo en el reglamento vigente.
- **No define umbrales de selección.** Esos viven en `INDEX.md` y en los módulos 2 y 3.
