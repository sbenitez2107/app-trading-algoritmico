# 📊 Diccionario de Métricas de Análisis - StrategyQuant X

Este documento contiene la descripción técnica de las métricas utilizadas en SQX para evaluar el rendimiento, riesgo y robustez de las estrategias de trading.

---

## 1. Métricas de Beneficio y Retorno
* **Total Profit**: Suma total de todas las ganancias menos todas las pérdidas.
* **Profit In Pips**: Beneficio o pérdida total expresado en pips.
* **Yearly AVG Profit**: Indica el beneficio promedio obtenido por cada año de operativa.
* **Yearly AVG % Return**: Porcentaje de retorno promedio anual basado en el capital inicial.
* **CAGR (Compound Annual Growth Rate)**: Tasa de crecimiento anual compuesta; representa el porcentaje de apreciación anual (p.a.).

## 2. Métricas de Riesgo y Eficiencia
* **Sharpe Ratio**: Mide el retorno de una inversión comparado con su riesgo (volatilidad). Es el exceso de retorno por unidad de riesgo.
* **Profit Factor**: Relación entre la ganancia bruta y la pérdida bruta. Se recomienda un valor mínimo de **1.3**.
* **Return/DD ratio**: Relación entre el beneficio neto y el máximo retroceso (drawdown). Es una de las métricas más utilizadas en la academia IMOX para medir la calidad del riesgo.
* **Winning Percentage**: Porcentaje de operaciones ganadoras (usualmente entre el 40% y 60%).
* **Draw Down**: La mayor caída de capital (equity) desde un pico hasta un valle en términos monetarios.
* **% Draw Down**: Máximo porcentaje de caída del capital durante el periodo de prueba.

## 3. Promedios y Expectativa
* **Daily AVG Profit**: Beneficio promedio obtenido por día.
* **Monthly AVG Profit**: Beneficio promedio obtenido por mes.
* **Average Trade**: Ganancia promedio por cada operación individual realizada.
* **Annual% / Max DD%**: Relación entre el porcentaje de retorno anual y el porcentaje de retroceso máximo.
* **R Expectancy**: Beneficio promedio por operación considerando el riesgo (pérdida potencial máxima por trade).
* **R Expectancy Score**: Extensión del R Expectancy multiplicado por el número promedio de trades anuales.

## 4. Métricas de Calidad (Van Tharp)
* **STR Quality Number (SQN)**: Mide la calidad de un sistema de trading.
    * **2.0 – 2.4**: Promedio.
    * **3.0 – 5.0**: Excelente.
    * **7.0+**: Posible "Santo Grial".
* **SQN Score**: Ajusta el SQN considerando la duración del test y el número de operaciones generadas.

## 5. Estadísticas de Operaciones
* **Wins/Losses ratio**: Relación numérica entre operaciones ganadoras y perdedoras.
* **Payout ratio**: Cuántas veces es mayor la ganancia promedio frente a la pérdida promedio.
* **AHPR (Average Holding Period Return)**: Retorno promedio por periodo de retención.
* **Z-Score**: Evalúa la dependencia entre operaciones (si una ganancia suele seguir a otra ganancia o pérdida).
* **Z-Probability**: Probabilidad estadística derivada del Z-Score.
* **Expectancy**: Valor estadístico de lo que se espera ganar en la próxima operación.
* **Exposure**: Porcentaje de tiempo que la estrategia mantiene posiciones abiertas en el mercado.

## 6. Estancamiento y Estabilidad
* **Stagnation In Days**: Periodo máximo de tiempo (en días) que la estrategia pasó sin registrar un nuevo máximo de capital.
* **Stagnation In %**: Porcentaje máximo de tiempo que la estrategia estuvo estancada.
* **Stability**: Métrica propietaria de SQX que usa regresión lineal para evaluar qué tan recta es la curva de equidad. Un valor de **1** indica una línea ascendente perfecta.

## 7. Métricas de Simetría
* **Symmetry**: Compara el beneficio del lado largo (compras) vs. el lado corto (ventas).
* **Trades Symmetry**: Compara la cantidad de operaciones largas vs. cortas.
* **NSymmetry**: Muestra '-1' si solo un lado es rentable; '0' si ambos lados ganan o ambos pierden.

---