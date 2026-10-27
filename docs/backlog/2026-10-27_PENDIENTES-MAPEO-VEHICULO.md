# Pendientes del mapeo de vehículo (#377)

_Fecha: 2026-10-27_

Lo que falta para cerrar `tbl_mapeo_vehiculo_tarifario` (`docs/Database/377_mapeo_vehiculo_tarifario.sql`, seeder `docs/Database/seeder/mapeo_vehiculos.csv`). La tabla ya está creada y cargada en la base de pruebas.

## 1. `tbl_tarifas_plaza` no usa los mismos códigos que SAP

El mapeo tiene una sola columna, `CodigoPlazaSap`, para `tblSAPTarifas` y `tbl_tarifas_plaza`, porque se asumió que las dos usan los mismos códigos. El seeder de `tbl_tarifas_plaza` (`docs/Database/seeder/tarifas_plaza.csv`) dice otra cosa: sacó el vehículo del rango de peso con los códigos de **SICE-TAC**.

| Código en `tarifas_plaza.csv` | Filas | Qué dice el mapeo |
|---|---|---|
| `2S2`, `2S3`, `3S3` | 42, 21, 21 | Coinciden |
| `2` | 1 | Coincide (Sencillo) |
| `3S2` | 21 | El mapeo dice que 3S2 no tiene código de plaza (`NULL`) |
| `2L3` | 1 | El mapeo dice que en plaza 2L3 se busca como `2L2` |

Con el mapeo como está, NEXO buscaría `2L2` en `tbl_tarifas_plaza` y no encontraría la fila de `2L3`, y las 21 filas de `3S2` nunca se usarían.

**Primero:** confirmar qué hay hoy en la tabla real, porque el CSV es la carga inicial y pudo cambiar después.

```sql
SELECT TipoVehiculo, COUNT(*) AS filas
FROM db_NEXO.tbl_tarifas_plaza
GROUP BY TipoVehiculo
ORDER BY TipoVehiculo;
```

**Después, decidir una de dos:**
1. Corregir `tbl_tarifas_plaza` para que use los códigos de SAP (`2L3` → `2L2`, y definir qué pasa con las filas de `3S2`). El mapeo queda con una sola columna.
2. Volver a separar las columnas: `CodigoPlazaSap` para `tblSAPTarifas` y otra para `tbl_tarifas_plaza` (con `3S2` → `3S2` y `2L3` → `2L3`). Hay que ajustar el script, el CSV y `ESQUEMA_NEXO.sql`.

## 2. Comparar los dos catálogos de vehículos de SAP

La llave foránea de `tblSAPTarifas.TIPOVEHICULO` apunta a `tblSAPTiposVehiculo`, pero NEXO traduce ese ID con `tblSAPTiposVehiculoTarifario` (`clsDbSolucionesTiDbContext.cs:41`). Si los dos catálogos no tienen los mismos códigos por ID, NEXO busca en SAP con el vehículo equivocado. Si sale algún `DISTINTO`, se documenta para el #380.

```sql
SELECT
    COALESCE(CONVERT(varchar(20), v.ID), CONVERT(varchar(20), vt.ID)) AS ID,
    v.CODIGO   AS codigo_tiposvehiculo,
    vt.CODIGO  AS codigo_tarifario,
    vt.DESCRIPCION AS descripcion_tarifario,
    CASE WHEN v.CODIGO = vt.CODIGO THEN 'igual' ELSE 'DISTINTO' END AS comparacion
FROM dbo.tblSAPTiposVehiculo v
FULL OUTER JOIN dbo.tblSAPTiposVehiculoTarifario vt
    ON CONVERT(varchar(20), vt.ID) = CONVERT(varchar(20), v.ID)
ORDER BY ID;
```

## 3. Evidencia de que cada código de SICE-TAC existe

Conteo por código en `tblRutasSicetac` (DWSice) para respaldar el mapeo de todos los vehículos. Si alguno sale en 0 o no aparece, está mal escrito o no existe en SICE-TAC. La tabla es grande y no tiene índice, puede tardar.

```sql
SELECT CONFIGURACION, COUNT(*) AS filas
FROM dbo.tblRutasSicetac
WHERE CONFIGURACION IN ('2','2_5','2_7','2L1','2L2','2L3','2S2','2S3','3','3S2','3S3','CA_','V2','V3','V4')
GROUP BY CONFIGURACION
ORDER BY CONFIGURACION;
```

## 4. Documentar el seeder

Agregar la sección de `mapeo_vehiculos.csv` en `docs/Database/seeder/README.md`, igual que los demás seeders. Depende de lo que se decida en el punto 1.

## 5. Cerrar

Commit, push y PR a QA (script, CSV, `ESQUEMA_NEXO.sql`, `.gitignore` y este archivo). El `.gitignore` va a chocar con la PR #395; es el mismo bloque y se resuelve fácil.
