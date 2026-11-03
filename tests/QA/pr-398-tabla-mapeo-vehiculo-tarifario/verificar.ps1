# QA — PR #398: Tabla tbl_mapeo_vehiculo_tarifario con carga inicial (#377)
# Aplica docs/Database/377_mapeo_vehiculo_tarifario.sql contra la BD real de pruebas:
# crea la tabla (idempotente) y carga 15 filas de catálogo vía MERGE (también idempotente,
# no duplica en una segunda corrida). Esto SÍ inserta datos reales de catálogo (no son
# datos de prueba desechables): el dato de negocio que la PR entrega.
# Requiere .env.pruebas-reales.local en la raíz del repo con ConnectionStrings__Nexo.

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\..\.."
Set-Location $root

function Load-EnvVar($name) {
    $line = Get-Content ".env.pruebas-reales.local" | Where-Object { $_ -match "^$name=" } | Select-Object -First 1
    if (-not $line) { throw "No se encontró $name en .env.pruebas-reales.local" }
    return $line.Substring($name.Length + 1)
}

$connStr = Load-EnvVar "ConnectionStrings__Nexo"

Write-Host "== Aplicando docs/Database/377_mapeo_vehiculo_tarifario.sql contra la BD real ==" -ForegroundColor Cyan

Add-Type -AssemblyName "System.Data"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
try {
    $sql = Get-Content "docs/Database/377_mapeo_vehiculo_tarifario.sql" -Raw
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $reader = $cmd.ExecuteReader()
    $table = New-Object System.Data.DataTable
    $table.Load($reader)
    $table | Format-Table -AutoSize | Out-String | Write-Host

    $fila = $table.Rows[0]
    if ($fila.tablas -ne 1 -or $fila.columnas -ne 10 -or $fila.indices -ne 1 -or $fila.filas -ne 15 -or $fila.filas_con_plaza -ne 8) {
        throw "El resultado de verificación no coincide con lo esperado (1 tabla, 10 columnas, 1 índice, 15 filas, 8 con plaza)"
    }
} finally {
    $conn.Close()
}

Write-Host "OK: tbl_mapeo_vehiculo_tarifario existe con la carga esperada (script es idempotente, se puede re-correr sin duplicar)." -ForegroundColor Green
