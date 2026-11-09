<#
.SINOPSIS
Verificacion de la PR #437: "feat(ia): IA_DESACTIVADA y tiempo de espera de 30 s con 1 reintento".

.QUE VERIFICA
Separa el motivo `IA_NO_DISPONIBLE` en dos: `IA_DESACTIVADA` (sin llave, IA apagada o la
funcion apagada en el ambiente -- nunca se llama al proveedor) y `IA_NO_DISPONIBLE` (el
proveedor SI fue llamado y fallo: red, 5xx, timeout real). Tambien baja el timeout por
defecto de 60s x 2 reintentos (~180s) a 30s x 1 reintento (~60s), por debajo del limite de
90s del frontend.

Afecta 3 casos de uso: `sugerir-mapeo`, `normalizar-valores` (MapeoExcel) y `leer-estructura`
(LecturaExcel, de la PR #433 ya mergeada).

.COMO SE VERIFICO EN VIVO
1. Diff acotado (15 archivos, ~70 lineas) revisado completo: `.env.example`,
   `clsOpcionesAnthropic.cs`, `IAsistenteIaPort.cs` (el motivo nuevo), los 3 casos de uso,
   y los tests.
2. Confirmado en codigo que los 3 casos de uso chequean `ia.Habilitada(funcion)` ANTES de
   intentar cualquier llamada de red -- devuelven `IA_DESACTIVADA` sin tocar el proveedor.
   `clsAnthropicAdapter.CompletarAsync` solo lanza `IA_NO_DISPONIBLE` (via `Traducir(ex)`)
   despues de que la llamada real a `cliente.Messages.Create` fallo.
3. Default de codigo (`clsOpcionesAnthropic`: 30s / 1 reintento) coincide exacto con
   `.env.example` (`Anthropic__TimeoutSegundos=30`, `Anthropic__MaxReintentos=1`).
4. Los reintentos de 429/5xx/red los hace el SDK oficial de Anthropic (`AnthropicClient`
   con `MaxRetries`/`Timeout`); no se pudo inspeccionar el codigo fuente del SDK (DLL
   compilada, sin fuente en el paquete NuGet) para confirmar si hay demora de backoff
   ADICIONAL entre reintentos mas alla del timeout por intento -- de haberla, el peor caso
   real podria superar ligeramente los ~60s teoricos que asume el test nuevo
   (`Una_llamada_a_la_IA_no_pasa_de_un_minuto...`), aunque seguiria muy por debajo del
   limite de 90s del frontend. Limitacion conocida, no bloqueante.
5. Tests modificados (`clsRecetasLecturaApiTests.cs` de la PR #433,
   `clsNormalizarValoresApiTests.cs`, `clsSugerirMapeoApiTests.cs`, y los de Application):
   confirmado que solo se actualizo el valor esperado de `IA_NO_DISPONIBLE` a
   `IA_DESACTIVADA` donde corresponde (misma asercion exacta de string, sin debilitar nada),
   y se agrego un caso nuevo que prueba que un proveedor caido SIGUE dando
   `IA_NO_DISPONIBLE` (no se confunde con apagado).
6. `CONTRATO.md`: los 3 endpoints documentan `IA_DESACTIVADA` junto a `IA_NO_DISPONIBLE`
   con la distincion correcta; la seccion general de IA (3.22) sigue documentando por
   separado los `503 IA_APAGADA`/`IA_NO_CONFIGURADA`/`IA_FUNCION_DESHABILITADA` -- eso es
   el camino de endpoints SIN el patron de degradacion suave (ej. el asistente
   conversacional, que esta PR no toca), coexiste sin contradiccion con el `IA_DESACTIVADA`
   nuevo del camino con degradacion suave.
7. Comentario de la PR en GitHub: pide a @scalderon-bs correr una consulta de solo lectura
   sobre `tbl_ia_uso` en la base real para diagnosticar fallas del front -- sigue sin
   respuesta, es informativo/diagnostico, no bloquea esta verificacion.
8. Domain y Application: corridos de punta a punta en un worktree aislado
   (`git worktree add`, rama `pr437`) para no interferir con otras verificaciones en
   paralelo sobre el mismo repo.
9. API: se intento correr contra la BD real de pruebas. El puerto TCP 1433 de
   10.10.40.85 SI responde desde este entorno, pero la conexion SQL real falla
   ("error 40 - no se pudo abrir una conexion con SQL Server", va por named pipes) --
   limitacion de entorno ya vista en verificaciones anteriores (PR #433), no una falla de
   esta PR. Domain y Application, que no dependen de esa BD, quedaron 100% verificados.

.LIMITACIONES CONOCIDAS
- No se corrio Nexo.Api.Tests completo (ver punto 9): no se pudo confirmar el numero
  exacto de "87 fallos de entorno" que la PR afirma que coinciden con QA.
- No se pudo inspeccionar el codigo fuente del SDK oficial de Anthropic (paquete NuGet
  sin codigo fuente) para confirmar si hay demora de backoff entre reintentos mas alla
  del timeout configurado (ver punto 4).
- No se repitio la llamada real a la IA (fuera de alcance de esta verificacion; la PR no
  afirma haberla hecho para este cambio puntual de timeouts/codigos).
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

$fallo = $false

Write-Host "=== 1. Default de codigo coincide con .env.example ===" -ForegroundColor Cyan
$opciones = Get-Content "src/Nexo.Infrastructure/IA/clsOpcionesAnthropic.cs" -Raw
$envExample = Get-Content ".env.example" -Raw

if ($opciones -match "TimeoutSegundos\s*\{\s*get;\s*set;\s*\}\s*=\s*(\d+)") {
    $timeoutCodigo = $matches[1]
} else { $timeoutCodigo = $null }
if ($opciones -match "MaxReintentos\s*\{\s*get;\s*set;\s*\}\s*=\s*(\d+)") {
    $reintentosCodigo = $matches[1]
} else { $reintentosCodigo = $null }

if ($envExample -match "Anthropic__TimeoutSegundos=(\d+)") { $timeoutEnv = $matches[1] } else { $timeoutEnv = $null }
if ($envExample -match "Anthropic__MaxReintentos=(\d+)") { $reintentosEnv = $matches[1] } else { $reintentosEnv = $null }

Write-Host "  Codigo: TimeoutSegundos=$timeoutCodigo, MaxReintentos=$reintentosCodigo"
Write-Host "  .env.example: Anthropic__TimeoutSegundos=$timeoutEnv, Anthropic__MaxReintentos=$reintentosEnv"

if ($timeoutCodigo -ne "30" -or $reintentosCodigo -ne "1" -or $timeoutEnv -ne "30" -or $reintentosEnv -ne "1") {
    Write-Host "  FALLO: no coinciden los valores esperados (30 / 1)" -ForegroundColor Red
    $fallo = $true
} else {
    Write-Host "  OK: codigo y .env.example coinciden en 30s / 1 reintento (peor caso ~60s)" -ForegroundColor Green
}

Write-Host "`n=== 2. IA_DESACTIVADA se decide ANTES de llamar al proveedor (sin red) ===" -ForegroundColor Cyan
$casosDeUso = @(
    "src/Nexo.Application/CasosDeUso/LecturaExcel/clsLecturaExcelCasosDeUso.cs",
    "src/Nexo.Application/CasosDeUso/MapeoExcel/clsSugerirMapeoExcelQuery.cs",
    "src/Nexo.Application/CasosDeUso/MapeoExcel/Normalizacion/clsNormalizarValoresQuery.cs"
)
foreach ($archivo in $casosDeUso) {
    $contenido = Get-Content $archivo -Raw
    if ($contenido -match "Habilitada\([^)]*\)[\s\S]{0,40}clsMotivosSinIa\.Desactivada") {
        Write-Host "  OK: $archivo chequea Habilitada() antes de usar IA_DESACTIVADA" -ForegroundColor Green
    } else {
        Write-Host "  FALLO: $archivo no tiene el patron esperado" -ForegroundColor Red
        $fallo = $true
    }
}

Write-Host "`n=== 3. IA_NO_DISPONIBLE solo sale de una llamada real fallida ===" -ForegroundColor Cyan
$adapter = Get-Content "src/Nexo.Infrastructure/IA/clsAnthropicAdapter.cs" -Raw
if ($adapter -match 'catch \(Exception ex\) when \(!ct\.IsCancellationRequested\)[\s\S]{0,200}Traducir\(ex\)' -and
    $adapter -match '"IA_NO_DISPONIBLE"') {
    Write-Host "  OK: IA_NO_DISPONIBLE sale del catch tras la llamada real a cliente.Messages.Create" -ForegroundColor Green
} else {
    Write-Host "  FALLO: no se encontro el patron esperado en clsAnthropicAdapter.cs" -ForegroundColor Red
    $fallo = $true
}

Write-Host "`n=== 4. Domain y Application (worktree aislado) ===" -ForegroundColor Cyan
$wt = Join-Path $env:TEMP "pr437-verify-$(Get-Random)"
git worktree add $wt pr437 2>&1 | Out-Null
try {
    Push-Location $wt
    $domain = dotnet test tests/Nexo.Domain.Tests --nologo -v minimal 2>&1
    $domainLinea = $domain | Select-String "Correctas!|Con error!" | Select-Object -Last 1
    Write-Host "  Domain: $domainLinea"
    if ($domainLinea -notmatch "Con error:\s*0,\s*Superado:\s*218,.*Total:\s*218") {
        Write-Host "  FALLO: Domain no dio 218/218" -ForegroundColor Red
        $fallo = $true
    } else {
        Write-Host "  OK: Domain 218/218" -ForegroundColor Green
    }

    $app = dotnet test tests/Nexo.Application.Tests --nologo -v minimal 2>&1
    $appLinea = $app | Select-String "Correctas!|Con error!" | Select-Object -Last 1
    Write-Host "  Application: $appLinea"
    if ($appLinea -notmatch "Con error:\s*0,\s*Superado:\s*1243,.*Total:\s*1243") {
        Write-Host "  FALLO: Application no dio 1243/1243" -ForegroundColor Red
        $fallo = $true
    } else {
        Write-Host "  OK: Application 1243/1243" -ForegroundColor Green
    }
    Pop-Location
} finally {
    git worktree remove $wt --force 2>&1 | Out-Null
}

Write-Host "`n=== 5. API: NO se ejecuta aqui (limitacion de entorno, ver .LIMITACIONES CONOCIDAS) ===" -ForegroundColor Yellow

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK (con limitaciones documentadas)" -ForegroundColor Green
    exit 0
}
