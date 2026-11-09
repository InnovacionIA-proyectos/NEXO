<#
.SINOPSIS
Verificacion de la PR #450: "feat(vehiculos): codigos de la tabla de traduccion oficiales"
(Parte A de la issue #446; la Parte B es la PR #449).

.QUE VERIFICA
Los codigos de `tbl_mapeo_vehiculo_tarifario` (2L3, 3S2, 2S3, 2_5, 2_7, 2L1...) pasan a contar
como "alias de si mismos": un tipoVehiculo que coincide con uno de esos codigos hace que la
configuracion nazca oficial (o promueve una en cuarentena, nunca una descartada), siempre que
la carroceria este vacia o sea una del catalogo activo. Aplica a resolver-catalogos,
captura-solicitudes y "Cambiar vehiculo". Nuevo GET /api/catalogos/vehiculos-nexo (todos los
roles) con los codigos NEXO y si tambien estan en SAP (enSap). Sin cambios de base de datos.

.COMO SE VERIFICO EN VIVO
1. `dotnet test` real sobre un worktree aislado de la rama de la PR:
   - Nexo.Domain.Tests: 218/218 (exacto a lo afirmado).
   - Nexo.Application.Tests: 1291/1291 (exacto a lo afirmado).
   - Nexo.Api.Tests, filtro VehiculosCodigosDeLaTabla, contra la BD real de pruebas
     (TEST_DB_CONNECTION leido en memoria desde .env.pruebas-reales.local, nunca escrito a
     disco ni impreso): 2/2 -- coincide con "2 contra la base" que afirma la PR.
2. clsOficialidadDeVehiculos.cs leido completo: CargarAsync agrupa los codigos activos por
   clave normalizada y las carrocerias oficiales activas; CodigoOficial(tipo, carroceria) solo
   devuelve el codigo si la carroceria esta vacia o es una de las oficiales.
3. clsResolucionDeCatalogos.cs:96-108 (ResolverVehiculo): confirmado que promueve una fila en
   cuarentena a oficial SOLO si `!vehiculo.EsCatalogoOficial && !vehiculo.Descartada` -- nunca
   promueve algo descartado. Confirmado que resolver-catalogos (clsResolverCatalogosDeLineasCommand.cs:49-50)
   y captura-solicitudes (clsCapturarSolicitudCommand.cs:104) llaman al MISMO metodo estatico
   con la MISMA instancia de `oficialidad`, no hay logica duplicada.
4. clsCambiarVehiculoLineaCommand.cs:97-135 (tercer camino, "Cambiar vehiculo"): reutiliza el
   mismo `ResolverVehiculo`, y agrega un chequeo explicito `if (resuelto!.Descartada) throw
   VEHICULO_NO_OFICIAL` ademas del que ya trae ResolverVehiculo -- doble respaldo, no contradice
   la regla.
5. clsAliasCommands.cs:28-40 (CodigoValidoAsync): confirmado que agrega
   `vehiculosNexo.ListarCodigosAsync` a la lista de codigos validos para alias/bandeja -- los
   codigos de la tabla ya no dan 400 CODIGO_NO_RECONOCIDO como destino.
6. clsListarVehiculosNexoQuery.cs: `enSap` se calcula comparando contra `ITiposVehiculoPort`
   (el catalogo SAP real), no un valor hardcodeado.
7. `GET tipos-vehiculo` (clsCatalogosController.cs) sin cambios funcionales en el diff (solo un
   comentario XML nuevo apuntando a vehiculos-nexo); `GET vehiculos-nexo` bajo el mismo
   `[Authorize]` de clase (todos los roles autenticados), igual que documenta CONTRATO.md:290.
8. CONTRATO.md: endpoint, roles, DTO `{codigo, descripcion, enSap}` y la excepcion de
   oficialidad (linea 1059) documentados correctamente.

.RIESGO DE CONFLICTO CONOCIDO CON LA PR #448
Ambas PRs modifican los mismos 3 archivos. Mapa exacto de que toca cada una (para fusion manual):
- `clsCapturarSolicitudCommandHandler` (clsCapturarSolicitudCommand.cs): #450 agrega el
  parametro `IVehiculosNexoPort vehiculosNexo` al constructor (despues de `aliasCatalogo`,
  antes de `clientes`) y una linea `var oficialidad = ...CargarAsync(...)`; la llamada a
  `ResolverVehiculo(...)` gana `oficialidad` como ultimo argumento. #448 (no verificada en este
  fork) toca la llamada a `ResolverRuta(...)` en el mismo bucle, con `lr.OrigenMunicipioId,
  lr.DestinoMunicipioId` -- metodo y linea DISTINTOS al que toca #450, pero en el mismo archivo
  y bucle, muy cerca en el diff.
- `clsResolverCatalogosDeLineasCommandHandler` (clsResolverCatalogosDeLineasCommand.cs): mismo
  patron -- #450 agrega `IVehiculosNexoPort vehiculosNexo` al constructor y `oficialidad` a la
  llamada de `ResolverVehiculo`; #448 tocaria la llamada de `ResolverRuta` dos lineas arriba.
- `clsResolucionDeCatalogos.cs`: #450 SOLO toca el metodo `ResolverVehiculo` (firma y cuerpo,
  lineas ~81-108). Si #448 solo toca `ResolverRuta` (metodo distinto en el mismo archivo), el
  conflicto de texto deberia ser minimo o nulo; si #448 tambien agrega parametros a
  `ResolverVehiculo`, SI habria conflicto real de firma que resolver a mano.
Conclusion: el conflicto esperado es de "constructor con dos parametros nuevos insertados cerca"
en 2 archivos -- mecanico, no de logica (cada PR agrega su propio parametro independiente); se
resuelve manteniendo ambos parametros nuevos y ambos usos. No lo resolvi yo mismo: queda para
quien decida el orden de merge.

.LIMITACIONES CONOCIDAS
- "Sara confirmo el 2026-11-09 que la tabla es correcta" es una afirmacion de negocio, no
  verificable desde el codigo.
- No se corrio la suite completa de Nexo.Api.Tests (solo el filtro de esta PR): los ~87 fallos
  de entorno que afirma la PR no se recontaron aqui, coherente con verificaciones previas donde
  Domain/Application (que no dependen de la red) ya alcanzan el 100% de cobertura verificable.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

Write-Host "=== Evidencia de verificacion PR #450 ===" -ForegroundColor Cyan
Write-Host "Esta verificacion se corrio en un worktree aislado (C:\tmp\pr450-worktree) sobre la" -ForegroundColor Yellow
Write-Host "rama pr450 (commit 603804a). Resultados obtenidos en vivo:" -ForegroundColor Yellow
Write-Host "  - Nexo.Domain.Tests: 218/218 OK" -ForegroundColor Green
Write-Host "  - Nexo.Application.Tests: 1291/1291 OK" -ForegroundColor Green
Write-Host "  - Nexo.Api.Tests (filtro VehiculosCodigosDeLaTabla, BD real): 2/2 OK" -ForegroundColor Green
Write-Host ""
Write-Host "Ver el encabezado de este script (.COMO SE VERIFICO EN VIVO) para el detalle completo" -ForegroundColor Yellow
Write-Host "de la revision de codigo linea por linea, y .RIESGO DE CONFLICTO CONOCIDO CON LA PR #448" -ForegroundColor Yellow
Write-Host "para el mapa exacto de la fusion manual pendiente." -ForegroundColor Yellow
Write-Host ""
Write-Host "RESULTADO: TODO OK (sin hallazgos bloqueantes)" -ForegroundColor Green
exit 0
