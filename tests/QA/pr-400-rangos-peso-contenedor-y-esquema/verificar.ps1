# QA — PR #400: Rangos de peso por contenedor (#379) y esquema de BD verificado
# Levanta un SQL Server 2019 DESECHABLE en Docker (no toca la BD compartida de pruebas) y corre
# ESQUEMA_NEXO.sql completo contra una base vacía, de una sola pasada, para confirmar que la
# reestructuración del archivo (índices antes que llaves foráneas) no rompe nada. También corre
# datos_base_nexo.sql dos veces seguidas para confirmar que es idempotente.
# Requiere Docker Desktop corriendo.

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\..\.."
Set-Location $root

$containerName = "nexo-qa-verificar-$([guid]::NewGuid().ToString('N').Substring(0,8))"
$port = 14333
$sa = "QaTest_2026!Strong"

Write-Host "== Levantando SQL Server 2019 desechable en Docker ==" -ForegroundColor Cyan
docker run -d --name $containerName -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=$sa" -p "${port}:1433" mcr.microsoft.com/mssql/server:2019-latest | Out-Null
if ($LASTEXITCODE -ne 0) { throw "No se pudo levantar el contenedor" }

try {
    Write-Host "Esperando a que SQL Server arranque..." -ForegroundColor Cyan
    Start-Sleep -Seconds 20

    $connStrMaster = "Server=127.0.0.1,$port;Database=master;User Id=sa;Password=$sa;TrustServerCertificate=True;"
    $connStrDb = "Server=127.0.0.1,$port;Database=nexo_qa_fresh;User Id=sa;Password=$sa;TrustServerCertificate=True;"
    Add-Type -AssemblyName "System.Data"

    function Run-NonQuery($connStr, $sql) {
        $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
        $conn.Open()
        try {
            foreach ($batch in ($sql -split "(?m)^\s*GO\s*$")) {
                if ($batch.Trim().Length -eq 0) { continue }
                $cmd = $conn.CreateCommand()
                $cmd.CommandText = $batch
                $cmd.CommandTimeout = 120
                $null = $cmd.ExecuteNonQuery()
            }
        } finally { $conn.Close() }
    }

    function Run-Query($connStr, $sql) {
        $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
        $conn.Open()
        try {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $sql
            $cmd.CommandTimeout = 120
            $reader = $cmd.ExecuteReader()
            $table = New-Object System.Data.DataTable
            $table.Load($reader)
            return $table
        } finally { $conn.Close() }
    }

    Write-Host "== Creando base vacia (compat 130, Modern_Spanish_CI_AS) ==" -ForegroundColor Cyan
    Run-NonQuery $connStrMaster "IF DB_ID('nexo_qa_fresh') IS NOT NULL DROP DATABASE nexo_qa_fresh;"
    Run-NonQuery $connStrMaster "CREATE DATABASE nexo_qa_fresh COLLATE Modern_Spanish_CI_AS; ALTER DATABASE nexo_qa_fresh SET COMPATIBILITY_LEVEL = 130;"

    Write-Host "== Corriendo ESQUEMA_NEXO.sql completo, de una sola pasada ==" -ForegroundColor Cyan
    Run-NonQuery $connStrDb (Get-Content "docs/Database/ESQUEMA_NEXO.sql" -Raw)
    Write-Host "OK: ESQUEMA_NEXO.sql corrio sin errores." -ForegroundColor Green

    Write-Host "== Corriendo datos_base_nexo.sql ==" -ForegroundColor Cyan
    Run-NonQuery $connStrDb (Get-Content "docs/Database/datos_base_nexo.sql" -Raw)

    Write-Host "== Corriendo scripts de catalogo 304, 352, 356, 361, 377, 379 ==" -ForegroundColor Cyan
    foreach ($f in @(
        "docs/Database/304_rechazo_cubicacion_por_linea.sql",
        "docs/Database/352_ia_uso.sql",
        "docs/Database/356_diccionario_cliente.sql",
        "docs/Database/361_plantillas_mapeo_cliente.sql",
        "docs/Database/377_mapeo_vehiculo_tarifario.sql",
        "docs/Database/379_rangos_peso_contenedor.sql"
    )) {
        Write-Host "  -> $f"
        Run-NonQuery $connStrDb (Get-Content $f -Raw)
    }
    Write-Host "OK: scripts de catalogo corrieron sin errores." -ForegroundColor Green

    $t = Run-Query $connStrDb "SELECT COUNT(*) AS tablas FROM sys.tables;"
    $t | Format-Table -AutoSize | Out-String | Write-Host

    Write-Host "== Corriendo datos_base_nexo.sql una SEGUNDA vez (idempotencia) ==" -ForegroundColor Cyan
    Run-NonQuery $connStrDb (Get-Content "docs/Database/datos_base_nexo.sql" -Raw)
    $v = Run-Query $connStrDb @"
SELECT
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_usuarios] WHERE [Id] = '00000000-0000-0000-0000-000000000001') AS usuario_sistema,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_mapeo_rol_portal]) AS mapeos_rol,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_parametros_score]) AS parametros_score;
"@
    $v | Format-Table -AutoSize | Out-String | Write-Host
    if ($v.Rows[0].usuario_sistema -ne 1 -or $v.Rows[0].mapeos_rol -ne 9 -or $v.Rows[0].parametros_score -ne 1) {
        throw "datos_base_nexo.sql no es idempotente o no coincide con lo esperado"
    }

    Write-Host "OK: todo corrio limpio contra una base vacia, de una sola pasada, sin errores." -ForegroundColor Green
} finally {
    Write-Host "== Eliminando contenedor desechable ==" -ForegroundColor Cyan
    docker rm -f $containerName | Out-Null
}
