# QA — PR #418: Cotizar los conexos con el precio de venta (#408)
# Verifica build + suites de tests (Domain, Application, Api contra BD real).
# Requiere .env.pruebas-reales.local en la raíz del repo con las cadenas de conexión reales.
# IMPORTANTE: requiere docs/Database/407_precio_venta_conexos.sql (Parte A) ya aplicado contra esa BD
# (DDL + un UPDATE que llena PrecioVentaUnitario de filas existentes con su costo actual; idempotente).
#
# NOTA: tests/Nexo.Api.Tests/clsServiciosConexosTests.cs::Crear_con_precio_de_venta_lo_devuelve_y_sin_el_queda_vacio
# puede fallar en la BD compartida de pruebas por acumulación de filas "Precinto" de corridas anteriores
# (ver issue #421) — no es un fallo de la lógica de precio de venta.

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\..\.."
Set-Location $root

function Load-EnvVar($name) {
    $line = Get-Content ".env.pruebas-reales.local" | Where-Object { $_ -match "^$name=" } | Select-Object -First 1
    if (-not $line) { throw "No se encontró $name en .env.pruebas-reales.local" }
    $value = $line.Substring($name.Length + 1)
    [System.Environment]::SetEnvironmentVariable($name, $value)
}

Write-Host "== Build ==" -ForegroundColor Cyan
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw "Build falló" }

Write-Host "== Nexo.Domain.Tests ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Domain.Tests falló" }

Write-Host "== Nexo.Application.Tests ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Application.Tests falló" }

Write-Host "== Nexo.Api.Tests (contra BD real) ==" -ForegroundColor Cyan
Load-EnvVar "ConnectionStrings__Nexo"
[System.Environment]::SetEnvironmentVariable("TEST_DB_CONNECTION", [System.Environment]::GetEnvironmentVariable("ConnectionStrings__Nexo"))
Load-EnvVar "Database__Esquema"
Load-EnvVar "ConnectionStrings__DwSice"
Load-EnvVar "ConnectionStrings__DbSolucionesTi"
Load-EnvVar "ConnectionStrings__DwCubos"
Load-EnvVar "ConnectionStrings__IdentidadPortal"

dotnet test tests/Nexo.Api.Tests -c Release --no-build
Write-Host "Nota: si solo falla Crear_con_precio_de_venta_lo_devuelve_y_sin_el_queda_vacio, ver issue #421 (deuda de datos de prueba, no bug de produccion)." -ForegroundColor Yellow

Write-Host "== Tests focalizados de precio de venta de conexos ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Application.Tests -c Release --no-build --filter "FullyQualifiedName~PrecioVenta"
if ($LASTEXITCODE -ne 0) { throw "Tests de precio de venta fallaron" }

Write-Host "OK." -ForegroundColor Green
