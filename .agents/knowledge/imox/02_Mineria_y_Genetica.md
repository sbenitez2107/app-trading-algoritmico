# Base de Conocimiento IMOX - Módulo 2: Builder (Minería y Genética)

## 🎯 Filosofía de Construcción "Anti-Rebaño"
* **Simplicidad sobre Complejidad:** Las mejores estrategias suelen tener 1 o 2 reglas lógicas. Añadir condiciones de más solo genera sobreajuste (overfitting). La solidez nace de lógicas fáciles de entender que funcionan en varios mercados.
* **El Oro es Long:** Dado que el XAUUSD es un activo con sesgo alcista histórico, se priorizan estrategias "Long-Only". El reto es que superen los Bear Markets (mercados bajistas) solo con compras, demostrando una ventaja estadística real.
* **No al "Santo Grial":** La perfección no existe en una sola cuenta o estrategia. La seguridad real reside en la **Diversificación**: diferentes instrumentos, marcos temporales y lógicas que se compensen entre sí.

## 📊 Configuración de Datos e IS/OOS
* **Historiales Largos:** Es obligatorio usar entre 10 y 20 años de datos. Ignorar datos antiguos es el error #1 que mata estrategias.
* **Propósito del IS (In-Sample):** Es el periodo de entrenamiento donde el algoritmo busca patrones.
* **Estrategia de OOS (Out-of-Sample):** * Se recomienda situar el OOS en periodos de **Bear Markets**. Si la estrategia sobrevive o gana en el peor escenario posible sin haber "visto" esos datos, es una señal de alta robustez.
* **Timeframes:** Aunque se trabaje en H1, la tendencia a futuro para máxima robustez en brokers como Darwinex apunta hacia **H4**.

## 🧬 Mecánica de Generación Genética
1. **Llenado de Islas:** El proceso comienza rellenando las islas con la población inicial (Gen 0) de forma aleatoria.
2. **Evolución:** Una vez llenas, se aplica el **Ranking** para seleccionar a los mejores individuos. Solo los que pasan los filtros iniciales se cruzan y mutan para mejorar en las siguientes generaciones.
3. **Población Sugerida:** 8 islas con 150-200 individuos. Es preferible calidad de evolución sobre cantidad masiva de basura aleatoria.

## 🛠️ Configuración de Trading y "What to Build"
* **What to Build:** Actualmente es el motor más robusto dentro de SQX para la generación de lógicas.
* **Tipos de Orden:** Uso prioritario de **Buy Stop**. Permite entrar al mercado solo cuando el precio confirma la dirección rompiendo niveles clave (como bandas o máximos).
* **Gestión de Salidas (Mandatorio):**
    * **Trailing Stop:** Es obligatorio en la operativa actual. Protege beneficios y permite que las ganancias corran en activos tendenciales como el Oro.
    * **Modelos ATR vs Pips:** Se prefiere el uso de **ATR** para Stop Loss, Profit Target y Trailing. El ATR se adapta a la volatilidad actual del mercado, mientras que los Pips son rígidos y mueren cuando el mercado cambia de volatilidad.



## 📈 Métricas de Selección (KPIs)
* **Sharpe Ratio:** Es la métrica reina para medir la relación retorno/riesgo.
    * *Definición:* Indica cuánto retorno obtenemos por cada unidad de volatilidad soportada. Un Sharpe > 1.2 es el estándar IMOX para considerar una estrategia como "profesional".
* **Ranking Multidimensional:** No basta con el beneficio neto. Se debe priorizar: **Sharpe OOS > Stability R2 OOS > Ret/DD Ratio**.

## ⚠️ Los 5 Errores que Matan Estrategias (Checklist)
1. **Pocos Datos:** Siempre usar > 10 años.
2. **Ignorar Test de Estrés:** Enamorarse del backtest sin pasar Monte Carlo o Walk Forward.
3. **Exceso de Reglas:** Si tiene más de 3 condiciones, probablemente es basura sobreajustada.
4. **Broker mal configurado:** El spread y horario en SQX deben ser idénticos a Darwinex/Axi.
5. **Buscar el Sistema Único:** El objetivo es el Portafolio, no la Estrategia única.