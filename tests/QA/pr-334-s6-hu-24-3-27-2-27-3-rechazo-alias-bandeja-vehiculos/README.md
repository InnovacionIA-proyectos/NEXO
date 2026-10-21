# PR #334 (S6-HU-24.3/27.2/27.3, #320-#322) — Rechazo de línea, diccionario de alias y bandeja de vehículos

## Qué cambia

- **Rechazar línea (#320)**: `POST`/`DELETE /api/cotizaciones/{id}/lineas/{lineaId}/rechazo`.
  Una línea rechazada no tiene flete, no suma al total, cuenta como resuelta
  al entregar, sale "Sin costeo" en el Excel y con su motivo en el correo.
  Se puede deshacer mientras no se entregue.
- **Alias (#321)**: la captura resuelve tipo de vehículo/clase de contenedor
  con `tbl_alias_elementos` (TB, T.B, Turbo, 2L2 → 2L2; "40 HC" → CNT40.HC).
  Endpoints para listar/resolver/agregar alias.
- **Vehículos (#322)**: `PUT .../lineas/{id}/vehiculo` cambia el vehículo de
  una línea (descarta el flete, recalcula propuesta y piso); bandeja de
  normalización de vehículos en cuarentena (listar, mapear, descartar).
- **Prerrequisito crítico de BD**: columna `Descartada` en
  `tbl_configuraciones_vehiculo` — sin ella el costeo falla con 500.

## Qué se verificó (ver `verificar.ps1`, script real ejecutado)

**Prerrequisitos de BD confirmados en la base real antes de correr nada**:
columna `Descartada` (bit, NOT NULL) ya existe en
`tbl_configuraciones_vehiculo`; `tbl_alias_elementos` (de la PR #330) sigue
con sus 71 filas intactas.

**Código revisado línea por línea**: `clsRechazarLineaCotizacionCommand`
(exige línea SIN flete, 409 si ya tiene uno — así una rechazada nunca puede
sumar al total) y `clsCambiarVehiculoLineaCommand` (descarta la fila de
cotización existente, recalcula con `clsCotizacionCalculos.Recalcular`,
vuelve la línea a `PorCostear`, re-propone contra el piso del vehículo
nuevo — nunca deja sobrevivir un flete calculado con el vehículo viejo).

**Valor EXACTO verificado, no solo "responde bien"**: los tests de la propia
PR (ya ejecutados como parte de la suite) confirman cifras exactas, no
aproximadas — `Entrega_con_una_linea_rechazada_el_total_no_la_suma...`:
total = **1.000.000** exacto (excluye la línea rechazada);
`Con_todas_las_lineas_rechazadas...`: total = **0** exacto; la celda "TOTAL
COTIZACIÓN" del Excel exportado = **1.000.000** exacto, igual al total de la
API.

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta,
0 errores (1 warning preexistente ajeno a esta PR).

**Suite completa contra la base real de pruebas**:
- `Nexo.Domain.Tests`: **203/203**.
- `Nexo.Application.Tests`: **521/521**.
- `Nexo.Api.Tests`: **1 fallo, el mismo ya diagnosticado en la verificación de
  la PR #333** (`clsHistoricoSapPaginacionTests`, tabla
  `dbo.tblSAPTarifasLog` real con 0 filas en este ambiente — no es un bug de
  código, no relacionado con esta PR).

**Cubicador real armado localmente**: segunda corrida completa de
`Nexo.Api.Tests` → **714/717, 1 mismo fallo (dato, no código), 2 omitidas**
(MailSender) — las 6 E2E del Cubicador corrieron y pasaron.

## Nota operativa

Igual que con la PR #333, confirmé el merge con `QA` localmente sin
conflictos y lo empujé preventivamente a la rama de la PR
(`origin/320-322`) antes de mergear, para evitar el mismo desajuste de
mergeabilidad de GitHub que se presentó en la PR #333.

## Limpieza

- `docker compose down -v` en `cubicador/`, `.env` temporal borrado.
- `docker compose stop/rm rabbit` en la raíz de NEXO.

## Resultado

Sin hallazgos bloqueantes. El único fallo de la suite es el mismo problema
de datos del ambiente ya reportado en la PR #333, no un defecto de esta PR.
