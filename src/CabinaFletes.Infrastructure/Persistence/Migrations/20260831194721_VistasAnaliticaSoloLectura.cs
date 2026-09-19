using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// S2-HU-10.1 (#31 -> en realidad #35): set de datos limpio para el equipo de
    /// datos. Vistas de SOLO LECTURA sobre la BD transaccional propia, en un esquema
    /// aparte (`analitica`) para poder otorgar permisos de SELECT acotados sin exponer
    /// el esquema `dbo` completo. Documentación de cada vista (columnas, tipos,
    /// semántica, origen) en docs/analitica/DICCIONARIO_DATOS.md.
    ///
    /// NO ES UN DW NI UN CUBO (alcance acordado "foto única"): son vistas simples que
    /// envuelven un SELECT — no precalculan nada, no imponen grano ni variable
    /// objetivo. Eso lo decide el análisis exploratorio de S2-HU-11.
    ///
    /// WITH (NOLOCK) en toda lectura: acepta lecturas sucias (una fila a medio
    /// escribir, rarísimo en esta carga) a cambio de no bloquear ni ser bloqueada por
    /// la operación — es una extracción analítica puntual, no una transacción de
    /// negocio. Ejecutar la exportación real en ventana de bajo uso de todas formas.
    /// </summary>
    public partial class VistasAnaliticaSoloLectura : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SCHEMA analitica;");

            // ----------------------------------------------------------------- clientes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.clientes AS
                SELECT
                    Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                    -- Telefonos queda fuera a propósito: dato de contacto, sin valor
                    -- predictivo, minimización de datos.
                FROM dbo.clientes WITH (NOLOCK);
                """);

            // -------------------------------------------------------------------- rutas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM dbo.rutas WITH (NOLOCK);
                """);

            // ------------------------------------------------- configuraciones_vehiculo
            migrationBuilder.Sql("""
                CREATE VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM dbo.configuraciones_vehiculo WITH (NOLOCK);
                """);

            // ------------------------------------------------------- servicios_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                    -- ReglaCobro (JSON de fórmula interna) y ActualizadoPor quedan fuera:
                    -- detalle operativo sin valor analítico directo.
                FROM dbo.servicios_conexos WITH (NOLOCK);
                """);

            // ---------------------------------------------------------------- usuarios
            migrationBuilder.Sql("""
                CREATE VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                    -- Nombre/Email/IdentidadPortalId quedan FUERA a propósito: son datos
                    -- personales del personal interno, sin valor para un modelo de
                    -- predicción sobre cotizaciones/tarifas. El Rol sí es un dato
                    -- potencialmente predictivo (quién crea/entrega una solicitud).
                FROM dbo.usuarios WITH (NOLOCK);
                """);

            // ----------------------------------------------------------------- solicitudes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                    -- CondicionesServicio (JSON libre) queda fuera: texto no estructurado,
                    -- sin esquema estable para un dataset limpio.
                FROM dbo.solicitudes WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- lineas_solicitud
            migrationBuilder.Sql("""
                CREATE VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                    -- CiudadDevolucionContenedor/CiudadLevanteContenedor y Dimensiones (JSON)
                    -- quedan fuera: texto libre de captura, sin catálogo detrás.
                FROM dbo.lineas_solicitud WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------ cotizaciones
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM dbo.cotizaciones WITH (NOLOCK);
                """);

            // -------------------------------------------------------------- cotizacion_lineas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                    -- Fuente (Sicetac/Historica/Manual/Tarifario) ES la trazabilidad de
                    -- origen de este valor: de dónde salió la propuesta que se cotizó.
                FROM dbo.cotizacion_lineas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- cotizacion_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM dbo.cotizacion_conexos WITH (NOLOCK);
                """);

            // ------------------------------------------------------------ tarifas_historicas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM dbo.tarifas_historicas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------- audit_log
            migrationBuilder.Sql("""
                CREATE VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                    -- Diff (JSON del antes/después) queda fuera: puede contener cualquier
                    -- campo de negocio, incluidos los ya excluidos arriba por minimización.
                FROM dbo.audit_log WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- inconsistencias
            // Registros que se identifican y documentan, NUNCA se descartan en silencio
            // de las vistas de arriba (criterio de cierre de la HU). Cada fila es un
            // hallazgo: qué entidad, qué fila, y por qué se marca.
            migrationBuilder.Sql("""
                CREATE VIEW analitica.inconsistencias AS
                SELECT 'cotizaciones' AS Entidad, c.Id AS EntidadId,
                    'Entregada sin EntregadaAt o EntregadaPor' AS Motivo
                FROM dbo.cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada' AND (c.EntregadaAt IS NULL OR c.EntregadaPor IS NULL)

                UNION ALL
                SELECT 'cotizaciones', c.Id,
                    'Resultado definido (Ganada/Perdida) sin haber sido entregada'
                FROM dbo.cotizaciones c WITH (NOLOCK)
                WHERE c.Resultado <> 'SinDefinir' AND c.Estado <> 'Entregada'

                UNION ALL
                SELECT 'cotizaciones', c.Id, 'Entregada sin ninguna línea de flete'
                FROM dbo.cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada'
                  AND NOT EXISTS (SELECT 1 FROM dbo.cotizacion_lineas cl WITH (NOLOCK) WHERE cl.CotizacionId = c.Id)

                UNION ALL
                SELECT 'cotizacion_lineas', cl.Id, 'ValorFlete en cero o negativo'
                FROM dbo.cotizacion_lineas cl WITH (NOLOCK)
                WHERE cl.ValorFlete <= 0

                UNION ALL
                SELECT 'tarifas_historicas', th.Id,
                    'Referencia una cotizacion que no quedo Ganada (contradice el proposito de la tarifa)'
                FROM dbo.tarifas_historicas th WITH (NOLOCK)
                JOIN dbo.cotizaciones c WITH (NOLOCK) ON c.Id = th.CotizacionId
                WHERE c.Resultado <> 'Ganada'

                UNION ALL
                SELECT 'tarifas_historicas', th.Id, 'Valor en cero o negativo'
                FROM dbo.tarifas_historicas th WITH (NOLOCK)
                WHERE th.Valor <= 0

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Sin ruta asignada (RutaId nulo)'
                FROM dbo.lineas_solicitud l WITH (NOLOCK)
                WHERE l.RutaId IS NULL

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Peso nulo o en cero'
                FROM dbo.lineas_solicitud l WITH (NOLOCK)
                WHERE l.Peso IS NULL OR l.Peso <= 0

                UNION ALL
                SELECT 'solicitudes', s.Id, 'Deadline anterior a la fecha de creacion'
                FROM dbo.solicitudes s WITH (NOLOCK)
                WHERE s.Deadline IS NOT NULL AND s.Deadline < s.CreatedAt

                UNION ALL
                SELECT 'audit_log', a.Id, 'Sin usuario identificado (UsuarioId nulo)'
                FROM dbo.audit_log a WITH (NOLOCK)
                WHERE a.UsuarioId IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
            migrationBuilder.Sql("DROP SCHEMA analitica;");
        }
    }
}
