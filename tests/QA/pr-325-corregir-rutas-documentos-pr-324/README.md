# PR #325 — docs: corregir rutas de documentos movidos en la PR #324

## Qué cambia

Corrige 19 referencias de ruta (en 7 archivos) que quedaron apuntando a la
ubicación vieja tras la reorganización de `docs/` de la PR #324 — incluye el
hallazgo que reporté en esa verificación
(`docs/ROLES_Y_PERMISOS.md` → `docs/documentacion/ROLES_Y_PERMISOS.md` en
`Nexo_Comparacion_Backend_vs_Entendimiento.md`) y 18 referencias más del mismo
patrón que el autor encontró en la misma revisión. Solo texto, sin cambios de
código ni comportamiento.

## Qué se verificó (ver `verificar.sh`, script real ejecutado)

1. **El hallazgo reportado está corregido**: confirmado en el diff de
   `Nexo_Comparacion_Backend_vs_Entendimiento.md` línea 171 (ahora apunta a
   `docs/documentacion/ROLES_Y_PERMISOS.md`).
2. **Búsqueda independiente de las rutas viejas específicas** que la PR dice
   haber corregido (`docs/ROLES_Y_PERMISOS.md`, `docs/2026-10-09_HU-*.md`,
   `docs/2026-10-05_REPARACIONES-...md`, `docs/REVISION-NEXO-PASO-A-PASO.md`,
   `docs_raw/` sin prefijo) en TODO el repo, no solo en los 7 archivos que
   toca la PR: solo quedan los 3 comentarios en código C# que la propia PR
   admite dejar fuera a propósito (no toca código fuente) y evidencia
   histórica de `tests/QA/` de PRs anteriores — nada nuevo sin corregir.
3. **Búsqueda de las rutas viejas de la reorganización original** (de la PR
   #324, no solo las que esta PR menciona): mismo resultado, sin pendientes
   nuevos.
4. **Cada ruta nueva apunta a un archivo que existe**: confirmado para los 7
   casos representativos del diff.
5. **Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación
   correcta, 0 errores (1 warning preexistente ajeno a esta PR).
6. **Rama al día con `QA`** (incluye la evidencia de la PR #324), sin
   necesidad de merge.

## Resultado

Sin hallazgos. Corrige correctamente el hallazgo reportado en la PR #324 y de
paso arregla 18 referencias más del mismo patrón, sin dejar nada nuevo roto.
