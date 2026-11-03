#!/usr/bin/env bash
# Verificación QA de la PR #324 (docs: issues por repositorio, decisiones de
# Nexo y reorganización de docs/). Correr desde la raíz del repo, con la rama
# de la PR descargada localmente.
set -e

echo "=== 1. Buscar nombres de variables de secretos (no deben traer el VALOR real) ==="
grep -rniE "password|pwd=|api[_-]?key|secret|token|connectionstring|Server=.*;.*User" \
  docs/backlog docs/documentacion docs/integraciones docs/pruebas docs/despliegue docs/archivo \
  | grep -viE "ejemplo|placeholder|<.*>|\.xxx|TODO" || echo "(sin resultados distintos de nombres de variable)"

echo
echo "=== 2. Buscar valores de contraseña reales (clave=valor, no nombre de variable) ==="
grep -rnE "(Password|PASSWORD|Pwd|clave)\s*[:=]\s*[A-Za-z0-9@#\$%^&*_+=!-]{6,}" docs/ \
  | grep -viE "RABBITMQ_PASSWORD|MSSQL_SA_PASSWORD|ALERTAS_SMTP_CLAVE|<.*clave|<.*password|CUBICADOR_API_KEY|clave-fuerte|tu-clave|PASSWORD=\$" \
  || echo "(sin resultados)"

echo
echo "=== 3. Confirmar que los archivos movidos existen en las rutas nuevas ==="
for f in \
  docs/despliegue/CI_CD_RUTAS_COMPARTIDAS.md \
  docs/documentacion/ROLES_Y_PERMISOS.md \
  docs/Database/diccionario-datos-cabinafletes.md \
  docs/pruebas/PRUEBAS-LOCALES.md \
  docs/integraciones/COTIFLY-CONTRATO.md \
  docs/archivo/VERIFICACION_NEXO_CUBICADOR_2026-09-14.md
do
  if [ -f "$f" ]; then echo "OK   $f"; else echo "FALTA $f"; fi
done

echo
echo "=== 4. Referencias a rutas VIEJAS que deberían haberse actualizado (fuera de tests/QA, evidencia histórica) ==="
grep -rln \
  "docs/CI_CD_RUTAS_COMPARTIDAS\.md\|docs/COTIFLY-\|docs/CUBICADOR-CONTRATO-\|docs/CHECKLIST_DESPLIEGUE\.md\|docs/ROLES_Y_PERMISOS\.md\|docs/REGLAS-CAPTURA-POR-LINEA-DE-NEGOCIO\.md\|docs/diccionario-datos-cabinafletes\.md\|docs/VERIFICACION_NEXO_CUBICADOR_2026-09-14\.md\|docs/Testing/PRUEBAS-LOCALES\.md" \
  --include="*.md" --include="*.cs" --include="*.yml" --include="*.py" . \
  | grep -v "^tests/QA/" || echo "(sin resultados)"

echo
echo "=== 5. Diff real de los workflows de CI (debe ser solo comentarios) ==="
git diff "$(git merge-base origin/QA HEAD)" HEAD -- .github/workflows/ci-cd-api.yml .github/workflows/ci-cd-cubicador-api.yml .github/workflows/ci-cd-cubicador-worker.yml

echo
echo "=== 6. Build de sanity (no debería haber cambio de código) ==="
dotnet build Nexo.Clean.sln -c Release -v minimal
