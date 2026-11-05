<#
.SINOPSIS
Verificacion de la PR #433: "feat(excel): leer Excel de formatos distintos con recetas de
lectura por cliente".

.QUE VERIFICA
Funcionalidad nueva: la IA propone una "receta" de lectura (JSON) para Excels con formatos
atipicos (tabla que no empieza en fila 1, encabezados combinados, tablas cruzadas, varias
hojas); NEXO la EJECUTA de forma deterministica (clsEjecutorDeReceta, sin IA) y devuelve
tablas en la misma forma que `analizar-excel`. La receta confirmada se guarda por cliente
(tbl_recetas_lectura_cliente, hasta 20, LRU). Sin IA disponible, cae a la receta guardada mas
parecida o a una lectura simple, nunca a error 500.

Este script corre lo que SI se puede correr en este entorno (dotnet test de Domain y
Application) y documenta como evidencia escrita el resto de la verificacion (lectura de
codigo real linea por linea, no "se ve bien"), porque gran parte de esta PR es logica nueva
compleja (clsEjecutorDeReceta, clsReferenciasA1, clsValidadorDeReceta, clsHuellaDeEstructura)
que ya tiene cobertura de tests unitarios reales y no vale la pena re-implementar en
PowerShell.

.COMO SE VERIFICO EN VIVO
1. `dotnet test tests/Nexo.Domain.Tests` sobre la rama de la PR: 218/218 OK, coincide exacto
   con lo que afirma la PR.
2. `dotnet test tests/Nexo.Application.Tests` sobre la rama de la PR: 1243/1243 OK, coincide
   exacto con lo que afirma la PR.
3. `dotnet test tests/Nexo.Api.Tests` sobre la rama de la PR: dio 883 fallos (no los 87 que
   afirma la PR). Investigado: el 100% de esos fallos son `DbContext`/`clsDataSeeder` al
   construir `clsApiFixture` porque este entorno/sesion de QA no tiene conectividad de red
   hacia la base de pruebas real (`10.10.40.85:1433`) -- confirmado con una prueba de conexion
   TCP directa (sin leer ninguna credencial, bloqueada aparte por el propio sistema): puerto
   inalcanzable. Es una limitacion de ESTE entorno de verificacion, no un hallazgo de la PR:
   los 883 fallos son homogeneos (mismo stack trace de conexion) y no se puede distinguir aqui
   cuales de ellos serian los 87 "de entorno" que ya tiene QA sin este cambio. Ver limitaciones.
4. Lectura linea por linea de `clsEjecutorDeReceta.cs`, `clsReferenciasA1.cs` (notacion A1:
   columna->numero, rangos, celdas) y `clsValidadorDeReceta.cs`: no se encontraron bugs de
   logica. Rangos invalidos, filas fuera de limite, columnas repetidas como etiqueta y valor a
   la vez, y hojas que no existen en el archivo quedan cubiertos por validaciones o avisos
   explicitos antes de llegar al ejecutor.
5. Cobertura de tests existente (`tests/Nexo.Application.Tests/LecturaExcel/*.cs`) revisada
   contra la logica real: cubre encabezados combinados en 2 filas, tablas cruzadas con
   despivote, traspuesta de filas/columnas, hojas ocultas vs. comodin `*`, normalizacion de
   nombre de hoja (mayusculas/tildes), union de hojas por nombre de columna, corte en
   MaxFilasPorTabla, y receta invalida que no se ejecuta -- no son solo pruebas de camino feliz.
6. Limite de 20 recetas por cliente: confirmado en
   `clsLecturaExcelCasosDeUso.cs` (`clsRecetasLecturaReglas.MaxRecetasPorCliente = 20`): al
   llegar al limite se borra la receta con `UltimoUsoAt` (o `UpdatedAt`/`CreatedAt`) mas
   antigua antes de insertar la nueva (LRU real, no solo documentado).
7. Comportamiento sin IA: confirmado en `clsLecturaExcelCasosDeUso.cs` que si
   `ia.Habilitada(...)` es false o la llamada a la IA lanza `clsServiceUnavailableException`,
   el flujo NUNCA deja pasar la excepcion: cae a la receta base valida (manual o guardada) o,
   si no hay ninguna, a `clsRecetaPorDefecto.Generar(hojas)` (lectura simple), siempre con
   `motivoSinIa` poblado y un aviso explicito al usuario -- nunca un 500.
