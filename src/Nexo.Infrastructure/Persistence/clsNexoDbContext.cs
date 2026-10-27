using Nexo.Domain.Asistente.Entidades;
using Nexo.Domain.Auditoria.Entidades;
using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Cubicacion.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.IA.Entidades;
using Nexo.Domain.PlanDeCargue.Entidades;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Persistence;

public class clsNexoDbContext : DbContext
{
    /// <summary>clsUsuario de sistema (placeholder) hasta que se cablee Azure AD; satisface las FK de autoría.</summary>
    public static readonly Guid UsuarioSistemaId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public clsNexoDbContext(DbContextOptions<clsNexoDbContext> options) : base(options) { }

    public DbSet<clsUsuario> Usuarios => Set<clsUsuario>();
    public DbSet<clsMapeoRolPortal> MapeosRolPortal => Set<clsMapeoRolPortal>();
    public DbSet<clsCliente> Clientes => Set<clsCliente>();
    public DbSet<clsSolicitud> Solicitudes => Set<clsSolicitud>();
    public DbSet<clsLineaSolicitud> LineasSolicitud => Set<clsLineaSolicitud>();
    public DbSet<clsRuta> Rutas => Set<clsRuta>();
    public DbSet<clsConfiguracionVehiculo> ConfiguracionesVehiculo => Set<clsConfiguracionVehiculo>();
    public DbSet<clsAliasElemento> AliasElementos => Set<clsAliasElemento>();
    public DbSet<clsTerminoCliente> TerminosCliente => Set<clsTerminoCliente>();
    public DbSet<clsTarifaHistorica> TarifasHistoricas => Set<clsTarifaHistorica>();
    public DbSet<clsServicioConexo> ServiciosConexos => Set<clsServicioConexo>();
    public DbSet<clsCotizacion> Cotizaciones => Set<clsCotizacion>();
    public DbSet<clsCotizacionLinea> CotizacionLineas => Set<clsCotizacionLinea>();
    public DbSet<clsCotizacionConexo> CotizacionConexos => Set<clsCotizacionConexo>();
    public DbSet<clsAuditLog> AuditLogs => Set<clsAuditLog>();
    public DbSet<clsParametrosScore> clsParametrosScore => Set<clsParametrosScore>();
    public DbSet<clsAjusteVigenciaPlaza> AjustesVigenciaPlaza => Set<clsAjusteVigenciaPlaza>();
    public DbSet<clsReglaSoloSicetac> ReglasSoloSicetac => Set<clsReglaSoloSicetac>();
    public DbSet<clsVehiculoCubicacion> VehiculosCubicacion => Set<clsVehiculoCubicacion>();
    public DbSet<clsTipoMercanciaCatalogo> TiposMercanciaCatalogo => Set<clsTipoMercanciaCatalogo>();
    public DbSet<clsTipoCarroceriaCatalogo> TiposCarroceriaCatalogo => Set<clsTipoCarroceriaCatalogo>();
    public DbSet<clsPlanCargue> PlanesCargue => Set<clsPlanCargue>();
    public DbSet<clsRechazoCubicacionLinea> RechazosCubicacionLinea => Set<clsRechazoCubicacionLinea>();
    public DbSet<clsUsoIa> UsosIa => Set<clsUsoIa>();
    public DbSet<clsConversacionAsistente> ConversacionesAsistente => Set<clsConversacionAsistente>();
    public DbSet<clsMensajeAsistente> MensajesAsistente => Set<clsMensajeAsistente>();
    public DbSet<clsUsoHerramientaAsistente> UsosHerramientaAsistente => Set<clsUsoHerramientaAsistente>();
    public DbSet<clsAccionAsistente> AccionesAsistente => Set<clsAccionAsistente>();
    public DbSet<clsPreferenciaAsistente> PreferenciasAsistente => Set<clsPreferenciaAsistente>();
    public DbSet<clsItemPlanCargue> ItemsPlanCargue => Set<clsItemPlanCargue>();
    public DbSet<clsColocacionCargue> ColocacionesCargue => Set<clsColocacionCargue>();
    public DbSet<clsMunicipio> Municipios => Set<clsMunicipio>();
    public DbSet<clsRangoPeso> RangosPeso => Set<clsRangoPeso>();
    public DbSet<clsTarifaPlaza> TarifasPlaza => Set<clsTarifaPlaza>();
    public DbSet<clsTarifaPlazaHistorico> TarifasPlazaHistorico => Set<clsTarifaPlazaHistorico>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Esquema propio dentro de dbInformesAutomatizados_Pruebas (BD compartida con
        // otra app ajena a NEXO, que ya vive en 'dbo' y en su propio 'dbo_prueba').
        // Sin esto, las tablas de Nexo quedan mezcladas con las de esa otra app.
        // Se llamó "dbo_NEXO_prueba" originalmente; se renombró a "db_NEXO" — sigue
        // siendo un schema dentro de la MISMA base compartida, no una base aparte.
        // Es la única línea que hay que tocar para mover NEXO a otro schema/base.
        b.HasDefaultSchema(clsEsquemaNexo.Nombre);

        // Tablas del Transactional Outbox de MassTransit. Viven en ESTA base a
        // propósito: la garantía de atomicidad depende de que el mensaje y los datos
        // del caso de uso se guarden en la misma transacción.
        b.AddInboxStateEntity();
        b.AddOutboxMessageEntity();
        b.AddOutboxStateEntity();

        // Líneas que quedaron sin costear, con su tipo y su motivo. Un solo sitio para
        // las dos razones: dos sitios contando lo mismo acaban contradiciéndose.
        //
        // El índice va filtrado por las que siguen pendientes, que es la consulta que
        // hace la pantalla; indexar las ya resueltas no aporta y la tabla crece.
        b.Entity<clsLineaPendienteDeCosteo>(e =>
        {
            e.ToTable("tbl_lineas_pendientes_de_costeo");
            e.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.TrabajoId, x.LineaSolicitudId, x.Tipo });
            e.HasIndex(x => x.SolicitudId).HasFilter("[ResueltaEn] IS NULL");
        });

        // Estado de la saga del auto-costeo. Se persiste aquí y no en otra base porque
        // ES la respuesta al cliente que preguntó por su trabajo: si un reinicio lo
        // borrara, quedaría preguntando por algo que ya nadie recuerda.
        b.Entity<clsEstadoAutoCosteo>(e =>
        {
            e.ToTable("tbl_trabajos_auto_costeo");
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
            // Derivadas del estado, no columnas.
            e.Ignore(x => x.ConDesenlace);
            e.Ignore(x => x.TodasConDesenlace);
            e.Ignore(x => x.TodoResueltoPorElServicio);
        });

        b.Entity<clsEstadoPriorizacion>(e =>
        {
            e.ToTable("tbl_cadena_priorizacion");
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        });

        b.Entity<clsUsuario>(e =>
        {
            e.ToTable("tbl_usuarios");
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Activo).HasDefaultValue(true);
            // Único pero permitiendo múltiples NULL (usuarios aún sin primer login por el portal).
            e.HasIndex(x => x.IdentidadPortalId).IsUnique().HasFilter("[IdentidadPortalId] IS NOT NULL");
            // clsUsuario de sistema (placeholder) para integridad referencial mientras no hay auth.
            e.HasData(new clsUsuario
            {
                Id = UsuarioSistemaId,
                Nombre = "Sistema (placeholder)",
                Email = "system@boterosoto.com.co",
                Rol = RolUsuario.Admin,
                Activo = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
        });

        b.Entity<clsMapeoRolPortal>(e =>
        {
            e.ToTable("tbl_mapeo_rol_portal");
            // Filtrado (no único plano): IdRolPortal queda NULL hasta que TI confirme el
            // id real (ver comentario del seed más abajo), y puede haber varias filas así
            // a la vez.
            e.HasIndex(x => x.IdRolPortal).IsUnique().HasFilter("[IdRolPortal] IS NOT NULL");
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.DescripcionRolPortal).HasMaxLength(200);

            // Equivalencias de ALTA confianza por nombre (docs/contrato-integracion,
            // sección 2). El resto de los 6 roles de Nexo (AdminBiblioteca,
            // LiderProceso, ServiciosLogisticos) no tienen candidato todavía: Pendiente
            // P2, a confirmar por negocio. IdRolPortal queda NULL a propósito: no hay
            // todavía un IDROLES real confirmado contra la base de identidad, y adivinar
            // uno arriesgaría matchear por accidente el rol de otra persona. La resolución
            // por nombre (clsResolverIdentidadPortalAdapter) alcanza mientras tanto; cuando
            // TI confirme el id real, se completa esta columna con un UPDATE, sin deploy.
            e.HasData(
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000001"), IdRolPortal = null, DescripcionRolPortal = "TarifasComercial", Rol = RolUsuario.Comercial, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000002"), IdRolPortal = null, DescripcionRolPortal = "TarifasAdministrador", Rol = RolUsuario.AdminTarifas, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000003"), IdRolPortal = null, DescripcionRolPortal = "Administracion", Rol = RolUsuario.Admin, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                // Roles nuevos creados en el portal ya con el sufijo "NEXO" (uno por
                // cada rol de Nexo, IDROLES real de tblSAPRoles en
                // dbSolucionesTI — ver docs/contrato-integracion): reemplazan en la
                // práctica a los 3 de arriba para las pruebas de identidad real.
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000004"), IdRolPortal = 48, DescripcionRolPortal = "ComercialNEXO", Rol = RolUsuario.Comercial, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000005"), IdRolPortal = 49, DescripcionRolPortal = "CoordinadorFletesNEXO", Rol = RolUsuario.AdminBiblioteca, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000006"), IdRolPortal = 50, DescripcionRolPortal = "AdminTarifasNEXO", Rol = RolUsuario.AdminTarifas, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000007"), IdRolPortal = 51, DescripcionRolPortal = "ServiciosLogisticosNEXO", Rol = RolUsuario.ServiciosLogisticos, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000008"), IdRolPortal = 52, DescripcionRolPortal = "LiderProcesoNEXO", Rol = RolUsuario.LiderProceso, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new clsMapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000009"), IdRolPortal = 53, DescripcionRolPortal = "AdminNEXO", Rol = RolUsuario.Admin, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) }
            );
        });

        b.Entity<clsCliente>(e =>
        {
            e.ToTable("tbl_clientes");
            e.HasIndex(x => x.Nit).IsUnique();
        });

        b.Entity<clsSolicitud>(e =>
        {
            e.ToTable("tbl_solicitudes");
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Modalidad).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20)
             .HasDefaultValue(EstadoSolicitud.Pendiente);
            e.Property(x => x.ValorEstimado).HasColumnType("decimal(18,2)");
            e.Property(x => x.ProbabilidadCierre).HasColumnType("decimal(9,4)");
            e.Property(x => x.ScorePrioridad).HasColumnType("decimal(6,2)").HasDefaultValue(0m);
            e.Property(x => x.RowVersion).IsRowVersion();
            // Índice que sirve la Bandeja Priorizada ordenada por score.
            e.HasIndex(x => new { x.Estado, x.ScorePrioridad });
            // Índice por deadline para tableros de SLA / cumplimiento (PRD §8).
            e.HasIndex(x => x.Deadline);
            e.HasOne(x => x.clsCliente)
             .WithMany(c => c.Solicitudes)
             .HasForeignKey(x => x.ClienteId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Creador)
             .WithMany()
             .HasForeignKey(x => x.CreadoPor)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Cotizaciones)
             .WithOne(c => c.clsSolicitud)
             .HasForeignKey(c => c.SolicitudId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsLineaSolicitud>(e =>
        {
            e.ToTable("tbl_lineas_solicitud");
            e.Property(x => x.Tipologia).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.LineaNegocio).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.ClaseViaje).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ClaseContenedor).HasMaxLength(20);
            e.Property(x => x.Exclusivo).HasDefaultValue(false);
            e.Property(x => x.LevanteContenedorDiferente).HasDefaultValue(false);
            e.Property(x => x.EstadoLinea).HasConversion<string>().HasMaxLength(20)
             .HasDefaultValue(EstadoLinea.PorCostear);
            e.Property(x => x.Peso).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorMercancia).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorAprovisionamiento).HasColumnType("decimal(18,2)");
            e.Property(x => x.HorasCargue).HasColumnType("decimal(6,2)");
            e.Property(x => x.HorasDescargue).HasColumnType("decimal(6,2)");
            e.Property(x => x.IdRecomendacionVehiculo).HasMaxLength(64);
            e.Property(x => x.MotivoRechazo).HasMaxLength(500);   // S6-HU-24.3/#320 (columnas de #314)
            e.HasOne(x => x.clsSolicitud)
             .WithMany(s => s.Lineas)
             .HasForeignKey(x => x.SolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.clsRuta)
             .WithMany()
             .HasForeignKey(x => x.RutaId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.clsConfiguracionVehiculo)
             .WithMany()
             .HasForeignKey(x => x.ConfigVehiculoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsRuta>(e =>
        {
            e.ToTable("tbl_rutas");
            e.HasIndex(x => new { x.Origen, x.Destino }).IsUnique();
            e.HasOne(x => x.OrigenMunicipio).WithMany().HasForeignKey(x => x.OrigenMunicipioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DestinoMunicipio).WithMany().HasForeignKey(x => x.DestinoMunicipioId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsConfiguracionVehiculo>(e =>
        {
            e.ToTable("tbl_configuraciones_vehiculo");
            e.Property(x => x.EsCatalogoOficial).HasDefaultValue(false);
            e.Property(x => x.Descartada).HasDefaultValue(false);
        });

        // S6-HU-27.2/#321: diccionario de alias (tabla creada en #315). Sin migración de EF: el esquema vive en ESQUEMA_NEXO.sql.
        b.Entity<clsAliasElemento>(e =>
        {
            e.ToTable("tbl_alias_elementos");
            e.HasIndex(x => x.AliasNormalizado).IsUnique();
            e.Property(x => x.Activo).HasDefaultValue(true);
        });

        // Términos que usa cada cliente en sus archivos. Sin migración de EF: el esquema vive en ESQUEMA_NEXO.sql.
        b.Entity<clsTerminoCliente>(e =>
        {
            e.ToTable("tbl_diccionario_cliente");
            e.Property(x => x.Termino).HasMaxLength(200).IsRequired();
            e.Property(x => x.TerminoNormalizado).HasMaxLength(200).IsRequired();
            e.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Concepto).HasMaxLength(100).IsRequired();
            e.Property(x => x.Contexto).HasMaxLength(50);
            e.Property(x => x.Ejemplo).HasMaxLength(500);
            e.Property(x => x.Activo).HasDefaultValue(true);
            e.HasIndex(x => new { x.ClienteId, x.TerminoNormalizado, x.Tipo, x.Contexto }).IsUnique();
            e.HasIndex(x => new { x.ClienteId, x.Tipo });
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsTarifaHistorica>(e =>
        {
            e.ToTable("tbl_tarifas_historicas");
            e.Property(x => x.Tipologia).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Valor).HasColumnType("decimal(18,2)");
            e.HasIndex(x => new { x.RutaId, x.ConfigVehiculoId, x.Tipologia });
            e.HasOne(x => x.clsRuta)
             .WithMany()
             .HasForeignKey(x => x.RutaId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.clsConfiguracionVehiculo)
             .WithMany()
             .HasForeignKey(x => x.ConfigVehiculoId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.clsCotizacion)
             .WithMany()
             .HasForeignKey(x => x.CotizacionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsServicioConexo>(e =>
        {
            e.ToTable("tbl_servicios_conexos");
            e.Property(x => x.CostoUnitario).HasColumnType("decimal(18,2)");
            e.Property(x => x.Origen).HasDefaultValue(clsServicioConexo.NoAplica);
            e.Property(x => x.Destino).HasDefaultValue(clsServicioConexo.NoAplica);
            e.Property(x => x.Puerto).HasDefaultValue(clsServicioConexo.NoAplica);
            e.Property(x => x.Ruta).HasDefaultValue(clsServicioConexo.NoAplica);
            e.Property(x => x.TipoMaquina).HasDefaultValue(clsServicioConexo.NoAplica);
            e.Property(x => x.Activo).HasDefaultValue(true);
            // Única real (S4-HU-15/#223): un mismo CodigoServicio puede repetirse con
            // dimensiones distintas (Almacenamiento en dos ciudades), nunca la misma
            // combinación completa.
            e.HasIndex(x => new { x.CodigoServicio, x.Origen, x.Destino, x.Puerto, x.Ruta, x.TipoMaquina }).IsUnique();
            e.HasIndex(x => x.CodigoServicio);
            e.HasOne(x => x.ActualizadoPorUsuario)
             .WithMany()
             .HasForeignKey(x => x.ActualizadoPor)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsCotizacion>(e =>
        {
            e.ToTable("tbl_cotizaciones");
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20)
             .HasDefaultValue(EstadoCotizacion.Borrador);
            e.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(20)
             .HasDefaultValue(ResultadoCotizacion.SinDefinir);
            e.Property(x => x.Total).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            e.Property(x => x.EsAutoCosteo).HasDefaultValue(false);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasMany(x => x.Lineas).WithOne().HasForeignKey(x => x.CotizacionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Conexos).WithOne().HasForeignKey(x => x.CotizacionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EntregadaPorUsuario)
             .WithMany()
             .HasForeignKey(x => x.EntregadaPor)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsCotizacionLinea>(e =>
        {
            e.ToTable("tbl_cotizacion_lineas");
            e.Property(x => x.Fuente).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ValorFlete).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorAprovisionamiento).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorRetornoContenedor).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.clsLineaSolicitud)
             .WithMany()
             .HasForeignKey(x => x.LineaSolicitudId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsCotizacionConexo>(e =>
        {
            e.ToTable("tbl_cotizacion_conexos");
            e.Property(x => x.Cantidad).HasColumnType("decimal(12,2)");
            e.Property(x => x.ValorAplicado).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.clsServicioConexo)
             .WithMany()
             .HasForeignKey(x => x.ServicioConexoId)
             .OnDelete(DeleteBehavior.Restrict);
            // Restrict (no Cascade): clsCotizacion ya cascadea hacia Conexos directo
            // (línea 281) — un segundo camino en cascada Cotizacion→Lineas→Conexos
            // produciría "multiple cascade paths", que SQL Server rechaza al crear el FK.
            e.HasOne(x => x.clsCotizacionLinea)
             .WithMany()
             .HasForeignKey(x => x.CotizacionLineaId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsAuditLog>(e =>
        {
            e.ToTable("tbl_audit_log");
            e.HasIndex(x => new { x.Entidad, x.EntidadId });
            e.HasOne(x => x.clsUsuario)
             .WithMany()
             .HasForeignKey(x => x.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Uso de los modelos de lenguaje: una fila por llamada, sin contenido. El esquema vive en ESQUEMA_NEXO.sql (sin migración de EF).
        b.Entity<clsUsoIa>(e =>
        {
            e.ToTable("tbl_ia_uso");
            e.Property(x => x.Funcion).HasMaxLength(40).IsRequired();
            e.Property(x => x.Modelo).HasMaxLength(80).IsRequired();
            e.Property(x => x.Resultado).HasMaxLength(40).IsRequired();
            e.Property(x => x.CostoEstimadoUsd).HasColumnType("decimal(12,6)");
            e.HasIndex(x => new { x.UsuarioId, x.CreatedAt });
            e.HasIndex(x => new { x.Funcion, x.CreatedAt });
            e.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Conversaciones del asistente de cotización y auditoría de sus herramientas. El esquema vive en ESQUEMA_NEXO.sql (sin migración de EF).
        b.Entity<clsConversacionAsistente>(e =>
        {
            e.ToTable("tbl_asistente_conversaciones");
            e.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
            e.HasIndex(x => new { x.UsuarioId, x.UpdatedAt });
            e.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsMensajeAsistente>(e =>
        {
            e.ToTable("tbl_asistente_mensajes");
            e.Property(x => x.Rol).HasMaxLength(10).IsRequired();
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.ConversacionId, x.Orden }).IsUnique();
            e.HasOne<clsConversacionAsistente>().WithMany().HasForeignKey(x => x.ConversacionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<clsUsoHerramientaAsistente>(e =>
        {
            e.ToTable("tbl_asistente_herramientas_uso");
            e.Property(x => x.Herramienta).HasMaxLength(60).IsRequired();
            e.Property(x => x.Fuente).HasMaxLength(200);
            e.Property(x => x.Resultado).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.ConversacionId, x.CreatedAt });
            e.HasOne<clsConversacionAsistente>().WithMany().HasForeignKey(x => x.ConversacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<clsMensajeAsistente>().WithMany().HasForeignKey(x => x.MensajeId).OnDelete(DeleteBehavior.Restrict);
        });

        // Cambios que el asistente propone y la persona confirma (o no): también son la auditoría de cada escritura.
        b.Entity<clsAccionAsistente>(e =>
        {
            e.ToTable("tbl_asistente_acciones");
            e.Property(x => x.Tipo).HasMaxLength(40).IsRequired();
            e.Property(x => x.Resumen).HasMaxLength(500).IsRequired();
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.Property(x => x.ResultadoCodigo).HasMaxLength(60);
            e.Property(x => x.ResultadoMensaje).HasMaxLength(500);
            e.HasIndex(x => new { x.ConversacionId, x.CreatedAt });
            e.HasIndex(x => x.MensajeId);
            e.HasOne<clsConversacionAsistente>().WithMany().HasForeignKey(x => x.ConversacionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<clsMensajeAsistente>().WithMany().HasForeignKey(x => x.MensajeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Reglas y preferencias propias de cada persona para el asistente de cotización.
        b.Entity<clsPreferenciaAsistente>(e =>
        {
            e.ToTable("tbl_asistente_preferencias");
            e.Property(x => x.Texto).HasMaxLength(300).IsRequired();
            e.HasIndex(x => new { x.UsuarioId, x.CreatedAt });
            e.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<clsConversacionAsistente>().WithMany().HasForeignKey(x => x.ConversacionId).OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------------------------------------------------- Cubicador 3D
        b.Entity<clsVehiculoCubicacion>(e =>
        {
            e.ToTable("tbl_vehiculos_cubicacion");
            foreach (var p in new[] { "LargoInternoMm", "AnchoInternoMm", "AltoInternoMm", "CapacidadKg", "TaraKg",
                                       "PuertaAnchoMm", "PuertaAltoMm", "ToleranciaParedesMm", "ToleranciaEntreUnidadesMm" })
                e.Property(p).HasColumnType("decimal(12,2)");
        });

        // S5-HU-18.5/#277: proyecciones del catálogo del Cubicador, mantenidas por eventos
        // (clsMercanciaCatalogoConsumer/clsCarroceriaCatalogoConsumer) — Id es el string que
        // asigna el Cubicador, no un Guid generado acá.
        b.Entity<clsTipoMercanciaCatalogo>(e =>
        {
            e.ToTable("tbl_tipos_mercancia_catalogo");
            e.HasKey(x => x.Id);
        });

        b.Entity<clsTipoCarroceriaCatalogo>(e =>
        {
            e.ToTable("tbl_tipos_carroceria_catalogo");
            e.HasKey(x => x.Id);
        });

        // #304: el rechazo del Cubicador a "crear_desde_linea", una fila por línea (la clave ES la línea).
        b.Entity<clsRechazoCubicacionLinea>(e =>
        {
            e.ToTable("tbl_rechazos_cubicacion_linea");
            e.HasKey(x => x.LineaSolicitudId);
            e.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            e.Property(x => x.Mensaje).HasMaxLength(1000).IsRequired();
            e.HasOne<clsLineaSolicitud>().WithMany().HasForeignKey(x => x.LineaSolicitudId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<clsPlanCargue>(e =>
        {
            e.ToTable("tbl_planes_cargue");
            e.Property(x => x.PesoTotalKg).HasColumnType("decimal(12,2)");
            e.Property(x => x.VolumenUtilizadoM3).HasColumnType("decimal(12,3)");
            e.Property(x => x.UtilizacionVolumen).HasColumnType("decimal(6,2)");
            e.Property(x => x.UtilizacionPiso).HasColumnType("decimal(6,2)");
            e.Property(x => x.Puntaje).HasColumnType("decimal(6,2)");
            e.HasOne(x => x.Vehiculo).WithMany().HasForeignKey(x => x.VehiculoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.LineaSolicitudId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PlanCargueId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Colocaciones).WithOne().HasForeignKey(x => x.PlanCargueId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<clsItemPlanCargue>(e =>
        {
            e.ToTable("tbl_items_plan_cargue");
            foreach (var p in new[] { "LargoMm", "AnchoMm", "AltoMm", "PesoKg", "MaxPesoEncimaKg" })
                e.Property(p).HasColumnType("decimal(12,2)");
            // La propiedad se llama clsCliente (dato suelto del ítem, no navegación a
            // clsCliente la entidad) pero la columna real en tbl_items_plan_cargue —
            // ver ESQUEMA_NEXO.sql — siempre fue [Cliente]. Sin este mapeo EF genera
            // "Invalid column name 'clsCliente'" en cualquier lectura/escritura de items.
            e.Property(x => x.clsCliente).HasColumnName("Cliente");
        });

        b.Entity<clsColocacionCargue>(e =>
        {
            e.ToTable("tbl_colocaciones_cargue");
            foreach (var p in new[] { "X", "Y", "Z", "LargoColocadoMm", "AnchoColocadoMm", "AltoColocadoMm",
                                       "PorcentajeSoporte", "PesoEncimaKg" })
                e.Property(p).HasColumnType("decimal(12,2)");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemPlanCargueId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PlanCargueId, x.Cargada });
        });

        b.Entity<clsAjusteVigenciaPlaza>(e =>
        {
            e.ToTable("tbl_ajustes_vigencia_plaza");
            // Un ajuste por tarifa de plaza (override): clave natural única.
            e.HasIndex(x => x.TarifaPlazaId).IsUnique();
            e.Property(x => x.Motivo).HasMaxLength(300);
        });

        b.Entity<clsReglaSoloSicetac>(e =>
        {
            e.ToTable("tbl_reglas_solo_sicetac");
            // Clave natural única: no repetir la misma ruta + tipo de vehículo.
            e.HasIndex(x => new { x.Origen, x.Destino, x.TipoVehiculo }).IsUnique();
        });

        b.Entity<clsMunicipio>(e =>
        {
            e.ToTable("tbl_municipios");
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            // Clave natural real: el código DANE por sí solo choca entre Venezuela y
            // Ecuador (verificado contra datos reales, S4-HU-09.1) — la combinación con
            // el país sí es única.
            e.HasIndex(x => new { x.CodigoDane, x.Pais }).IsUnique();
            e.HasIndex(x => x.NombreTarifasPlaza);
        });

        // tbl_pesos/tbl_tarifas_plaza/tbl_tarifas_plaza_historico creadas y pobladas a
        // mano en S4-HU-10.1 — mismo criterio "sin migración de EF Core" que tbl_municipios.
        b.Entity<clsRangoPeso>(e =>
        {
            e.ToTable("tbl_pesos");
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.PesoMinimo, x.PesoMaximo }).IsUnique();
        });

        b.Entity<clsTarifaPlaza>(e =>
        {
            // UseSqlOutputClause(false): tbl_tarifas_plaza tiene un trigger (histórico de
            // vigencia) y SQL Server prohíbe combinar la cláusula OUTPUT (la que usa EF Core
            // por defecto para leer valores generados) con triggers en la misma tabla —
            // sin esto, cualquier INSERT/UPDATE lanza "the target table has database
            // triggers" (confirmado con datos reales al verificar esta issue).
            e.ToTable("tbl_tarifas_plaza", tb => tb.UseSqlOutputClause(false));
            e.HasIndex(x => new { x.LineaNegocio, x.ClaseViaje, x.OrigenMunicipioId, x.DestinoMunicipioId, x.ClaseContenedor, x.PesoId, x.TipoVehiculo }).IsUnique();
            e.HasOne(x => x.Origen).WithMany().HasForeignKey(x => x.OrigenMunicipioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Destino).WithMany().HasForeignKey(x => x.DestinoMunicipioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Peso).WithMany().HasForeignKey(x => x.PesoId).OnDelete(DeleteBehavior.Restrict);
        });

        // S5-HU-16.3/#256: mapeo de SOLO LECTURA — la llena únicamente el trigger de
        // tbl_tarifas_plaza (TRG_tarifas_plaza_historico), nunca esta aplicación. Sin
        // navegaciones a clsMunicipio/clsRangoPeso: el motor de costeo (#256) solo
        // necesita los ids crudos para el match exacto, no las entidades relacionadas.
        b.Entity<clsTarifaPlazaHistorico>(e =>
        {
            e.ToTable("tbl_tarifas_plaza_historico");
            e.HasKey(x => x.Id);
            e.Property(x => x.Valor).HasColumnType("decimal(18,2)");
        });

        b.Entity<clsParametrosScore>(e =>
        {
            e.ToTable("tbl_parametros_score");
            e.Property(x => x.ValorReferencia).HasColumnType("decimal(18,2)");
            e.Property(x => x.RowVersion).IsRowVersion();
            // Fila única (singleton) con los valores por defecto del algoritmo.
            e.HasData(new clsParametrosScore
            {
                Id = 1,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
        });

        // LA CLAVE LA ASIGNA EL DOMINIO, NO LA BASE.
        //
        // Las entidades nacen con su Id (`= Guid.NewGuid()`), que es lo correcto en DDD:
        // una entidad tiene identidad desde que existe, no desde que se guarda. Pero por
        // convención EF marca toda clave Guid como generada al insertar, y entonces una
        // clave YA puesta le dice "esto viene de la base, o sea ya existe". Al agregar un
        // hijo nuevo a un padre ya persistido, EF emitía UPDATE en vez de INSERT: cero
        // filas afectadas, DbUpdateConcurrencyException y un 409 CONFLICTO_CONCURRENCIA
        // al usuario.
        //
        // Se rompía POST /api/cotizaciones/{id}/lineas —agregar el flete a una cotización,
        // el camino principal del módulo piloto— y llevaba así tiempo sin detectarse
        // porque la suite de Api.Tests no se corría completa.
        //
        // Esto le dice al modelo lo que de verdad pasa: la clave llega puesta y hay que
        // insertarla tal cual.
        foreach (var entity in b.Model.GetEntityTypes().Where(t => t.ClrType.IsClass))
        {
            var clave = entity.FindPrimaryKey();
            if (clave is { Properties.Count: 1 } && clave.Properties[0].ClrType == typeof(Guid))
                b.Entity(entity.ClrType).Property(clave.Properties[0].Name).ValueGeneratedNever();
        }

        // Default a nivel de BD para los timestamps de toda tabla auditable.
        foreach (var entity in b.Model.GetEntityTypes()
                     .Where(t => typeof(IAuditable).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(entity.ClrType).Property(nameof(IAuditable.CreatedAt))
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
            b.Entity(entity.ClrType).Property(nameof(IAuditable.UpdatedAt))
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
        }
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Estampa CreatedAt al insertar y UpdatedAt al insertar/actualizar.</summary>
    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Property(e => e.CreatedAt).IsModified = false;
            }
        }
    }
}
