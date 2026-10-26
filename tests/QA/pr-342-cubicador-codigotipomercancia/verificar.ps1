# Verificación QA de la PR #342 (Plan 3D desde línea: el Cubicador lee
# codigoTipoMercancia). Cierra el issue #337.
#
# El bug: el cliente Python del Cubicador (nexo_client.py) leia
# `codigoMercancia` del cuerpo de NEXO, pero NEXO emite `codigoTipoMercancia`
# en GET /api/lineas-solicitud/{id}/cubicacion. Toda linea se rechazaba con
# MERCANCIA_NO_DECLARADA. El fix es de una linea; la verificacion real son
# los tests de contrato en ambos lados (no hace falta SQL aparte).
#
# Requiere:
#  - .env.pruebas-reales.local en la raiz del repo, para la parte .NET.
#  - Un venv de Python con las dependencias de cubicador/requirements.txt
#    instaladas (incluye aioodbc/pyodbc), para la parte Python.
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-342-cubicador-codigotipomercancia/verificar.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

# --- Lado NEXO (.NET) ---
$envFile = Join-Path $repoRoot ".env.pruebas-reales.local"
if (-not (Test-Path $envFile)) {
    Write-Error ".env.pruebas-reales.local no existe en la raiz del repo. No se puede verificar el lado .NET contra la BD real sin él."
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

Write-Host ""
Write-Host "=== dotnet build Nexo.Clean.sln -c Release ==="
dotnet build Nexo.Clean.sln -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build fallo"; exit 1 }

Write-Host ""
Write-Host "=== Test de contrato (lado NEXO): el campo se llama codigoTipoMercancia ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~clsLineasSolicitudCubicacionControllerTests"
if ($LASTEXITCODE -ne 0) { Write-Error "clsLineasSolicitudCubicacionControllerTests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Suite .NET completa (esperado: Domain 218/218, Application 533/533, Api 733/733 con 10 omitidos esperados) ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
dotnet test tests/Nexo.Application.Tests -c Release --no-build
dotnet test tests/Nexo.Api.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Suite .NET fallo"; exit 1 }

# --- Lado Cubicador (Python) ---
Write-Host ""
Write-Host "=== pytest del Cubicador (esperado: 275/275) ==="
Set-Location (Join-Path $repoRoot "cubicador")
& .\.venv\Scripts\python.exe -m pytest
if ($LASTEXITCODE -ne 0) { Write-Error "pytest del Cubicador fallo"; exit 1 }
Set-Location $repoRoot

Write-Host ""
Write-Host "=== Verificación completa ==="
