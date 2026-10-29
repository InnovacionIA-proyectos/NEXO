-- =============================================================================
-- Precio de venta de los servicios conexos (S6-HU-38.1, #407)
-- =============================================================================
-- Qué cambia: dos columnas nuevas. No cambia ninguna columna existente ni el valor de ninguna cotización.
--   tbl_servicios_conexos.PrecioVenta          : precio al cliente del servicio, con su margen ya incluido. Puede quedar
--                                                vacío (NULL): el servicio todavía no tiene precio de venta.
--   tbl_cotizacion_conexos.PrecioVentaUnitario : precio de venta del servicio en el momento de agregarlo a la cotización
--                                                (queda fijo aunque después cambie la biblioteca). Nunca vacío: si el
--                                                servicio no tiene precio de venta, guarda el costo.
-- Lo que se cotiza de un conexo pasa a ser PrecioVentaUnitario × Cantidad (en NEXO y en Cotifly, S6-HU-38.2 y Cotifly
-- HU-043). ValorAplicado sigue siendo el COSTO y no cambia de significado.
--
-- Las columnas quedan al final de cada tabla: SQL Server no permite agregar una columna en medio de una tabla existente
-- sin reconstruirla. ESQUEMA_NEXO.sql las pone en el mismo lugar.
--
-- DOS PARTES:
--   PARTE A — ahora, antes de desplegar el backend. Agrega las columnas permitiendo NULL y llena las filas existentes de
--             tbl_cotizacion_conexos con su costo (ValorAplicado / Cantidad). El backend actual no conoce la columna y
--             sigue funcionando: los conexos que agregue mientras tanto quedan con PrecioVentaUnitario en NULL.
--   PARTE B — DESPUÉS de desplegar S6-HU-38.2 (#408), que ya llena PrecioVentaUnitario. Vuelve a llenar las filas que
--             hayan quedado en NULL entre A y el despliegue, y deja la columna NOT NULL. Está comentada al final: se
--             selecciona y se ejecuta sola. Si se corriera antes del despliegue, agregar conexos fallaría.
--
-- Las dos partes son idempotentes (se pueden correr dos veces). Las sentencias que usan una columna recién creada van
-- con EXEC para que el lote compile sin GO (DBeaver). Reemplazar db_NEXO por el valor de Database__Esquema si es otro.
--
-- Deshacer (solo si hiciera falta):
--   ALTER TABLE [db_NEXO].[tbl_servicios_conexos] DROP CONSTRAINT [CK_servicios_conexos_PrecioVenta];
--   ALTER TABLE [db_NEXO].[tbl_servicios_conexos] DROP COLUMN [PrecioVenta];
--   ALTER TABLE [db_NEXO].[tbl_cotizacion_conexos] DROP CONSTRAINT [CK_cotizacion_conexos_PrecioVentaUnitario];
--   ALTER TABLE [db_NEXO].[tbl_cotizacion_conexos] DROP COLUMN [PrecioVentaUnitario];
-- =============================================================================

-- =============================================================================
-- PARTE A
-- =============================================================================

-- A.1 Precio de venta en el catálogo (vacío en los servicios existentes).
IF COL_LENGTH(N'[db_NEXO].[tbl_servicios_conexos]', N'PrecioVenta') IS NULL
    ALTER TABLE [db_NEXO].[tbl_servicios_conexos] ADD [PrecioVenta] decimal(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_servicios_conexos_PrecioVenta'
               AND parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_servicios_conexos]'))
    EXEC(N'ALTER TABLE [db_NEXO].[tbl_servicios_conexos] ADD CONSTRAINT [CK_servicios_conexos_PrecioVenta]
           CHECK ([PrecioVenta] IS NULL OR [PrecioVenta] >= 0);');

-- A.2 Precio de venta de cada conexo de una cotización (NULL por ahora; NOT NULL en la parte B).
IF COL_LENGTH(N'[db_NEXO].[tbl_cotizacion_conexos]', N'PrecioVentaUnitario') IS NULL
    ALTER TABLE [db_NEXO].[tbl_cotizacion_conexos] ADD [PrecioVentaUnitario] decimal(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_cotizacion_conexos_PrecioVentaUnitario'
               AND parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_cotizacion_conexos]'))
    EXEC(N'ALTER TABLE [db_NEXO].[tbl_cotizacion_conexos] ADD CONSTRAINT [CK_cotizacion_conexos_PrecioVentaUnitario]
           CHECK ([PrecioVentaUnitario] >= 0);');

-- A.3 Filas existentes: hasta hoy un conexo se cobraba a su costo, así que su precio de venta es ese costo
-- (ValorAplicado / Cantidad). Con cantidad 0 (no hay ninguna en pruebas al 2026-10-28) se toma el costo del catálogo.
-- Solo toca filas en NULL: no pisa un valor que ya haya puesto el backend.
EXEC(N'UPDATE cc
          SET cc.[PrecioVentaUnitario] = CASE WHEN cc.[Cantidad] <> 0 THEN cc.[ValorAplicado] / cc.[Cantidad]
                                              ELSE COALESCE(s.[CostoUnitario], 0) END
         FROM [db_NEXO].[tbl_cotizacion_conexos] cc
         LEFT JOIN [db_NEXO].[tbl_servicios_conexos] s ON s.[Id] = cc.[ServicioConexoId]
        WHERE cc.[PrecioVentaUnitario] IS NULL;');

-- Verificación (solo lectura). Esperado después de la parte A:
--   columnas_nuevas = 2, checks = 2, servicios_con_precio_venta = 0,
--   conexos_sin_precio_venta = 0 (o los que el backend haya agregado después de correr A),
--   conexos_cuyo_total_cambia = 0 (PrecioVentaUnitario × Cantidad da lo mismo que ValorAplicado),
--   precio_venta_unitario_nullable = 1 (después de la parte B: 0).
EXEC(N'SELECT
    (SELECT COUNT(*) FROM sys.columns
      WHERE (object_id = OBJECT_ID(N''[db_NEXO].[tbl_servicios_conexos]'') AND name = N''PrecioVenta'')
         OR (object_id = OBJECT_ID(N''[db_NEXO].[tbl_cotizacion_conexos]'') AND name = N''PrecioVentaUnitario'')) AS columnas_nuevas,
    (SELECT COUNT(*) FROM sys.check_constraints
      WHERE name IN (N''CK_servicios_conexos_PrecioVenta'', N''CK_cotizacion_conexos_PrecioVentaUnitario'')) AS checks,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_servicios_conexos] WHERE [PrecioVenta] IS NOT NULL) AS servicios_con_precio_venta,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_cotizacion_conexos]) AS conexos_en_cotizaciones,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_cotizacion_conexos] WHERE [PrecioVentaUnitario] IS NULL) AS conexos_sin_precio_venta,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_cotizacion_conexos]
      WHERE ROUND([PrecioVentaUnitario] * [Cantidad], 2) <> [ValorAplicado]) AS conexos_cuyo_total_cambia,
    (SELECT CAST(is_nullable AS int) FROM sys.columns
      WHERE object_id = OBJECT_ID(N''[db_NEXO].[tbl_cotizacion_conexos]'') AND name = N''PrecioVentaUnitario'') AS precio_venta_unitario_nullable;');

-- =============================================================================
-- PARTE B — correr SOLO después de desplegar S6-HU-38.2 (#408). Seleccionar desde IF hasta el último END y ejecutar.
-- =============================================================================
/*
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_cotizacion_conexos]')
           AND name = N'PrecioVentaUnitario' AND is_nullable = 1)
BEGIN
    -- Conexos agregados entre la parte A y el despliegue: mismo criterio que A.3.
    EXEC(N'UPDATE cc
              SET cc.[PrecioVentaUnitario] = CASE WHEN cc.[Cantidad] <> 0 THEN cc.[ValorAplicado] / cc.[Cantidad]
                                                  ELSE COALESCE(s.[CostoUnitario], 0) END
             FROM [db_NEXO].[tbl_cotizacion_conexos] cc
             LEFT JOIN [db_NEXO].[tbl_servicios_conexos] s ON s.[Id] = cc.[ServicioConexoId]
            WHERE cc.[PrecioVentaUnitario] IS NULL;');
    ALTER TABLE [db_NEXO].[tbl_cotizacion_conexos] ALTER COLUMN [PrecioVentaUnitario] decimal(18,2) NOT NULL;
END
*/
