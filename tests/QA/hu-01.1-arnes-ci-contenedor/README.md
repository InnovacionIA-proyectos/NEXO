# Verificación QA — S2-HU-01.1 (issue #1, PR #57)

Rama: `1-s2-hu-011-compilar-nexo-clean-por-primera-vez`. Verificación hecha
situándose en esa rama, antes de que se apruebe/mergee el PR #57.

## Qué trae el PR (según su propia descripción, confirmado por diff)
- **Nuevo de verdad (13 archivos, pero solo 3 no están ya en `QA`):** `.dockerignore`, `build/Dockerfile.ci` y `docker-compose.ci.yml` — un arnés que compila las 4 capas + los 3 proyectos de test dentro de un contenedor Linux (`mcr.microsoft.com/dotnet/sdk:10.0`), pensado para que el futuro workflow de CI (issue #4) solo tenga que leer el código de salida del compilador.
- **Los otros 10 archivos** son el mismo fix de `using` que ya está en `QA` desde el PR #51 (`fe35ea0`) — el propio autor de este PR lo señala en la descripción y verificó que son idénticos línea por línea. Confirmado independientemente: `git diff origin/QA <esta-rama> -- <esos-10-archivos>` no muestra ninguna diferencia.

## Cómo se probó
No aplica Playwright (no hay UI) ni pytest (no hay código Python en este PR) — lo que se agrega es infraestructura de build en Docker, así que se verificó ejecutándola de verdad, igual que la va a usar el CI real.

```
node tests/QA/hu-01.1-arnes-ci-contenedor/verify-ci-harness.mjs
```

Este script:
1. `docker compose -f docker-compose.ci.yml build` — construye la imagen del arnés.
2. `docker compose -f docker-compose.ci.yml run --rm build` — corre `dotnet build` de la solución completa (4 capas + 3 test) dentro del contenedor y verifica que el código de salida sea 0.
3. (Extra, no exigido por la issue) corre `dotnet test` de `Domain.Tests` y `Application.Tests` dentro del mismo contenedor — solo para dejar registrado el estado de esos tests en esta rama puntual.

## Resultado

**Arnés de build — ✅ pasa exactamente como describe el PR:**
```
Build succeeded.
    1 Warning(s)  (ASPDEPR005, preexistente, ajeno a este PR)
    0 Error(s)
```
Compila los 7 proyectos (Domain, Domain.Tests, Application, Application.Tests, Infrastructure, Api, Api.Tests) con exit code 0.

**Extra — estado de los tests unitarios en ESTA rama (no en QA):**
- `Domain.Tests`: 40/40 ✅
- `Application.Tests`: **41/43** — fallan `Regla_duplicada_lanza_ConflictException` y `Nueva_ganada_no_duplica_si_ya_existen_tarifas_para_la_cotizacion`.

## Hallazgo — por qué fallan esos 2 (y por qué NO es un problema para mergear)
Esta rama está ~20 commits detrás de `QA`: es anterior al PR #53, que corrigió exactamente estos 2 bugs (ver `tests/QA/README.md` en `QA`, sección "Bugs de lógica de negocio"). El PR #57 **no toca** `ReglaSoloSicetacTexto.cs` ni `MarcarResultadoCommandTests.cs` — al mergear a `QA`, esos 2 archivos se quedan con la versión ya corregida (`QA` no se sobrescribe con la versión vieja de la rama). Es decir: esta falla es un artefacto de probar la rama aislada contra su propia base desactualizada, no un riesgo real de regresión al mergear.

## Conclusión
El arnés de CI en contenedor funciona tal como lo describe el PR #57. Recomendación técnica (ya sugerida por el propio autor en la descripción del PR): dado que los 10 archivos de `using` son 100% redundantes con el PR #51 ya mergeado, lo único que aporta este PR es el arnés — se puede aprobar sin objeciones de mi parte; git debería resolver los 10 archivos duplicados como no-op al mergear por ser contenido idéntico.
