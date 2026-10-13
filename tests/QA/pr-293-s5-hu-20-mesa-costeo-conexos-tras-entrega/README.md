# PR #293 (S5-HU-20.1) — API de Mesa de Costeo y servicios conexos tras la entrega (cierra #286)

## Qué cambia

- `valorMercancia` por línea; desglose `valorPisoBase` + `valorTiemposLogisticos` en el
  candidato SICE-TAC (invariante: suman `Valor`).
- `GET /api/cotizaciones/por-solicitud/{id}` (200/204, sin crear nada) y
  `PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}` (editar flete manual sobre la
  misma línea, sin duplicar).
- Conexos: agregar/quitar con la cotización Borrador **o** Entregada (antes solo
  Borrador); Ganada/Perdida → 409 `COTIZACION_CERRADA`; marca `ModificadaTrasEntregaAt`
  sin tocar `Estado`/`EntregadaAt`; aviso por correo al creador de la solicitud (salvo
  que el actor sea esa misma persona).
- Columna nueva `tbl_cotizaciones.ModificadaTrasEntregaAt` (nullable, aditiva).

## Qué se verificó

**Merge real contra `QA`**: sin conflictos.

**Código revisado línea por línea:** `clsEditarLineaCotizacionCommand` (solo Borrador,
solo fuente Manual, re-valida el piso SICE-TAC server-side), `clsCambioDeConexos`
(regla compartida agregar/quitar: `ExigirSinResultado` en vez de `Estado`, por eso
Entregada sigue admitiendo cambios pero Ganada/Perdida no; `ModificadaTrasEntregaAt`
sin tocar SLA), `clsObtenerCotizacionPorSolicitudQuery` (sin efectos secundarios,
resuelve la ambigüedad Entregada-vs-Borrador igual que el comando de creación).

**Suite completa contra la base real de pruebas (2026-10-13):**
- `Nexo.Domain.Tests`: 157/157.
- `Nexo.Application.Tests`: 320/320.
- `Nexo.Api.Tests`, primera corrida sin Cubicador: 262/271 (6 fallos por falta del
  Cubicador local — mismo patrón que en #291/#292 — + 1 ya diagnosticado en la
  verificación de la PR #292: dato SAP vencido por calendario, ajeno a esta PR).
- Segunda corrida, con el Cubicador real armado localmente (mismo procedimiento que
  en PRs anteriores): **268/271 superadas, 2 omitidas, solo el 1 fallo ya conocido**
  — los 6 E2E del Cubicador pasan. Coincide con lo declarado por la PR (269 OK la
  autora, 268 acá por la misma fila SAP que venció entre su corrida y la mía).

**Verificación en vivo adicional (API real contra BD real, no solo tests
automatizados):**
- `GET /api/cotizaciones/por-solicitud/{id}` **antes** de crear la cotización → `204`,
  confirmado que no crea nada (repetí el GET, seguía en 204).
- Creé la cotización (`POST /api/cotizaciones`) → `GET /por-solicitud` ahora → `200`,
  con el campo `modificadaTrasEntregaAt: null` ya presente en la respuesta.
- Agregué un flete manual (`POST .../lineas`, 2.000.000) y lo edité
  (`PUT .../lineas/{lineaSolicitudId}`, 2.500.000) — confirmado: sigue siendo **una
  sola línea** (no se duplicó) y el total de la cotización quedó en 2.500.000, no
  4.500.000.

## Resultado

Sin hallazgos bloqueantes. El único fallo en la suite es el ya documentado en la
verificación de la PR #292 (dato SAP vencido por calendario, no relacionado con esta
PR). Los comportamientos nuevos de la HU se verificaron tanto por tests automatizados
como en vivo contra la base real.
