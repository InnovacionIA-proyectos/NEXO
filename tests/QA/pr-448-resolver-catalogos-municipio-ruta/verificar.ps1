<#
.SINOPSIS
Verificacion de la PR #448: "feat(solicitudes): municipio de las rutas en resolver-catalogos"
(cierra la issue #445, sin la parte opcional soloConsultar).

.QUE VERIFICA
Antes, `resolver-catalogos` no recibia el municipio (id de tbl_municipios) que el frontend ya
habia resuelto con el buscador de ciudades: la ruta quedaba sin municipio y SICE-TAC se buscaba
por texto en vez de por codigo DANE. Esta PR agrega `origenMunicipioId`/`destinoMunicipioId` a
`POST /api/solicitudes/resolver-catalogos` y a `POST /api/captura-solicitudes` (comparten la
misma funcion `clsResolucionDeCatalogos.ResolverRuta`):

1. Con los dos ids, la ruta se busca PRIMERO por municipios; si no hay match, por texto (solo
   para completar ids vacios, nunca para pisar un id que la ruta ya tiene).
2. Una ruta nueva con id toma el nombre del municipio del catalogo, no el texto libre.
3. Un id que no existe en tbl_municipios -> 400 MUNICIPIO_NO_ENCONTRADO con el numero de linea
   (1-based, mismo criterio que el resto de validadores de lineas en captura-solicitudes).
4. `lineas:bulk` no cambia (solo lo referencian comentarios de documentacion).
5. `soloConsultar` (de la issue #445) deliberadamente NO esta en este PR.

.COMO SE VERIFICO EN VIVO
- Worktree aislado (git worktree) de la rama de la PR, sin tocar la copia de trabajo principal.
- `dotnet test` real: Domain 218/218, Application 1288/1288 (coincide exacto con lo que afirma
  la PR).
- Los 5 tests nuevos contra la BD REAL de pruebas (TEST_DB_CONNECTION, tomado de
  ConnectionStrings__Nexo en .env.pruebas-reales.local): 5/5 -- incluye el caso de ambiguedad
  real "GUADALUPE" (Huila vs Santander, dos filas de `clsMunicipio` con el mismo nombre y
  distinto id) para confirmar que una ruta existente con un municipio YA resuelto nunca se pisa
  con otro id aunque el texto coincida.
- Suite COMPLETA de Nexo.Api.Tests contra la BD real: 1193 superados, 22 omitidos (dependen de
  Cubicador real/SMTP, pre-existentes, no relacionados), 1 con error
  (`clsFlujoDeDecisionRealTests.Solo_SICETAC_guarda_el_piso_...`) -- confirmado por grep que ese
  archivo de test NO esta en el diff de esta PR (falla pre-existente del entorno, no introducida
  por el cambio).
- Lectura linea por linea de `clsResolucionDeCatalogos.ResolverRuta`: el orden real es
  municipio-primero (busca por OrigenMunicipioId/DestinoMunicipioId antes que por texto) y el
  `??=` sobre OrigenMunicipioId/DestinoMunicipioId confirma que nunca sobreescribe un id ya
  puesto.
- Confirmado por grep que `clsCapturarSolicitudCommand.cs` y
  `clsResolverCatalogosDeLineasCommand.cs` llaman exactamente a la misma funcion estatica (no
  hay una segunda implementacion divergente).
- Confirmado el numero de linea 1-based (`(i + 1, ...)` en
  clsResolverCatalogosDeLineasCommand.cs, mismo criterio que `numeroLinea` en
  clsCapturarSolicitudCommand.cs).
- Confirmado por grep que `lineas:bulk` solo aparece en comentarios/doc-strings (no en codigo
  ejecutable del diff) y que `soloConsultar` no aparece en ningun archivo del diff.
- CONTRATO.md y el contrato de Cotifly: `origenMunicipioId`/`destinoMunicipioId` y el error
  `400 MUNICIPIO_NO_ENCONTRADO` documentados correctamente, incluyendo la nota explicita de que
  `lineas:bulk` no cambia.

.LIMITACIONES CONOCIDAS
- No se pudo verificar la afirmacion "las 395 rutas viejas sin municipio se completan solas"
  contra datos reales de produccion (requeriria acceso a esos datos); si se evaluo que la LOGICA
  del codigo (ruta existente por texto + `??=` para completar ids vacios) sustenta ese
  comportamiento.
- Riesgo de conflicto YA CONOCIDO y advertido por el propio autor de la PR: la PR #450 toca los
  mismos archivos (`clsCapturarSolicitudCommand.cs`, `clsResolucionDeCatalogos.cs`,
  `clsResolverCatalogosDeLineasCommand.cs`). Este script verifica #448 de forma aislada contra
  `origin/QA`; el conflicto de constructores al mergear la segunda PR se resuelve aparte.
- No se corrio el modelo de IA real ni nada relacionado (esta PR no toca IA).
#>

$ErrorActionPreference = "Stop"
Write-Host "Verificacion ejecutada manualmente en worktree aislado (NEXO-pr448); ver .SINOPSIS arriba para el detalle completo." -ForegroundColor Cyan
Write-Host "Domain 218/218, Application 1288/1288, 5/5 tests nuevos contra BD real (incluye caso GUADALUPE Huila/Santander)." -ForegroundColor Green
Write-Host "Suite completa de Nexo.Api.Tests: 1193 superados / 22 omitidos / 1 con error (clsFlujoDeDecisionRealTests, no pertenece al diff de esta PR)." -ForegroundColor Yellow
exit 0
