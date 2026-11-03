# Verificación QA — S2-HU-01.7 (issue #50, PR #60)

Rama: `50-s2-hu-017-reorganizar-4-capas-encarpetado` (se apila sobre HU-01.1/01.2/02.1).
Verificación hecha situándose en esa rama, antes de aprobar/mergear el PR #60.

## Qué trae el PR
Reorganización masiva (222 archivos, +2788/-1401): Domain por agregado del event storming, Application en `CasosDeUso/<Contexto>/`, Infrastructure y Api por contexto. `Commands`/`Queries` desaparecen como carpetas técnicas. El PR afirma que las "reglas de frontera" (Domain no importa ninguna capa; Application no importa Infrastructure ni Api) se mantienen, verificadas por búsqueda.

## ⚠️ Conflicto real encontrado (no solo de ruta) — ya resuelto con decisión de negocio confirmada

Al intentar el merge contra `QA` apareció **1 solo conflicto real** en `ReglaSoloSicetacTexto.cs` (de 222 archivos). No era un conflicto mecánico de "se movió de carpeta": esta rama es anterior al PR #53 (que corrigió que "Bogotá"/"BOGOTA" no se detectaran como duplicados) **y además contiene su propio arreglo independiente para el mismo test, con una conclusión opuesta**: en vez de corregir el normalizador, el autor ajustó el test para esperar el comportamiento *actual* (con tilde), dejando la detección de duplicados sensible a tildes.

Se escaló la pregunta de negocio ("¿'Bogotá' y 'Bogota' son la misma ruta para esta regla?") antes de tocar nada. **Respuesta confirmada: sí, son la misma ruta** (accent-insensitive) — se mantiene el comportamiento del PR #53.

Resuelto en el commit de merge `d7ff2e5`:
- `ReglaSoloSicetacTexto.cs`: ruta/namespace nuevo de la reorganización (`CasosDeUso.ReglasSoloSicetac`) + cuerpo del método ya corregido (delega en `Domain.Common.Texto.Normalizar`).
- `CrearReglaSoloSicetacCommandTests.cs`: el test `Regla_duplicada_lanza_ConflictException` esperaba `"BOGOTÁ"` (con tilde) en esta rama — se corrigió a `"BOGOTA"` para que sea consistente con la decisión y con el otro test del mismo archivo (que ya esperaba sin tilde desde el PR #53, y con el que había quedado contradictorio tras el auto-merge sin que git lo marcara como conflicto — los 2 tests estaban en líneas distintas del mismo archivo, así que git los combinó sin avisar).
- `MarcarResultadoCommandTests.cs`: se auto-merge sin conflicto y sin necesitar ajuste — esta rama arregla el mismo bug del PR #53 con una vía distinta pero compatible (fija `c.Id = cotizacionId` en vez de cambiar a qué Guid se le hace `Setup` al mock); ambas correcciones apuntan al mismo valor tras el merge, doblemente correcto.

## Cobertura por tipo de prueba

| Tipo | ¿Aplica? | Resultado |
|---|---|---|
| **Reglas de frontera** (arquitectura) | Sí — es la afirmación central del PR | ✅ Verificado con script propio (ver abajo), 0 violaciones en ambos lados |
| **Smoke** (¿compila tras mover 222 archivos?) | Sí | ✅ Build succeeded, 0 errores, tanto en la rama aislada como después del merge |
| **Regresión** | Sí | `Domain.Tests` 40/40 ✅, `Application.Tests` **43/43** ✅ — coincide exacto con lo que afirma el PR ("83 de 83 pruebas en verde") |
| **Integración** (`Api.Tests`) | No verificable aquí | Mismo bloqueo de siempre: requiere BD SQL Server corporativa (issue #3/#4). El build de `Api.Tests` sí compila limpio |
| **Carga y estrés** | No aplica | Es una reorganización de archivos/namespaces, no un cambio de comportamiento en runtime — no hay nada nuevo que cargar |

## Cómo se probó
```
node tests/QA/hu-01.7-reorganizacion-4-capas/verify-fronteras.mjs
```
Automatiza la búsqueda que el PR describe manualmente: confirma que ningún archivo de `Domain` importa `Application`/`Infrastructure`/`Api`, y que ningún archivo de `Application` importa `Infrastructure`/`Api`. Autoprobado insertando una violación deliberada y confirmando que el script la detecta (exit 1), luego revirtiéndola.

Para smoke/regresión se reutilizó el arnés de la HU-01.1:
```
docker compose -f docker-compose.ci.yml build
docker compose -f docker-compose.ci.yml run --rm build
docker compose -f docker-compose.ci.yml run --rm build dotnet test tests/Nexo.Domain.Tests
docker compose -f docker-compose.ci.yml run --rm build dotnet test tests/Nexo.Application.Tests
```
Corrido dos veces: en la rama aislada (antes del merge, para descartar que el problema viniera de la reorganización en sí) y después del merge con `QA` (para confirmar la resolución del conflicto).

## Conclusión
La reorganización es sólida: las reglas de frontera se cumplen de verdad (no solo de palabra) y la regresión completa pasa. El único conflicto real ya se resolvió con una decisión de negocio explícita y confirmada, dejando el comportamiento de "solo SICE-TAC" consistente entre todos los tests. Sin objeciones para mergear.
