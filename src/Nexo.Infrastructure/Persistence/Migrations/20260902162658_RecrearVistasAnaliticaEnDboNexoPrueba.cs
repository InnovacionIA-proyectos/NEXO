using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hallazgo de QA: las 13 vistas `analitica.*`
    /// (`clsVistasAnaliticaSoloLectura`) se crearon con SQL crudo
    /// `FROM dbo.&lt;tabla&gt;` — un `string`, no una referencia que EF Core rastree.
    /// `clsMoverEsquemaDboNexoPrueba` solo hace `RenameTable` sobre las 27 tablas base;
    /// no reescribe el texto de vistas que las referencian desde fuera del modelo, así
    /// que las 13 quedaron apuntando a un esquema `dbo` donde esas tablas ya no viven
    /// ("Invalid object name 'dbo.solicitudes'").
    ///
    /// Se recrean aquí (no se edita `clsVistasAnaliticaSoloLectura`: esa migración ya se
    /// aplicó contra la BD real, y `dotnet ef` no vuelve a correr una migración por
    /// haberle cambiado el código) apuntando a `dbo_NEXO_prueba`, mismo criterio que
    /// falta MENOS a propósito: mismas 13 vistas, mismas columnas, mismo
    /// `WITH (NOLOCK)` — el único cambio es el esquema del `FROM`/`JOIN`.
    /// </summary>
    public partial class clsRecrearVistasAnaliticaEnDboNexoPrueba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            // ----------------------------------------------------------------- clientes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.clientes AS
                SELECT
                    Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.clientes WITH (NOLOCK);
                """);

            // -------------------------------------------------------------------- rutas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.rutas WITH (NOLOCK);
                """);

            // ------------------------------------------------- configuraciones_vehiculo
            migrationBuilder.Sql("""
                CREATE VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.configuraciones_vehiculo WITH (NOLOCK);
                """);

            // ------------------------------------------------------- servicios_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.servicios_conexos WITH (NOLOCK);
                """);

            // ---------------------------------------------------------------- usuarios
            migrationBuilder.Sql("""
                CREATE VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM dbo_NEXO_prueba.usuarios WITH (NOLOCK);
                """);

            // ----------------------------------------------------------------- solicitudes
            migrationBuilder.Sql("""
                CREATE VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.solicitudes WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- lineas_solicitud
            migrationBuilder.Sql("""
                CREATE VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.lineas_solicitud WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------ cotizaciones
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizaciones WITH (NOLOCK);
                """);

            // -------------------------------------------------------------- cotizacion_lineas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizacion_lineas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- cotizacion_conexos
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizacion_conexos WITH (NOLOCK);
                """);

            // ------------------------------------------------------------ tarifas_historicas
            migrationBuilder.Sql("""
                CREATE VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.tarifas_historicas WITH (NOLOCK);
                """);

            // ------------------------------------------------------------------- audit_log
            migrationBuilder.Sql("""
                CREATE VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM dbo_NEXO_prueba.audit_log WITH (NOLOCK);
                """);

            // ------------------------------------------------------------- inconsistencias
            migrationBuilder.Sql("""
                CREATE VIEW analitica.inconsistencias AS
                SELECT 'cotizaciones' AS Entidad, c.Id AS EntidadId,
                    'Entregada sin EntregadaAt o EntregadaPor' AS Motivo
                FROM dbo_NEXO_prueba.cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada' AND (c.EntregadaAt IS NULL OR c.EntregadaPor IS NULL)

                UNION ALL
                SELECT 'cotizaciones', c.Id,
                    'Resultado definido (Ganada/Perdida) sin haber sido entregada'
                FROM dbo_NEXO_prueba.cotizaciones c WITH (NOLOCK)
                WHERE c.Resultado <> 'SinDefinir' AND c.Estado <> 'Entregada'

                UNION ALL
                SELECT 'cotizaciones', c.Id, 'Entregada sin ninguna línea de flete'
                FROM dbo_NEXO_prueba.cotizaciones c WITH (NOLOCK)
                WHERE c.Estado = 'Entregada'
                  AND NOT EXISTS (SELECT 1 FROM dbo_NEXO_prueba.cotizacion_lineas cl WITH (NOLOCK) WHERE cl.CotizacionId = c.Id)

                UNION ALL
                SELECT 'cotizacion_lineas', cl.Id, 'ValorFlete en cero o negativo'
                FROM dbo_NEXO_prueba.cotizacion_lineas cl WITH (NOLOCK)
                WHERE cl.ValorFlete <= 0

                UNION ALL
                SELECT 'tarifas_historicas', th.Id,
                    'Referencia una cotizacion que no quedo Ganada (contradice el proposito de la tarifa)'
                FROM dbo_NEXO_prueba.tarifas_historicas th WITH (NOLOCK)
                JOIN dbo_NEXO_prueba.cotizaciones c WITH (NOLOCK) ON c.Id = th.CotizacionId
                WHERE c.Resultado <> 'Ganada'

                UNION ALL
                SELECT 'tarifas_historicas', th.Id, 'Valor en cero o negativo'
                FROM dbo_NEXO_prueba.tarifas_historicas th WITH (NOLOCK)
                WHERE th.Valor <= 0

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Sin ruta asignada (RutaId nulo)'
                FROM dbo_NEXO_prueba.lineas_solicitud l WITH (NOLOCK)
                WHERE l.RutaId IS NULL

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Peso nulo o en cero'
                FROM dbo_NEXO_prueba.lineas_solicitud l WITH (NOLOCK)
                WHERE l.Peso IS NULL OR l.Peso <= 0

                UNION ALL
                SELECT 'solicitudes', s.Id, 'Deadline anterior a la fecha de creacion'
                FROM dbo_NEXO_prueba.solicitudes s WITH (NOLOCK)
                WHERE s.Deadline IS NOT NULL AND s.Deadline < s.CreatedAt

                UNION ALL
                SELECT 'audit_log', a.Id, 'Sin usuario identificado (UsuarioId nulo)'
                FROM dbo_NEXO_prueba.audit_log a WITH (NOLOCK)
                WHERE a.UsuarioId IS NULL;
                """);
        }

        /// <inheritdoc />
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

            // Simetría exacta con Up(): vuelve a dejar las vistas apuntando a 'dbo'
            // (el estado, roto, de justo antes de esta migración) — no restaura datos,
            // solo el texto de la vista.
            migrationBuilder.Sql("""
                CREATE VIEW analitica.clientes AS
                SELECT Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                FROM dbo.clientes WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM dbo.rutas WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM dbo.configuraciones_vehiculo WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.servicios_conexos AS
                SELECT Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM dbo.servicios_conexos WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM dbo.usuarios WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM dbo.solicitudes WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM dbo.lineas_solicitud WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM dbo.cotizaciones WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM dbo.cotizacion_lineas WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM dbo.cotizacion_conexos WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM dbo.tarifas_historicas WITH (NOLOCK);
                """);
            migrationBuilder.Sql("""
                CREATE VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM dbo.audit_log WITH (NOLOCK);
                """);
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
    }
}
