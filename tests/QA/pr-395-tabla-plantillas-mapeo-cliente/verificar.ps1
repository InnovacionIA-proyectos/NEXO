# QA — PR #395: Tabla tbl_plantillas_mapeo_cliente y esquema actualizado (#361)
# Aplica docs/Database/361_plantillas_mapeo_cliente.sql contra la BD real de pruebas
# y verifica que la tabla quede con la forma esperada. No inserta ningún dato,
# solo DDL (CREATE TABLE/INDEX) + un SELECT de solo lectura contra sys.*.
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

Write-Host "== Aplicando docs/Database/361_plantillas_mapeo_cliente.sql contra la BD real ==" -ForegroundColor Cyan

Add-Type -AssemblyName "System.Data"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
try {
    $sql = Get-Content "docs/Database/361_plantillas_mapeo_cliente.sql" -Raw
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $reader = $cmd.ExecuteReader()
    $table = New-Object System.Data.DataTable
    $table.Load($reader)
    $table | Format-Table -AutoSize | Out-String | Write-Host

    $fila = $table.Rows[0]
    if ($fila.tablas -ne 1 -or $fila.columnas -ne 8 -or $fila.indices -ne 1 -or $fila.llaves_foraneas -ne 1 -or $fila.restricciones_check -ne 2) {
        throw "El resultado de verificación no coincide con lo esperado (1 tabla, 8 columnas, 1 índice, 1 FK, 2 checks)"
    }
} finally {
    $conn.Close()
}

Write-Host "OK: tbl_plantillas_mapeo_cliente existe con la forma esperada (script es idempotente, se puede re-correr)." -ForegroundColor Green
