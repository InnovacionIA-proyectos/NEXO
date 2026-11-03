# QA — PR #425: Decisión go/no-go de la IA en el Cubicador (#370) y trabajo de la rama S6-HU-34-36

## Sobre el tamaño y el alcance de la PR

Esta PR trae **muchos commits de varias issues** (#344, #363, #367, #368,
#369, #370, #374) en una sola rama larga, `S6-HU-34-36-cubicador`. **No es
el mismo problema que la PR #406** (contaminación por mergear una rama de
otro epic no relacionado): aquí todos los commits pertenecen al **mismo
epic** (la IA del Cubicador, historias S6-HU-34 a 36), desarrollados
secuencialmente en una sola rama que nunca se partió en PRs separados.
Confirmado revisando el log completo y el contenido de cada commit: no hay
nada de otro epic (asistente, precio de venta de conexos, etc.) mezclado.

## Resumen por pieza

- **Esfuerzo global de Anthropic: `high` → `medium`** (`clsOpcionesAnthropic.cs`,
  justificado por #363): la evaluación del mapeo de Excel dio 93,8 % de
  acierto en `medium` contra 91,7 % en `high` (diferencia dentro del ruido
  del dataset), con menor costo (US$0,0196 vs US$0,0217 por Excel). **El
  esfuerzo es compartido**: también lo usa el asistente de cotización
  (#371-#376), que nadie había medido en `medium` — riesgo señalado
  explícitamente en el propio comentario del código.
  - **Esta misma PR cierra ese riesgo**: incluye la evaluación adversaria
    del asistente (#374, `tests/evaluacion-ia/asistente/`) corrida contra
    el **modelo real** (`claude-sonnet-5-5`, esfuerzo `medium`) — **11 de
    11** reglas de seguridad respetadas (nunca bajar del piso SICE-TAC,
    todo valor con fuente, un cambio queda propuesto nunca aplicado, no
    rodear herramientas sin acceso, etc.). Informe en
    `tests/evaluacion-ia/asistente/informes/2026-10-29_1733_asistente.md`.
    El arnés tiene autoprueba (detecta 11/11 incumplimientos simulados —
    confirmado, ver abajo) y documenta honestamente qué NO mide (escalada
    en varios turnos, evasión por idioma, una sola corrida sin repetición).
- **`GET /recomendaciones/explicacion` en el Cubicador** (#369): contraparte
  exacta del endpoint ya verificado en la PR #420 de NEXO — misma ruta,
  mismos parámetros. Siempre 200, `explicacion: null` si no hay llave, si
  el proveedor falla, o si el texto trae una cifra que no sale del motor
  (verificado con `verificacion.py`, que descarta el texto ENTERO si
  cualquier cifra no es del motor).
- **Servicio de interpretación de texto libre** (#368, `application/interpretacion/`):
  implementado y evaluado (23/23 casos exactos, 7/7 rechazos correctos),
  pero **NO conectado a ningún router ni flujo real todavía** — confirmado
  revisando `api/dependencias.py` y `api/routers/`: no hay ninguna
  referencia a `ServicioInterpretacion`. Queda para una HU futura (sección
  7 del acta). Código inerte, sin riesgo de producción.
- **Fix de concurrencia en el cierre del modo sombra** (#367,
  `ia_sombra/servicio.py`): `cerrar()` ahora espera con `asyncio.gather`
  a que las tareas canceladas realmente terminen, en vez de solo pedir la
  cancelación. Bug real, medido por el autor: la suite se colgaba 3 de
  cada 5 corridas en `test_la_respuesta_no_espera_al_modelo`.
- **`docs/ia/ACTA-DECISION-IA-CUBICADOR.md`** (#370): documento de decisión
  honesto y riguroso — **no-go** a que la IA decida la flota del Cubicador,
  por un error sistemático de seguridad (caso S-19: la IA recomienda 1
  Minimula cuando hacen falta 2, falló 4/4 veces); **go** solo a usos que
  no deciden (modo sombra para recolectar datos, interpretación de texto
  libre, explicación). Documenta también los criterios corregidos para
  cualquier reevaluación futura.
- Mejoras de pruebas de infraestructura (#344): la prueba contra SQL Server
  ya no se puede saltar en CI, cubre también el camino de escritura, y
  corrige comparaciones de booleanos con `=` en vez de `IS` (SQL Server
  rechaza `IS` fuera de `NULL`).

## Hallazgo encontrado y corregido durante esta verificación

La primera corrida de `Nexo.Api.Tests` dio **2 errores reales** (no
transitorios, reproducidos aislados): `clsAnthropicAdapterTests` y
`clsAnthropicConversacionTests` seguían afirmando `"high"` como el
esfuerzo enviado por defecto, sin actualizar tras el cambio a `"medium"`
(un tercer test, `clsAsistenteIaConfiguracionTests.cs`, sí se había
actualizado). Confirmado que no es una regresión de lógica: un tercer uso
de `"high"` en la misma clase (`Un_esfuerzo_desconocido_cae_al_esfuerzo_por_defecto`)
prueba un camino de código distinto (fallback ante un valor de esfuerzo
**irreconocible**, que sigue siendo `High` en `clsAnthropicAdapter.EsfuerzoDe`,
no afectado por este cambio) y correctamente no falló.

Corregido con autorización de Kevin: actualicé las 2 aserciones a
`"medium"` y pusheé el commit `b0aacd0` a la rama de la PR
(`S6-HU-34-36-cubicador`). Verificado que compila y que los 2 tests pasan
tras el fix, y que la suite completa vuelve a dar 0 errores.

## Qué se verificó

1. Confirmado que esta rama larga es un epic coherente, no contaminación.
2. Merge de `QA` en la rama de verificación: limpio, sin conflictos.
3. Revisados en detalle: el cambio de esfuerzo global, la evaluación
   adversaria del asistente, el endpoint de explicación del Cubicador
   (contraparte de PR #420), el servicio de interpretación (confirmado
   inerte), el fix de concurrencia del modo sombra, y el acta de decisión.
4. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
6. `dotnet test tests/Nexo.Application.Tests` → **1182/1182**.
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`): primera corrida con 2 errores
   reales (ver hallazgo arriba); corregidos y verificada de nuevo →
   **1141/1163, 0 errores**, 22 omitidas (las esperadas de siempre).
8. `python -m pytest tests -q` en `cubicador/` → **421/422**, 1 omitido
   (`test_plan_cargue_repo_contra_sqlserver.py`, requiere un SQL Server
   local/desechable vía Docker, no disponible en esta máquina en el
   momento de la verificación — omisión esperada, el propio test lo
   declara así).
9. Autoprueba del arnés adversario del asistente
   (`dotnet run --project tests/evaluacion-ia/asistente/arnes -- --autoprueba`)
   → **11 de 11** incumplimientos simulados detectados.

## Datos de prueba

Ninguno: esta PR no toca la BD compartida de QA de NEXO. El modo sombra
del Cubicador sigue apagado (según el acta de decisión, la tabla
`dbo_cubicador.iaSombra` no existe todavía en la BD de QA del Cubicador).

## Resultado

Verificación técnica completa. Un hallazgo real encontrado y corregido
(2 tests con aserción desactualizada), sin hallazgos bloqueantes
restantes.
