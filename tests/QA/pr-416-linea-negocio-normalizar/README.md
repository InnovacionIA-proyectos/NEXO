# QA — PR #416: Normalizar la línea de negocio del Excel y rechazar valores desconocidos al capturar (#412)

## Resumen

Dos cambios relacionados:

1. **Normalización de línea de negocio en la importación inteligente**
   (`normalizar-valores`): agrega el campo `lineaNegocio` a las reglas
   exactas (diccionario del cliente, catálogo oficial de las 6 líneas,
   un puñado de sinónimos inequívocos: `impo`/`importación` →
   Puerto a interior, `expo`/`exportación` → Interior a puerto) y, si no
   resuelve, a la única llamada a la IA por lote, con lista cerrada de
   las 6 líneas. Deliberadamente **no** incluye "inbound"/"outbound": cada
   cliente los usa distinto, así que esos quedan para el diccionario del
   cliente o la IA con confianza baja.
2. **Rechazo explícito al capturar** (`clsCapturarSolicitudCommand`): antes,
   un texto de línea de negocio que no era ninguna de las 6 se guardaba
   en blanco sin avisar (`ParseLineaNegocio` devolvía `null` silenciosamente).
   Ahora, un texto no vacío que no reconoce ninguna de las 6 líneas
   **rechaza toda la solicitud** con `400 LINEA_NEGOCIO_INVALIDO`, el
   número de línea y el valor recibido — nada se guarda. Vacío sigue
   comportándose igual que antes (sin línea de negocio).
3. `clsLineasDeNegocio` centraliza el catálogo de las 6 líneas y su
   reconocimiento de texto libre (antes duplicado en
   `clsCapturarSolicitudCommand` como método privado); ahora también lo
   usa el command de captura.

## Qué se verificó

1. **Conflicto de merge resuelto** en `CONTRATO.md` y
   `clsCapturaSolicitudesController.cs`: la rama traía el comentario de
   `normalizar-valores` sin la mención a `clienteNit` (de la PR #415,
   mergeada después); se combinaron ambos cambios (línea de negocio +
   `clienteNit`) sin perder ninguno.
2. Diff aislado de la PR (1 commit propio).
3. Revisado que el rechazo al capturar es **atómico**: el test
   `Un_texto_que_no_es_una_de_las_seis_lineas_es_400...` confirma que con
   2 líneas (una válida, una inválida) no se guarda nada — ni la línea
   válida ni la solicitud.
4. Confirmado que los sinónimos de línea de negocio son deliberadamente
   acotados (`impo`/`expo` con sus formas largas, nada de
   "inbound"/"outbound") — documentado en el propio código como decisión
   consciente.
5. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
6. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
7. `dotnet test tests/Nexo.Application.Tests` → **1168/1168** (14 nuevos:
   `clsNormalizarLineaNegocioTests.cs`).
8. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`):
   - **Primera corrida: 1093/1116, 1 error.** Aislado el test sospechoso
     (`FullyQualifiedName~LineaNegocio|~NormalizarValores`, 17 tests) →
     **0 errores**. Repetida la suite **completa** una segunda vez →
     **0 errores, 1094/1116**. Confirmado que el único error de la
     primera corrida fue transitorio (no se reprodujo ni aislado ni en
     una segunda corrida completa), no una regresión de esta PR.
   - 22 omitidas en ambas corridas: las mismas 13+9 ya documentadas en
     las PRs #405 y #414.

## Datos de prueba

Ninguno manual: los tests de integración limpian lo que crean (solicitudes
de prueba) en su propio `finally`.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes. Con esta PR
quedan verificadas **todas** las PRs que estaban abiertas en NEXO al
momento de esta sesión de QA.
