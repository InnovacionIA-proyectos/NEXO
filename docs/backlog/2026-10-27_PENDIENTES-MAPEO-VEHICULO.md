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

---

# Otros pendientes (no son del #377)

## 6. `ESQUEMA_NEXO.sql` se detiene a mitad de camino en una base nueva — ✅ RESUELTO (2026-10-27)

Se confirmó la causa (el orden) y se corrigió: ahora los índices van antes que las llaves foráneas. El archivo también crea el
esquema `db_NEXO` si no existe y fija `QUOTED_IDENTIFIER`/`ANSI_NULLS` (sin eso, `sqlcmd` fallaba en los índices filtrados).
Probado completo en un SQL Server 2019 vacío y comparado objeto por objeto contra la base de pruebas: coincide en todo salvo
las 8 llaves foráneas del punto 8. Lo de abajo queda como historia.

`ESQUEMA_NEXO.sql` es el archivo que construye toda la base de NEXO desde cero. Ismael lo corrió en una base vacía (2026-10-27, al probar el #365) y se detuvo por un error en la llave foránea de `OutboxMessage`. Lo que venía después en ese lote no se creó: índices y algunas llaves foráneas. En la base de pruebas no se nota porque las tablas ya existían; el problema aparece al montar un ambiente nuevo.

**Causa probable (sin probar todavía):** un problema de orden.
- Línea 845: `FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId` apunta a `InboxState (MessageId, ConsumerId)`.
- Línea 883: el índice único `AK_InboxState_MessageId_ConsumerId` sobre esas mismas columnas se crea después, en la sección de índices.

SQL Server no permite crear una llave foránea hacia columnas que todavía no tienen una llave primaria o un índice único.

**Qué hacer:** mover la creación de `AK_InboxState_MessageId_ConsumerId` antes de la sección de llaves foráneas, y probar el archivo completo en una base vacía para confirmar que ya no se detiene.

## 7. Decisiones de Ismael sobre las plantillas (#365)

Pendiente responderle:
1. **Orden de columnas:** sí, si el cliente cambia el orden se vuelve a sugerir.
2. **Diccionario:** sí a las tres (con plantilla manda la plantilla; confirmar no agrega términos al diccionario; editar el diccionario no invalida plantillas). Consecuencia: el diccionario solo se llena a mano y arranca casi vacío, porque la carga inicial (#357) todavía no tiene fuente.
3. **`clienteId` desde la solicitud:** no hace falta por ahora. NEXO no asigna clientes por usuario, así que no cambiaría los permisos, y `tbl_audit_log` ya registra quién crea, reemplaza o borra cada plantilla.

## 8. A la base de pruebas le faltan 8 llaves foráneas

Al comparar `ESQUEMA_NEXO.sql` contra `dbInformesAutomatizados_Pruebas.db_NEXO` (2026-10-27), la base no tiene estas 8 llaves
foráneas, que sí están en el esquema y en el modelo del backend (`clsNexoDbContext`):

| Llave foránea | Filas que hoy la incumplen |
|---|---|
| `FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId` | 0 |
| `FK_OutboxMessage_OutboxState_OutboxId` | 0 |
| `FK_planes_cargue_vehiculos_cubicacion_VehiculoId` | 0 |
| `FK_solicitudes_clientes_ClienteId` | 0 |
| `FK_solicitudes_usuarios_CreadoPor` | **2** (solicitudes cuyo `CreadoPor` ya no existe en `tbl_usuarios`) |
| `FK_tarifas_historicas_configuraciones_vehiculo_ConfigVehiculoId` | 0 |
| `FK_tarifas_historicas_cotizaciones_CotizacionId` | 0 |
| `FK_tarifas_historicas_rutas_RutaId` | 0 |

**Qué hacer:**
1. Decidir qué hacer con las 2 solicitudes huérfanas (asignarles un `CreadoPor` válido o borrarlas, si son de prueba):
   ```sql
   SELECT s.Id, s.CreadoPor, s.CreatedAt
   FROM db_NEXO.tbl_solicitudes s
   WHERE NOT EXISTS (SELECT 1 FROM db_NEXO.tbl_usuarios u WHERE u.Id = s.CreadoPor);
   ```
2. Crear las 8 llaves foráneas en la base de pruebas (`WITH CHECK` valida los datos existentes; si algo falla, no se crea):
   ```sql
   ALTER TABLE [db_NEXO].[OutboxMessage] WITH CHECK ADD CONSTRAINT [FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId] FOREIGN KEY ([InboxMessageId], [InboxConsumerId]) REFERENCES [db_NEXO].[InboxState] ([MessageId], [ConsumerId]);
   ALTER TABLE [db_NEXO].[OutboxMessage] WITH CHECK ADD CONSTRAINT [FK_OutboxMessage_OutboxState_OutboxId] FOREIGN KEY ([OutboxId]) REFERENCES [db_NEXO].[OutboxState] ([OutboxId]);
   ALTER TABLE [db_NEXO].[tbl_planes_cargue] WITH CHECK ADD CONSTRAINT [FK_planes_cargue_vehiculos_cubicacion_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [db_NEXO].[tbl_vehiculos_cubicacion] ([Id]);
   ALTER TABLE [db_NEXO].[tbl_solicitudes] WITH CHECK ADD CONSTRAINT [FK_solicitudes_clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [db_NEXO].[tbl_clientes] ([Id]);
   ALTER TABLE [db_NEXO].[tbl_solicitudes] WITH CHECK ADD CONSTRAINT [FK_solicitudes_usuarios_CreadoPor] FOREIGN KEY ([CreadoPor]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id]);
   ALTER TABLE [db_NEXO].[tbl_tarifas_historicas] WITH CHECK ADD CONSTRAINT [FK_tarifas_historicas_configuraciones_vehiculo_ConfigVehiculoId] FOREIGN KEY ([ConfigVehiculoId]) REFERENCES [db_NEXO].[tbl_configuraciones_vehiculo] ([Id]);
   ALTER TABLE [db_NEXO].[tbl_tarifas_historicas] WITH CHECK ADD CONSTRAINT [FK_tarifas_historicas_cotizaciones_CotizacionId] FOREIGN KEY ([CotizacionId]) REFERENCES [db_NEXO].[tbl_cotizaciones] ([Id]);
   ALTER TABLE [db_NEXO].[tbl_tarifas_historicas] WITH CHECK ADD CONSTRAINT [FK_tarifas_historicas_rutas_RutaId] FOREIGN KEY ([RutaId]) REFERENCES [db_NEXO].[tbl_rutas] ([Id]);
   ```
   Conviene avisarle a Ismael antes: con estas llaves, el código que hoy guarda datos inconsistentes empezaría a fallar en vez
   de guardarlos.

## 9. Restos en la base de pruebas que no están en el esquema

Existen en `db_NEXO` pero no se recrean en una base nueva (no forman parte de `ESQUEMA_NEXO.sql`):

| Tabla | Qué es | Creada |
|---|---|---|
| `tbl_tarifas_sicetac` | Caché manual de SICE-TAC, retirada (el código ya no la usa; 13 filas) | 2026-09-20 |
| `__EFMigrationsHistory_backup_20260926` | Respaldo del historial de migraciones | 2026-09-26 |
| `tbl_lineas_solicitud_bak_20261019` | Respaldo | 2026-10-19 |
| `tbl_usuarios_bak_20261019` | Respaldo del cambio de rol (#307) | 2026-10-19 |
| `tbl_mapeo_rol_portal_bak_20261019` | Respaldo del cambio de rol (#307) | 2026-10-19 |

**Qué hacer:** decidir con el equipo si se borran. Los dos respaldos del rol tienen su condición en
`docs/Database/PENDIENTE-ROLES-COORDINADORFLETES-ADMINBIBLIOTECA.md`.
