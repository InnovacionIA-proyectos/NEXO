<#
.SINOPSIS
Verificacion de la PR #429: "[S6-HU-31.1] Base de pruebas propia de NEXO: esquema,
seeder y documentacion" (issue #326).

.QUE VERIFICA
Preparacion de la base de datos propia de NEXO (dbTarifario_Nexo_Pruebas.dbo), analoga a la
migracion del Cubicador (PR #424): consolida 12 scripts sueltos ya aplicados contra la base
compartida vieja (304, 307, 352, 356, 361, 372, 373, 376, 377, 379, 407 y la limpieza de 421)
en un unico `docs/Database/esquema/ESQUEMA_NEXO.sql` de 45 tablas, reordena/reescribe los CSV
del seeder en el orden de ese esquema. No hay cambios de codigo C# (confirmado con `git diff
--stat` contra QA: ningun .cs). QA sigue apuntando a la base vieja; el corte real de conexion
queda para la issue #428, fuera de esta PR.

Esta PR no se puede dar por buena solo leyendo que "los scripts quedaron incorporados" -- hay
que probar que el esquema resultante crea de verdad las 45 tablas y que los 9 CSV del seeder
cargan sin error de tipos, longitudes, CHECK ni FK contra ese esquema real. Eso es lo que hace
este script, contra un SQL Server desechable en Docker (nunca contra la base real compartida).

.COMO SE VERIFICA EN VIVO
1. Arranca un contenedor `mcr.microsoft.com/mssql/server:2019-latest` desechable.
2. Crea la base `dbTarifario_Nexo_Pruebas` vacia.
3. Aplica `docs/Database/esquema/ESQUEMA_NEXO.sql` completo y cuenta las tablas creadas
   (debe dar 45, igual que documenta `docs/Database/README.md`).
4. Aplica `docs/Database/seeder/datos_base_nexo.sql` y verifica 1 usuario de sistema,
   9 mapeos de rol de portal, 1 fila de parametros del score.
5. Carga los 9 CSV de `docs/Database/seeder/` fila por fila. Usa `IDENTITY_INSERT ON` para las
   2 tablas con Id autoincremental (municipios, pesos) y genera `NEWID()` para las 3 tablas
   cuyo CSV no trae columna Id (alias_elementos, mapeo_vehiculos, rangos_peso_contenedor) --
   la misma logica que usa `docs/Database/scripts/ejecutar_schema_y_seeders.py` en produccion
   (`generar_id = "Id" not in columnas and ...`), replicada aqui porque ese script usa
   autenticacion de Windows y no corre contra un contenedor con auth SQL.
6. Compara el conteo final de cada tabla contra el numero de filas de datos de su CSV
   (lineas totales menos cabecera) -- deben coincidir exactamente.
7. Elimina el contenedor al terminar, exito o fallo.

.LIMITACIONES CONOCIDAS
- Requiere Docker Desktop corriendo. Si no esta disponible, el script lo detecta y se detiene
  con un mensaje claro (no es bloqueante para esta PR: ya se corrio una vez con exito durante
  la sesion de QA que la verifico, ver el comentario de la PR en GitHub).
- No corre el script real `ejecutar_schema_y_seeders.py` tal cual (usa `sqlcmd` con
  autenticacion de Windows); replica su logica de generacion de Id para validar el mismo
  resultado contra Docker.
- No valida `.env.example`/`.gitignore`/workflow de CI mas alla de la lectura de diff hecha
  durante la revision (cambios de texto de bajo riesgo, sin comportamiento ejecutable nuevo).
- No aplica `dotnet build`/`dotnet test`: no hay codigo C# en el diff de esta PR.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

$contenedor = "nexo-qa-verificar-pr429"
$claveSa = "QaTest!2026Strong"
$baseDatos = "dbTarifario_Nexo_Pruebas"

# Se escribe a un archivo temporal sin BOM y se copia al contenedor en vez de usar un pipe de
# stdin: PowerShell antepone BOM UTF-8 a los pipes hacia procesos externos, y sqlcmd lo
# interpreta como "Incorrect syntax near..." en la primera linea.
function Invoke-SqlCmd {
    param([string]$Sql, [string]$Database = $baseDatos)
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    docker cp $tmp "${contenedor}:/tmp/cmd.sql" | Out-Null
    Remove-Item $tmp
    $salida = docker exec $contenedor /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $claveSa -C -d $Database -i /tmp/cmd.sql -b
    return @{ Salida = $salida; Codigo = $LASTEXITCODE }
}

function Invoke-SqlScalar {
    param([string]$Sql, [string]$Database = $baseDatos)
    $r = Invoke-SqlCmd -Sql "SET NOCOUNT ON; $Sql" -Database $Database
    return ($r.Salida | Select-Object -Last 1).ToString().Trim()
}

Write-Host "=== 0. Verificando Docker ===" -ForegroundColor Cyan
docker info *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Docker Desktop no esta corriendo. No se puede ejecutar esta verificacion ahora." -ForegroundColor Yellow
    Write-Host "  Ya se corrio con exito durante la sesion de QA que aprobo esta PR (ver comentario en GitHub)." -ForegroundColor Yellow
    exit 2
}

$fallo = $false
try {
    Write-Host "=== 1. Arrancando contenedor desechable ===" -ForegroundColor Cyan
    docker rm -f $contenedor *> $null
    docker run -d --name $contenedor -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=$claveSa" -p 14334:1433 mcr.microsoft.com/mssql/server:2019-latest | Out-Null

    $listo = $false
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 2
        $r = Invoke-SqlCmd -Sql "SELECT 1" -Database "master"
        if ($r.Codigo -eq 0) { $listo = $true; break }
    }
    if (-not $listo) { throw "El contenedor no respondio a tiempo." }
    Write-Host "  OK: SQL Server listo" -ForegroundColor Green

    Write-Host "`n=== 2. Creando base vacia ===" -ForegroundColor Cyan
    $r = Invoke-SqlCmd -Sql "CREATE DATABASE $baseDatos" -Database "master"
    if ($r.Codigo -ne 0) { throw "No se pudo crear la base: $($r.Salida)" }
    Write-Host "  OK: $baseDatos creada" -ForegroundColor Green

    Write-Host "`n=== 3. Aplicando ESQUEMA_NEXO.sql ===" -ForegroundColor Cyan
    docker cp "docs/Database/esquema/ESQUEMA_NEXO.sql" "${contenedor}:/tmp/ESQUEMA_NEXO.sql" | Out-Null
    docker exec $contenedor /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $claveSa -C -d $baseDatos -i /tmp/ESQUEMA_NEXO.sql
    if ($LASTEXITCODE -ne 0) { throw "ESQUEMA_NEXO.sql fallo al aplicarse." }
    $numTablas = Invoke-SqlScalar -Sql "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'"
    if ($numTablas -eq "45") {
        Write-Host "  OK: 45 tablas creadas" -ForegroundColor Green
    } else {
        Write-Host "  FALLO: se crearon $numTablas tablas, se esperaban 45" -ForegroundColor Red
        $fallo = $true
    }

    Write-Host "`n=== 4. Aplicando datos_base_nexo.sql ===" -ForegroundColor Cyan
    docker cp "docs/Database/seeder/datos_base_nexo.sql" "${contenedor}:/tmp/datos_base_nexo.sql" | Out-Null
    $resultado = docker exec $contenedor /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $claveSa -C -d $baseDatos -i /tmp/datos_base_nexo.sql
    if ($LASTEXITCODE -ne 0) { throw "datos_base_nexo.sql fallo al aplicarse." }
    Write-Host "  $resultado"

    Write-Host "`n=== 5. Cargando los 9 CSV del seeder ===" -ForegroundColor Cyan
    $tablasIdentity = @("tbl_municipios", "tbl_pesos")
    $csvATabla = [ordered]@{
        "municipios.csv"                = "tbl_municipios"
        "pesos.csv"                     = "tbl_pesos"
        "reglas_solo_sicetac.csv"       = "tbl_reglas_solo_sicetac"
        "tarifas_plaza.csv"             = "tbl_tarifas_plaza"
        "servicios_conexos.csv"         = "tbl_servicios_conexos"
        "alias_elementos.csv"           = "tbl_alias_elementos"
        "mapeo_vehiculos.csv"           = "tbl_mapeo_vehiculo_tarifario"
        "rangos_peso_contenedor.csv"    = "tbl_rangos_peso_contenedor"
        "tipos_carroceria_catalogo.csv" = "tbl_tipos_carroceria_catalogo"
    }

    foreach ($nombreCsv in $csvATabla.Keys) {
        $tabla = $csvATabla[$nombreCsv]
        $ruta = "docs/Database/seeder/$nombreCsv"
        # Import-Csv parsea comillas/comas escapadas correctamente (a diferencia de un split
        # manual), necesario porque alias_elementos.csv trae valores como "x40""" (= x40").
        $filas = @(Import-Csv -Path $ruta -Encoding UTF8)
        $header = $filas[0].PSObject.Properties.Name
        $generarId = -not ($header -contains "Id")

        $usaIdentity = $tablasIdentity -contains $tabla
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.AppendLine("SET NOCOUNT ON;")
        if ($usaIdentity) { [void]$sb.AppendLine("SET IDENTITY_INSERT [dbo].[$tabla] ON;") }
        $columnas = "[" + (($(if ($generarId) { ,"Id" + $header } else { $header })) -join "],[") + "]"

        foreach ($fila in $filas) {
            $valores = @()
            if ($generarId) { $valores += "NEWID()" }
            foreach ($col in $header) {
                $v = $fila.$col
                if ([string]::IsNullOrEmpty($v)) { $valores += "NULL" }
                else { $valores += "N'" + ($v -replace "'", "''") + "'" }
            }
            [void]$sb.AppendLine("INSERT INTO [dbo].[$tabla] ($columnas) VALUES (" + ($valores -join ",") + ");")
        }
        if ($usaIdentity) { [void]$sb.AppendLine("SET IDENTITY_INSERT [dbo].[$tabla] OFF;") }

        $tmpSql = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($tmpSql, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
        docker cp $tmpSql "${contenedor}:/tmp/carga.sql" | Out-Null
        Remove-Item $tmpSql
        $salida = docker exec $contenedor /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $claveSa -C -d $baseDatos -i /tmp/carga.sql 2>&1
        if ($LASTEXITCODE -ne 0 -or ($salida -match "Msg \d+")) {
            Write-Host "  FALLO cargando $tabla :" -ForegroundColor Red
            Write-Host ($salida | Select-Object -First 10)
            $fallo = $true
            continue
        }
        $esperado = $filas.Count
        $real = Invoke-SqlScalar -Sql "SELECT COUNT(*) FROM [dbo].[$tabla]"
        if ($real -eq "$esperado") {
            Write-Host "  OK: $tabla -> $real filas (igual a $nombreCsv)" -ForegroundColor Green
        } else {
            Write-Host "  FALLO: $tabla -> $real filas, se esperaban $esperado de $nombreCsv" -ForegroundColor Red
            $fallo = $true
        }
    }
}
finally {
    Write-Host "`n=== Limpiando contenedor desechable ===" -ForegroundColor Cyan
    docker rm -f $contenedor *> $null
    Write-Host "  OK: contenedor eliminado" -ForegroundColor Green
}

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK" -ForegroundColor Green
    exit 0
}
