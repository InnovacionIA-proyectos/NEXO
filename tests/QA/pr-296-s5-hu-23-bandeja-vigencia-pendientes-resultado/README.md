# PR #296 (S5-HU-23.1) — Bandeja con vigencia por Fecha Límite, pendientes por entregar y resultado (cierra #289)

## Qué cambia

- `vigencia` se mide contra el `Deadline` de la solicitud (sin Deadline = vigente), no
  contra la antigüedad de la captura.
- `atencion=pendientes` ahora también incluye la solicitud con todas sus líneas
  revisadas por el Administrador de Fletes que aún no se entregó (sigue apareciendo
  también en `atendidas`; no aplica a `automatico`).
- Columna `resultado` en cada fila + filtro `resultado=esperando|ganada|perdida`
  (`esperando` = cotización entregada o auto-costeada sin resultado, solicitud no
  descartada). `resultado=xx` → 400.
- Sin cambios de esquema.

## Qué se verificó

**Merge real contra `QA`**: sin conflictos.

**Código revisado línea por línea:** `clsBandejaResultado.Parsear` (valor desconocido
es error del cliente, no cae a "todos"), y el filtro en
`clsEfSolicitudesCatalogoRepository` (aislamiento por comercial va ANTES que
cualquier otro filtro incluido el nuevo OR de pendientes; "esperando" exige que
NINGUNA cotización tenga resultado, para mantener coherencia con la columna
`resultado`).

**Suite completa contra la base real de pruebas (2026-10-14):**
- `Nexo.Domain.Tests`: 157/157.
- `Nexo.Application.Tests`: 373/373.
- `Nexo.Api.Tests`: primera corrida sin Cubicador → 7 fallos (6 por falta del
  Cubicador local, mismo patrón que en PRs anteriores de esta sesión + 1 ya
  diagnosticado en #292, dato SAP vencido por calendario, ajeno a esta PR). Armé
  el Cubicador real localmente de nuevo (Docker Desktop no estaba corriendo al
  principio, Kevin lo inició) → **360/363 superadas, 2 omitidas, solo el fallo ya
  conocido**. Coincide con lo declarado (361 OK la autora — 1 de diferencia por la
  misma fila SAP).

**Verificación en vivo (API real contra BD real):**
- `GET /api/solicitudes?resultado=xx` → `400 RESULTADO_INVALIDO`.
- `?resultado=esperando` → 50 filas, todas con `resultado: SinDefinir`.
- `?resultado=ganada` → 50 filas, todas `Ganada`.
- `?resultado=perdida` → 28 filas, todas `Perdida`.
- `?vigencia=vigente` → 50 filas, todas `vigente`.
- `?vigencia=vencida` → 50 filas, todas `vencida`.

## Resultado

Sin hallazgos bloqueantes. El único fallo de la suite es el ya documentado en la
verificación de la PR #292 (dato SAP vencido por calendario), no relacionado con
esta PR.
