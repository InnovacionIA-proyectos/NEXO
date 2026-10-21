# PR #332 (S6-HU-26.1, #306) — Editar el flete de cualquier fuente, salvo con regla Solo-SICE

## Qué cambia

- `PUT` sobre la línea de una cotización: editar el flete de CUALQUIER
  fuente (antes solo Manual, 409 `FLETE_NO_EDITABLE` — código que ya no
  existe). Al guardar, la fuente pasa a `Manual` y la auditoría guarda valor
  y fuente anteriores.
- Si la ruta+vehículo tienen regla Solo-SICE: `409 FLETE_SOLO_SICETAC` (una
  sola definición, `clsReglaSoloSicetacDeLinea`, compartida con el motor de
  propuesta y con `tieneReglaSoloSicetac` de la Mesa de Costeo).
- Nunca por debajo del piso SICE-TAC (`400 FLETE_BAJO_PISO_SICETAC`).
- Si el piso no se puede verificar (sin fila en DWSice, período no es el
  actual, o la consulta falló): `409 PISO_SICETAC_NO_VERIFICABLE` — ya no se
  guarda en silencio como antes (#283). Se confirma con `confirmarSinPiso`,
  queda auditado.

## Qué se verificó (ver `verificar.sh`, script real ejecutado)

**Código revisado línea por línea**: `clsReglaSoloSicetacDeLinea.Tiene`
(exige DANE en los dos lados, excluye la línea de Cupo sin vehículo que ya
salta la regla) y `clsEditarLineaCotizacionCommandHandler` (orden de
validación: Solo-SICE → piso bajo → piso no verificable; la comparación es
`ValorFlete < piso`, estrictamente menor).

**Valor límite EXACTO verificado, no solo "responde bien"**: leí
`clsEditarLineaCotizacionCommandTests` y confirmé que la aserción cubre el
límite preciso — con `Piso = 2.000.000`: `Piso - 1` (1.999.999) se rechaza
con `FLETE_BAJO_PISO_SICETAC`; `Piso` exacto (2.000.000) y `Piso + 1`
(2.000.001) se aceptan y se guardan con el valor exacto enviado (sin
redondeo). Confirma que el operador de comparación (`<`, no `<=`) está
implementado correctamente: el piso mismo es un valor VÁLIDO, no el límite
de rechazo.

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta,
0 errores (1 warning preexistente ajeno a esta PR).

**Suite completa contra la base real de pruebas**:
- `Nexo.Domain.Tests`: **191/191**.
- `Nexo.Application.Tests`: **417/417**.
- `Nexo.Api.Tests`: primera corrida sin Cubicador local → **398/406** (0
  errores, 8 omitidas: 2 MailSender + 6 E2E de Cubicador).

**Cubicador real armado localmente** (esta PR no lo toca, se levantó para no
depender de los 6 skips): segunda corrida → **403 superadas, 1 con error, 2
omitidas** (406 total). El único fallo
(`clsOutboxConsistenciaTests.Publicar_sin_confirmar_tampoco_deja_nada`) fue
un **deadlock transitorio de SQL Server** (`Transaction was deadlocked on
lock resources`) por la carga concurrente de la suite completa contra la
base compartida — **no relacionado con esta PR** (no toca código de
mensajería/Outbox). Confirmado aislándola: `dotnet test --filter
clsOutboxConsistenciaTests` → **3/3, 0 errores**. Las 6 E2E del Cubicador
corrieron de verdad con el código real de QA y pasaron.

## Limpieza

- `docker compose down -v` en `cubicador/`, `.env` temporal borrado.
- `docker compose stop/rm rabbit` en la raíz de NEXO.

## Resultado

Sin hallazgos. El límite del piso SICE-TAC es exactamente el declarado (sin
error de +/-1), y el único fallo de la suite fue un flake de infraestructura
compartida, no un defecto de esta PR.
