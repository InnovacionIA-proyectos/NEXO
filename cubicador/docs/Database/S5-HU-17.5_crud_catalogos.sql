/* ============================================================================
   S5-HU-17.5 - Lo que la base necesita para los CRUD de catalogo
   ============================================================================

   Esquema: dbo_cubicador de dbTarifario_Nexo_Pruebas (COMPARTIDO con Cotifly,
   esquema dbo_cotifly). Este script toca SOLO dbo_cubicador.

   Tres cosas:

   1. `Version` en los seis catalogos - concurrencia optimista.
   2. Un CHECK que faltaba en relacionVehiculosCarroceria.
   3. Normalizar el par de las reglas de incompatibilidad.

   Idempotente: se puede correr dos veces.

   Aplicar con:
     ./.venv/Scripts/python.exe cubicador/scripts/aplicar_sql.py \
         cubicador/docs/Database/S5-HU-17.5_crud_catalogos.sql --simular
   y luego sin --simular.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* --- 1. Concurrencia optimista ------------------------------------------

   Dos administradores abren el mismo vehiculo; el segundo en guardar escribe
   la fila completa con los valores que tenia al abrirla y el cambio del
   primero desaparece sin aviso. Duele mas de lo normal porque este catalogo
   alimenta el motor: no se manifiesta como error sino como recomendaciones
   sutilmente equivocadas.

   Es un ENTERO que lleva SQLAlchemy (`version_id_col`), no un ROWVERSION de
   SQL Server. El criterio de la issue pide `version_id_col`, y un entero
   funciona igual contra SQL Server y contra SQLite, asi que las pruebas lo
   cubren de verdad. Un ROWVERSION lo llevaria el motor -mas robusto ante
   escrituras que no pasen por el ORM- pero SQLite no lo tiene.

   `relacionVehiculosCarroceria` NO lleva columna propia a proposito: no se
   edita sola, viaja dentro del payload del vehiculo, y es la version del
   VEHICULO la que se sube ante cualquier cambio de ese payload. Darle una
   version propia haria creer que se puede editar por separado.
*/

DECLARE @tablas TABLE (nombre SYSNAME);
INSERT INTO @tablas (nombre) VALUES
    ('vehiculos'), ('TiposCarroceria'), ('categoriaMercancia'),
    ('tiposMercancia'), ('reglasIncompatibilidad'), ('parametrosMotor');

DECLARE @t SYSNAME, @sql NVARCHAR(MAX);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT nombre FROM @tablas;
OPEN cur;
FETCH NEXT FROM cur INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = 'dbo_cubicador' AND t.name = @t AND c.name = 'Version'
    )
    BEGIN
        SET @sql = N'ALTER TABLE dbo_cubicador.' + QUOTENAME(@t) +
                   N' ADD [Version] INT NOT NULL CONSTRAINT ' +
                   QUOTENAME('DF_' + @t + '_Version') + N' DEFAULT (1);';
        EXEC sp_executesql @sql;
        PRINT 'Version agregada a ' + @t;
    END
    ELSE
        PRINT 'Version ya existia en ' + @t;
    FETCH NEXT FROM cur INTO @t;
END
CLOSE cur;
DEALLOCATE cur;
GO

/* --- 2. Capacidad especifica > 0 cuando no usa la del vehiculo -----------

   Sin esto, una combinacion con `UsaCapacidadMaximaDelVehiculo = 0` y
   capacidad 0 le dice al motor que ese vehiculo no carga nada: la
   recomendacion sale vacia y nada avisa por que. Verificado antes de
   aplicarlo: 0 filas lo violan.
*/

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    JOIN sys.tables t ON t.object_id = cc.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo_cubicador'
      AND t.name = 'relacionVehiculosCarroceria'
      AND cc.name = 'CK_relacionVehiculosCarroceria_CapacidadEspecifica'
)
BEGIN
    ALTER TABLE dbo_cubicador.relacionVehiculosCarroceria
        ADD CONSTRAINT CK_relacionVehiculosCarroceria_CapacidadEspecifica
        CHECK (UsaCapacidadMaximaDelVehiculo = 1 OR CapacidadMaximaEspecifica > 0);
    PRINT 'CHECK de capacidad especifica agregado.';
END
ELSE
    PRINT 'CHECK de capacidad especifica ya existia.';
GO

/* --- 3. Par normalizado en las reglas de incompatibilidad ----------------

   La tabla ya trae UNIQUE sobre (IdCategoria1, IdCategoria2), pero ese indice
   es sobre el par ORDENADO: no impide que exista (B,A) habiendo (A,B). Y la
   incompatibilidad es simetrica - `ReglaIncompatibilidad.aplica_a` ya la
   evalua en las dos direcciones - asi que el orden no significa nada.

   Guardando siempre el id menor primero, el UNIQUE que ya existe pasa a
   garantizar que no haya duplicados ni en un sentido ni en el otro.

   La comparacion es por TEXTO (CAST a char(36)), no por el operador nativo de
   `uniqueidentifier`: SQL Server ordena ese tipo por los ultimos seis bytes
   primero, y el codigo Python compara cadenas. Comparar distinto en cada lado
   dejaria filas que un lado cree normalizadas y el otro no.
*/

UPDATE dbo_cubicador.reglasIncompatibilidad
SET IdCategoria1 = IdCategoria2,
    IdCategoria2 = IdCategoria1
WHERE CAST(IdCategoria1 AS CHAR(36)) > CAST(IdCategoria2 AS CHAR(36));

PRINT CONCAT('Reglas normalizadas en esta corrida: ', @@ROWCOUNT);
GO

/* --- 4. Verificacion ----------------------------------------------------- */

SELECT t.name AS tabla, c.name AS columna, dc.definition AS valor_por_defecto
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.default_constraints dc
       ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo_cubicador' AND c.name = 'Version'
ORDER BY t.name;

SELECT COUNT(*) AS reglas_sin_normalizar
FROM dbo_cubicador.reglasIncompatibilidad
WHERE CAST(IdCategoria1 AS CHAR(36)) > CAST(IdCategoria2 AS CHAR(36));
GO
