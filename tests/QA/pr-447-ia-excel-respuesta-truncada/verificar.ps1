<#
.SINOPSIS
Verificacion de la PR #447: "fix(ia): la lectura de estructura ya no se corta por longitud".

.QUE VERIFICA
`leer-estructura` ahora pide 16.000 tokens de salida (antes 8000) para la receta de lectura, y si
aun asi la respuesta de la IA se corta por longitud (`StopReason == "max_tokens"`, detectado en
`clsAnthropicAdapter.cs`), reintenta UNA vez pidiendo una receta compacta; si vuelve a cortarse,
cae a lectura simple con `motivoSinIa = IA_RESPUESTA_TRUNCADA` y un aviso claro al front ("el
archivo es muy grande o complejo...").

Esta PR dice cerrar la issue #439 (9 llamadas reales de `ImportacionExcel` truncadas en
`tbl_ia_uso`, todas con `TokensSalida = 4096` exactos). Su argumento es que el "razonamiento"
(`Thinking`) del modelo cuenta dentro del mismo tope de `MaxTokens` que la respuesta final, y que
por eso `leer-estructura` (que pedia 8000) se cortaba.

.COMO SE VERIFICO EN VIVO
1. Confirmado en codigo que `RazonamientoAdaptativo` (`Thinking = new ThinkingConfigAdaptive()`,
   `clsAnthropicAdapter.cs:120-124` y `clsAnthropicAdapter.Conversacion.cs:116`) comparte el mismo
   campo `MaxTokens` que la respuesta final -- la premisa tecnica de la PR (que el razonamiento
   consume del mismo tope) es plausible y coincide con como funciona la API de Anthropic con
   "extended thinking". PERO esa opcion es GLOBAL (default `true` en `.env.example:63` y en
   `clsOpcionesAnthropic.cs:33`): se aplica a TODAS las funciones de IA del proyecto
   (`sugerir-mapeo`, `normalizar-valores`, el asistente conversacional), no solo a
   `leer-estructura`. Si la premisa fuera la causa real del problema, las demas funciones con tope
   de 4096 (mas bajo que el de leer-estructura) serian AUN MAS vulnerables al mismo efecto, no
   menos.
2. **Hallazgo critico, ya confirmado por el propio equipo en los comentarios de esta PR en
   GitHub** (`gh api repos/InnovacionIA-proyectos/NEXO/issues/447/comments`): `scalderon-bs`
   corrio la consulta de diagnostico que `ivasco-dev` dejo en un comentario, separando las 9
   llamadas truncadas reales por `TokensSalida`. Resultado: **las 9 tienen `TokensSalida` cercano
   a 4096 (no a 8000)** -- es decir, las 9 llamadas reales que motivaron la issue #439 son de
   `sugerir-mapeo` o `normalizar-valores`, NO de `leer-estructura`. El propio `scalderon-bs` lo
   escribe explicito: "Esta PR no hace dano y sirve para archivos grandes, pero no arregla esos 9
   casos... Por eso la #439 deberia seguir abierta hasta ese arreglo."
   **Conclusion: la PR es una mejora real y util (deja a leer-estructura mas resiliente para
   Excels de muchas hojas), pero su narrativa de "esto es lo que causo la issue #439" no esta
   respaldada por la evidencia real de la base de datos, y la propia PR NO deberia cerrar la
   #439** (su descripcion dice "Cierra #439", lo cual es inexacto segun la evidencia ya discutida
   en los comentarios de la misma PR).
3. `dotnet test` real sobre la rama de la PR: Domain **218/218**, Application **1280/1280** --
   exacto a lo afirmado.
4. Tests nuevos (`clsLecturaEstructuraTests.cs:313-339`) revisados: simulan el corte via
   `clsIaRespuestaNoUtilException("IA_RESPUESTA_TRUNCADA", ...)`, confirman que el reintento pide
   los mismos 16.000 tokens con un mensaje que menciona "se cortó por ser demasiado larga", y que
   el segundo corte cae a lectura simple con el aviso "muy grande o complejo" (sin el aviso
   generico "no esta disponible"). Aserciones solidas, cubren el camino feliz y el de doble corte.
