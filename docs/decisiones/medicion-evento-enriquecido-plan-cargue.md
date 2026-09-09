# Medición — tamaño real del evento `plan-de-cargue.generado` enriquecido

**Estado:** aceptada · **Fecha:** 2026-09-09 · **Issue:** #109 (S3-HU-03.1) · **Bloquea a:** #110, #111 (no se escribe el consumer hasta cerrar esto)

## Qué se pidió

Antes de tocar código: construir el payload enriquecido de
`plan-de-cargue.generado` (con la lista completa de colocaciones, no solo el
conteo) contra el plan más grande que exista HOY, medir su tamaño real en KB,
y decidir — aceptar tal cual, paginar, o comprimir.

## Metodología (y por qué no son datos de producción)

Esta sesión no tiene acceso a un ambiente de pruebas compartido con datos
reales, y usarlos igual habría expuesto datos de clientes sin necesidad. En
su lugar se construyó el escenario más grande **posible** con datos
sintéticos, corrido contra el motor real (`domain/motor`, sin mocks) para que
la geometría y el shape del resultado sean idénticos a los de un plan real:

- **Caso peor (techo de validación)**: 2.000 unidades físicas — el tope duro
  de una sola `importar_items` (issue #104/#105, `ValidationFailedException`
  por encima de esto) — repartidas en 20 tipos de ítem, contra el vehículo
  más grande del catálogo (Tractocamión furgón 13.6 m). El motor optimizó de
  verdad: 940 cargadas, 1.060 rechazadas (todas aparecen en `colocaciones`,
  cargadas o no).
- **Caso grande pero típico**: 150 unidades en 10 tipos de ítem, mismo
  vehículo, las 150 cargadas.

El payload medido en ambos casos es el propuesto para #110: el `resultado`
completo (`items`, `colocaciones`, `metricas`, `incidencias`,
`pesoTotalKg`/`volumenUtilizadoM3`/`utilizacionVolumen`/`utilizacionPiso`) —
ver el shape exacto en `docs/mensajeria/CUBICACION.md` §7. El script queda en
el historial de esta conversación, no en el repo (era de un solo uso).

## Resultado medido

| Escenario | Colocaciones | Tamaño evento ACTUAL (solo conteo) | Tamaño evento ENRIQUECIDO | Por colocación |
|---|---|---|---|---|
| Típico grande (150 u.) | 150 | 184 bytes | **165.78 KB** | ~1.13 KB |
| Peor caso (2.000 u., techo de una importación) | 2.000 | 185 bytes | **2,889.75 KB (≈2.9 MB)** | ~1.48 KB |

El costo por colocación baja un poco en el caso grande (~1.13 KB vs ~1.48 KB)
porque los `items` (fijos, 10 o 20) se amortizan entre más colocaciones —
la mayor parte del peso es la lista de `colocaciones`, no `items`.

## Decisión

**Se acepta el payload enriquecido tal cual, sin paginar ni comprimir, para
este sprint.**

Razones, con la medición como evidencia:

1. **RabbitMQ no es el limitante.** El límite práctico de tamaño de mensaje
   de RabbitMQ (`max_message_size`, por defecto 128 MB) está muy por encima
   incluso del peor caso medido (2.9 MB) — no hay riesgo de que el broker
   rechace o fragmente el mensaje.
2. **El Outbox de SQL Server tampoco lo es.** El body del mensaje se guarda
   en una columna `nvarchar(max)`/`varbinary(max)` de la tabla de Outbox de
   MassTransit — unos pocos MB no es un problema de almacenamiento ni de
   tamaño de fila.
3. **No es un camino caliente.** Este evento se publica una vez por
   operación de escritura del plan (crear, optimizar, guardar/importar/
   eliminar ítem, ajustar colocación) — no es telemetría de alta frecuencia.
   El costo de un mensaje de ~1-3 MB ocasional es irrelevante frente al costo
   de construir paginación o compresión para un problema que hoy no existe.
4. **El "peor caso" es justamente eso: el techo de UNA sola importación**,
   no lo que se espera ver en un plan típico — el escenario realista (150
   unidades) pesa 165 KB, casi 18 veces menos.

**No se descarta paginar/comprimir para siempre** — se deja escrito cuándo
revisar esto (abajo), en vez de decidirlo una sola vez y olvidarlo.

## Cuándo hay que volver a mirar esto

- Si en producción los planes reales se acercan de forma habitual (no como
  excepción) al techo de 2.000 unidades — es decir, si el "peor caso" de hoy
  se vuelve el caso común.
- Si se mide un mensaje enriquecido por encima de ~5 MB en producción.
- Si el tiempo de procesamiento del consumer (issue #111, escritura de
  Items/Colocaciones vía EF) se vuelve un cuello de botella medible — la
  primera palanca ahí NO es el tamaño del mensaje sino el `SaveChangesAsync`
  de cientos/miles de filas en una sola transacción (ver nota de
  implementación en #111).

## Qué se haría si hubiera que resolverlo

- **Comprimir (gzip el body JSON)** antes que paginar: el JSON de
  `colocaciones`/`items` tiene claves repetidas por cada elemento — comprime
  muy bien (80-90% típico en payloads así), llevaría el peor caso medido a un
  estimado de 300-600 KB. Es un cambio de una sola línea en
  `RabbitMqEventosPublisher._publicar`/en el consumer de NEXO (Content-
  Encoding), sin tocar el contrato de campos.
- **Paginar** (partir `colocaciones` en varios mensajes) solo si comprimir no
  alcanza — es la opción más cara: exige un id de "página", reensamblado del
  lado de NEXO y manejo de fallos parciales. No se justifica con la evidencia
  de hoy.

## Alternativas descartadas (para este sprint)

**Paginar de una vez, "por si acaso".** Es la opción más cara en complejidad
y esta medición no encontró evidencia de que haga falta — el criterio del
equipo es no pagar ese costo sin un problema real que lo justifique.

**Comprimir de una vez.** Más barato que paginar, pero tampoco tiene
evidencia que lo justifique hoy; además complica la depuración manual de
mensajes en la DLQ (`cubicacion.dlq`) — hoy se pueden inspeccionar como texto
plano. Queda como la primera palanca a activar si el escenario de "cuándo
revisar esto" se cumple, no como algo que se active preventivamente.
