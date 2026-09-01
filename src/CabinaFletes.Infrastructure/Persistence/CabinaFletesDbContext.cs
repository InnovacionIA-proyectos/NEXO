using CabinaFletes.Domain.Auditoria.Entidades;
using CabinaFletes.Domain.BibliotecaTarifas.Entidades;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Common;
using CabinaFletes.Domain.Costeo.Entidades;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Domain.Usuarios.Entidades;
using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Persistence;

public class CabinaFletesDbContext : DbContext
{
    /// <summary>Usuario de sistema (placeholder) hasta que se cablee Azure AD; satisface las FK de autoría.</summary>
    public static readonly Guid UsuarioSistemaId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public CabinaFletesDbContext(DbContextOptions<CabinaFletesDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<MapeoRolPortal> MapeosRolPortal => Set<MapeoRolPortal>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<LineaSolicitud> LineasSolicitud => Set<LineaSolicitud>();
    public DbSet<Ruta> Rutas => Set<Ruta>();
    public DbSet<ConfiguracionVehiculo> ConfiguracionesVehiculo => Set<ConfiguracionVehiculo>();
    public DbSet<TarifaSicetac> TarifasSicetac => Set<TarifaSicetac>();
    public DbSet<TarifaHistorica> TarifasHistoricas => Set<TarifaHistorica>();
    public DbSet<ServicioConexo> ServiciosConexos => Set<ServicioConexo>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<CotizacionLinea> CotizacionLineas => Set<CotizacionLinea>();
    public DbSet<CotizacionConexo> CotizacionConexos => Set<CotizacionConexo>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ParametrosScore> ParametrosScore => Set<ParametrosScore>();
    public DbSet<AjusteVigenciaPlaza> AjustesVigenciaPlaza => Set<AjusteVigenciaPlaza>();
    public DbSet<ReglaSoloSicetac> ReglasSoloSicetac => Set<ReglaSoloSicetac>();
    public DbSet<VehiculoCubicacion> VehiculosCubicacion => Set<VehiculoCubicacion>();
    public DbSet<PlanCargue> PlanesCargue => Set<PlanCargue>();
    public DbSet<ItemPlanCargue> ItemsPlanCargue => Set<ItemPlanCargue>();
    public DbSet<ColocacionCargue> ColocacionesCargue => Set<ColocacionCargue>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Esquema propio dentro de dbInformesAutomatizados_Pruebas (BD compartida con
        // otra app ajena a NEXO, que ya vive en 'dbo' y en su propio 'dbo_prueba').
        // Sin esto, las tablas de CabinaFletes quedan mezcladas con las de esa otra app.
        b.HasDefaultSchema("dbo_NEXO_prueba");

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
        b.Entity<LineaPendienteDeCosteo>(e =>
        {
            e.ToTable("lineas_pendientes_de_costeo");
            e.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.TrabajoId, x.LineaSolicitudId, x.Tipo });
            e.HasIndex(x => x.SolicitudId).HasFilter("[ResueltaEn] IS NULL");
        });

        // Estado de la saga del auto-costeo. Se persiste aquí y no en otra base porque
        // ES la respuesta al cliente que preguntó por su trabajo: si un reinicio lo
        // borrara, quedaría preguntando por algo que ya nadie recuerda.
        b.Entity<EstadoAutoCosteo>(e =>
        {
            e.ToTable("trabajos_auto_costeo");
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
            // Derivadas del estado, no columnas.
            e.Ignore(x => x.ConDesenlace);
            e.Ignore(x => x.TodasConDesenlace);
            e.Ignore(x => x.TodoResueltoPorElServicio);
        });

        b.Entity<EstadoPriorizacion>(e =>
        {
            e.ToTable("cadena_priorizacion");
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        });

