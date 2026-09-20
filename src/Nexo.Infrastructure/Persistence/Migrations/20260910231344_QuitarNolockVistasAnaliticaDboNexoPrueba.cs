using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reemplaza a `20260901201038_QuitarNolockVistasAnalitica` (commit
    /// cd17068), retirada de esta rama por quedar desactualizada: fue escrita
    /// antes del rename de esquema `dbo` → `db_NEXO`
    /// (`clsRecrearVistasAnaliticaEnDboNexoPrueba`, ya en QA) y por eso reventaba
    /// contra QA con "Invalid object name 'dbo.clientes'" — hallazgo de
    /// revisión de código.
    ///
    /// El objetivo original sigue vigente: RCSI ya está activo en esta base
    /// (sys.databases.is_read_committed_snapshot_on = 1), así que las 13
    /// vistas `analitica.*` no necesitan `WITH (NOLOCK)` para evitar bloqueos
    /// de lectura/escritura — verificado empíricamente (2026-09-01) contra
    /// `nexo-sql`: una fila insertada sin COMMIT y luego revertida con
    /// ROLLBACK era visible durante la transacción abierta a través de una
    /// vista con NOLOCK, pero nunca a través de una lectura protegida por
    /// RCSI, sin diferencia de bloqueo entre ambas.
    ///
    /// Esta migración solo quita `WITH (NOLOCK)`; el esquema (`db_NEXO`,
    /// ya corregido por `clsRecrearVistasAnaliticaEnDboNexoPrueba`) no cambia.
    /// </summary>
    public partial class clsQuitarNolockVistasAnaliticaDboNexoPrueba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER VIEW analitica.clientes AS
                SELECT
                    Id, Nombre, Nit, RazonSocial, CreatedAt, UpdatedAt
                FROM db_NEXO.clientes;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM db_NEXO.rutas;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM db_NEXO.configuraciones_vehiculo;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM db_NEXO.servicios_conexos;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM db_NEXO.usuarios;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM db_NEXO.solicitudes;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM db_NEXO.lineas_solicitud;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.cotizaciones;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM db_NEXO.cotizacion_lineas;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM db_NEXO.cotizacion_conexos;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.tarifas_historicas;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM db_NEXO.audit_log;
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.inconsistencias AS
                SELECT 'cotizaciones' AS Entidad, c.Id AS EntidadId,
                    'Entregada sin EntregadaAt o EntregadaPor' AS Motivo
                FROM db_NEXO.cotizaciones c
                WHERE c.Estado = 'Entregada' AND (c.EntregadaAt IS NULL OR c.EntregadaPor IS NULL)

                UNION ALL
                SELECT 'cotizaciones', c.Id,
                    'Resultado definido (Ganada/Perdida) sin haber sido entregada'
                FROM db_NEXO.cotizaciones c
                WHERE c.Resultado <> 'SinDefinir' AND c.Estado <> 'Entregada'

                UNION ALL
                SELECT 'cotizaciones', c.Id, 'Entregada sin ninguna línea de flete'
                FROM db_NEXO.cotizaciones c
                WHERE c.Estado = 'Entregada'
                  AND NOT EXISTS (SELECT 1 FROM db_NEXO.cotizacion_lineas cl WHERE cl.CotizacionId = c.Id)

                UNION ALL
                SELECT 'cotizacion_lineas', cl.Id, 'ValorFlete en cero o negativo'
                FROM db_NEXO.cotizacion_lineas cl
                WHERE cl.ValorFlete <= 0

                UNION ALL
                SELECT 'tarifas_historicas', th.Id,
                    'Referencia una cotizacion que no quedo Ganada (contradice el proposito de la tarifa)'
                FROM db_NEXO.tarifas_historicas th
                JOIN db_NEXO.cotizaciones c ON c.Id = th.CotizacionId
                WHERE c.Resultado <> 'Ganada'

                UNION ALL
                SELECT 'tarifas_historicas', th.Id, 'Valor en cero o negativo'
                FROM db_NEXO.tarifas_historicas th
                WHERE th.Valor <= 0

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Sin ruta asignada (RutaId nulo)'
                FROM db_NEXO.lineas_solicitud l
                WHERE l.RutaId IS NULL

                UNION ALL
                SELECT 'lineas_solicitud', l.Id, 'Peso nulo o en cero'
                FROM db_NEXO.lineas_solicitud l
                WHERE l.Peso IS NULL OR l.Peso <= 0

                UNION ALL
                SELECT 'solicitudes', s.Id, 'Deadline anterior a la fecha de creacion'
                FROM db_NEXO.solicitudes s
                WHERE s.Deadline IS NOT NULL AND s.Deadline < s.CreatedAt

                UNION ALL
                SELECT 'audit_log', a.Id, 'Sin usuario identificado (UsuarioId nulo)'
                FROM db_NEXO.audit_log a
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
                FROM db_NEXO.clientes WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.rutas AS
                SELECT Id, Origen, Destino, CreatedAt, UpdatedAt
                FROM db_NEXO.rutas WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.configuraciones_vehiculo AS
                SELECT Id, TipoVehiculo, Carroceria, RangoPeso, CreatedAt, UpdatedAt
                FROM db_NEXO.configuraciones_vehiculo WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.servicios_conexos AS
                SELECT
                    Id, Nombre, Categoria, ValorBase, VigenteHasta, Activo, CreatedAt, UpdatedAt
                FROM db_NEXO.servicios_conexos WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.usuarios AS
                SELECT Id, Rol, Activo, CreatedAt
                FROM db_NEXO.usuarios WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.solicitudes AS
                SELECT
                    Id, ClienteId, Tipo, Modalidad, Deadline, ProbabilidadCierre,
                    ValorEstimado, ScorePrioridad, Estado, CreadoPor, CreatedAt, UpdatedAt
                FROM db_NEXO.solicitudes WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.lineas_solicitud AS
                SELECT
                    Id, SolicitudId, RutaId, ConfigVehiculoId, Tipologia, LineaNegocio,
                    ClaseViaje, ClaseContenedor, Exclusivo, Consolidado, RetornaContenedor,
                    LevanteContenedorDiferente, ValorAprovisionamiento, Peso, ValorMercancia,
                    EstadoLinea, CreatedAt, UpdatedAt
                FROM db_NEXO.lineas_solicitud WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizaciones AS
                SELECT
                    Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.cotizaciones WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_lineas AS
                SELECT
                    Id, CotizacionId, LineaSolicitudId, ValorFlete, ValorAprovisionamiento,
                    ValorRetornoContenedor, Fuente, CreatedAt, UpdatedAt
                FROM db_NEXO.cotizacion_lineas WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.cotizacion_conexos AS
                SELECT Id, CotizacionId, ServicioConexoId, Cantidad, ValorAplicado, CreatedAt, UpdatedAt
                FROM db_NEXO.cotizacion_conexos WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.tarifas_historicas AS
                SELECT
                    Id, RutaId, ConfigVehiculoId, Tipologia, Valor, Fecha, CotizacionId,
                    CreatedAt, UpdatedAt
                FROM db_NEXO.tarifas_historicas WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.audit_log AS
                SELECT Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt
                FROM db_NEXO.audit_log WITH (NOLOCK);
                """);

            migrationBuilder.Sql("""
                ALTER VIEW analitica.inconsistencias AS
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
        }
    }
}