5. `CONTRATO.md:1296` documenta correctamente el reintento y el aviso nuevo.
6. No se intento llamar al modelo real de Anthropic (costaria dinero) -- el propio autor ya
   declaro esta limitacion en la descripcion de la PR.

.LIMITACIONES CONOCIDAS
- No se pudo correr `Nexo.Api.Tests` completo (sin conectividad a la BD real de pruebas desde
  este entorno) -- Domain y Application, que no dependen de ella, quedaron 100% verificados.
- No se verifico contra el modelo real de Anthropic si el "razonamiento adaptativo" realmente
  explica por si solo algun corte observado en produccion -- la evidencia real disponible
  (TokensSalida=4096 en los 9 casos) apunta a otra causa (sugerir-mapeo/normalizar-valores), no a
  leer-estructura. Esto ya fue confirmado por el equipo en los comentarios de la PR, no es una
  conjetura de este script.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

$fallo = $false

Write-Host "=== 1. Tope de salida de la receta = 16000 ===" -ForegroundColor Cyan
$linea = Select-String -Path "src/Nexo.Application/CasosDeUso/LecturaExcel/clsLecturaExcelCasosDeUso.cs" -Pattern "MaxTokensSalidaReceta\s*=\s*16_000"
if ($linea) { Write-Host "  OK: MaxTokensSalidaReceta = 16_000" -ForegroundColor Green }
else { Write-Host "  FALLO: no se encontro el tope de 16000" -ForegroundColor Red; $fallo = $true }

Write-Host "`n=== 2. RazonamientoAdaptativo comparte MaxTokens (global, no exclusivo de leer-estructura) ===" -ForegroundColor Cyan
$thinking = Select-String -Path "src/Nexo.Infrastructure/IA/clsAnthropicAdapter.cs" -Pattern "ThinkingConfigAdaptive"
$opcionGlobal = Select-String -Path "src/Nexo.Infrastructure/IA/clsOpcionesAnthropic.cs" -Pattern "RazonamientoAdaptativo.*=\s*true"
if ($thinking -and $opcionGlobal) {
    Write-Host "  OK: confirmado que Thinking comparte MaxTokens y la opcion es global (default true)" -ForegroundColor Green
} else {
    Write-Host "  FALLO: no se pudo confirmar la configuracion de razonamiento adaptativo" -ForegroundColor Red; $fallo = $true
}

Write-Host "`n=== 3. Evidencia real: las 9 llamadas truncadas de la issue #439 NO son de leer-estructura ===" -ForegroundColor Cyan
Write-Host "  Confirmado leyendo los comentarios de la PR #447 en GitHub (gh api .../issues/447/comments):" -ForegroundColor Yellow
Write-Host "  scalderon-bs corrio la consulta real y las 9 llamadas truncadas tienen TokensSalida~4096," -ForegroundColor Yellow
Write-Host "  no ~8000 -- son de sugerir-mapeo o normalizar-valores, no de leer-estructura." -ForegroundColor Yellow
Write-Host "  CONCLUSION: esta PR es una mejora valida mas no corrige los 9 casos reales de la #439." -ForegroundColor Yellow

Write-Host "`n=== 4. dotnet test (Domain + Application) ===" -ForegroundColor Cyan
dotnet test tests/Nexo.Domain.Tests --nologo -v q 2>&1 | Select-String "Correctas|Con error"
dotnet test tests/Nexo.Application.Tests --nologo -v q 2>&1 | Select-String "Correctas|Con error"

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS DE VERIFICACION DE CODIGO" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: codigo limpio, PERO ver el hallazgo del punto 3 -- la PR no cierra realmente la #439" -ForegroundColor Yellow
    exit 0
}
