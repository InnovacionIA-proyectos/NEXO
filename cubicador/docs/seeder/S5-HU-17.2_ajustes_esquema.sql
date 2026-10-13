-- =============================================================================
-- S5-HU-17.2 — Ajustes al esquema dbo_cubicador
-- =============================================================================
-- Resuelve los puntos 2, 3 y 5 de los bloqueos de la issue. Escrito contra la
-- estructura real leída el 2026-10-09, siguiendo las convenciones que ya tiene
-- el esquema: tablas en camelCase, columnas en PascalCase, `Id` con default
-- `newid()`, fechas con `sysdatetimeoffset()`, texto en Modern_Spanish_CI_AS.
--
-- Lo ejecuta quien tenga permisos DDL sobre `dbo_cubicador`. La aplicación NO
-- necesita esos permisos: su usuario es de solo datos a propósito.
--
-- Es idempotente: se puede correr más de una vez sin efecto adicional.
--
-- SEGURO DE CORRER: `recomendacionVehiculo` y `recomendacionVehiculoMercancia`
-- están vacías (0 filas, verificado), así que ningún cambio toca datos reales.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- PUNTO 2 — Tabla padre `recomendaciones`: una fila por corrida del motor
-- -----------------------------------------------------------------------------
-- Hace falta porque `SIN_SELECCION_AUTOMATICA` no produce ningún vehículo, y
-- sin esta tabla esa corrida no tiene dónde guardarse. Además, los datos de la
-- llamada (línea, origen, resultado) hoy se repetirían una vez por camión, sin
-- nada que impida que queden inconsistentes entre sí.
IF OBJECT_ID('dbo_cubicador.recomendaciones') IS NULL
BEGIN
    CREATE TABLE dbo_cubicador.recomendaciones (
        Id                  UNIQUEIDENTIFIER NOT NULL
                            CONSTRAINT DF_recomendaciones_Id DEFAULT (newid()),
        IdLineaSolicitud    NVARCHAR(100)  COLLATE Modern_Spanish_CI_AS NOT NULL,
        OrigenSistema       NVARCHAR(20)   COLLATE Modern_Spanish_CI_AS NOT NULL,
        -- ASIGNADO | SIN_SELECCION_AUTOMATICA
        Resultado           NVARCHAR(30)   COLLATE Modern_Spanish_CI_AS NOT NULL,
        -- EXTRADIMENSIONADO | SIN_CANDIDATOS_DIMENSIONALES | SUPERA_K_MAX |
        -- SIN_CARROCERIA_COMPATIBLE. 'NINGUNO' cuando Resultado = ASIGNADO:
        -- se evita NULL para no manejar un tercer estado, mismo criterio que
        -- `ControlAmbientalRequerido` en el resto del esquema.
        MotivoSinSeleccion  NVARCHAR(500)  COLLATE Modern_Spanish_CI_AS NOT NULL
                            CONSTRAINT DF_recomendaciones_MotivoSinSeleccion DEFAULT (N'NINGUNO'),
        -- Marca, no flujo de aprobación: la solicitud viaja completa igual.
        Condicionada        BIT NOT NULL
                            CONSTRAINT DF_recomendaciones_Condicionada DEFAULT ((0)),
        -- JSON. `advertencias[]` del contrato, incluida la de clase de viaje
        -- que no coincide con la que marcó el usuario.
        AdvertenciasJson    NVARCHAR(MAX)  COLLATE Modern_Spanish_CI_AS NOT NULL
                            CONSTRAINT DF_recomendaciones_AdvertenciasJson DEFAULT (N'[]'),
        -- JSON. El objeto `extradimension` del contrato: medidas estándar y
        -- las piezas que las exceden, con su motivo.
        ExtradimensionJson  NVARCHAR(MAX)  COLLATE Modern_Spanish_CI_AS NOT NULL
                            CONSTRAINT DF_recomendaciones_ExtradimensionJson DEFAULT (N'{}'),
        CreadoPor           NVARCHAR(100)  COLLATE Modern_Spanish_CI_AS NOT NULL,
        CreatedAt           DATETIMEOFFSET NOT NULL
                            CONSTRAINT DF_recomendaciones_CreatedAt DEFAULT (sysdatetimeoffset()),
        UpdatedAt           DATETIMEOFFSET NOT NULL
                            CONSTRAINT DF_recomendaciones_UpdatedAt DEFAULT (sysdatetimeoffset()),
        CONSTRAINT PK_recomendaciones PRIMARY KEY (Id)
    );

    -- La reconsulta del contrato es por (idLineaSolicitud, origenSistema): el
    -- filtro por origen es obligatorio porque un mismo código de línea puede
    -- existir en NEXO y en Cotifly a la vez.
    CREATE INDEX IX_recomendaciones_Linea_Origen
        ON dbo_cubicador.recomendaciones (IdLineaSolicitud, OrigenSistema);
END;
GO

