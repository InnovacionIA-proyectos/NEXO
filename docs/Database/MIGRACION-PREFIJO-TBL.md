# Migración pendiente: prefijo `tbl_` en las tablas

El código ya quedó listo (`clsNexoDbContext.cs` usa `.ToTable("tbl_...")` en las 24
tablas propias) y el DDL de referencia (`ESQUEMA_NEXO.sql`) ya
refleja los nombres nuevos. Falta generar la migración de EF Core, porque este entorno
no tiene el SDK de .NET para correr `dotnet ef`.

No se renombran `InboxState`, `OutboxMessage`, `OutboxState` ni `__EFMigrationsHistory`:
son tablas que MassTransit/EF Core administran por convención propia, no tablas de
negocio de Nexo.

## Paso 1 — generar la migración

```bash
dotnet ef migrations add RenombrarTablasPrefijoTbl \
  --project src/Nexo.Infrastructure --startup-project src/Nexo.Api
```

Como el modelo actual ya tiene `.ToTable("tbl_...")` y el snapshot anterior tiene los
nombres viejos, EF Core debería detectar esto como 24 `RenameTable` (no como
drop+create). Verifica el `Up()`/`Down()` generado contra esta lista:

```csharp
// Up()
            migrationBuilder.RenameTable(name: "ajustes_vigencia_plaza", schema: "db_NEXO", newName: "tbl_ajustes_vigencia_plaza");
            migrationBuilder.RenameTable(name: "audit_log", schema: "db_NEXO", newName: "tbl_audit_log");
            migrationBuilder.RenameTable(name: "cadena_priorizacion", schema: "db_NEXO", newName: "tbl_cadena_priorizacion");
            migrationBuilder.RenameTable(name: "clientes", schema: "db_NEXO", newName: "tbl_clientes");
            migrationBuilder.RenameTable(name: "colocaciones_cargue", schema: "db_NEXO", newName: "tbl_colocaciones_cargue");
            migrationBuilder.RenameTable(name: "configuraciones_vehiculo", schema: "db_NEXO", newName: "tbl_configuraciones_vehiculo");
            migrationBuilder.RenameTable(name: "cotizacion_conexos", schema: "db_NEXO", newName: "tbl_cotizacion_conexos");
            migrationBuilder.RenameTable(name: "cotizacion_lineas", schema: "db_NEXO", newName: "tbl_cotizacion_lineas");
            migrationBuilder.RenameTable(name: "cotizaciones", schema: "db_NEXO", newName: "tbl_cotizaciones");
            migrationBuilder.RenameTable(name: "items_plan_cargue", schema: "db_NEXO", newName: "tbl_items_plan_cargue");
            migrationBuilder.RenameTable(name: "lineas_pendientes_de_costeo", schema: "db_NEXO", newName: "tbl_lineas_pendientes_de_costeo");
            migrationBuilder.RenameTable(name: "lineas_solicitud", schema: "db_NEXO", newName: "tbl_lineas_solicitud");
            migrationBuilder.RenameTable(name: "mapeo_rol_portal", schema: "db_NEXO", newName: "tbl_mapeo_rol_portal");
            migrationBuilder.RenameTable(name: "parametros_score", schema: "db_NEXO", newName: "tbl_parametros_score");
            migrationBuilder.RenameTable(name: "planes_cargue", schema: "db_NEXO", newName: "tbl_planes_cargue");
            migrationBuilder.RenameTable(name: "reglas_solo_sicetac", schema: "db_NEXO", newName: "tbl_reglas_solo_sicetac");
            migrationBuilder.RenameTable(name: "rutas", schema: "db_NEXO", newName: "tbl_rutas");
            migrationBuilder.RenameTable(name: "servicios_conexos", schema: "db_NEXO", newName: "tbl_servicios_conexos");
            migrationBuilder.RenameTable(name: "solicitudes", schema: "db_NEXO", newName: "tbl_solicitudes");
            migrationBuilder.RenameTable(name: "tarifas_historicas", schema: "db_NEXO", newName: "tbl_tarifas_historicas");
            migrationBuilder.RenameTable(name: "tarifas_sicetac", schema: "db_NEXO", newName: "tbl_tarifas_sicetac");
            migrationBuilder.RenameTable(name: "trabajos_auto_costeo", schema: "db_NEXO", newName: "tbl_trabajos_auto_costeo");
            migrationBuilder.RenameTable(name: "usuarios", schema: "db_NEXO", newName: "tbl_usuarios");
            migrationBuilder.RenameTable(name: "vehiculos_cubicacion", schema: "db_NEXO", newName: "tbl_vehiculos_cubicacion");

// Down() (orden inverso)
            migrationBuilder.RenameTable(name: "tbl_vehiculos_cubicacion", schema: "db_NEXO", newName: "vehiculos_cubicacion");
            migrationBuilder.RenameTable(name: "tbl_usuarios", schema: "db_NEXO", newName: "usuarios");
            migrationBuilder.RenameTable(name: "tbl_trabajos_auto_costeo", schema: "db_NEXO", newName: "trabajos_auto_costeo");
            migrationBuilder.RenameTable(name: "tbl_tarifas_sicetac", schema: "db_NEXO", newName: "tarifas_sicetac");
            migrationBuilder.RenameTable(name: "tbl_tarifas_historicas", schema: "db_NEXO", newName: "tarifas_historicas");
            migrationBuilder.RenameTable(name: "tbl_solicitudes", schema: "db_NEXO", newName: "solicitudes");
            migrationBuilder.RenameTable(name: "tbl_servicios_conexos", schema: "db_NEXO", newName: "servicios_conexos");
            migrationBuilder.RenameTable(name: "tbl_rutas", schema: "db_NEXO", newName: "rutas");
            migrationBuilder.RenameTable(name: "tbl_reglas_solo_sicetac", schema: "db_NEXO", newName: "reglas_solo_sicetac");
            migrationBuilder.RenameTable(name: "tbl_planes_cargue", schema: "db_NEXO", newName: "planes_cargue");
            migrationBuilder.RenameTable(name: "tbl_parametros_score", schema: "db_NEXO", newName: "parametros_score");
            migrationBuilder.RenameTable(name: "tbl_mapeo_rol_portal", schema: "db_NEXO", newName: "mapeo_rol_portal");
            migrationBuilder.RenameTable(name: "tbl_lineas_solicitud", schema: "db_NEXO", newName: "lineas_solicitud");
            migrationBuilder.RenameTable(name: "tbl_lineas_pendientes_de_costeo", schema: "db_NEXO", newName: "lineas_pendientes_de_costeo");
            migrationBuilder.RenameTable(name: "tbl_items_plan_cargue", schema: "db_NEXO", newName: "items_plan_cargue");
            migrationBuilder.RenameTable(name: "tbl_cotizaciones", schema: "db_NEXO", newName: "cotizaciones");
            migrationBuilder.RenameTable(name: "tbl_cotizacion_lineas", schema: "db_NEXO", newName: "cotizacion_lineas");
            migrationBuilder.RenameTable(name: "tbl_cotizacion_conexos", schema: "db_NEXO", newName: "cotizacion_conexos");
            migrationBuilder.RenameTable(name: "tbl_configuraciones_vehiculo", schema: "db_NEXO", newName: "configuraciones_vehiculo");
            migrationBuilder.RenameTable(name: "tbl_colocaciones_cargue", schema: "db_NEXO", newName: "colocaciones_cargue");
            migrationBuilder.RenameTable(name: "tbl_clientes", schema: "db_NEXO", newName: "clientes");
            migrationBuilder.RenameTable(name: "tbl_cadena_priorizacion", schema: "db_NEXO", newName: "cadena_priorizacion");
            migrationBuilder.RenameTable(name: "tbl_audit_log", schema: "db_NEXO", newName: "audit_log");
            migrationBuilder.RenameTable(name: "tbl_ajustes_vigencia_plaza", schema: "db_NEXO", newName: "ajustes_vigencia_plaza");
```

