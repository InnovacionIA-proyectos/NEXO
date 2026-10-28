# QA — PR #395: Tabla tbl_plantillas_mapeo_cliente y esquema actualizado (#361)

## Resumen

PR de solo esquema de base de datos (sin código de aplicación). Agrega
`docs/Database/361_plantillas_mapeo_cliente.sql` (idempotente) que crea
`tbl_plantillas_mapeo_cliente` — guarda, por cliente, el mapeo de columnas ya
confirmado de un formato de Excel, reconocido por `FirmaEncabezados` (SHA-256
de los encabezados normalizados y en orden). Es una tabla nueva y aditiva: no
toca ninguna tabla existente. También actualiza `ESQUEMA_NEXO.sql` reflejando
la misma tabla, su FK y su índice único, y agrega al `.gitignore` los Excels
de licitaciones de clientes reales y los exports crudos de tablas (`docs/tbl*.csv`).

Desbloquea #394 (reutilización de la plantilla en sugerir-mapeo) y #365
(endurecimiento, PR #396).

## Qué se verificó

1. Diff aislado de la PR con `git diff origin/QA...origin/361-s6-hu-343-tabla-de-plantillas-de-mapeo-por-cliente`
   (solo 2 commits propios: la tabla y un ajuste de documentación posterior).
   Sin cambios de código `.cs` — no aplica build ni suites de test.
2. Revisado línea por línea `361_plantillas_mapeo_cliente.sql` contra
   `ESQUEMA_NEXO.sql`: mismas columnas, mismo PK, mismos CHECK de JSON
   (`ISJSON`), misma FK a `tbl_clientes`, mismo índice único
   `(ClienteId, FirmaEncabezados)`.
3. **Aplicado en vivo** contra la BD real de pruebas
   (`dbInformesAutomatizados_Pruebas`, esquema `db_NEXO`) — autorizado
   explícitamente por Kevin tras confirmar que el script no inserta ningún
   dato, solo DDL (`CREATE TABLE`/`CREATE INDEX`) + un `SELECT` de solo
   lectura contra `sys.tables`/`sys.columns`/`sys.indexes`/`sys.foreign_keys`/
   `sys.check_constraints`.
4. Resultado de la verificación (ambas corridas, confirmando idempotencia):

   | tablas | columnas | indices | llaves_foraneas | restricciones_check |
   |---|---|---|---|---|
   | 1 | 8 | 1 | 1 | 2 |

   Coincide exactamente con lo que la PR afirma.
5. Corrido dos veces seguidas sin error y sin duplicar nada (`IF OBJECT_ID(...) IS NULL` / `IF NOT EXISTS(...)`) — confirma que el script es seguro de re-ejecutar en un despliegue real.

## Datos de prueba

Ninguno. El script no crea filas, solo la tabla vacía (que es el propio
entregable de la PR, no un dato de prueba a limpiar).

Ver `verificar.ps1` para el script runnable equivalente (reproducible en
otra máquina con `.env.pruebas-reales.local` presente).

## Resultado

Verificación técnica completa, sin hallazgos. Pendiente de autorización de
Kevin para comentar/aprobar/mergear en GitHub.