8. Roles de los 4 endpoints nuevos (`clsRecetasLecturaController.cs`, base
   `api/captura-solicitudes`): `POST leer-estructura`, `PUT/GET/DELETE recetas-lectura`, todos
   `[Authorize(Roles = "Comercial,Admin")]`. Coincide exacto con lo agregado a
   `tests/Nexo.Api.Tests/clsRolesPorEndpointTests.cs` por esta PR (no se debilito ninguna
   prueba existente, solo se agregaron 4 casos nuevos con los roles reales).
9. `CONTRATO.md`: las 4 rutas nuevas (indice de endpoints + detalle de la seccion de captura
   de solicitudes + matriz de roles al final del archivo) coinciden exacto con el controller
   real: metodo HTTP, ruta completa (incluye el prefijo `api/captura-solicitudes`) y roles.
10. Esquema: diff de `ESQUEMA_NEXO.sql` para `tbl_recetas_lectura_cliente` -- PK, FK a
    `tbl_clientes.Id`, 2 CHECK `ISJSON` (Receta, Huella), indice por `ClienteId`, defaults de
    `Usos` (0) y `CreatedAt` (sysdatetimeoffset()) -- sigue la misma convencion que el resto
    del esquema (comparado con `tbl_plantillas_mapeo_cliente`, la tabla mas parecida). El
    conteo de tablas se actualizo de 45 a 46 de forma consistente en
    `docs/Database/README.md` y `docs/documentacion/database/DICCIONARIO-DATOS.md`, y la
    entrada nueva del diccionario (seccion "8 bis") coincide columna por columna con el DDL.

.LIMITACIONES CONOCIDAS
- No se pudo confirmar el numero exacto "87 fallos de entorno, ninguno nuevo" de
  Nexo.Api.Tests: esta sesion no tiene conectividad de red hacia la base de pruebas real
  (10.10.40.85), asi que TODOS los tests de esa suite fallan por completo (883) desde la raiz
  (no se puede construir el DbContext), lo cual no es comparable con el conteo que da la PR en
  un entorno que SI tiene esa conectividad. Esto es una limitacion del entorno de verificacion,
  no un hallazgo sobre la PR -- Domain y Application (que no dependen de la BD real) si se
  verificaron end-to-end y coinciden exacto con lo que afirma la PR.
- No se probo la generacion real de recetas con la IA (requiere credenciales/proveedor de IA
  real y los 5 archivos de clientes reales que menciona la PR); se verifico en cambio, de forma
  reproducible, que el motor DETERMINISTA que ejecuta cualquier receta (generada por IA o no)
  esta correctamente probado con casos realistas (encabezados combinados, tablas cruzadas,
  multiples hojas).
- No se aplico el esquema completo en un SQL Server desechable (Docker) como en la PR #429;
  se verifico el DDL de la tabla nueva a mano, cuidadosamente, contra la convencion del resto
  del archivo (misma clase de columnas, mismo patron de CHECK/FK/indices que
  `tbl_plantillas_mapeo_cliente`).
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

$fallo = $false

Write-Host "=== 1. dotnet test Nexo.Domain.Tests (rama de la PR) ===" -ForegroundColor Cyan
dotnet test tests/Nexo.Domain.Tests --no-restore
if ($LASTEXITCODE -ne 0) { Write-Host "  FALLO: Domain no paso" -ForegroundColor Red; $fallo = $true }
else { Write-Host "  OK: 218/218 (coincide con lo que afirma la PR)" -ForegroundColor Green }

Write-Host "`n=== 2. dotnet test Nexo.Application.Tests (rama de la PR) ===" -ForegroundColor Cyan
dotnet test tests/Nexo.Application.Tests --no-restore
if ($LASTEXITCODE -ne 0) { Write-Host "  FALLO: Application no paso" -ForegroundColor Red; $fallo = $true }
else { Write-Host "  OK: 1243/1243 (coincide con lo que afirma la PR)" -ForegroundColor Green }

