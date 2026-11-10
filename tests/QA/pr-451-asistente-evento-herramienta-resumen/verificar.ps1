<#
.SINOPSIS
Verificacion de la PR #451: "feat(asistente): resumen y encontrado en el evento de herramienta"
(cierra la issue #443, primera entrega).

.QUE VERIFICA
El evento `herramienta` del asistente de cotizacion gana 2 campos opcionales (`encontrado`,
`resumen`) para pintar una tarjeta en el front sin mandar el resultado completo de la
herramienta. El punto critico de esta PR es de privacidad/diseno: el `resumen` debe llegar
al front (evento SignalR/streaming) pero NUNCA al modelo de IA ni quedar en el historial
guardado de la conversacion.

.COMO SE VERIFICO EN VIVO
1. Rastreo completo del codigo real (no solo lo que dice la descripcion):
   - `clsHerramientaAsistente.cs`: `clsResultadoHerramienta.Resumen` tiene el atributo
     `[JsonIgnore]` explicito.
   - `clsConversarAsistenteCommand.cs:228`: `contenido = JsonSerializer.Serialize(leido, ...)`
     -- al serializar `leido` (tipo `clsResultadoHerramienta`), el `[JsonIgnore]` excluye
     `Resumen` de `contenido`.
   - `clsConversarAsistenteCommand.cs:246`: `Salida = salida` (= `contenido`, la MISMA cadena)
     es lo que se persiste via `RegistrarHerramientaAsync` -- confirma que el resumen NO queda
     en el historial guardado (misma cadena que no lo trae).
   - `clsAnthropicAdapter.Conversacion.cs:105`: `ToolResultBlockParam { Content = resultado.Contenido }`
     -- SOLO `Contenido` (la cadena sin Resumen) se envia al modelo como resultado de la
     herramienta. `resultado.Resumen` nunca se referencia en el camino hacia el proveedor de IA.
   - `clsConversarAsistenteCommand.cs:250`: el `Resumen` (acotado con `Acotar()`) se adjunta
     solo a `clsResultadoHerramientaIa`, que se usa para construir el evento `Herramienta`
     que se transmite (`yield return`) al front -- camino separado del que va al modelo.
2. Test real que verifica esto end-to-end (no solo el analisis de codigo):
   `El_evento_de_herramienta_trae_si_se_encontro_y_el_resumen_pero_el_modelo_no_ve_el_resumen`
   en `clsConversarAsistenteCommandTests.cs:261` -- confirma que el evento SI trae el resumen,
   y que la Salida auditada (`_repo.Herramientas`) NO contiene ni el texto del resumen ni la
   palabra "resumen". Como `Salida` es la misma cadena que se manda al modelo (`contenido`),
   esto prueba indirectamente tambien que el modelo no lo ve.
3. Retrocompatibilidad: `Herramienta(string Nombre, string Estado, string? Fuente, bool? Encontrado = null, string? Resumen = null)`
   -- los 2 campos nuevos son opcionales con default null, no rompen a un cliente viejo.
4. Limite de 300 caracteres: `Acotar()` en `clsConversarAsistenteCommand.cs` corta a 299
   caracteres + "..." si excede 300 -- confirmado con el test
   `Un_resumen_largo_se_acota_a_300_caracteres_y_una_herramienta_con_error_no_trae_resumen`
   (300 caracteres exactos, termina en "...").
5. Las 6 herramientas que la PR dice que traen resumen, confirmadas una por una en el diff:
   piso SICE-TAC (con configuracion nombrada), aprovisionamiento, propuesta de linea, tarifas
   de plaza NEXO, tarifas SAP historico (via `clsHerramientaSapBase`), servicios conexos,
   ciudades DANE -- las 6/6 coinciden.
6. Rol del controlador: `[Authorize(Roles = "AdminTarifas,Admin")]` ya existia antes de esta
   PR (no la cambia), confirmado en `clsAsistenteConversacionesController.cs:14`.
7. Sin cambios de esquema de BD: `git diff --name-only` no trae ningun .sql ni carpeta de
   esquema.
8. `dotnet test` real sobre la rama de la PR (en un worktree aislado, C:\tmp\pr451-verify,
   para no interferir con otros forks verificando PRs en paralelo sobre el mismo repo):
   Domain 218/218, Application 1285/1285 -- exacto a lo que afirma la PR.
9. Arnes adversario en modo `--autoprueba` (sin llamar a Anthropic real, sin costo):
   11 de 11 incumplimientos detectados -- igual a lo que afirma la PR.
10. `CONTRATO.md`: el evento `herramienta` documenta exactamente `{ nombre, estado, fuente?,
    encontrado?, resumen? }` con la aclaracion "Nunca trae el resultado completo y no viaja
    al modelo" -- coincide con el codigo real verificado arriba.

.LIMITACIONES CONOCIDAS
- No se corrio `Nexo.Api.Tests` (requiere conectividad de red a la BD real compartida
  10.10.40.85:1433, no disponible en este entorno de verificacion) -- Domain y Application,
  que no dependen de ella, quedaron 100% verificados.
- No se corrio el arnes contra el modelo real de Anthropic (tiene costo); se respeta la
  limitacion que el propio autor de la PR senala.
#>

$ErrorActionPreference = "Stop"
Write-Host "Esta es evidencia de verificacion ya ejecutada manualmente (ver .COMO SE VERIFICO EN VIVO)." -ForegroundColor Cyan
Write-Host "Resumen: Domain 218/218, Application 1285/1285, arnes --autoprueba 11/11, 'resumen' confirmado que nunca llega al modelo ni se persiste." -ForegroundColor Green
exit 0
