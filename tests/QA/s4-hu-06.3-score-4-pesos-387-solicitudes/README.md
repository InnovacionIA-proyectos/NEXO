# S4-HU-06.3 (issue #174) — Reverificar el score contra las 387 solicitudes de prueba

## Por qué esto no se corrió desde el entorno de trabajo

Este sandbox no tiene conectividad hacia la base de datos donde viven las 387
solicitudes de prueba mencionadas en el issue (mismo límite que ya se dio en HUs
anteriores de esta sesión: no hay acceso a bases compartidas/corporativas desde acá).
El fix en sí (issue #173) ya está verificado con pruebas unitarias deterministas
(`tests/Nexo.Domain.Tests/ScorePrioridadCalculatorTests.cs`), pero la
reverificación específica que pide #174 — contra las 387 solicitudes reales, con los
números de antes/después — requiere correrla contra esa base.

## Qué correr

`verify-score-4-pesos.sql` reproduce en SQL la misma fórmula que
`ScorePrioridadCalculator.Calcular` (4 pesos, con los defaults leídos de
`parametros_score`) contra la tabla `solicitudes` real, y devuelve solo agregados
(conteos por score, nunca solicitud por solicitud ni datos de cliente) para minimizar
los datos expuestos.

```
sqlcmd -S <servidor> -d dbInformesAutomatizados_Pruebas -E -i verify-score-4-pesos.sql
```

(ajustar servidor/BD/autenticación al ambiente real donde estén las 387 solicitudes).

## Qué confirma cada consulta

1. Conteo por score con la fórmula vieja (solo urgencia) — debería reproducir
   aproximadamente el 337/45 del issue, como control de que se está apuntando a la
   base correcta.
2. Conteo por score con la fórmula nueva (4 pesos) — no debería haber un valor que
   concentre una fracción comparable del total.
3. Resumen: total de solicitudes y qué porcentaje representa el score más repetido
   (antes: ~87% = 337/387 en un solo valor).
4. Las dos oportunidades de referencia del issue ($9.000.000 y $3.900.000) — deben
   quedar con `ScoreNuevo_4Pesos` distinto.

## Qué hacer con el resultado

Pegar el resultado de las 4 consultas (son solo conteos y un par de filas puntuales,
no hace falta anonimizar nada adicional) en la sección "Verificación #174" que se
puede agregar a `docs/decisiones/score-prioridad-4-pesos-vs-solo-urgencia.md`, para
dejar trazado el antes/después con números reales.