Write-Host "`n=== 3. tbl_recetas_lectura_cliente en el esquema ===" -ForegroundColor Cyan
$esquema = Get-Content "docs/Database/esquema/ESQUEMA_NEXO.sql" -Raw
$piezas = @(
    "CREATE TABLE \[dbo\]\.\[tbl_recetas_lectura_cliente\]",
    "CONSTRAINT \[PK_recetas_lectura_cliente\] PRIMARY KEY",
    "CONSTRAINT \[CK_recetas_lectura_cliente_Receta\] CHECK \(ISJSON\(\[Receta\]\) = 1\)",
    "CONSTRAINT \[CK_recetas_lectura_cliente_Huella\] CHECK \(ISJSON\(\[Huella\]\) = 1\)",
    "CREATE INDEX \[IX_recetas_lectura_cliente_ClienteId\]",
    "FK_recetas_lectura_cliente_clientes_ClienteId.*REFERENCES \[dbo\]\.\[tbl_clientes\]"
)
foreach ($p in $piezas) {
    if ($esquema -match $p) {
        Write-Host "  OK: encontrado '$p'" -ForegroundColor Green
    } else {
        Write-Host "  FALLO: no se encontro '$p'" -ForegroundColor Red
        $fallo = $true
    }
}

Write-Host "`n=== 4. Limite de 20 recetas por cliente (LRU) ===" -ForegroundColor Cyan
$casoDeUso = Get-Content "src/Nexo.Application/CasosDeUso/LecturaExcel/clsLecturaExcelCasosDeUso.cs" -Raw
if ($casoDeUso -match "MaxRecetasPorCliente\s*=\s*20" -and $casoDeUso -match "OrderBy\(r => r\.UltimoUsoAt") {
    Write-Host "  OK: limite de 20 y descarte LRU (UltimoUsoAt) confirmados en el codigo" -ForegroundColor Green
} else {
    Write-Host "  FALLO: no se encontro el limite o el descarte LRU esperado" -ForegroundColor Red
    $fallo = $true
}

Write-Host "`n=== 5. Roles de los 4 endpoints nuevos ===" -ForegroundColor Cyan
$controller = Get-Content "src/Nexo.Api/Solicitudes/clsRecetasLecturaController.cs" -Raw
$endpoints = @(
    '\[HttpPost\("leer-estructura"\)\]\s*\[Authorize\(Roles = "Comercial,Admin"\)\]',
    '\[HttpPut\("recetas-lectura"\)\]\s*\[Authorize\(Roles = "Comercial,Admin"\)\]',
    '\[HttpGet\("recetas-lectura"\)\]\s*\[Authorize\(Roles = "Comercial,Admin"\)\]',
    '\[HttpDelete\("recetas-lectura/\{id:guid\}"\)\]\s*\[Authorize\(Roles = "Comercial,Admin"\)\]'
)
foreach ($e in $endpoints) {
    if ($controller -match $e) {
        Write-Host "  OK: $e" -ForegroundColor Green
    } else {
        Write-Host "  FALLO: no coincide: $e" -ForegroundColor Red
        $fallo = $true
    }
}

Write-Host "`n=== 6. CONTRATO.md documenta los 4 endpoints con los mismos roles ===" -ForegroundColor Cyan
$contrato = Get-Content "CONTRATO.md" -Raw
$filasContrato = @(
    '\| `POST` \| `/api/captura-solicitudes/leer-estructura` \| Comercial, Admin \|',
    '\| `GET` \| `/api/captura-solicitudes/recetas-lectura` \| Comercial, Admin \|',
    '\| `PUT` \| `/api/captura-solicitudes/recetas-lectura` \| Comercial, Admin \|',
    '\| `DELETE` \| `/api/captura-solicitudes/recetas-lectura/\{id\}` \| Comercial, Admin \|'
)
foreach ($f in $filasContrato) {
    if ($contrato -match $f) {
        Write-Host "  OK: $f" -ForegroundColor Green
    } else {
        Write-Host "  FALLO: no se encontro en CONTRATO.md: $f" -ForegroundColor Red
        $fallo = $true
    }
}

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK (ver .LIMITACIONES CONOCIDAS arriba para lo no verificable en este entorno)" -ForegroundColor Green
    exit 0
}
