# QA — PR #414: Vehículo de contenedor por la tabla de rangos y PORTACONTENEDORES por defecto (#381)
# Reemplaza a la PR #406 (rama limpia, un solo commit, sin el merge contaminante de #376-preferencias-asistente).
# Verifica build + suites de tests (Domain, Application, Api contra BD real).
# Requiere .env.pruebas-reales.local en la raíz del repo con las cadenas de conexión reales.
# tbl_rangos_peso_contenedor ya debe existir en esa BD (docs/Database/379_rangos_peso_contenedor.sql, PR #400);
# clsRangosPesoContenedorTests corre igual si la tabla está vacía o con su carga inicial.

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
if ($LASTEXITCODE -ne 0) { throw "Api.Tests falló" }

Write-Host "== Tests focalizados del vehiculo de contenedor por rangos de peso ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~RangosPesoContenedor"
if ($LASTEXITCODE -ne 0) { throw "Tests de rangos de peso contenedor fallaron" }

Write-Host "OK: build y suites en verde." -ForegroundColor Green
