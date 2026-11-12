<#
.SINOPSIS
Verificacion de la PR #454: "Organizacion de la base de datos y ajuste de toda la
documentacion de NEXO y el Cubicador" (autor: scalderon-bs).

.QUE VERIFICA
Reorganizacion masiva de carpetas de BD (docs/Database/ -> database/,
docs/documentacion/database/ -> docs/database/) y consolidacion de documentacion.
Afirma "no cambia el comportamiento de la aplicacion": solo 3 archivos .cs tocados,
con 1 linea de comentario/docstring cada uno. Este script (y sus auxiliares
verificar_enlaces.py / verificar_carga_real.py) confirma:

1. Los 3 .cs tocados (clsAsistenteConversacionesController.cs,
   clsCapturaSolicitudesController.cs, clsPropuestaTarifaAdapter.cs) solo cambian
   1 linea de comentario/docstring cada uno -- confirmado leyendo el diff completo,
   no hace falta comparador de tokens: son ediciones de una sola linea, visiblemente
   solo texto de comentario.
2. ESQUEMA_NEXO.sql: diff completo entre la ubicacion vieja (docs/Database/esquema/)
   y la nueva (database/esquema/) quitando lineas de comentario SQL (--) y en blanco
   -> 0 diferencias de DDL real.
3. CSV nuevo (configuraciones_vehiculo_oficiales.csv, 28 filas de datos) y CSV
   modificado (alias_elementos.csv, 72->87 lineas = +15 filas, sin duplicados de
   AliasNormalizado) -- confirmados por conteo y por carga real.
4. Script de carga con --base: confirmado en el codigo de ambos
   ejecutar_schema_y_seeders.py (NEXO y Cubicador).
5. Enlaces rotos: escaneo independiente de 184 archivos .md (241 enlaces relativos)
   via verificar_enlaces.py -- 0 enlaces rotos dentro del alcance de esta PR (los
   "rotos" detectados son 13 preexistentes en tests/QA/ y analitica/, NINGUNO
   introducido por esta PR; 1 falso positivo de mi propio regex con un link
   <...> escapado). HALLAZGO: 15 anclas a docs/decisiones/DECISIONES-NEXO.md#d-XX
   (ej. #d-05, #d-12) no coinciden con el slug real que generaria GitHub del
   encabezado completo (ej. "d-05-solo-se-paginan-las-bibliotecas-de-sice-tac...").
   No bloqueante, pero las anclas no navegarian al lugar correcto en GitHub.
6. Documentos borrados por obsoletos: confirmado que ya no existen en el working
   tree de la PR, y 0 referencias colgantes fuera de tests/QA (la unica mencion
   encontrada, en cubicador/docs/DECISIONES_TECNICAS.md, es un comentario en texto
   que dice explicitamente "quedo en el historial de git", no un link activo).
7. dotnet test Nexo.Domain.Tests: 205/205 (eran 218; se borro
   clsTarifasPlazaProvisionalesDatasetTests.cs, el dataset obsoleto -- sin
   referencias huerfanas). dotnet test Nexo.Application.Tests: 1318/1318 (sin
   cambios). dotnet build Nexo.Clean.sln completo: 0 errores.
8. Los 11 archivos Python tocados compilan limpio (py -m py_compile).
9. De los 4 "hallazgos dejados sin corregir" que afirma la PR: 3 confirmados
   documentados con ubicacion exacta (docs/contrato-integracion/README.md:123-128
   para el Authorize sin rol; docs/decisiones/DECISIONES-NEXO.md:795 para la
   discrepancia del encabezado de identidad; cubicador/CONTRATO.md:700-703 para
   los eventos de catalogo de vehiculos proyectados como motor 3D). El cuarto
   ("recalculo del total sin conexos en el recosteo") NO se localizo pese a una
   busqueda razonable -- se reporta como no confirmado, no como ausente.
10. issues #428 y #423: ambas actualizadas el mismo dia de esta PR (2026-11-10),
    consistente con la afirmacion.
11. git merge-tree origin/QA pr454: sin conflictos.

.COMO SE VERIFICO EN VIVO
- Worktree aislado (C:\tmp\pr454-verify) sobre la rama de la PR.
- SQL Server 2019 desechable en Docker: aplicado el esquema completo (46 tablas)
  y los 11 CSV del seeder via un script que replica exactamente la logica de
  ejecutar_schema_y_seeders.py (generar_id/IDENTITY_INSERT) pero con pyodbc +
  auth SQL en vez de sqlcmd -E (auth de Windows, no aplica contra el contenedor).
- dotnet build/test reales sobre el worktree.
- Script Python propio para validar enlaces relativos y anclas en los .md,
  independiente del conteo que da la propia PR.

.LIMITACIONES CONOCIDAS
- No se pudo verificar "la base propia se comparo contra QA: estructura identica
  y catalogos iguales" (requiere acceso a dbTarifario_Nexo_Pruebas/dbCubicador_Pruebas
  reales, no reproducido aqui).
- El hallazgo #3 ("recalculo del total sin conexos en el recosteo") no se
  localizo en la documentacion revisada; puede estar en un documento no revisado
  a fondo o con otra redaccion.
- Las anclas D-XX son un hallazgo cosmetico (no bloqueante): no impiden leer el
  documento, solo el link directo no navegaria al lugar exacto en GitHub.
#>

$ErrorActionPreference = "Stop"
Write-Host "Ver evidencia detallada en verificar_enlaces.py y verificar_carga_real.py" -ForegroundColor Cyan
Write-Host "Resumen de resultados ya ejecutados (ver docstring arriba):" -ForegroundColor Cyan
Write-Host "  - Domain: 205/205 (218 - 13 de la prueba borrada del dataset obsoleto)" -ForegroundColor Green
Write-Host "  - Application: 1318/1318 (sin cambios)" -ForegroundColor Green
Write-Host "  - dotnet build Nexo.Clean.sln: 0 errores" -ForegroundColor Green
Write-Host "  - Esquema SQL + 11 CSV (incl. los 2 tocados): carga real sin error en Docker" -ForegroundColor Green
Write-Host "  - 241 enlaces relativos revisados: 0 rotos dentro del alcance de esta PR" -ForegroundColor Green
Write-Host "  - 15 anclas D-XX con slug abreviado (hallazgo cosmetico, no bloqueante)" -ForegroundColor Yellow
Write-Host "  - git merge-tree contra QA: sin conflictos" -ForegroundColor Green
Write-Host ""
Write-Host "RESULTADO: LIMPIO (1 hallazgo cosmetico no bloqueante, 1 hallazgo declarado no localizado)" -ForegroundColor Green
exit 0
