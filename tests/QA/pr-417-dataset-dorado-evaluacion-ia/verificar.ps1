# QA — PR #417: Dataset dorado y arnés de evaluación de la IA del mapeo de Excel (#353)
# Verifica build + suites de tests de la solución principal (Domain, Application, Api contra BD real),
# que no deben cambiar (esta PR no toca src/), y el arnés de evaluación en modo seco y autoprueba (sin llamar a la IA real).
# Requiere .env.pruebas-reales.local en la raíz del repo con las cadenas de conexión reales.

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\..\.."
Set-Location $root

function Load-EnvVar($name) {
    $line = Get-Content ".env.pruebas-reales.local" | Where-Object { $_ -match "^$name=" } | Select-Object -First 1
    if (-not $line) { throw "No se encontró $name en .env.pruebas-reales.local" }
    $value = $line.Substring($name.Length + 1)
    [System.Environment]::SetEnvironmentVariable($name, $value)
}

Write-Host "== Build (solución principal) ==" -ForegroundColor Cyan
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

Write-Host "== Arnes de evaluacion: modo seco (sin llamar a la IA) ==" -ForegroundColor Cyan
dotnet run --project tests/evaluacion-ia/arnes -- --seco
if ($LASTEXITCODE -ne 0) { throw "Modo seco del arnes fallo" }

Write-Host "== Arnes de evaluacion: autoprueba (IA falsa, sin llamar a la IA real) ==" -ForegroundColor Cyan
dotnet run --project tests/evaluacion-ia/arnes -- --autoprueba --repeticiones 1
if ($LASTEXITCODE -ne 0) { throw "Autoprueba del arnes fallo" }
Write-Host "Revisa el informe generado en tests/evaluacion-ia/informes/ : debe dar 100% de acierto y 0 fallos." -ForegroundColor Yellow

Write-Host "OK: build, suites y arnes de evaluacion en verde." -ForegroundColor Green
