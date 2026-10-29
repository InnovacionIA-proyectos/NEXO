# QA — PR #398: Tabla tbl_mapeo_vehiculo_tarifario con carga inicial (#377)

## Resumen

PR de solo esquema de base de datos (sin código de aplicación). Agrega
`docs/Database/377_mapeo_vehiculo_tarifario.sql` (idempotente, con `MERGE`)
que crea `tbl_mapeo_vehiculo_tarifario` y la carga con 15 vehículos: por cada
tipo de vehículo de NEXO, el código con el que se busca en SICE-TAC
(`CONFIGURACION` de `tblRutasSicetac`) y en Plaza/SAP, más de dónde salió
cada código (`Verificacion`). Corrige un problema real conocido: hasta ahora
NEXO usaba el mismo código para las dos fuentes y, al no encontrar filas,
tomaba la ruta más barata de cualquier vehículo sin avisar (ej. "Sencillo"
`C2` no existe en Plaza/SAP, que usa `2`).

Es una tabla nueva y aditiva. Es consumida por #380 (PR #405). Al mergear
`QA` sobre esta rama hubo conflicto aditivo en `.gitignore` (archivos a
ignorar en ambas ramas) y en `docs/Database/ESQUEMA_NEXO.sql` (esta PR
agrega `tbl_mapeo_vehiculo_tarifario`, #395 ya había agregado
`tbl_plantillas_mapeo_cliente` en el mismo punto del archivo) — se
combinaron ambos bloques, se validó con `dotnet build -c Release` → 0
errores.

## Qué se verificó

1. Diff aislado de la PR (`git diff origin/QA...origin/377-s6-hu-371-...`):
   3 commits propios, solo el script SQL, su reflejo en `ESQUEMA_NEXO.sql`,
   el seeder CSV y un doc de pendientes. Sin cambios `.cs` — no aplica
   build de código ni suites de test más allá de la compilación tras el
   merge.
2. `377_mapeo_vehiculo_tarifario.sql` revisado línea por línea contra
   `docs/Database/seeder/mapeo_vehiculos.csv`: las 15 filas del `MERGE`
   coinciden exactamente con el CSV (código SICE-TAC, código Plaza/SAP,
   nombres).
3. **Aplicado en vivo** contra la BD real de pruebas
   (`dbInformesAutomatizados_Pruebas`, esquema `db_NEXO`) — autorizado
   explícitamente por Kevin, con la salvedad de que, a diferencia de #395,
   este script **sí inserta datos** (15 filas de catálogo real, no datos de
   prueba desechables).
4. Resultado de la verificación (ambas corridas, confirmando idempotencia
   del `MERGE`, sin duplicar filas):

   | tablas | columnas | indices | filas | filas_con_plaza |
   |---|---|---|---|---|
   | 1 | 10 | 1 | 15 | 8 |

   Coincide exactamente con lo que la PR afirma.
5. Revisado `docs/backlog/2026-10-27_PENDIENTES-MAPEO-VEHICULO.md`: deja
   documentados 2 temas abiertos (discrepancia de códigos entre
   `tbl_tarifas_plaza` y el mapeo nuevo; comparación de los catálogos de
   SAP `tblSAPTiposVehiculo` vs `tblSAPTiposVehiculoTarifario`) — ya
   reconocidos por el propio autor como pendientes para #380/#381, no son
   un hallazgo nuevo de esta verificación.

## Datos de prueba

Ninguno creado por esta verificación. Las 15 filas que deja el script **son
el entregable real de la PR** (catálogo de negocio), no datos de prueba a
limpiar — son las mismas que usará el código de producción una vez se
despliegue #380.

Ver `verificar.ps1` para el script runnable equivalente (reproducible en
otra máquina con `.env.pruebas-reales.local` presente).

## Resultado

Verificación técnica completa, sin hallazgos. Pendiente de autorización de
Kevin para comentar/aprobar/mergear en GitHub.
