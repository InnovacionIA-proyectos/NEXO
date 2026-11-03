# PR #247 — Fixes de DI (Catálogos/RangosPeso/TarifasPlaza/ReglasSoloSicetac) + Excel de fletes sin columnas internas (S4-HU-17.4, #230)

## Contexto

Esta PR y la #246 (`DEPLOY`) terminaron apuntando al **mismo commit exacto**
(`a674cbc`) — #247 es un superset de #246 (todos sus commits + 2 más: el
fix de DI de `ReglasSoloSicetac` y el fix del Excel de fletes). Por acuerdo
con el responsable del proyecto, se verificó y mergeó solo #247; #246 se
cierra por separado como redundante, referenciando esta PR.

## Qué se verificó

**1. Fix de DI (bug real, no cubierto por ningún test automatizado a nivel HTTP):**
`Catalogos`, `RangosPeso`, `TarifasPlaza` y `ReglasSoloSicetac` (esta última
solo el handler de la plantilla Excel) tenían sus casos de uso de
`Application` sin registrar en el contenedor de DI — el adaptador de
`Infrastructure` sí estaba registrado, pero nadie llamaba
`Add*UseCases()` desde `clsProgram.cs`. Los endpoints fallaban con 500
("Unable to resolve service for type IQueryHandler<...>") al primer
request real, porque el contenedor por defecto de .NET no valida el grafo
de dependencias al arrancar (`ValidateOnBuild` no está activado) — la suite
de `Nexo.Api.Tests` pasa igual de verde con o sin el bug, porque ningún
test golpea esos endpoints por HTTP. Se revisó el diff línea por línea
(`clsProgram.cs` + 4 archivos `clsDependencyInjection.cs`) para confirmar
que los `Add*UseCases()` nuevos quedaron invocados.

Verificación independiente (no solo confiar en el "probé localmente" de la
PR): se levantó la API real (`dotnet run`) contra la base real de pruebas
(`dbInformesAutomatizados_Pruebas`), sin `ConnectionStrings:IdentidadPortal`
(cae al adapter local, que resuelve el rol contra el usuario Admin ya
sembrado `IdentidadPortalId=1005`), y se golpearon los 5 endpoints
afectados con el header real `X-Portal-clsUsuario-Id: 1005`:

| Endpoint | Resultado |
|---|---|
| `GET /api/catalogos/tipos-vehiculo` | 200, con datos reales del catálogo |
| `GET /api/pesos` | 200 |
| `GET /api/tarifas-plaza` | 200, con datos reales |
| `GET /api/reglas-solo-sicetac` | 200 |
| `GET /api/reglas-solo-sicetac/plantilla-excel` | 200 |

**2. Fix del Excel de fletes (#230):** se revisó
`clsExportadorCotizacionExcelClosedXml.cs` línea por línea — el encabezado
de 7 columnas pasa a 3 (`clsRuta`, `Tipología`, `Flete`), y `Flete` ahora
escribe `totalLinea` (`ValorFlete + Aprovisionamiento + Retorno contenedor`)
en vez de `ValorFlete` aislado. Los 3 tests nuevos
(`clsExportadorCotizacionExcelTests`) generan el `.xlsx` real en memoria
con ClosedXML y lo vuelven a leer — confirmado en verde:
`El_encabezado_ya_no_trae_Fuente_Aprovisionamiento_ni_Retorno_contenedor`,
`La_columna_Flete_es_el_total_completo_no_solo_ValorFlete`,
`Sin_aprovisionamiento_ni_retorno_Flete_es_solo_ValorFlete`.

El paso manual pendiente marcado por la PR ("QA: descargar el Excel de una
cotización real con aprovisionamiento/retorno y confirmar visualmente") se
da por cubierto con la verificación automatizada de arriba (genera y lee un
`.xlsx` real, no un mock) — crear una cotización real solo para ese chequeo
visual habría añadido datos reales innecesarios a la base de pruebas.

## Cómo correrlo

```powershell
./verify-suite-completa.ps1
```

Requiere VPN corporativa activa y `.env.pruebas-reales.local` en la raíz.

## Resultado real verificado (2026-09-29)

- Build: 0 errores.
- `Nexo.Domain.Tests`: 143/143.
- `Nexo.Application.Tests`: 229/229.
- `Nexo.Api.Tests`: 205/207 (2 skips preexistentes de `clsMailSenderRealTests`).
- Los 5 endpoints afectados por el fix de DI: 200 (verificado con `curl`
  real, ver tabla arriba).
- Los 3 tests nuevos del Excel: Passed.

## Hallazgo (para tener en cuenta, no bloqueante)

El fix de DI no queda protegido por ningún test automatizado a nivel HTTP
(`Nexo.Api.Tests`) — solo por la verificación manual de esta PR y la de QA
documentada acá. Si alguien vuelve a olvidar un `Add*UseCases()` en el
futuro, la suite completa seguiría en verde. Vale la pena, en algún momento,
un test que arme el `WebApplicationFactory` real y golpee cada controller
al menos una vez (smoke test de rutas), para que este tipo de bug no
dependa de que alguien lo pruebe a mano contra el ambiente desplegado.
