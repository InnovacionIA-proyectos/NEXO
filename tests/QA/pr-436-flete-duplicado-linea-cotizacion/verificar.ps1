<#
.SINOPSIS
Verificacion de la PR #436: "fix(cotizaciones): un solo flete por linea en cada cotizacion".

.QUE VERIFICA
Antes, `POST /api/cotizaciones/{id}/lineas` siempre agregaba una fila nueva: repetir el flete
de una linea (doble clic, reintento de red) duplicaba la linea e inflaba el total. La PR agrega:
- Chequeo en memoria: si la linea de solicitud ya tiene flete en esa cotizacion -> `409 LINEA_YA_COSTEADA`.
- Respaldo a nivel BD: indice unico filtrado `IX_cotizacion_lineas_Cotizacion_LineaSolicitud`
  sobre (CotizacionId, LineaSolicitudId) WHERE LineaSolicitudId IS NOT NULL. Con 2 peticiones
  simultaneas, el chequeo en memoria no basta (misma lectura, ambas pasan) -> el que pierde la
  carrera choca contra el indice en el SaveChanges, y el codigo debe traducir esa excepcion a
  409 (no un 500).
- `aplicar-propuestas`: ante el mismo choque, relee la cotizacion y responde sin agregar nada
  (idempotente); si el choque se repite, 409.
- Los fletes SIN `LineaSolicitudId` (lineas "sueltas") no tienen este limite: el indice es
  FILTRADO justamente para dejarlos pasar.

.COMO SE VERIFICO EN VIVO
1. `dotnet test` real sobre Domain (218/218) y Application (1250/1250) en la rama de la PR:
   coincide exacto con lo que afirma la descripcion.
2. Lectura linea por linea de `clsAgregarLineaCotizacionCommand.cs`: el chequeo
   `c.Lineas.Any(l => l.LineaSolicitudId == lid)` ocurre ANTES del insert (ventana de carrera
   real, mitigada solo por el indice de BD) y el catch `cotizaciones.EsChoqueDeLineaDuplicada(ex)`
   filtra por `SqlException.Number in (2601, 2627)` Y por el nombre del indice
   (constante `IndiceLineaUnica`) -- no es un catch generico de cualquier violacion de
   constraint, especificamente el de esta carrera.
3. Confirmado que la definicion Fluent API del indice en `clsNexoDbContext.cs`
   (`HasFilter("[LineaSolicitudId] IS NOT NULL")`, mismo nombre) coincide EXACTAMENTE con el
   `CREATE UNIQUE INDEX ... WHERE (...)` de `ESQUEMA_NEXO.sql` -- EF y el script manual no
   divergen.
4. `clsAplicarPropuestasCommand.cs`: confirmado que ante el choque relee (recursion con
   `reintento: true`) y en el segundo choque lanza 409 `LINEA_YA_COSTEADA` en vez de reintentar
   indefinidamente.
5. Contra la BD real de pruebas (`TEST_DB_CONNECTION` = misma cadena de
   `.env.pruebas-reales.local`, `ConnectionStrings__Nexo`): se corrieron los 27 tests de
   `clsCotizacionesTests` -- 26 pasaron, pero
   `La_base_rechaza_una_segunda_fila_para_la_misma_linea_en_la_misma_cotizacion` fallo
   (no lanzo `DbUpdateException`). Diagnostico de causa raiz: se consulto `sys.indexes` en esa
   BD real y el indice `IX_cotizacion_lineas_Cotizacion_LineaSolicitud` **NO existe todavia ahi**
   -- es un hallazgo real de infraestructura (la BD compartida de pruebas no tiene aplicado el
   DDL de esta PR, no un bug del codigo), ver seccion de HALLAZGOS abajo.
6. Se valido el DDL en un SQL Server 2019 desechable (Docker), igual criterio que PRs #429/#433:
   - El esquema completo (46 tablas + este indice) se crea limpio desde cero.
   - Escenario "repetido": dos filas con mismo (CotizacionId, LineaSolicitudId) -> la segunda
     es rechazada por el indice (confirmado con el mensaje real de SQL Server).
   - Escenario "sin linea": dos filas con LineaSolicitudId NULL para la misma cotizacion ->
     ambas se insertan sin problema (el indice es filtrado, NULL no cuenta).
   Esto SI confirma la afirmacion de la PR de haber validado el script en una base desechable.
7. `CONTRATO.md`: `409 LINEA_YA_COSTEADA` documentado en `POST .../lineas` y referenciado en
   `PUT .../lineas/{lineaSolicitudId}`; coincide con los codigos reales del handler.

