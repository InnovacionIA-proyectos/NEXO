#!/usr/bin/env bash
# Verificación QA de la PR #332 (S6-HU-26.1/#306: editar el flete de
# cualquier fuente, salvo con regla Solo-SICE). Correr desde la raíz del
# repo, con la rama de la PR descargada localmente y mergeada con QA.
set -euo pipefail

echo "=== 1. Build en Release ==="
dotnet build Nexo.Clean.sln -c Release -v minimal

echo
echo "=== 2. Boundary EXACTO del piso SICE-TAC: Piso-1 falla, Piso y Piso+1 pasan (unit) ==="
dotnet test tests/Nexo.Application.Tests --filter "FullyQualifiedName~clsEditarLineaCotizacionCommandTests" -c Release -v minimal

echo
echo "=== 3. Suite completa contra la base real de pruebas ==="
set -a
while IFS='=' read -r key value; do
  [[ -z "$key" || "$key" == \#* ]] && continue
  export "$key=$value"
done < .env.pruebas-reales.local
set +a
export TEST_DB_CONNECTION="$ConnectionStrings__Nexo"
dotnet test Nexo.Clean.sln -c Release -v minimal

echo
echo "=== 4. Reintento aislado de clsOutboxConsistenciaTests (si falló por deadlock en la corrida completa) ==="
dotnet test tests/Nexo.Api.Tests --filter "FullyQualifiedName~clsOutboxConsistenciaTests" -c Release -v minimal
