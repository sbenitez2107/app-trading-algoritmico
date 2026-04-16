# Base de Conocimiento IMOX - Módulo 0 y 1: Filosofía, Datos e Instrumentos

## 🎯 Filosofía de Inversión IMOX
* **Tendencia sobre todo:** No se buscan sistemas contratendencia (ej. Nasdaq bajista). Si el mercado no tiene dirección clara, no se opera.
* **Swing Trading como Objetivo:** Es más fácil encontrar estrategias robustas a nivel Swing que Day Trading. El Day Trading es más difícil psicológicamente y el Swing es castigado por los brokers mediante Swaps inflados.
* **CFDs vs Futuros:** * CFDs: Mercado descentralizado. Se requiere data de alta calidad (Darwinex) para ser robusto.
    * Futuros: Data centralizada pero mercado altamente manipulado (CME).
* **Selección de Broker:** * **Darwinex:** El mejor para intradía por spreads bajos y ejecución real (DMA). Es la base para construir todas las estrategias.
    * **Axi:** Bueno para Swing en índices debido al manejo de futuros/rollover, pero con limitaciones de lotaje y manipulación de datos.

## 📊 Gestión de Datos y Temporalidades
* **Histórico:** Se construyen estrategias basadas en los últimos **10 años**.
* **Temporalidad:** Para minería (Builder) alcanza con data en **M1** (contiene Open/Close y Spread real).
* **Precisión:** La data Tick se reserva para la fase final (Optimizer) para validar con máxima precisión.
* **Timezone:** **UTC+02** (EET) con ajuste **DST** (+-1h verano/invierno). 
* **Calidad de Data:** No descartar periodos con "gaps" o mala calidad de inmediato; probar en demo primero. Se pueden obviar periodos de anomalías extremas para no ensuciar el modelo.

## ⚙️ Configuración de Instrumentos en SQX v136

### Parámetros Generales
* **Data Type:** `Index` para índices americanos, `Forex` para divisas, `CFD/Forex` para Commodities (Oro).
* **Order Size Multiplier:** 1.
* **Slippage:** 0 (en fase de minado).

### Definición de Valores (Crucial)
1. **Point Value $ (Valor del Punto):** Es el dinero en USD que se gana/pierde por cada movimiento de **1.0** en el precio con 1 lote.
    * *Cálculo Forex:* 100,000 / Cotización del par contra el USD (ej: para GBPJPY, convertir 100k Yen a USD).
    * *Oro (XAUUSD):* 100.
2. **Pip/Tick Size (Tamaño del Pip):** Precisión del activo.
    * Índices: 1.
    * Oro/Forex: 0.01 o 0.1 (según precisión del broker).
3. **Pip/Tick Step (Paso mínimo):** Mínimo movimiento posible (generalmente igual al Size).

### El Spread y la "Regla de la Coma"
* **Minería:** Usar spreads bajos para no dificultar el hallazgo de lógicas (Oro/Índices = 1, Divisas = 1 a 3).
* **Interpretación:** Si hay un decimal de diferencia entre `Size` y `Step`, corre la coma del spread un lugar a la izquierda (ej: Spread 25 en MT4 = 2.5 en SQX). Si son iguales, el valor es directo.

## ⚠️ Swaps y Comisiones
* **Builder:** Configurar como **NONE**. No meter ruido de Swaps actuales en datos de hace 10 años.
* **Retester:** Aquí es donde se aplican como **Crosschecks**. Se testean rangos de Swaps y Spreads para ver si la estrategia sobrevive a los costos de diferentes brokers.
* **Advertencia v136:** El Swap está buggeado (lo suma como positivo). Ignorar en esta versión durante el minado.

## 🔒 Seguridad Operativa
* **Cierre de Fin de Semana:** No dejar operaciones abiertas el fin de semana. Un gap de lunes puede quemar una cuenta. Si un broker/prop no te obliga a cerrar, es indicio de que no operan en mercado real.