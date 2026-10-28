# QA — PR #397: Estudio comparativo del Cubicador (#366)
# Verifica build + suites de tests (Domain, Application, Api contra BD real) + el harness de Python.
# Requiere .env.pruebas-reales.local en la raíz del repo con las cadenas de conexión reales.
# Requiere Python 3.14+ para el harness de evaluacion-ia/cubicador.

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

Write-Host "== Harness de evaluacion-ia/cubicador (Python) ==" -ForegroundColor Cyan
python -m pytest evaluacion-ia/cubicador --noconftest -q
if ($LASTEXITCODE -ne 0) { throw "Pruebas del harness de evaluacion fallaron" }

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

Write-Host "OK: build, harness de evaluacion y suites en verde." -ForegroundColor Green
