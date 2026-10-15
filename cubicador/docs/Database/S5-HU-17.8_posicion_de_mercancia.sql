/* ============================================================================
   S5-HU-17.8 - La posicion de la mercancia dentro de la solicitud
   ============================================================================

   Esquema: dbo_cubicador de dbTarifario_Nexo_Pruebas (COMPARTIDO con Cotifly,
   esquema dbo_cotifly). Este script toca SOLO dbo_cubicador.

   Por que hace falta
   -------------------
   Una solicitud puede traer dos posiciones del MISMO tipo de mercancia con
   medidas y pesos distintos -dos tamanos de caja del mismo producto-. Hasta
   S5-HU-17.8 la respuesta agrupaba por codigo, asi que un consumidor recibia
   "800 unidades de BEBIDAS en este camion" sin saber cuantas eran de cada
   posicion capturada. Sin ese dato no puede crear una linea de solicitud por
   camion con las posiciones que le tocan a cada uno.

   Ahora `recomendacionVehiculoMercancia` guarda una fila POR POSICION, no por
   codigo: dos filas del mismo camion pueden compartir `IdMercancia`.

   Por que es NULL y no 0
   ----------------------
   Las corridas guardadas antes de este cambio no tienen posicion. Ponerles 0
   seria inventarse que todas eran la primera. NULL dice lo que es: esa corrida
   es anterior al cambio y su detalle esta agrupado por codigo. Es el mismo
   criterio de `CapturaRequests.CodigoTipoMercancia` en NEXO ("Null = tipo
   desconocido; no se asigna un default").

   Las tablas de recomendacion son append-only, asi que las filas viejas no se
   rellenan: se dejan como quedaron.

   Seguro de correr mas de una vez.
   ============================================================================ */

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo_cubicador].[recomendacionVehiculoMercancia]')
      AND name = N'IndicePosicion'
)
BEGIN
    ALTER TABLE [dbo_cubicador].[recomendacionVehiculoMercancia]
        ADD [IndicePosicion] int NULL;

    PRINT 'recomendacionVehiculoMercancia.IndicePosicion creada.';
END
ELSE
    PRINT 'recomendacionVehiculoMercancia.IndicePosicion ya existia.';
GO