        b.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Activo).HasDefaultValue(true);
            // Único pero permitiendo múltiples NULL (usuarios aún sin primer login por el portal).
            e.HasIndex(x => x.IdentidadPortalId).IsUnique().HasFilter("[IdentidadPortalId] IS NOT NULL");
            // Usuario de sistema (placeholder) para integridad referencial mientras no hay auth.
            e.HasData(new Usuario
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

        b.Entity<MapeoRolPortal>(e =>
        {
            e.ToTable("mapeo_rol_portal");
            // Filtrado (no único plano): IdRolPortal queda NULL hasta que TI confirme el
            // id real (ver comentario del seed más abajo), y puede haber varias filas así
            // a la vez.
            e.HasIndex(x => x.IdRolPortal).IsUnique().HasFilter("[IdRolPortal] IS NOT NULL");
            e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.DescripcionRolPortal).HasMaxLength(200);

            // Equivalencias de ALTA confianza por nombre (docs/contrato-integracion,
            // sección 2). El resto de los 6 roles de Nexo (CoordinadorFletes,
            // LiderProceso, ServiciosLogisticos) no tienen candidato todavía: Pendiente
            // P2, a confirmar por negocio. IdRolPortal queda NULL a propósito: no hay
            // todavía un IDROLES real confirmado contra la base de identidad, y adivinar
            // uno arriesgaría matchear por accidente el rol de otra persona. La resolución
            // por nombre (ResolverIdentidadPortalAdapter) alcanza mientras tanto; cuando
            // TI confirme el id real, se completa esta columna con un UPDATE, sin deploy.
            e.HasData(
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000001"), IdRolPortal = null, DescripcionRolPortal = "TarifasComercial", Rol = RolUsuario.Comercial, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000002"), IdRolPortal = null, DescripcionRolPortal = "TarifasAdministrador", Rol = RolUsuario.AdminTarifas, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000003"), IdRolPortal = null, DescripcionRolPortal = "Administracion", Rol = RolUsuario.Admin, CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                // Roles nuevos creados en el portal ya con el sufijo "NEXO" (uno por
                // cada rol de Cabina de Fletes, IDROLES real de tblSAPRoles en
                // dbSolucionesTI — ver docs/contrato-integracion): reemplazan en la
                // práctica a los 3 de arriba para las pruebas de identidad real.
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000004"), IdRolPortal = 48, DescripcionRolPortal = "ComercialNEXO", Rol = RolUsuario.Comercial, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000005"), IdRolPortal = 49, DescripcionRolPortal = "CoordinadorFletesNEXO", Rol = RolUsuario.CoordinadorFletes, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000006"), IdRolPortal = 50, DescripcionRolPortal = "AdminTarifasNEXO", Rol = RolUsuario.AdminTarifas, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000007"), IdRolPortal = 51, DescripcionRolPortal = "ServiciosLogisticosNEXO", Rol = RolUsuario.ServiciosLogisticos, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000008"), IdRolPortal = 52, DescripcionRolPortal = "LiderProcesoNEXO", Rol = RolUsuario.LiderProceso, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) },
                new MapeoRolPortal { Id = Guid.Parse("00000000-0000-0000-0001-000000000009"), IdRolPortal = 53, DescripcionRolPortal = "AdminNEXO", Rol = RolUsuario.Admin, CreatedAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) }
            );
        });

        b.Entity<Cliente>(e =>
        {
            e.ToTable("clientes");
            e.HasIndex(x => x.Nit).IsUnique();
        });

        b.Entity<Solicitud>(e =>
        {
            e.ToTable("solicitudes");
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
            e.HasOne(x => x.Cliente)
             .WithMany(c => c.Solicitudes)
             .HasForeignKey(x => x.ClienteId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Creador)
             .WithMany()
             .HasForeignKey(x => x.CreadoPor)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Cotizaciones)
             .WithOne(c => c.Solicitud)
             .HasForeignKey(c => c.SolicitudId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LineaSolicitud>(e =>
        {
            e.ToTable("lineas_solicitud");
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
            e.HasOne(x => x.Solicitud)
             .WithMany(s => s.Lineas)
             .HasForeignKey(x => x.SolicitudId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ruta)
             .WithMany()
             .HasForeignKey(x => x.RutaId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ConfiguracionVehiculo)
             .WithMany()
             .HasForeignKey(x => x.ConfigVehiculoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Ruta>(e =>
        {
            e.ToTable("rutas");
            e.HasIndex(x => new { x.Origen, x.Destino }).IsUnique();
        });

        b.Entity<ConfiguracionVehiculo>(e => e.ToTable("configuraciones_vehiculo"));

        b.Entity<TarifaSicetac>(e =>
        {
            e.ToTable("tarifas_sicetac");
            e.Property(x => x.CondicionCarga).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ValorPiso).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorHora).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorSiceHora).HasColumnType("decimal(18,2)");
            e.Property(x => x.HorasPactadas).HasColumnType("decimal(9,2)");
            e.Property(x => x.HorasRecorrido).HasColumnType("decimal(9,2)");
            // Llave natural del piso (resolución al abrir una línea).
            e.HasIndex(x => new
            {
                x.Periodo, x.OrigenDane, x.DestinoDane, x.Configuracion,
                x.TipoCarga, x.UnidadTransporte, x.CondicionCarga
            }).IsUnique();
            e.HasOne(x => x.Ruta)
             .WithMany()
             .HasForeignKey(x => x.RutaId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TarifaHistorica>(e =>
        {
            e.ToTable("tarifas_historicas");
            e.Property(x => x.Tipologia).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Valor).HasColumnType("decimal(18,2)");
            e.HasIndex(x => new { x.RutaId, x.ConfigVehiculoId, x.Tipologia });
            e.HasOne(x => x.Ruta)
             .WithMany()
             .HasForeignKey(x => x.RutaId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ConfiguracionVehiculo)
             .WithMany()
             .HasForeignKey(x => x.ConfigVehiculoId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cotizacion)
             .WithMany()
             .HasForeignKey(x => x.CotizacionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ServicioConexo>(e =>
        {
            e.ToTable("servicios_conexos");
            e.Property(x => x.ValorBase).HasColumnType("decimal(18,2)");
            e.Property(x => x.Activo).HasDefaultValue(true);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.ActualizadoPorUsuario)
             .WithMany()
             .HasForeignKey(x => x.ActualizadoPor)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Cotizacion>(e =>
        {
            e.ToTable("cotizaciones");
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20)
             .HasDefaultValue(EstadoCotizacion.Borrador);
            e.Property(x => x.Resultado).HasConversion<string>().HasMaxLength(20)
             .HasDefaultValue(ResultadoCotizacion.SinDefinir);
            e.Property(x => x.Total).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasMany(x => x.Lineas).WithOne().HasForeignKey(x => x.CotizacionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Conexos).WithOne().HasForeignKey(x => x.CotizacionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EntregadaPorUsuario)
             .WithMany()
             .HasForeignKey(x => x.EntregadaPor)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CotizacionLinea>(e =>
        {
            e.ToTable("cotizacion_lineas");
            e.Property(x => x.Fuente).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ValorFlete).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorAprovisionamiento).HasColumnType("decimal(18,2)");
            e.Property(x => x.ValorRetornoContenedor).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.LineaSolicitud)
             .WithMany()
             .HasForeignKey(x => x.LineaSolicitudId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CotizacionConexo>(e =>
        {
            e.ToTable("cotizacion_conexos");
            e.Property(x => x.Cantidad).HasColumnType("decimal(12,2)");
            e.Property(x => x.ValorAplicado).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.ServicioConexo)
             .WithMany()
             .HasForeignKey(x => x.ServicioConexoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasIndex(x => new { x.Entidad, x.EntidadId });
            e.HasOne(x => x.Usuario)
             .WithMany()
             .HasForeignKey(x => x.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ------------------------------------------------------------- Cubicador 3D
        b.Entity<VehiculoCubicacion>(e =>
        {
            e.ToTable("vehiculos_cubicacion");
            foreach (var p in new[] { "LargoInternoMm", "AnchoInternoMm", "AltoInternoMm", "CapacidadKg", "TaraKg",
                                       "PuertaAnchoMm", "PuertaAltoMm", "ToleranciaParedesMm", "ToleranciaEntreUnidadesMm" })
                e.Property(p).HasColumnType("decimal(12,2)");
        });

        b.Entity<PlanCargue>(e =>
        {
            e.ToTable("planes_cargue");
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

        b.Entity<ItemPlanCargue>(e =>
        {
            e.ToTable("items_plan_cargue");
            foreach (var p in new[] { "LargoMm", "AnchoMm", "AltoMm", "PesoKg", "MaxPesoEncimaKg" })
                e.Property(p).HasColumnType("decimal(12,2)");
        });

        b.Entity<ColocacionCargue>(e =>
        {
            e.ToTable("colocaciones_cargue");
            foreach (var p in new[] { "X", "Y", "Z", "LargoColocadoMm", "AnchoColocadoMm", "AltoColocadoMm",
                                       "PorcentajeSoporte", "PesoEncimaKg" })
                e.Property(p).HasColumnType("decimal(12,2)");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemPlanCargueId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PlanCargueId, x.Cargada });
        });

        b.Entity<AjusteVigenciaPlaza>(e =>
        {
            e.ToTable("ajustes_vigencia_plaza");
            // Un ajuste por tarifa de plaza (override): clave natural única.
            e.HasIndex(x => x.TarifaPlazaId).IsUnique();
            e.Property(x => x.Motivo).HasMaxLength(300);
        });

        b.Entity<ReglaSoloSicetac>(e =>
        {
            e.ToTable("reglas_solo_sicetac");
            // Clave natural única: no repetir la misma ruta + tipo de vehículo.
            e.HasIndex(x => new { x.Origen, x.Destino, x.TipoVehiculo }).IsUnique();
        });

        b.Entity<ParametrosScore>(e =>
        {
            e.ToTable("parametros_score");
            e.Property(x => x.ValorReferencia).HasColumnType("decimal(18,2)");
            e.Property(x => x.RowVersion).IsRowVersion();
            // Fila única (singleton) con los valores por defecto del algoritmo.
            e.HasData(new ParametrosScore
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
        // el camino principal del módulo piloto— y llevaba así desde antes del sprint 2.
        // No se veía porque la suite de Api.Tests no se corría completa.
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
