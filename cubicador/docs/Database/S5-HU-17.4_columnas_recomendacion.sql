/* ============================================================================
   S5-HU-17.4 - Dos columnas que faltan para persistir la respuesta del contrato
   ============================================================================

   Esquema: dbo_cubicador de dbTarifario_Nexo_Pruebas (COMPARTIDO con Cotifly,
   esquema dbo_cotifly). Este script toca SOLO dbo_cubicador.

   Por que hacen falta
   -------------------
   La seccion 2.3 de HOJA-DE-RUTA-CUBICADOR.md define dos campos de respuesta
   que hoy no tienen donde guardarse:

     - `mercanciasCondicionadas` (seccion 2.5): que mercancias quedaron
       marcadas. No se puede derivar al releer: una mercancia queda condicionada
       por su propia marca o por haberse usado su carroceria de respaldo, y esa
       segunda razon se pierde si no se guarda.

     - `alertaBajoLlenado` por vehiculo: se calcula con `umbral_alerta_llenado`
       de `parametrosMotor`, que es un parametro EDITABLE por CRUD. Las tablas
       de recomendacion son append-only: una fila consultada dentro de dos anos
       tiene que decir lo mismo que dijo el dia que se calculo, no lo que diria
       con el umbral de hoy. Por eso se guarda el resultado, no se recalcula.

   Criterio de la nota del equipo de Datos (#267): nada en NULL si hay un valor
   por defecto razonable. Las dos entran NOT NULL con default, asi que las filas
   que ya existan no se rompen.

   Idempotente: se puede correr dos veces.

   Aplicar con:
     ./.venv/Scripts/python.exe cubicador/scripts/aplicar_sql.py \
         cubicador/docs/Database/S5-HU-17.4_columnas_recomendacion.sql --simular
   y luego sin --simular.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* --- 1. recomendaciones.MercanciasCondicionadasJson ---------------------- */

IF NOT EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo_cubicador'
      AND t.name = 'recomendaciones'
      AND c.name = 'MercanciasCondicionadasJson'
)
BEGIN
    ALTER TABLE dbo_cubicador.recomendaciones
        ADD MercanciasCondicionadasJson NVARCHAR(MAX) NOT NULL
            CONSTRAINT DF_recomendaciones_MercanciasCondicionadasJson DEFAULT (N'[]');
    PRINT 'recomendaciones.MercanciasCondicionadasJson agregada.';
END
ELSE
    PRINT 'recomendaciones.MercanciasCondicionadasJson ya existia.';
GO

/* --- 2. recomendacionVehiculo.AlertaBajoLlenado -------------------------- */

IF NOT EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo_cubicador'
      AND t.name = 'recomendacionVehiculo'
      AND c.name = 'AlertaBajoLlenado'
)
BEGIN
    ALTER TABLE dbo_cubicador.recomendacionVehiculo
        ADD AlertaBajoLlenado BIT NOT NULL
            CONSTRAINT DF_recomendacionVehiculo_AlertaBajoLlenado DEFAULT (0);
    PRINT 'recomendacionVehiculo.AlertaBajoLlenado agregada.';
END
ELSE
    PRINT 'recomendacionVehiculo.AlertaBajoLlenado ya existia.';
GO

/* --- 3. Verificacion ----------------------------------------------------- */

SELECT t.name AS tabla, c.name AS columna, ty.name AS tipo,
       CASE WHEN c.is_nullable = 1 THEN 'NULL' ELSE 'NOT NULL' END AS nulabilidad,
       dc.definition AS valor_por_defecto
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc
       ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo_cubicador'
  AND c.name IN ('MercanciasCondicionadasJson', 'AlertaBajoLlenado')
ORDER BY t.name, c.name;
GO
