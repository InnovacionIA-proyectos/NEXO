# Verificación QA — S2-HU-01.2 (issue #2, PR #58)

Rama: `2-s2-hu-012-limpieza-post-migracion` (se apila sobre la de la HU-01.1).
Verificación hecha situándose en esa rama, antes de que se apruebe/mergee el PR #58.

## Qué trae el PR
- **Borra `src/_to_delete/`** (4 archivos): marcadores dejados por la migración que solo tenían un comentario pidiendo borrado manual ("SUPERADO"/"movido a..."), sin una línea de código. La carpeta estaba fuera del `.sln`/`.csproj`, así que no toca la compilación.
- Actualiza `PENDIENTES.md` y `MIGRACION.md` para reflejar que ese borrado ya se hizo.
- El resto del diff (10 `using`, arnés de CI en Docker) es idéntico a lo ya mergeado en `QA` vía #51 y #57 — confirmado, no aporta nada nuevo.

## Por qué esta herramienta y no otra
Es un cambio **estructural** (borrar archivos sin código, actualizar documentación), no un cambio de comportamiento en runtime. Ni pytest ni Playwright ni un test de xUnit nuevo tienen sentido para el caso de prueba principal ("¿de verdad se fue y no quedó nada roto?") — lo correcto es un script que audite el árbol de archivos, y apoyarse en el arnés de xUnit/Docker ya existente (HU-01.1) para la regresión.

## Cobertura por tipo de prueba

| Tipo | ¿Aplica? | Resultado |
|---|---|---|
| **Estático/estructural** | Sí — es el caso de prueba principal | Ver `verify-limpieza.mjs` abajo: ✅ todo OK |
| **Smoke** (¿sigue compilando?) | Sí | `docker compose -f docker-compose.ci.yml run --rm build` → Build succeeded, 0 errores |
| **Regresión** (¿algo que ya pasaba dejó de pasar?) | Sí | `Domain.Tests` 40/40 ✅. `Application.Tests` 41/43 — mismas 2 fallas preexistentes de la rama desactualizada (ver nota abajo), no causadas por este PR |
| **Integración** (`Api.Tests`) | No aplica | Este PR no toca ningún endpoint, `DbContext` ni comportamiento en runtime — no hay nada nuevo que integrar |
| **Carga y estrés** | No aplica | No hay código ejecutable nuevo; son archivos borrados y documentación |

## Cómo se probó
```
node tests/QA/hu-01.2-limpieza-post-migracion/verify-limpieza.mjs
```
Verifica, contra el árbol de archivos real de esta rama:
1. `src/_to_delete/` ya no existe.
2. `git grep "_to_delete"` no encuentra ninguna referencia en `.cs`/`.csproj`/`.sln`.
3. Los 4 tipos que vivían ahí siguen existiendo en su ubicación definitiva (los que dice `PENDIENTES.md`): confirmé manualmente, antes de escribir el script, el contenido real de los 4 archivos borrados (`git show origin/main:src/_to_delete/...`) — los 4 eran exclusivamente comentarios, cero código, tal como afirma el PR.
4. `PENDIENTES.md` ya no promete un borrado manual pendiente (contradiría el propio cierre de la HU).

Para el smoke/regresión se reutilizó el arnés de la HU-01.1:
```
docker compose -f docker-compose.ci.yml build
docker compose -f docker-compose.ci.yml run --rm build
docker compose -f docker-compose.ci.yml run --rm build dotnet test tests/CabinaFletes.Domain.Tests
docker compose -f docker-compose.ci.yml run --rm build dotnet test tests/CabinaFletes.Application.Tests
```

## Nota sobre las 2 fallas de `Application.Tests`
Mismo caso que en la HU-01.1: esta rama es anterior al PR #53 (que corrigió el bug de tildes en `ReglaSoloSicetacTexto` y el stub mal escrito en `MarcarResultadoCommandTests`). El PR #58 no toca ninguno de esos 2 archivos — al mergear a `QA`, la versión ya corregida se mantiene intacta. No es una regresión de este PR.

## Conclusión
El borrado es seguro, la afirmación del PR ("solo comentarios, cero impacto en compilación") es exacta, y la documentación quedó consistente con el estado real del código. Sin objeciones.
