<#
.SYNOPSIS
    QA - S2-HU-11.2 (PR #145): corre los 8 notebooks del EDA-2 (extraccion que cruza
    las 4 fuentes) de punta a punta, desde cero.

.DESCRIPTION
    Borra los artefactos generados (datos/interim/*.parquet, reportes/figures/*.png),
    corre los 8 notebooks en orden vía `jupyter nbconvert --execute`, y verifica que
    cada uno termine con exit code 0. Al final restaura el arbol de trabajo al estado
    commiteado (git checkout), para no dejar cambios de la corrida de prueba.

    Mismo patron que verify-notebooks-desde-cero.ps1 de
    tests/QA/s2-hu-111-eda-viajes-buenaventura/ (EDA-1), adaptado a esta carpeta.

.PARAMETER PythonExe
    Ruta al ejecutable de Python/venv a usar. Por defecto "python".

.EXAMPLE
    ./tests/QA/s2-hu-112-eda-extraccion-4-fuentes/verify-notebooks-desde-cero.ps1
#>
param(
    [string]$PythonExe = "python"
)

$ErrorActionPreference = "Stop"
$repoRoot = git rev-parse --show-toplevel
$edaDir = Join-Path $repoRoot "analitica/03-eda-extraccion-4-fuentes"

Push-Location $edaDir
try {
    Write-Host "Borrando artefactos generados para forzar una corrida desde cero..."
    Remove-Item -Path "datos/interim/*.parquet" -ErrorAction SilentlyContinue
    Remove-Item -Path "reportes/figures/*.png" -ErrorAction SilentlyContinue

    $notebooks = Get-ChildItem "notebooks/*.ipynb" | Sort-Object Name
    $fallidos = @()

    foreach ($nb in $notebooks) {
        Write-Host "=== Ejecutando $($nb.Name) ==="
        # Ver la nota de verify-notebooks-desde-cero.ps1 (EDA-1): jupyter nbconvert
        # escribe su progreso normal a stderr, y en PowerShell 5.1 cualquier
        # redireccion de stderr de un comando nativo lo envuelve en un
        # NativeCommandError -aunque el proceso termine con exit code 0-. Se captura
        # en una variable y el resultado real se juzga por $LASTEXITCODE.
        $previousEap = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $log = & $PythonExe -m jupyter nbconvert --to notebook --execute --inplace `
            --ExecutePreprocessor.timeout=600 $nb.FullName 2>&1 | Out-String
        $ErrorActionPreference = $previousEap
        if ($LASTEXITCODE -ne 0) {
            Write-Host "FALLO: $($nb.Name) termino con exit code $LASTEXITCODE" -ForegroundColor Red
            Write-Host $log
            $fallidos += $nb.Name
        } else {
            Write-Host "OK: $($nb.Name)" -ForegroundColor Green
        }
    }

    Write-Host ""
    if ($fallidos.Count -eq 0) {
        Write-Host "TODO OK: los 8 notebooks corrieron sin errores, desde cero." -ForegroundColor Green
    } else {
        Write-Host "HAY FALLAS en: $($fallidos -join ', ')" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Artefactos regenerados:"
    Get-ChildItem "datos/interim" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $($_.Name)" }
    Get-ChildItem "reportes/figures" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $($_.Name)" }
}
finally {
    Write-Host ""
    Write-Host "Restaurando el arbol de trabajo al estado commiteado..."
    Push-Location $repoRoot
    git checkout -- analitica/03-eda-extraccion-4-fuentes/
    Pop-Location
    Pop-Location
}

if ($fallidos.Count -gt 0) { exit 1 }
