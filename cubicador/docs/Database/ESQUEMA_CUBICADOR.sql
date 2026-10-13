-- =============================================================================
-- Esquema del motor 3D del Cubicador — dbTarifario_Nexo_Pruebas.dbo_cubicador
-- =============================================================================
-- `dbTarifario_Nexo_Pruebas` es una base COMPARTIDA: Cotifly vive en el esquema
-- `dbo_cotifly`. Por eso TODAS las tablas de este archivo van explícitamente en
-- `dbo_cubicador` — nunca en `dbo`, que también es compartido.
--
-- Este archivo ES la fuente de la verdad del esquema del motor 3D. El proyecto
-- NO usa migraciones: para crear las tablas la primera vez en un ambiente
-- nuevo, correr este script contra una base que ya tenga el esquema
-- `dbo_cubicador`. Cualquier cambio se hace editando este archivo y aplicando
-- el `ALTER` correspondiente a mano contra cada base existente.
--
-- Mismo criterio que `docs/Database/ESQUEMA_NEXO.sql` en la raíz de NEXO.
--
-- Por qué no alembic (S5-HU-17.2, 2026-10-09): su `--autogenerate` lee la base
-- entera y propone borrar lo que sus modelos no conocen. Contra la base real
-- generó 36 `op.drop_table` sobre tablas de Cotifly. Se puede acotar con
-- `include_name` e `include_object`, pero eso deja tres capas de protección que
-- tienen que seguir correctas para siempre, sobre una base que no es solo
-- nuestra. Para 5 tablas que cambian rara vez, el intercambio no cierra.
--
-- Las otras 10 tablas de `dbo_cubicador` (el catálogo: vehículos, carrocerías,
-- mercancías, recomendaciones) las administra el equipo de Datos con sus
-- propios scripts. Este archivo NO las incluye. El Cubicador solo las lee, y su
-- mapeo está en `infrastructure/persistence/modelos_catalogo.py`.
--
-- El usuario de la aplicación es de solo datos a propósito: este script lo
-- ejecuta alguien con permisos DDL, no la app al arrancar.
-- =============================================================================


CREATE TABLE dbo_cubicador.parametros_cubicacion (
    id INTEGER NOT NULL IDENTITY, 
    largo_max_cm FLOAT NOT NULL, 
    ancho_max_cm FLOAT NOT NULL, 
    alto_max_cm FLOAT NOT NULL, 
    peso_max_kg FLOAT NOT NULL, 
    updated_at DATETIMEOFFSET NOT NULL, 
    PRIMARY KEY (id)
);
GO

CREATE TABLE dbo_cubicador.vehiculos_cubicacion (
    id VARCHAR(36) NOT NULL, 
    nombre VARCHAR(100) NOT NULL, 
    descripcion VARCHAR(300) NULL, 
    largo_interno_mm FLOAT NOT NULL, 
    ancho_interno_mm FLOAT NOT NULL, 
    alto_interno_mm FLOAT NOT NULL, 
    capacidad_kg FLOAT NOT NULL, 
    tara_kg FLOAT NOT NULL, 
    puerta_ancho_mm FLOAT NOT NULL, 
    puerta_alto_mm FLOAT NOT NULL, 
    puerta_posicion VARCHAR(20) NOT NULL, 
    ejes_json TEXT NOT NULL, 
    tolerancia_paredes_mm FLOAT NOT NULL, 
    tolerancia_entre_unidades_mm FLOAT NOT NULL, 
    es_preset BIT NOT NULL, 
    created_at DATETIMEOFFSET NOT NULL, 
    updated_at DATETIMEOFFSET NOT NULL, 
    PRIMARY KEY (id)
);
GO

CREATE TABLE dbo_cubicador.planes_cargue (
    id VARCHAR(36) NOT NULL, 
    nombre VARCHAR(150) NOT NULL, 
    vehiculo_id VARCHAR(36) NOT NULL, 
    linea_solicitud_id VARCHAR(36) NULL, 
    numero_vehiculo INTEGER NOT NULL, 
    estado VARCHAR(20) NOT NULL, 
    estrategia VARCHAR(40) NOT NULL, 
    ponderaciones_json TEXT NULL, 
    algoritmo VARCHAR(10) NOT NULL, 
    semilla INTEGER NULL, 
    peso_total_kg FLOAT NOT NULL, 
    volumen_utilizado_m3 FLOAT NOT NULL, 
    utilizacion_volumen FLOAT NOT NULL, 
    utilizacion_piso FLOAT NOT NULL, 
    puntaje FLOAT NOT NULL, 
    incidencias_json TEXT NOT NULL, 
    error_mensaje VARCHAR(500) NULL, 
    created_at DATETIMEOFFSET NOT NULL, 
    updated_at DATETIMEOFFSET NOT NULL, 
    PRIMARY KEY (id), 
    FOREIGN KEY(vehiculo_id) REFERENCES dbo_cubicador.vehiculos_cubicacion (id)
);
GO