## Paso 2 — pegar la recreación de las 13 vistas de `analitica`

EF Core no rastrea las vistas (son SQL crudo, ver
`RecrearVistasAnaliticaEnDboNexoPrueba.cs`), así que la migración generada en el Paso 1
NO las va a tocar — pero las 13 vistas siguen apuntando a las tablas viejas y se
rompen en cuanto el rename corra ("Invalid object name"). Agrega esto al **final** del
`Up()` generado:

```csharp

            migrationBuilder.Sql("DROP VIEW analitica.inconsistencias;");
            migrationBuilder.Sql("DROP VIEW analitica.audit_log;");
            migrationBuilder.Sql("DROP VIEW analitica.tarifas_historicas;");
            migrationBuilder.Sql("DROP VIEW analitica.cotizacion_conexos;");
            migrationBuilder.Sql("DROP VIEW analitica.cotizacion_lineas;");
            migrationBuilder.Sql("DROP VIEW analitica.cotizaciones;");
            migrationBuilder.Sql("DROP VIEW analitica.lineas_solicitud;");
            migrationBuilder.Sql("DROP VIEW analitica.solicitudes;");
            migrationBuilder.Sql("DROP VIEW analitica.usuarios;");
            migrationBuilder.Sql("DROP VIEW analitica.servicios_conexos;");
            migrationBuilder.Sql("DROP VIEW analitica.configuraciones_vehiculo;");
            migrationBuilder.Sql("DROP VIEW analitica.rutas;");
            migrationBuilder.Sql("DROP VIEW analitica.clientes;");

            // ----------------------------------------------------------------- clientes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.clientes AS
                SELECT
                    Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_clientes WITH (NOLOCK);
                """);

            // -------------------------------------------------------------------- rutas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_rutas WITH (NOLOCK);
                """);

            // ------------------------------------------------- configuraciones_vehiculo
            migrationBuilder.Sql("""
                CREATE VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_configuraciones_vehiculo WITH (NOLOCK);
                """);

            // ------------------------------------------------------- servicios_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_servicios_conexos WITH (NOLOCK);
                """);

            // ---------------------------------------------------------------- usuarios
            migrationBuilder.Sql("""
                CREATE VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM db_NEXO.tbl_usuarios WITH (NOLOCK);
                """);

            // ----------------------------------------------------------------- solicitudes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_solicitudes WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- lineas_solicitud
            migrationBuilder.Sql("""
                CREATE VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_lineas_solicitud WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------ cotizaciones
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_cotizaciones WITH (NOLOCK);
                """);

            // -------------------------------------------------------------- cotizacion_lineas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_cotizacion_lineas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- cotizacion_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_cotizacion_conexos WITH (NOLOCK);
                """);

            // ------------------------------------------------------------ tarifas_historicas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.tbl_tarifas_historicas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------- audit_log
            migrationBuilder.Sql("""
                CREATE VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM db_NEXO.tbl_audit_log WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- inconsistencias
            migrationBuilder.Sql("""
                CREATE VIEW analitica.inconsistencias AS
                SELECT 'cotizaciones' AS Entidad, c.Id AS EntidadId,
                    'Entregada sin EntregadaAt o EntregadaPor' AS Motivo
                FROM db_NEXO.tbl_cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada' AND (c.EntregadaAt IS NULL OR c.EntregadaPor IS NULL)

                UNION ALL
                SELECT 'cotizaciones', c.Id,
                    'Resultado definido (Ganada/Perdida) sin haber sido entregada'
                FROM db_NEXO.tbl_cotizaciones c WITH (NOLOCK)
                WHERE c.Resultado <> 'SinDefinir' AND c.Estado <> 'Entregada'

                UNION ALL
                SELECT 'cotizaciones', c.Id, 'Entregada sin ninguna línea de flete'
                FROM db_NEXO.tbl_cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada'
                  AND NOT EXISTS (SELECT 1 FROM db_NEXO.tbl_cotizacion_lineas cl WITH (NOLOCK) WHERE cl.CotizacionId = c.Id)

                UNION ALL
                SELECT 'cotizacion_lineas', cl.Id, 'ValorFlete en cero o negativo'
                FROM db_NEXO.tbl_cotizacion_lineas cl WITH (NOLOCK)
                WHERE cl.ValorFlete <= 0

                UNION ALL
                SELECT 'tarifas_historicas', th.Id,
                    'Referencia una cotizacion que no quedo Ganada (contradice el proposito de la tarifa)'
                FROM db_NEXO.tbl_tarifas_historicas th WITH (NOLOCK)
                JOIN db_NEXO.tbl_cotizaciones c WITH (NOLOCK) ON c.Id = th.CotizacionId
                WHERE c.Resultado <> 'Ganada'

                UNION ALL
                SELECT 'tarifas_historicas', th.Id, 'Valor en cero o negativo'
                FROM db_NEXO.tbl_tarifas_historicas th WITH (NOLOCK)
                WHERE th.Valor <= 0

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Sin ruta asignada (RutaId nulo)'
                FROM db_NEXO.tbl_lineas_solicitud l WITH (NOLOCK)
                WHERE l.RutaId IS NULL

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Peso nulo o en cero'
                FROM db_NEXO.tbl_lineas_solicitud l WITH (NOLOCK)
                WHERE l.Peso IS NULL OR l.Peso <= 0

                UNION ALL
                SELECT 'solicitudes', s.Id, 'Deadline anterior a la fecha de creacion'
                FROM db_NEXO.tbl_solicitudes s WITH (NOLOCK)
                WHERE s.Deadline IS NOT NULL AND s.Deadline < s.CreatedAt

                UNION ALL
                SELECT 'audit_log', a.Id, 'Sin usuario identificado (UsuarioId nulo)'
                FROM db_NEXO.tbl_audit_log a WITH (NOLOCK)
                WHERE a.UsuarioId IS NULL;
                """);
```

