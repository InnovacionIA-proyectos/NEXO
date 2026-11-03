# QA — PR #392: Normalizar textos libres del Excel (vehículo, carrocería, ciudad) con IA

## Resumen

Nuevo endpoint `POST /api/captura-solicitudes/normalizar-valores`, último de
la cadena 389→390→391→392 (diccionario de cliente, sugerir mapeo, aplicar
mapeo, normalizar valores). Lleva los textos libres de vehículo, carrocería
y ciudad de un Excel de cliente a los valores oficiales de NEXO.

Esta PR dependía de la #391 (aún sin mergear al momento de la primera
revisión), así que la verificación completa de suites se repitió dos veces:
una vez con el diff aislado contra la rama de #391 (para revisar el código
propio de #392 sin ruido), y otra después de que #391 se corrigió y mergeó,
para confirmar el estado final limpio.

No se encontraron hallazgos bloqueantes en ninguna de las dos rondas.

## Qué hace

1. **Reglas exactas, sin IA**: diccionario del cliente, alias de vehículos,
   catálogo oficial (código o descripción idéntica), y para ciudades código
   DANE o nombre de municipio (desambiguando por departamento cuando hace
   falta, p. ej. "Santa Rosa, Cauca").
2. **Una sola llamada a la IA** por todos los textos distintos que las reglas
   no resolvieron (no una por fila). Antes de enviar, cada texto pasa por el
   mismo enmascarado de PII de la PR #390 (`clsEnmascaradorDeCeldas.Celda`):
   si trae datos sensibles, no se envía a la IA.
3. **Validación estricta de la respuesta**: lo que el modelo elige solo se
   acepta si la clave está en la lista cerrada (catálogo o candidatos) que
   se le dio para ese texto específico, y la confianza llega al mínimo
   (0.7). Si no, queda `sin_resolver` para revisión manual.
4. No persiste nada; sin cambios de base de datos.

## Puntos de diseño verificados

- `clsSimilitudDeTexto` (distancia de Levenshtein normalizada) se usa
  **solo** para elegir qué candidatos de ciudad mostrarle al modelo, nunca
  para asignar directamente — una coincidencia aproximada nunca se cuela
  como asignación automática.
- Refactor de reuso: se extrajo `clsValoresDelCliente.CargarAsync` (antes
  duplicado/privado dentro de `clsAplicarMapeoExcelQuery`) a una clase
  compartida, reutilizada también por el mapeo de columnas (#391).

## Cómo se verificó

1. Diff aislado del código propio de #392 vía
   `git diff origin/360-aplicar-mapeo-excel <head-de-392>` (para no mezclarlo
   con el código de #391, que aún no estaba en QA en la primera ronda).
2. Segunda ronda tras el merge de #391 ya corregida y mergeada en QA.
3. `dotnet build -c Release` → 0 errores (hubo que corregir un `using`
   duplicado cosmético dejado por un merge de QA intermedio, sin impacto
   funcional).
4. `dotnet test tests/Nexo.Domain.Tests` → 218/218.
5. `dotnet test tests/Nexo.Application.Tests` → 827/827 (incluye 50 tests
   nuevos de `Normalizacion`).
6. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → 847/857, 10 omitidas esperadas
   (servicios externos no disponibles en este entorno).

Ver `verificar.ps1` para el script runnable equivalente.

## Resultado

Aprobada y mergeada (`gh pr merge 392 --merge`). Con esta PR se cierra la
cadena completa de PRs de mapeo de Excel con IA: #389 → #390 → #391 → #392.
