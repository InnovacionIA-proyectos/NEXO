<#
.SINOPSIS
Verificacion de la PR #430: "[S6-HU-33.2] Carga inicial del diccionario del cliente"
(issue #357).

.QUE VERIFICA
Carga inicial de 27 terminos generales (ClienteId vacio) en `tbl_diccionario_cliente`, tomados
de un unico Excel de licitacion, en `docs/Database/seeder/diccionario_cliente.csv`. Incluye
15 encabezados de columna que no corresponden a ningun campo -> `ignorar`, 11 valores de
contenedor con rango de peso -> `CNT20.ST`/`CNT40.HC` y 1 valor de vehiculo ("NPR / Turbo") ->
`2L2`. Sin cambios de codigo C#. El autor afirma que los 27 terminos ya se cargaron, el
2026-11-03, en las dos bases de pruebas reales (`dbInformesAutomatizados_Pruebas.db_NEXO` y
`dbTarifario_Nexo_Pruebas.dbo`), sin duplicados.

.COMO SE VERIFICO EN VIVO
1. CSV: 27 filas de datos, orden de columnas igual al de `CREATE TABLE tbl_diccionario_cliente`
   en `ESQUEMA_NEXO.sql`, y cumplen el CHECK `CK_diccionario_cliente_Contexto` (Contexto NOT NULL
   solo cuando Tipo = 'valor') -- verificado leyendo cada fila.
2. `TerminoNormalizado`: se recalculo a mano el algoritmo real de
   `clsResolucionDeCatalogos.Normalizar` (colapsar espacios, quitar espacio antes de `.`/`,`,
   quitar tildes, mayusculas) sobre varios terminos con acentos/parentesis/simbolos
   ("Costo fijo (conducción...)", "Intermediación (sin aux.)", "N° entregas", "Config. SICE-TAC")
   y coincide cifra por cifra con el valor que trae el CSV.
3. **Contra la BD real de pruebas** (`dbInformesAutomatizados_Pruebas.db_NEXO`, solo lectura,
   nunca se escribio nada): `SELECT COUNT(*), COUNT(DISTINCT TerminoNormalizado)` da 27 y 27 --
   confirma la afirmacion de la PR de que ya estan cargados sin duplicados. Se volco cada fila
   real (Termino, TerminoNormalizado, Tipo, Concepto, Contexto, ClienteId) y se comparo contra el
   CSV de la PR: coinciden exactamente las 27 filas, y `ClienteId` esta vacio en las 27 (termino
   general, no de cliente).
4. No se corrio contra `dbTarifario_Nexo_Pruebas.dbo` (la base propia nueva, preparada en la PR
   #429): esa base todavia no esta conectada como destino real de NEXO (el corte de conexion
   queda para la issue #428), asi que no hay conexion configurada localmente hacia ella.
5. No aplica `dotnet build`/`dotnet test`: no hay codigo de aplicacion en el diff de esta PR.

.LIMITACIONES CONOCIDAS
- No se verifico el estado de `dbTarifario_Nexo_Pruebas.dbo` (ver punto 4) -- queda pendiente de
  una proxima sesion si se necesita, una vez `ConnectionStrings__Nexo` apunte ahi.
- No se repitio la carga de los 27 terminos (ya estaban cargados; no se inserta ni se borra nada
  contra la base real en este script, solo lectura).
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

function Load-EnvVar($name) {
    $line = Get-Content ".env.pruebas-reales.local" | Where-Object { $_ -match "^$name=" } | Select-Object -First 1
    if (-not $line) { throw "No se encontro $name en .env.pruebas-reales.local" }
    return $line.Substring($name.Length + 1)
}

$fallo = $false

Write-Host "=== 1. CSV: 27 filas, columnas y CHECK de Contexto ===" -ForegroundColor Cyan
$filasCsv = @(Import-Csv -Path "docs/Database/seeder/diccionario_cliente.csv" -Encoding UTF8)
if ($filasCsv.Count -eq 27) {
    Write-Host "  OK: 27 filas en el CSV" -ForegroundColor Green
} else {
    Write-Host "  FALLO: $($filasCsv.Count) filas, se esperaban 27" -ForegroundColor Red
    $fallo = $true
}
foreach ($f in $filasCsv) {
    $esperaContexto = $f.Tipo -eq "valor"
    $tieneContexto = -not [string]::IsNullOrEmpty($f.Contexto)
    if ($esperaContexto -ne $tieneContexto) {
        Write-Host "  FALLO: CHECK de Contexto violado en '$($f.Termino)' (Tipo=$($f.Tipo), Contexto='$($f.Contexto)')" -ForegroundColor Red
        $fallo = $true
    }
}
Write-Host "  OK: todas las filas cumplen el CHECK Tipo/Contexto" -ForegroundColor Green

Write-Host "`n=== 2. Formula de TerminoNormalizado (clsResolucionDeCatalogos.Normalizar) ===" -ForegroundColor Cyan
function Limpiar([string]$s) {
    $colapsado = ($s.Trim() -split '\s+') -join ' '
    return [System.Text.RegularExpressions.Regex]::Replace($colapsado, '\s+([.,])', '$1')
}
function SinTildes([string]$s) {
    $formD = $s.Normalize([System.Text.NormalizationForm]::FormD)
    return -join ($formD.ToCharArray() | Where-Object {
        [System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [System.Globalization.UnicodeCategory]::NonSpacingMark
    })
}
function Normalizar([string]$s) {
    # Replica clsResolucionDeCatalogos.Normalizar: Limpiar -> reemplazar _/- por espacio -> SinTildes -> Limpiar -> Upper
    $limpio = Limpiar $s
    $sinSeparadores = $limpio.Replace('_', ' ').Replace('-', ' ')
    return (Limpiar (SinTildes $sinSeparadores)).ToUpperInvariant()
}
foreach ($f in $filasCsv) {
    $calculado = Normalizar $f.Termino
    if ($calculado -ne $f.TerminoNormalizado) {
        Write-Host "  FALLO: '$($f.Termino)' -> calculado '$calculado', CSV trae '$($f.TerminoNormalizado)'" -ForegroundColor Red
        $fallo = $true
    }
}
Write-Host "  OK: los 27 TerminoNormalizado coinciden con la formula real del backend" -ForegroundColor Green

Write-Host "`n=== 3. Contra la BD real de pruebas (solo lectura) ===" -ForegroundColor Cyan
$cs = Load-EnvVar "ConnectionStrings__Nexo"
Add-Type -AssemblyName "System.Data"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Termino, TerminoNormalizado, Tipo, Concepto, ISNULL(Contexto,'') AS Contexto, ClienteId FROM db_NEXO.tbl_diccionario_cliente"
    $reader = $cmd.ExecuteReader()
    $filasBd = @{}
    while ($reader.Read()) {
        $filasBd[$reader["Termino"]] = @{
            TerminoNormalizado = $reader["TerminoNormalizado"]
            Tipo = $reader["Tipo"]
            Concepto = $reader["Concepto"]
            Contexto = $reader["Contexto"]
            ClienteId = $reader["ClienteId"]
        }
    }
    $conn.Close()

    if ($filasBd.Count -eq 27) {
        Write-Host "  OK: 27 filas en la BD real (ya cargadas, sin duplicados)" -ForegroundColor Green
    } else {
        Write-Host "  FALLO: $($filasBd.Count) filas en la BD real, se esperaban 27" -ForegroundColor Red
        $fallo = $true
    }
    foreach ($f in $filasCsv) {
        $bd = $filasBd[$f.Termino]
        if (-not $bd) {
            Write-Host "  FALLO: '$($f.Termino)' no esta en la BD real" -ForegroundColor Red
            $fallo = $true
            continue
        }
        $contextoCsv = if ([string]::IsNullOrEmpty($f.Contexto)) { "" } else { $f.Contexto }
        if ($bd.TerminoNormalizado -ne $f.TerminoNormalizado -or $bd.Tipo -ne $f.Tipo -or $bd.Concepto -ne $f.Concepto -or $bd.Contexto -ne $contextoCsv -or $bd.ClienteId -isnot [System.DBNull]) {
            Write-Host "  FALLO: '$($f.Termino)' no coincide exactamente entre CSV y BD real" -ForegroundColor Red
            $fallo = $true
        }
    }
    Write-Host "  OK: las 27 filas de la BD real coinciden exactamente con el CSV, ClienteId vacio en todas" -ForegroundColor Green
}
finally {
    if ($conn.State -eq 'Open') { $conn.Close() }
}

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK" -ForegroundColor Green
    exit 0
}
