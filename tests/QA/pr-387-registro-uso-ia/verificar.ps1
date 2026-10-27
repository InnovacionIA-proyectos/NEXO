# Verificacion QA de la PR #387 (registro de uso de la IA, tope diario por persona, consulta de gasto). Cierra el issue #355.
#
# La PR toca el DbContext (mapeo de tbl_ia_uso), el composition root (el puerto de IA pasa a ser un decorador scoped), el
# middleware de errores (429) y agrega un endpoint, asi que ademas de los tests de IA se corre la suite completa de Api.Tests.
# Requiere que tbl_ia_uso (PR #386) ya este aplicada en la base de pruebas; los tests de integracion escriben filas con una marca
# unica (modelo o funcion con GUID) y las borran en un finally. Ningun test llama a la API real de Anthropic: el script fuerza
# vacias ANTHROPIC_API_KEY y Anthropic__ApiKey.
#
# .env.pruebas-reales.local debe existir en la raiz del repo y la VPN corporativa debe estar conectada.
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-387-registro-uso-ia/verificar.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

$envFile = Join-Path $repoRoot ".env.pruebas-reales.local"
if (-not (Test-Path $envFile)) {
    Write-Error ".env.pruebas-reales.local no existe en la raiz del repo. No se puede verificar contra la BD real sin el."
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
foreach ($n in "ANTHROPIC_API_KEY", "Anthropic__ApiKey") { [System.Environment]::SetEnvironmentVariable($n, "") }
Write-Host "Database__Esquema = $env:Database__Esquema"
Write-Host "Anthropic__ApiKey y ANTHROPIC_API_KEY forzadas a vacio para esta corrida"

Write-Host ""
Write-Host "=== dotnet build Nexo.Clean.sln -c Release ==="
dotnet build Nexo.Clean.sln -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build fallo"; exit 1 }

Write-Host ""
Write-Host "=== Tests de IA (decorador, tope, integracion con la tabla real, middleware 429, endpoint de gasto) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~Nexo.Api.Tests.IA"
if ($LASTEXITCODE -ne 0) { Write-Error "Tests de IA fallaron"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Domain.Tests y Nexo.Application.Tests ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Domain.Tests fallo"; exit 1 }
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Application.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Api.Tests completo contra la BD real (regresion por DbContext, DI y middleware) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Api.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Verificacion completa ==="
