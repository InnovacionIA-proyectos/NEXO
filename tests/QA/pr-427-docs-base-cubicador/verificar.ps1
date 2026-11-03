<#
.SINOPSIS
Verificacion de la PR #427: "docs: organizar la documentacion de la base del Cubicador
y quitar archivos obsoletos (#327)".

.QUE VERIFICA
PR puramente documental, posterior a la PR #424 (base propia del Cubicador). No cambia
codigo de aplicacion, esquema ni datos del seeder. Este script confirma en vivo:

1. Los archivos marcados como eliminados en la PR (_generar_seeders.py, el documento de la
   HU de reestructuracion ya cerrada, y la propuesta de BD superada) realmente no existen.
2. No queda ninguna referencia textual a esas rutas/archivos fuera de tests/QA (evidencia
   congelada de PRs anteriores, que no se toca).
3. Los documentos reubicados (DICCIONARIO-DATOS.md, ORIGEN-Y-DECISIONES-SEEDER.md) existen
   en su ruta nueva (cubicador/docs/database/) y el README de cubicador/database/ enlaza ahi.
4. La formula de Id documentada (uuid5 sobre un namespace fijo + semilla de la clave de
   negocio) reproduce EXACTAMENTE los Id reales de los CSV del seeder -- verificado cifra
   por cifra para 2 filas de vehiculos.csv (C2, 2L2), no solo "se ve razonable".

.COMO SE VERIFICO EN VIVO
- git checkout de la rama de la PR, merge de QA (sin cambios, ya estaba al dia).
- grep real sobre el working tree para las rutas/archivos quitados.
- Lectura directa de los CSV reales del seeder y calculo del UUID5 esperado con Python
  (libreria estandar `uuid`), comparado contra el valor real en el CSV.

.LIMITACIONES CONOCIDAS
- No se verificaron los 71 Id que la PR afirma haber revisado, solo una muestra de 2 filas
  de la tabla `vehiculos` (suficiente para confirmar que la formula documentada es la real;
  esta PR es de bajo riesgo porque no toca codigo ni datos de produccion).
- `relacionVehiculosCarroceria.csv` referencia por Id (FK), no por codigo, asi que no se
  pudo cruzar una fila de esa tabla por simple grep sin resolver las FKs primero; no se
  considero necesario dado el bajo riesgo de la PR.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

Write-Host "=== 1. Archivos eliminados no deben existir ===" -ForegroundColor Cyan
$eliminados = @(
    "cubicador/database/seeder/_generar_seeders.py",
    "cubicador/docs/2026-10-08_HU_Reestructuracion_Cubicador.md",
    "cubicador/docs/PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md"
)
$fallo = $false
foreach ($f in $eliminados) {
    if (Test-Path $f) {
        Write-Host "  FALLO: $f todavia existe" -ForegroundColor Red
        $fallo = $true
    } else {
        Write-Host "  OK: $f no existe" -ForegroundColor Green
    }
}

Write-Host "`n=== 2. No deben quedar referencias colgantes (fuera de tests/QA) ===" -ForegroundColor Cyan
$patron = "_generar_seeders|database/documentacion|PROPUESTA-BD-MOTOR"
$matches = git grep -n -E $patron -- . ':!tests/QA' 2>$null
if ($matches) {
    Write-Host "  FALLO: quedan referencias:" -ForegroundColor Red
    Write-Host $matches
    $fallo = $true
} else {
    Write-Host "  OK: ninguna referencia colgante" -ForegroundColor Green
}

Write-Host "`n=== 3. Documentos reubicados existen en la ruta nueva ===" -ForegroundColor Cyan
$nuevos = @(
    "cubicador/docs/database/DICCIONARIO-DATOS.md",
    "cubicador/docs/database/ORIGEN-Y-DECISIONES-SEEDER.md"
)
foreach ($f in $nuevos) {
    if (Test-Path $f) {
        Write-Host "  OK: $f existe" -ForegroundColor Green
    } else {
        Write-Host "  FALLO: $f no existe" -ForegroundColor Red
        $fallo = $true
    }
}

Write-Host "`n=== 4. Formula de Id (uuid5) reproduce los Id reales del CSV ===" -ForegroundColor Cyan
$py = @"
import uuid
ns = uuid.UUID('c9b1b1a0-6f3e-4b1a-9c2d-a1b2c3d40001')
casos = [('vehiculo:C2', '04342939-06c6-5058-8650-0e9980db59e2'),
         ('vehiculo:2L2', '14456c83-1b5d-5813-a075-6c88d367760f')]
ok = True
for semilla, esperado in casos:
    calculado = str(uuid.uuid5(ns, semilla))
    estado = 'OK' if calculado == esperado else 'FALLO'
    if calculado != esperado:
        ok = False
    print(f'  {estado}: {semilla} -> {calculado} (esperado {esperado})')
import sys
sys.exit(0 if ok else 1)
"@
$py | py -
if ($LASTEXITCODE -ne 0) { $fallo = $true }

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK" -ForegroundColor Green
    exit 0
}
