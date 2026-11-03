# QA — PR #403: Herramientas de escritura del asistente con confirmación (#373)
# Verifica build + suites de tests (Domain, Application, Api contra BD real).
# Requiere .env.pruebas-reales.local en la raíz del repo con las cadenas de conexión reales.
# IMPORTANTE: requiere docs/Database/372_asistente_conversaciones.sql Y
# docs/Database/373_asistente_acciones.sql ya aplicados contra esa BD (solo DDL, idempotentes,
# en ese orden); sin ellos, las pruebas de clsAsistenteAccionesApiTests fallan con
# "Invalid object name 'db_NEXO.tbl_asistente_acciones'".

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

Write-Host "== Tests focalizados de las acciones de escritura del asistente ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Application.Tests -c Release --no-build --filter "FullyQualifiedName~AccionesAsistente"
if ($LASTEXITCODE -ne 0) { throw "Tests de acciones del asistente fallaron" }
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~AsistenteAcciones"
if ($LASTEXITCODE -ne 0) { throw "Tests API de acciones del asistente fallaron" }

Write-Host "OK: build y suites en verde." -ForegroundColor Green
