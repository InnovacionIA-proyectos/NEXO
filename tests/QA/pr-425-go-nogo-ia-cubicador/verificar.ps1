# QA — PR #425: Decisión go/no-go de la IA en el Cubicador (#370) y trabajo de la rama S6-HU-34-36
# Verifica build + suites de tests .NET (Domain, Application, Api contra BD real) + la suite de Python
# del Cubicador + la autoprueba del arnés adversario del asistente (sin llamar a la IA real).
# Requiere .env.pruebas-reales.local en la raíz del repo con las cadenas de conexión reales.
# Requiere el .venv de cubicador/ con sus dependencias.

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

Write-Host "== Tests focalizados del esfuerzo de Anthropic ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~clsAnthropicAdapterTests|FullyQualifiedName~clsAnthropicConversacionTests|FullyQualifiedName~AsistenteIaConfiguracion"
if ($LASTEXITCODE -ne 0) { throw "Tests de esfuerzo de Anthropic fallaron" }

Write-Host "== Suite de Python del Cubicador ==" -ForegroundColor Cyan
Push-Location cubicador
try {
    & .\.venv\Scripts\python.exe -m pytest tests -q
    if ($LASTEXITCODE -ne 0) { throw "Pruebas de cubicador fallaron" }
} finally {
    Pop-Location
}

Write-Host "== Autoprueba del arnes adversario del asistente (sin llamar a la IA real) ==" -ForegroundColor Cyan
dotnet run --project tests/evaluacion-ia/asistente/arnes -- --autoprueba
if ($LASTEXITCODE -ne 0) { throw "Autoprueba del arnes adversario fallo" }

Write-Host "OK: build, suites y arneses en verde." -ForegroundColor Green
