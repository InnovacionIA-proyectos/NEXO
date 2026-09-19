using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reemplaza a `20260901201038_QuitarNolockVistasAnalitica` (commit
    /// cd17068), retirada de esta rama por quedar desactualizada: fue escrita
    /// antes del rename de esquema `dbo` → `dbo_NEXO_prueba` (issue #93,
    /// `RecrearVistasAnaliticaEnDboNexoPrueba`, ya en QA) y por eso reventaba
    /// contra QA con "Invalid object name 'dbo.clientes'" — hallazgo de
    /// revisión de esta PR.
    ///
    /// El objetivo original sigue vigente: RCSI ya está activo en esta base
    /// (sys.databases.is_read_committed_snapshot_on = 1), así que las 13
    /// vistas `analitica.*` no necesitan `WITH (NOLOCK)` para evitar bloqueos
    /// de lectura/escritura — verificado empíricamente (2026-09-01) contra
    /// `cabinafletes-sql`: una fila insertada sin COMMIT y luego revertida con
    /// ROLLBACK era visible durante la transacción abierta a través de una
    /// vista con NOLOCK, pero nunca a través de una lectura protegida por
    /// RCSI, sin diferencia de bloqueo entre ambas.
    ///
    /// Esta migración solo quita `WITH (NOLOCK)`; el esquema (`dbo_NEXO_prueba`,
    /// ya corregido por `RecrearVistasAnaliticaEnDboNexoPrueba`) no cambia.
    /// </summary>
    public partial class QuitarNolockVistasAnaliticaDboNexoPrueba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER VIEW analitica.clientes AS
                SELECT
                    Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.clientes;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.rutas;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.configuraciones_vehiculo;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.servicios_conexos;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM dbo_NEXO_prueba.usuarios;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.solicitudes;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.lineas_solicitud;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizaciones;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizacion_lineas;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizacion_conexos;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.tarifas_historicas;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM dbo_NEXO_prueba.audit_log;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.inconsistencias AS
                SELECT 'cotizaciones' AS Entidad, c.Id AS EntidadId,
                    'Entregada sin EntregadaAt o EntregadaPor' AS Motivo
                FROM dbo_NEXO_prueba.cotizaciones c
                WHERE c.Estado = 'Entregada' AND (c.EntregadaAt IS NULL OR c.EntregadaPor IS NULL)

                UNION ALL
                SELECT 'cotizaciones', c.Id,
                    'Resultado definido (Ganada/Perdida) sin haber sido entregada'
                FROM dbo_NEXO_prueba.cotizaciones c
                WHERE c.Resultado <> 'SinDefinir' AND c.Estado <> 'Entregada'

                UNION ALL
                SELECT 'cotizaciones', c.Id, 'Entregada sin ninguna línea de flete'
                FROM dbo_NEXO_prueba.cotizaciones c
                WHERE c.Estado = 'Entregada'
                  AND NOT EXISTS (SELECT 1 FROM dbo_NEXO_prueba.cotizacion_lineas cl WHERE cl.CotizacionId = c.Id)

                UNION ALL
                SELECT 'cotizacion_lineas', cl.Id, 'ValorFlete en cero o negativo'
                FROM dbo_NEXO_prueba.cotizacion_lineas cl
                WHERE cl.ValorFlete <= 0

                UNION ALL
                SELECT 'tarifas_historicas', th.Id,
                    'Referencia una cotizacion que no quedo Ganada (contradice el proposito de la tarifa)'
                FROM dbo_NEXO_prueba.tarifas_historicas th
                JOIN dbo_NEXO_prueba.cotizaciones c ON c.Id = th.CotizacionId
                WHERE c.Resultado <> 'Ganada'

                UNION ALL
                SELECT 'tarifas_historicas', th.Id, 'Valor en cero o negativo'
                FROM dbo_NEXO_prueba.tarifas_historicas th
                WHERE th.Valor <= 0

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Sin ruta asignada (RutaId nulo)'
                FROM dbo_NEXO_prueba.lineas_solicitud l
                WHERE l.RutaId IS NULL

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Peso nulo o en cero'
                FROM dbo_NEXO_prueba.lineas_solicitud l
                WHERE l.Peso IS NULL OR l.Peso <= 0

                UNION ALL
                SELECT 'solicitudes', s.Id, 'Deadline anterior a la fecha de creacion'
                FROM dbo_NEXO_prueba.solicitudes s
                WHERE s.Deadline IS NOT NULL AND s.Deadline < s.CreatedAt

                UNION ALL
                SELECT 'audit_log', a.Id, 'Sin usuario identificado (UsuarioId nulo)'
                FROM dbo_NEXO_prueba.audit_log a
                WHERE a.UsuarioId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER VIEW analitica.clientes AS
                SELECT
                    Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.clientes WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.rutas WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.configuraciones_vehiculo WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.servicios_conexos WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM dbo_NEXO_prueba.usuarios WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.solicitudes WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.lineas_solicitud WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizaciones WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizacion_lineas WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.cotizacion_conexos WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM dbo_NEXO_prueba.tarifas_historicas WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM dbo_NEXO_prueba.audit_log WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.inconsistencias AS
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
    }
}