Y esto al **final** del `Down()` generado (deja las vistas como están hoy, antes de
este cambio):

```csharp

            migrationBuilder.Sql("DROP VIEW analitica.inconsistencias;");
            migrationBuilder.Sql("DROP VIEW analitica.audit_log;");
            migrationBuilder.Sql("DROP VIEW analitica.tarifas_historicas;");
            migrationBuilder.Sql("DROP VIEW analitica.cotizacion_conexos;");
            migrationBuilder.Sql("DROP VIEW analitica.cotizacion_lineas;");
            migrationBuilder.Sql("DROP VIEW analitica.cotizaciones;");
            migrationBuilder.Sql("DROP VIEW analitica.lineas_solicitud;");
            migrationBuilder.Sql("DROP VIEW analitica.solicitudes;");
            migrationBuilder.Sql("DROP VIEW analitica.usuarios;");
            migrationBuilder.Sql("DROP VIEW analitica.servicios_conexos;");
            migrationBuilder.Sql("DROP VIEW analitica.configuraciones_vehiculo;");
            migrationBuilder.Sql("DROP VIEW analitica.rutas;");
            migrationBuilder.Sql("DROP VIEW analitica.clientes;");

            // ----------------------------------------------------------------- clientes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.clientes AS
                SELECT
                    Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                FROM db_NEXO.clientes WITH (NOLOCK);
                """);

            // -------------------------------------------------------------------- rutas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM db_NEXO.rutas WITH (NOLOCK);
                """);

            // ------------------------------------------------- configuraciones_vehiculo
            migrationBuilder.Sql("""
                CREATE VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM db_NEXO.configuraciones_vehiculo WITH (NOLOCK);
                """);

            // ------------------------------------------------------- servicios_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM db_NEXO.servicios_conexos WITH (NOLOCK);
                """);

            // ---------------------------------------------------------------- usuarios
            migrationBuilder.Sql("""
                CREATE VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM db_NEXO.usuarios WITH (NOLOCK);
                """);

            // ----------------------------------------------------------------- solicitudes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM db_NEXO.solicitudes WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- lineas_solicitud
            migrationBuilder.Sql("""
                CREATE VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM db_NEXO.lineas_solicitud WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------ cotizaciones
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.cotizaciones WITH (NOLOCK);
                """);

            // -------------------------------------------------------------- cotizacion_lineas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM db_NEXO.cotizacion_lineas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- cotizacion_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM db_NEXO.cotizacion_conexos WITH (NOLOCK);
                """);

            // ------------------------------------------------------------ tarifas_historicas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.tarifas_historicas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------- audit_log
            migrationBuilder.Sql("""
                CREATE VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM db_NEXO.audit_log WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- inconsistencias
            migrationBuilder.Sql("""
                CREATE VIEW analitica.inconsistencias AS
                SELECT 'cotizaciones' AS Entidad, c.Id AS EntidadId,
                    'Entregada sin EntregadaAt o EntregadaPor' AS Motivo
                FROM db_NEXO.cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada' AND (c.EntregadaAt IS NULL OR c.EntregadaPor IS NULL)

                UNION ALL
                SELECT 'cotizaciones', c.Id,
                    'Resultado definido (Ganada/Perdida) sin haber sido entregada'
                FROM db_NEXO.cotizaciones c WITH (NOLOCK)
                WHERE c.Resultado <> 'SinDefinir' AND c.Estado <> 'Entregada'

                UNION ALL
                SELECT 'cotizaciones', c.Id, 'Entregada sin ninguna línea de flete'
                FROM db_NEXO.cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada'
                  AND NOT EXISTS (SELECT 1 FROM db_NEXO.cotizacion_lineas cl WITH (NOLOCK) WHERE cl.CotizacionId = c.Id)

                UNION ALL
                SELECT 'cotizacion_lineas', cl.Id, 'ValorFlete en cero o negativo'
                FROM db_NEXO.cotizacion_lineas cl WITH (NOLOCK)
                WHERE cl.ValorFlete <= 0

                UNION ALL
                SELECT 'tarifas_historicas', th.Id,
                    'Referencia una cotizacion que no quedo Ganada (contradice el proposito de la tarifa)'
                FROM db_NEXO.tarifas_historicas th WITH (NOLOCK)
                JOIN db_NEXO.cotizaciones c WITH (NOLOCK) ON c.Id = th.CotizacionId
                WHERE c.Resultado <> 'Ganada'

                UNION ALL
                SELECT 'tarifas_historicas', th.Id, 'Valor en cero o negativo'
                FROM db_NEXO.tarifas_historicas th WITH (NOLOCK)
                WHERE th.Valor <= 0

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Sin ruta asignada (RutaId nulo)'
                FROM db_NEXO.lineas_solicitud l WITH (NOLOCK)
                WHERE l.RutaId IS NULL

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Peso nulo o en cero'
                FROM db_NEXO.lineas_solicitud l WITH (NOLOCK)
                WHERE l.Peso IS NULL OR l.Peso <= 0

                UNION ALL
                SELECT 'solicitudes', s.Id, 'Deadline anterior a la fecha de creacion'
                FROM db_NEXO.solicitudes s WITH (NOLOCK)
                WHERE s.Deadline IS NOT NULL AND s.Deadline < s.CreatedAt

                UNION ALL
                SELECT 'audit_log', a.Id, 'Sin usuario identificado (UsuarioId nulo)'
                FROM db_NEXO.audit_log a WITH (NOLOCK)
                WHERE a.UsuarioId IS NULL;
                """);
```

## Paso 3 — aplicar y verificar

```bash
dotnet ef database update --project src/Nexo.Infrastructure --startup-project src/Nexo.Api
```

Verificar en la BD: `SELECT * FROM db_NEXO.tbl_usuarios;` y
`SELECT * FROM analitica.usuarios;` deben funcionar igual que antes.
