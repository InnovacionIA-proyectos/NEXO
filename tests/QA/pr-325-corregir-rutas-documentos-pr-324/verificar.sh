#!/usr/bin/env bash
# Verificación QA de la PR #325 (docs: corregir rutas de documentos movidos
# en la PR #324). Correr desde la raíz del repo, con la rama de la PR
# descargada localmente.
set -e

echo "=== 1. Rutas viejas específicas que esta PR dice haber corregido ==="
grep -rln \
  "docs/ROLES_Y_PERMISOS\.md\|docs/2026-10-09_HU-BACKEND\.md\|docs/2026-10-09_HU-FRONTEND\.md\|docs/2026-10-05_REPARACIONES-FINALES-TARIFAS-PLAZA\.md\|docs/REVISION-NEXO-PASO-A-PASO\.md\|[^/]docs_raw/" \
  --include="*.md" --include="*.cs" --include="*.yml" --include="*.py" . \
  | grep -v "^tests/QA/" || echo "(sin resultados — correcto)"

echo
echo "=== 2. Otras rutas viejas de la reorganización original de la PR #324 ==="
grep -rln \
  "docs/CI_CD_RUTAS_COMPARTIDAS\.md\|docs/COTIFLY-\|docs/CUBICADOR-CONTRATO-\|docs/CHECKLIST_DESPLIEGUE\.md\|docs/REGLAS-CAPTURA-POR-LINEA-DE-NEGOCIO\.md\|docs/diccionario-datos-cabinafletes\.md\|docs/VERIFICACION_NEXO_CUBICADOR_2026-09-14\.md\|docs/Testing/PRUEBAS-LOCALES\.md" \
  --include="*.md" --include="*.cs" --include="*.yml" --include="*.py" . \
  | grep -v "^tests/QA/" || echo "(sin resultados — correcto)"

echo
echo "=== 3. Confirmar que cada ruta nueva apunta a un archivo que existe ==="
for f in \
  "docs/documentacion/ROLES_Y_PERMISOS.md" \
  "docs/backlog/2026-10-09_HU-BACKEND.md" \
  "docs/backlog/2026-10-09_HU-FRONTEND.md" \
  "docs/backlog/2026-10-05_REPARACIONES-FINALES-TARIFAS-PLAZA.md" \
  "docs/documentacion/REVISION-NEXO-PASO-A-PASO.md" \
  "docs/docs_raw/conexos costos.xlsx" \
  "docs/docs_raw/fletes-plaza-normalizado-plaza.csv"
do
  if [ -f "$f" ]; then echo "OK    $f"; else echo "FALTA $f"; fi
done

echo
echo "=== 4. Build de sanity (solo texto, no debería haber cambio de código) ==="
dotnet build Nexo.Clean.sln -c Release -v minimal
