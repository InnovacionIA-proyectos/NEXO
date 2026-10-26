# Verificación QA de la PR #338 (Flujo de decisión del flete: guardado
# automático, tramos vacíos y vehículo relajado). PR con head = rama DEPLOY,
# base = QA.
#
# La verificación real de esta PR es correr las tres suites de pruebas
# (Domain, Application, API) contra la base de datos REAL de pruebas -
# .env.pruebas-reales.local debe existir en la raiz del repo antes de correr
# este script. Los tests de Nexo.Api.Tests (clsFlujoDeDecisionRealTests.cs,
# clsSicetacPeriodoYTramosVaciosTests.cs, clsSeleccionSiceVacioTests.cs) ya
# traen aserciones de valor exacto (GetDecimal()) contra datos reales, por
# lo que no hace falta una consulta SQL aparte para esta PR: la prueba de
# valores exactos ya vive en el propio test.
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-338-flujo-decision-flete/verificar.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

$envFile = Join-Path $repoRoot ".env.pruebas-reales.local"
if (-not (Test-Path $envFile)) {
    Write-Error ".env.pruebas-reales.local no existe en la raiz del repo. No se puede verificar contra la BD real sin él."
    exit 1
}

Write-Host "=== Cargando variables de entorno de .env.pruebas-reales.local ==="
Get-Content $envFile | Where-Object { $_ -match '^[A-Za-z_]+__?[A-Za-z]*=' } | ForEach-Object {
    $idx = $_.IndexOf('=')
    $name = $_.Substring(0, $idx)
    $value = $_.Substring($idx + 1)
    [System.Environment]::SetEnvironmentVariable($name, $value)
    if ($name -eq "ConnectionStrings__Nexo") {
        [System.Environment]::SetEnvironmentVariable("TEST_DB_CONNECTION", $value)
    }
}
Write-Host "Database__Esquema = $env:Database__Esquema"

Write-Host ""
Write-Host "=== dotnet build Nexo.Clean.sln -c Release ==="
dotnet build Nexo.Clean.sln -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build fallo"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Domain.Tests (esperado: 218/218) ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Domain.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Application.Tests (esperado: 524/524) ==="
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Application.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Api.Tests contra la BD real (esperado: 728/738, 10 omitidos esperados) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Api.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Verificación completa ==="
