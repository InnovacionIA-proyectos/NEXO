# PR #324 — docs: issues por repositorio, decisiones de Nexo y reorganización de docs/

## Qué cambia

Solo documentación y rutas, sin cambios de código ni comportamiento:
- Documentos nuevos en `docs/backlog/`, `docs/documentacion/`, `docs/pruebas/`.
- Reorganización de `docs/` vía `git mv` (se conserva el historial).
- Referencias corregidas en `README.md`, `CONTRATO.md`, `cubicador/CONTRATO.md`,
  `docs/mensajeria/CUBICACION.md`, `docs/Database/seeder/README.md`, y
  comentarios (no reglas) de los 3 workflows de CI.

No toca cotización/costeo ni ningún cálculo numérico — no aplica la
verificación de valores exactos.

## Qué se verificó (ver `verificar.sh`, script real ejecutado)

1. **Sin secretos reales**: solo nombres de variables (`CUBICADOR_API_KEY`,
   `RABBITMQ_PASSWORD`, etc.), ningún valor real de contraseña/token/cadena de
   conexión en los documentos nuevos o movidos.
2. **Archivos movidos existen en las rutas nuevas**: confirmado para los 6
   casos representativos de la tabla de la PR.
3. **Diff de los 3 workflows de CI**: solo comentarios (rutas de
   documentación citadas), ninguna regla de `on:`/`jobs:`/`steps:` cambiada.
4. **Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación
   correcta, 0 errores (1 warning preexistente ajeno a esta PR) — confirma
   que no hay cambio de comportamiento.
5. **Rama al día con `QA`**: sin commits detrás, confirmado.

## Hallazgo no bloqueante (nuevo, no admitido por la PR)

`docs/documentacion/Nexo_Comparacion_Backend_vs_Entendimiento.md` (documento
NUEVO agregado por esta misma PR), línea 174, todavía cita la ruta VIEJA
`docs/ROLES_Y_PERMISOS.md` en vez de la nueva `docs/documentacion/ROLES_Y_PERMISOS.md`
(a la que el propio archivo fue movido por esta PR). Es distinto de los 3
casos que la PR ya admite como pendientes (comentarios en código C#) — este
es un enlace roto dentro de un documento que la PR introduce de cero.

## Resultado

Sin hallazgos bloqueantes. Un hallazgo menor (enlace a ruta vieja dentro de un
documento nuevo de la propia PR), no bloqueante — es solo texto de
documentación, no afecta build ni funcionamiento.