-- Nota sobre `UpdatedAt`: se incluye por consistencia con el resto del esquema,
-- pero estas tablas son append-only por diseño — recalcular una ruta crea una
-- corrida NUEVA, nunca actualiza la anterior. Una cotización de Cotifly guarda
-- el id de su camión y esa fila tiene que seguir diciendo lo mismo dentro de
-- dos años.


-- -----------------------------------------------------------------------------
-- PUNTO 3 — Columnas que el contrato necesita en `recomendacionVehiculo`
-- -----------------------------------------------------------------------------
-- Ver sección 2.3 de cubicador/docs/HOJA-DE-RUTA-CUBICADOR.md.

-- Enlace con la corrida.
IF COL_LENGTH('dbo_cubicador.recomendacionVehiculo', 'IdRecomendacion') IS NULL
BEGIN
    ALTER TABLE dbo_cubicador.recomendacionVehiculo
        ADD IdRecomendacion UNIQUEIDENTIFIER NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_recomendacionVehiculo_recomendaciones')
BEGIN
    ALTER TABLE dbo_cubicador.recomendacionVehiculo
        ADD CONSTRAINT FK_recomendacionVehiculo_recomendaciones
            FOREIGN KEY (IdRecomendacion)
            REFERENCES dbo_cubicador.recomendaciones (Id);

    CREATE INDEX IX_recomendacionVehiculo_IdRecomendacion
        ON dbo_cubicador.recomendacionVehiculo (IdRecomendacion);
END;
GO

-- Los DOS porcentajes. Hoy hay un solo `PorcentajeLlenado` y el contrato
-- devuelve ambos: el motor filtra por peso pero DECIDE por el mayor de los dos,
-- así que reportar uno solo oculta el criterio que se usó.
IF COL_LENGTH('dbo_cubicador.recomendacionVehiculo', 'PorcentajeLlenadoVolumen') IS NULL
BEGIN
    ALTER TABLE dbo_cubicador.recomendacionVehiculo
        ADD PorcentajeLlenadoVolumen DECIMAL(5,2) NOT NULL,
            PorcentajeLlenadoPeso    DECIMAL(5,2) NOT NULL;
END;
GO

-- PesoTotalKg es el campo que COTIFLY LE MANDA A NEXO al crear la solicitud
-- (requisito C2 de su HU-021.5). Sin él esa integración no cierra: el
-- porcentaje de llenado no sirve para costear.
IF COL_LENGTH('dbo_cubicador.recomendacionVehiculo', 'PesoTotalKg') IS NULL
BEGIN
    ALTER TABLE dbo_cubicador.recomendacionVehiculo
        ADD PesoTotalKg    DECIMAL(18,2) NOT NULL,
            VolumenTotalM3 DECIMAL(18,3) NOT NULL,
            NumeroVehiculo INT           NOT NULL;
END;
GO

-- OPCIONAL, evaluar con el equipo antes de correrlo.
-- `IdLineaSolicitud`, `OrigenSistema`, `Resultado` y `MotivoSinSeleccion` pasan
-- a vivir en `recomendaciones`. Dejarlas también acá las duplica una vez por
-- camión. Las dos tablas están vacías, así que el cambio no pierde nada — pero
-- es destructivo si alguien ya empezó a escribir contra ellas.
--
-- ALTER TABLE dbo_cubicador.recomendacionVehiculo DROP CONSTRAINT <default de cada una>;
-- ALTER TABLE dbo_cubicador.recomendacionVehiculo
--     DROP COLUMN IdLineaSolicitud, OrigenSistema, Resultado, MotivoSinSeleccion;


-- -----------------------------------------------------------------------------
-- PUNTO 5 — Los 10 tipos excluidos: NO se siembran
-- -----------------------------------------------------------------------------
-- Decision del equipo de Datos (issue #267): `tiposMercancia` se queda con las
-- 31 filas reales de la matriz. Si alguna vez hace falta rechazar graneles,
-- cemento o combustibles con motivo, entran por CRUD cuando se necesiten, no
-- desde el seeder inicial.
--
-- Consecuencia a tener presente al escribir el motor: sin esas filas, un codigo
-- de mercancia desconocido y uno no cubicable son el mismo caso. "Cemento a
-- granel" no existe en el catalogo, asi que se rechaza por desconocido.


-- -----------------------------------------------------------------------------
-- Verificación
-- -----------------------------------------------------------------------------
SELECT 'tablas en el esquema' AS que, COUNT(*) AS valor
  FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
 WHERE s.name = 'dbo_cubicador'
UNION ALL
SELECT 'columnas nuevas en recomendacionVehiculo',
       (SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('dbo_cubicador.recomendacionVehiculo')
           AND name IN ('IdRecomendacion','PorcentajeLlenadoVolumen','PorcentajeLlenadoPeso',
                        'PesoTotalKg','VolumenTotalM3','NumeroVehiculo'))
UNION ALL
SELECT 'tipos de mercancia', (SELECT COUNT(*) FROM dbo_cubicador.tiposMercancia);
-- Esperado: 10 tablas, 6 columnas nuevas, 31 tipos de mercancia.
