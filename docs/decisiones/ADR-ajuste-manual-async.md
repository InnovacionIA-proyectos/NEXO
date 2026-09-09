# ADR — El ajuste manual de colocaciones pasa a comando async

**Estado:** aceptada e implementada · **Fecha:** 2026-09-09 · **Issue:** #101 (S3-HU-01.1) · **Implementado en:** #104/#105 (S3-HU-02)

## Contexto

Hasta S3-HU-02, `PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}` solo
existía en Cubicador (`cubicador/api/routers/planes_cargue.py`) y era **siempre
síncrono** — así lo documentaba `cubicador/CONTRATO.md` §3.2 antes de esta HU
("ajuste manual — siempre síncrono, nunca pasa por cola"). El navegador le
hablaba directo al motor 3D para mover, rotar, bloquear, retirar o reinsertar
una unidad ya colocada.

Eso rompía el principio que ya regía `optimizar`/`crear_desde_linea`: toda
escritura del plan de cargue pasa por el mismo camino auditable de colas
(mismo exchange, mismos reintentos 1s/5s/15s/60s, mismo DLQ, misma alerta). El
ajuste manual era la única excepción, y S3-HU-02 (criterio 3) la cierra.

## Decisión

El ajuste manual deja de ser un PATCH síncrono contra Cubicador. Pasa a ser
`ajustar_colocacion`, uno de los 8 comandos async de S3-HU-02:

- El endpoint vive en **NEXO** (`PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}`,
  `AjustarColocacionCommand.cs`) — nunca una URL de Cubicador.
- Responde **202** de inmediato con `ComandoAceptadoDto` — no con el resultado
  aplicado.
- El worker de Cubicador (`worker/main.py`, caso `"ajustar_colocacion"`)
  ejecuta la MISMA validación que ya tenía el PATCH síncrono
  (`application/plan_cargue/ajustar.py::ajustar_colocacion` — límites,
  colisión, soporte mínimo 60%, apilamiento) contra su propia base, sin
  cambios de lógica de negocio.

### UI optimista

El frontend (consumidor en S3-HU-04, sprint futuro) aplica el ajuste de
inmediato en pantalla al recibir el 202, sin esperar confirmación — es una
interacción interactiva de arrastrar/soltar en un visor 3D, y esperar un
round-trip completo por cola la haría inutilizable.

### Reconciliación si el worker rechaza el ajuste

Esto se resuelve en el CONTRATO desde ya, aunque el frontend lo consuma
después — no se puede publicar un comando cuyo camino de fallo quede
indefinido:

1. Si `ajustar_colocacion` viola una regla (fuera de límites, colisión,
   soporte insuficiente, apilamiento inválido, orientación no permitida),
   Cubicador publica `cubicacion.rechazada` con el código y el motivo — el
   MISMO evento que ya usa `optimizar` cuando falla.
2. `CubicacionRechazadaConsumer` (ya existente, sin cambios) marca
   `PlanCargue.Estado = "error"` en NEXO y avisa por `/hubs/plan-cargue` con
   ese motivo en `AvisoPlanCargueDto.Motivo`.
3. El frontend, suscrito a ese hub para el plan en cuestión, recibe el aviso y
   **revierte** el movimiento optimista (o vuelve a consultar
   `GET /api/planes-cargue/{id}` para reconstruir el estado real) —
   exactamente el mismo patrón de reconciliación que ya usa el resto del
   contrato de trabajos pesados (`docs/contrato-integracion/README.md` §5:
   "el aviso es una mejora, no una garantía", la verdad vive en el GET).

No hace falta un evento ni un canal nuevo para esto: es el mismo camino de
rechazo que ya estaba verificado en producción para `optimizar`.

## Por qué se acepta

- Cierra el único hueco que quedaba en "toda escritura pasa por la cola" —
  sin esto, S3-HU-02 habría sido incompleta a propósito.
- No agrega superficie nueva de fallo: reutiliza el evento
  `cubicacion.rechazada` y el consumer que ya existían, verificados en
  producción para `optimizar` (ver `docs/mensajeria/CUBICACION.md` §3).
- La UI optimista no es una idea nueva de esta ADR: ya era la única forma
  razonable de que un ajuste interactivo en pantalla se sintiera instantáneo,
  esté detrás de una cola o no.

## Cuándo hay que volver a mirar esto

- Si se mide en producción que el tiempo entre el 202 y la confirmación (o el
  rechazo) es tan largo que la ventana de "aplicado optimista pero no
  confirmado" genera ediciones concurrentes conflictivas sobre la misma
  colocación — hoy no hay bloqueo optimista por versión, solo reconciliación
  después del hecho.
- Si el frontend (S3-HU-04) necesita distinguir "todavía procesando" de
  "confirmado" en la propia colocación (hoy el aviso es a nivel de plan, no
  de colocación individual).

## Alternativas descartadas

**Dejarlo síncrono, solo moviendo la URL a NEXO (proxy).** Resuelve el "nunca
una URL de Cubicador" pero no el "mismo camino auditable de colas" — seguiría
siendo la única escritura sin reintentos/DLQ/alerta uniformes, y NEXO tendría
que abrir una llamada HTTP saliente a Cubicador con su propio manejo de
timeouts, duplicando lo que la cola ya resuelve.

**Bloqueo optimista por versión (ETag/versión de la colocación).** Cerraría el
hueco de ediciones concurrentes mencionado arriba, pero es complejidad que
esta HU no pidió y que el frontend (todavía sin construir) no tiene forma de
aprovechar hoy. Queda anotado como el primer punto de "cuándo revisar esto".
