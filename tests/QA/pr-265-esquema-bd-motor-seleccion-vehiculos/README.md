# PR #265 (issue #260) — Esquema de BD para el nuevo motor de selección de tipo/cantidad de vehículos (Cubicación)

## Qué cambia

PR de solo esquema + datos semilla + documentación — **sin tocar ningún
código C#** ni `docs/Database/ESQUEMA_NEXO.sql` (confirmado con diff contra
el merge-base: cero archivos `.cs` modificados). Crea el esquema nuevo
`dbo_cubicador` (9 tablas) dentro de la base compartida
`dbTarifario_Nexo_Pruebas` (servidor `10.10.40.85,1433`, distinta de
`dbInformesAutomatizados_Pruebas` que se usa para el resto de NEXO), con 7
CSV de datos iniciales (vehículos, carrocerías, mercancías, reglas de
incompatibilidad, parámetros del motor).

## Qué se verificó

**Merge real contra `QA`**: sin conflictos; confirmado que no pisa nada de
#253/#257/#264 ya mergeados.

**Integridad referencial de los 7 CSVs semilla** (script propio, sin tocar
la BD): sin IDs huérfanos entre `tiposMercancia`→`categoriaMercancia`/
`TiposCarroceria`, `relacionVehiculosCarroceria`→`vehiculos`/
`TiposCarroceria`, `reglasIncompatibilidad`→`categoriaMercancia`; sin IDs
duplicados en ninguna tabla; sin pares vehículo+carrocería repetidos; sin
ninguna categoría marcada incompatible consigo misma.

**Cruce contra el catálogo real de SAP**
(`tblSAPTiposVehiculoTarifario_202609142052.csv`): los 5 códigos de
vehículo (`2L2`, `C2`, `C3`, `2S2`, `3S3`) existen tal cual en el catálogo
real. SAP agrupa Turbo y Sencillo bajo el mismo código (`C2` = "Rígidos,
Turbo o Sencillo"), pero este motor necesita distinguirlos como vehículos
físicos distintos (dimensiones diferentes) — la PR le asigna a Turbo el
código más específico `2L2` ("Liviano (Capacidad 4 ton)") y deja `C2` para
Sencillo. Decisión razonable y documentada en el SQL, no un error de
mapeo.

**Verificación en vivo contra la base real** (`dbTarifario_Nexo_Pruebas`,
con credenciales SQL provistas por Kevin para esta base puntual, no
guardadas en ningún archivo del repo):
- Las 9 tablas de `dbo_cubicador` existen.
- Conteos de fila exactos contra los 7 CSV: `vehiculos` 5, `TiposCarroceria`
  17, `categoriaMercancia` 13, `tiposMercancia` 31,
  `relacionVehiculosCarroceria` 26, `reglasIncompatibilidad` 3,
  `parametrosMotor` 5. Las 2 tablas de resultado
  (`recomendacionVehiculo`/`recomendacionVehiculoMercancia`) están vacías,
  como corresponde — son de salida futura, no datos semilla.
- Contenido de `vehiculos` verificado fila por fila contra el CSV (código,
  nombre, largo, capacidad) — coincide exacto.
- Encoding de tildes/eñe confirmado en vivo: `TiposCarroceria.NINERA` →
  `"Niñera"`, sin corrupción — tal como afirma la PR.
- Revisé el script `ejecutar_schema_y_seeders.py`: el fix documentado del
  bug de tipado numérico (`COLUMNAS_NUMERICAS`/`COLUMNAS_BOOLEANAS`, antes
  SQL Server forzaba la columna completa a `int` al mezclar códigos
  numéricos puros con texto) está presente en el código.

## Resultado

Sin hallazgos. Esquema, seeders y documentación consistentes entre sí, con
el catálogo real de SAP, y con el estado real de la base compartida.