.HALLAZGOS (no bloqueantes para el codigo, si accionables para el despliegue)
- La base de datos REAL de pruebas compartida (dbInformesAutomatizados_Pruebas) todavia NO
  tiene el indice unico filtrado de esta PR aplicado. Mientras no se corra el DDL nuevo de
  `ESQUEMA_NEXO.sql` contra esa base (y, llegado el momento, contra la de produccion), el
  respaldo a nivel BD no existe ahi: el chequeo en memoria sigue protegiendo el caso normal,
  pero la ventana de carrera real (2 peticiones simultaneas) NO esta cerrada hasta aplicar
  el indice. Es un paso de despliegue pendiente, no un defecto de esta PR.

.LIMITACIONES CONOCIDAS
- No se corrio el resto de la suite de Nexo.Api.Tests (87 fallos de entorno preexistentes en
  QA) para no invertir tiempo en algo ya conocido; solo se corrio `clsCotizacionesTests`
  (la clase que toca esta PR).
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

function Load-EnvVar($name, $archivo = ".env.pruebas-reales.local") {
    $line = Get-Content $archivo | Where-Object { $_ -match "^$name=" } | Select-Object -First 1
    if (-not $line) { throw "No se encontro $name en $archivo" }
    return $line.Substring($name.Length + 1)
}

$fallo = $false

Write-Host "=== 1. Indice unico filtrado: EF Core vs ESQUEMA_NEXO.sql ===" -ForegroundColor Cyan
$efIdx = Select-String -Path "src/Nexo.Infrastructure/Persistence/clsNexoDbContext.cs" -Pattern "IX_cotizacion_lineas_Cotizacion_LineaSolicitud"
$sqlIdx = Select-String -Path "docs/Database/esquema/ESQUEMA_NEXO.sql" -Pattern "IX_cotizacion_lineas_Cotizacion_LineaSolicitud"
if ($efIdx -and $sqlIdx) {
    Write-Host "  OK: el indice aparece en ambos lugares (EF Core y el DDL manual)" -ForegroundColor Green
} else {
    Write-Host "  FALLO: el indice no aparece en EF Core y/o en el esquema SQL" -ForegroundColor Red
    $fallo = $true
}

Write-Host "`n=== 2. Contra la BD real de pruebas: existe el indice? ===" -ForegroundColor Cyan
$cs = Load-EnvVar "ConnectionStrings__Nexo"
Add-Type -AssemblyName "System.Data"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_cotizacion_lineas_Cotizacion_LineaSolicitud'"
    $existe = [int]$cmd.ExecuteScalar()
    if ($existe -gt 0) {
        Write-Host "  OK: el indice ya esta aplicado en la BD real de pruebas" -ForegroundColor Green
    } else {
        Write-Host "  HALLAZGO (no bloqueante): el indice todavia NO existe en la BD real de pruebas -- falta aplicar el DDL nuevo antes de que el respaldo a nivel BD funcione ahi (ver .HALLAZGOS arriba)." -ForegroundColor Yellow
    }
}
finally {
    if ($conn.State -eq 'Open') { $conn.Close() }
}

Write-Host "`n=== 3. CONTRATO.md documenta LINEA_YA_COSTEADA donde corresponde ===" -ForegroundColor Cyan
$contrato = Get-Content "CONTRATO.md" -Raw
if ($contrato -match "LINEA_YA_COSTEADA") {
    Write-Host "  OK: LINEA_YA_COSTEADA aparece documentado en CONTRATO.md" -ForegroundColor Green
} else {
    Write-Host "  FALLO: LINEA_YA_COSTEADA no aparece en CONTRATO.md" -ForegroundColor Red
    $fallo = $true
}

Write-Host ""
Write-Host "NOTA: la validacion de dotnet test (Domain 218/218, Application 1250/1250), la" -ForegroundColor DarkGray
Write-Host "revision linea por linea del codigo, los 4 tests nuevos contra la BD real (26/27," -ForegroundColor DarkGray
Write-Host "1 hallazgo de infraestructura documentado arriba) y la validacion Docker de los 3" -ForegroundColor DarkGray
Write-Host "escenarios del indice se hicieron en vivo durante esta sesion -- ver .COMO SE" -ForegroundColor DarkGray
Write-Host "VERIFICO EN VIVO arriba para el detalle completo." -ForegroundColor DarkGray

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: OK (con 1 hallazgo de infraestructura no bloqueante, ver arriba)" -ForegroundColor Green
    exit 0
}
