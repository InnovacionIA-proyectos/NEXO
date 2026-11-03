# Verificacion QA (parte .NET) de la PR #389 (diccionario de terminos por cliente: tabla y CRUD). Cierra los issues #356 y #358.
# El SQL de la tabla se verifica aparte, en un SQL Server local de Docker: ver verificar-sql.ps1 en esta misma carpeta.
#
# La PR toca el DbContext (mapeo de tbl_diccionario_cliente), el composition root (DI y Program.cs) y agrega un controlador, asi que
# ademas de los tests del diccionario se corre la suite completa de Api.Tests. Requiere que tbl_diccionario_cliente (script 356) ya
# este aplicada en la base de pruebas. Los tests escriben terminos con una marca unica (T358<guid>) y los borran en un finally; ADEMAS
# cada crear/editar/borrar deja una fila en tbl_audit_log, que no tiene camino de borrado (ver README: practica ya existente en el repo).
#
# .env.pruebas-reales.local debe existir en la raiz del repo y la VPN corporativa debe estar conectada.
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-389-diccionario-cliente/verificar.ps1

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

Write-Host ""
Write-Host "=== dotnet build Nexo.Clean.sln -c Release ==="
dotnet build Nexo.Clean.sln -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build fallo"; exit 1 }

Write-Host ""
Write-Host "=== Tests del diccionario (reglas de dominio/aplicacion y API contra la tabla real) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~DiccionarioCliente"
if ($LASTEXITCODE -ne 0) { Write-Error "Tests del diccionario fallaron"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Domain.Tests y Nexo.Application.Tests ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Domain.Tests fallo"; exit 1 }
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Application.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Api.Tests completo contra la BD real (regresion por DbContext, DI y Program.cs) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Api.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Verificacion completa ==="
