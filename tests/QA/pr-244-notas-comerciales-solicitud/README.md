# PR #244 — Notas/observaciones comerciales en la solicitud (S4-HU-18.10, #227)

## Qué se verificó

Que el campo `notas` (opcional, máx. 2000 caracteres) viaja completo desde
`POST /api/captura-solicitudes` hasta `GET /api/solicitudes/{id}`, persistido
vía el mismo mecanismo JSON que ya usa `CondicionesServicio` (sin migración de
esquema) — y que una solicitud sin notas se crea y lee con `notas: null`, sin
romper el JSON legado que no tiene el campo.

Se revisó línea por línea el diff (`CapturaRequests`, el command, el mapper de
lectura `clsSolicitudDtoMappers`, y `clsCondicionesServicioDto`) antes de
correr nada, para confirmar que la escritura y la lectura usan el mismo campo
y que no hay una ruta de lectura que no pase por el mapper actualizado.

## Cómo correrlo

```powershell
./verify-suite-completa.ps1
```

Usa la Opción 1 (PowerShell nativo) de `docs/Testing/PRUEBAS-LOCALES.md`, no
el script bash habitual de las PRs anteriores (`docker-compose.ci.yml` /
`pruebas-reales-completas`) — el test nuevo
`Notas_capturadas_se_leen_de_vuelta_al_abrir_la_solicitud` abre/costea una
solicitud con ruta real (Medellín→Bogotá), y ese flujo falla en Docker por la
limitación de TLS con DWSice que la propia PR documentó en esa misma guía.
Requiere VPN corporativa activa.

## Resultado real verificado (2026-09-29)

- Build (`dotnet build -c Release`): 0 errores.
- `Nexo.Domain.Tests`: 143/143.
- `Nexo.Application.Tests`: 229/229 (incluye los 3 tests nuevos de
  `clsCondicionesServicioDtoTests`: round-trip JSON, `Desde(null)` da `Notas`
  nulo, JSON legado sin el campo no revienta).
- `Nexo.Api.Tests`: 202/204 (2 skips preexistentes de `clsMailSenderRealTests`,
  no relacionados). Incluye los 2 tests nuevos de `clsCapturaSolicitudesTests`,
  ambos en verde:
  - `Notas_capturadas_se_leen_de_vuelta_al_abrir_la_solicitud` (4.4s, ruta real
    Medellín→Bogotá contra DWSice).
  - `Sin_notas_la_solicitud_se_crea_y_lee_con_notas_nulas`.
- Verificado contra la base real de pruebas (`dbInformesAutomatizados_Pruebas`,
  vía `ConnectionStrings__Nexo` de `.env.pruebas-reales.local`).

## Hallazgo ajeno a esta PR

Los 3 checks de CI que aparecen en rojo en GitHub (`CI/CD Cubicador-API`,
`CI/CD Cubicador-Worker`, `CI/CD RabbitMQ`) no llegan a arrancar por un
problema de facturación de la cuenta de GitHub Actions ("recent account
payments have failed or your spending limit needs to be increased"), no por
el código de esta PR — no toca `cubicador/` ni RabbitMQ. El check relevante
(`CI/CD API`, self-hosted) pasó en verde.

## Fuera de alcance / sin limpieza de datos

Los 2 tests nuevos crean solicitudes reales en `dbInformesAutomatizados_Pruebas`
(BD dedicada de pruebas, no producción) — mismo patrón sin teardown ya usado
por el resto de la suite de `Nexo.Api.Tests` desde antes de esta PR.