CREATE INDEX ix_dbo_cubicador_planes_cargue_linea_solicitud_id ON dbo_cubicador.planes_cargue (linea_solicitud_id);
GO

CREATE TABLE dbo_cubicador.items_plan_cargue (
    id VARCHAR(36) NOT NULL, 
    plan_cargue_id VARCHAR(36) NOT NULL, 
    sku VARCHAR(60) NOT NULL, 
    descripcion VARCHAR(200) NOT NULL, 
    pedido VARCHAR(60) NULL, 
    cliente VARCHAR(120) NULL, 
    destino VARCHAR(120) NULL, 
    secuencia_entrega INTEGER NOT NULL, 
    largo_mm FLOAT NOT NULL, 
    ancho_mm FLOAT NOT NULL, 
    alto_mm FLOAT NOT NULL, 
    peso_kg FLOAT NOT NULL, 
    cantidad INTEGER NOT NULL, 
    color VARCHAR(9) NULL, 
    prioridad INTEGER NOT NULL, 
    tipo_embalaje VARCHAR(40) NULL, 
    apilable BIT NOT NULL, 
    max_niveles INTEGER NOT NULL, 
    max_peso_encima_kg FLOAT NOT NULL, 
    rotable BIT NOT NULL, 
    mantener_vertical BIT NOT NULL, 
    fragil BIT NOT NULL, 
    peligroso BIT NOT NULL, 
    refrigerado BIT NOT NULL, 
    grupo_compatibilidad VARCHAR(40) NULL, 
    observaciones VARCHAR(300) NULL, 
    created_at DATETIMEOFFSET NOT NULL, 
    updated_at DATETIMEOFFSET NOT NULL, 
    PRIMARY KEY (id), 
    FOREIGN KEY(plan_cargue_id) REFERENCES dbo_cubicador.planes_cargue (id)
);
GO

CREATE INDEX ix_dbo_cubicador_items_plan_cargue_plan_cargue_id ON dbo_cubicador.items_plan_cargue (plan_cargue_id);
GO

CREATE TABLE dbo_cubicador.colocaciones_cargue (
    id VARCHAR(36) NOT NULL, 
    plan_cargue_id VARCHAR(36) NOT NULL, 
    item_plan_cargue_id VARCHAR(36) NOT NULL, 
    indice_unidad INTEGER NOT NULL, 
    cargada BIT NOT NULL, 
    x FLOAT NOT NULL, 
    y FLOAT NOT NULL, 
    z FLOAT NOT NULL, 
    largo_colocado_mm FLOAT NOT NULL, 
    ancho_colocado_mm FLOAT NOT NULL, 
    alto_colocado_mm FLOAT NOT NULL, 
    orientacion_indice INTEGER NOT NULL, 
    secuencia_cargue INTEGER NOT NULL, 
    soporte_colocacion_id VARCHAR(36) NULL, 
    porcentaje_soporte FLOAT NOT NULL, 
    peso_encima_kg FLOAT NOT NULL, 
    estado_estabilidad VARCHAR(20) NOT NULL, 
    accesibilidad VARCHAR(30) NOT NULL, 
    bloqueada BIT NOT NULL, 
    ajuste_manual BIT NOT NULL, 
    codigo_rechazo VARCHAR(30) NULL, 
    razon_rechazo VARCHAR(500) NULL, 
    created_at DATETIMEOFFSET NOT NULL, 
    updated_at DATETIMEOFFSET NOT NULL, 
    PRIMARY KEY (id), 
    FOREIGN KEY(item_plan_cargue_id) REFERENCES dbo_cubicador.items_plan_cargue (id), 
    FOREIGN KEY(plan_cargue_id) REFERENCES dbo_cubicador.planes_cargue (id)
);
GO

CREATE INDEX ix_dbo_cubicador_colocaciones_cargue_item_plan_cargue_id ON dbo_cubicador.colocaciones_cargue (item_plan_cargue_id);
GO

CREATE INDEX ix_dbo_cubicador_colocaciones_cargue_plan_cargue_id ON dbo_cubicador.colocaciones_cargue (plan_cargue_id);
GO
