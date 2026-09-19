using Nexo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.External;

/// <summary>
/// Contexto de SOLO LECTURA contra el datacenter corporativo (base dbsolucionesTI).
/// El usuario de conexión solo tiene permisos de lectura; además este contexto
/// deshabilita el tracking y bloquea SaveChanges como defensa en profundidad.
/// </summary>
public class clsDbSolucionesTiDbContext : DbContext
{
    public clsDbSolucionesTiDbContext(DbContextOptions<clsDbSolucionesTiDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<clsSapTarifaLog> TarifasLogSap => Set<clsSapTarifaLog>();
    public DbSet<clsSapTarifaPlaza> TarifasPlazaSap => Set<clsSapTarifaPlaza>();
    public DbSet<clsSapDane> CentrosPobladosDane => Set<clsSapDane>();
    public DbSet<clsSapLineaNegocio> LineasNegocio => Set<clsSapLineaNegocio>();
    public DbSet<clsSapTipoVehiculo> TiposVehiculo => Set<clsSapTipoVehiculo>();
    public DbSet<clsSapTipoCarroseria> TiposCarroseria => Set<clsSapTipoCarroseria>();
    public DbSet<clsSapClaseViaje> ClasesViaje => Set<clsSapClaseViaje>();
    public DbSet<clsSapZonaTarificacionRelacion> ZonasTarificacion => Set<clsSapZonaTarificacionRelacion>();
    public DbSet<clsSapRangoPeso> RangosPeso => Set<clsSapRangoPeso>();
    public DbSet<clsSapContenedorRangoPeso> ContenedorRangosPeso => Set<clsSapContenedorRangoPeso>();
    public DbSet<clsSapParametro> Parametros => Set<clsSapParametro>();
    public DbSet<clsSapClaseContenedorCatalogo> ClasesContenedor => Set<clsSapClaseContenedorCatalogo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ln = modelBuilder.Entity<clsSapLineaNegocio>();
        ln.ToTable("tblSAPLineaNegocio", "dbo");
        ln.HasKey(x => x.Id);
        ln.Property(x => x.Id).HasColumnName("ID");
        ln.Property(x => x.Descripcion).HasColumnName("DESCRIPCION").HasMaxLength(50);

        var tv = modelBuilder.Entity<clsSapTipoVehiculo>();
        tv.ToTable("tblSAPTiposVehiculoTarifario", "dbo");
        tv.HasKey(x => x.Id);
        tv.Property(x => x.Id).HasColumnName("ID");
        tv.Property(x => x.Codigo).HasColumnName("CODIGO").HasMaxLength(50);
        tv.Property(x => x.Descripcion).HasColumnName("DESCRIPCION").HasMaxLength(50);
        tv.Property(x => x.Estado).HasColumnName("ESTADO");

        var tc = modelBuilder.Entity<clsSapTipoCarroseria>();
        tc.ToTable("tblSAPTiposCarroseria", "dbo");
        tc.HasKey(x => x.Id);
        tc.Property(x => x.Id).HasColumnName("ID");
        tc.Property(x => x.Codigo).HasColumnName("CODIGO").HasMaxLength(40);
        tc.Property(x => x.Descripcion).HasColumnName("DESCRIPCION").HasMaxLength(100);

        var cv = modelBuilder.Entity<clsSapClaseViaje>();
        cv.ToTable("tblSAPClaseViaje", "dbo");
        cv.HasKey(x => x.Id);
        cv.Property(x => x.Id).HasColumnName("ID");
        cv.Property(x => x.Descripcion).HasColumnName("DESCRIPCION").HasMaxLength(50);

        var d = modelBuilder.Entity<clsSapDane>();
        d.ToTable("tblsapdane", "dbo");
        d.HasKey(x => x.Id);
        d.Property(x => x.Id).HasColumnName("ID");
        d.Property(x => x.CodigoCentroPoblado).HasColumnName("CODIGOCENTROPOBLADO").HasMaxLength(15);
        d.Property(x => x.NombreDepartamento).HasColumnName("NOMBREDEPARTAMENTO").HasMaxLength(100);
        d.Property(x => x.NombreMunicipio).HasColumnName("NOMBREMUNICIPIO").HasMaxLength(100);
        d.Property(x => x.NombreCentroPoblado).HasColumnName("NOMBRECENTROPOBLADO").HasMaxLength(100);
        d.Property(x => x.NombreMostrar).HasColumnName("NOMBREMOSTRAR").HasMaxLength(100);

        var zt = modelBuilder.Entity<clsSapZonaTarificacionRelacion>();
        zt.ToTable("tblZonasTarificacionRelacion", "dbo");
        zt.HasKey(x => x.Id);
        zt.Property(x => x.Id).HasColumnName("ID");
        zt.Property(x => x.IdZonaTarificacion).HasColumnName("IDZONATARIFICACION").HasMaxLength(50);
        zt.Property(x => x.IdZonaTransporte).HasColumnName("IDZONATRANSPORTE").HasMaxLength(50);

        var rp = modelBuilder.Entity<clsSapRangoPeso>();
        rp.ToTable("tblRangoPeso", "dbo");
        rp.HasKey(x => x.Id);
        rp.Property(x => x.Id).HasColumnName("ID");
        rp.Property(x => x.PesoMinimo).HasColumnName("PESOMINIMO");
        rp.Property(x => x.PesoMaximo).HasColumnName("PESOMAXIMO");
        rp.Property(x => x.TipoVehiculo).HasColumnName("TIPOVEHICULO").HasMaxLength(40);

        var crp = modelBuilder.Entity<clsSapContenedorRangoPeso>();
        crp.ToTable("tblContenedorRangoPeso", "dbo");
        crp.HasKey(x => x.Id);
        crp.Property(x => x.Id).HasColumnName("ID");
        crp.Property(x => x.IdClaseContenedor).HasColumnName("IDCLASECONTENEDOR");
        crp.Property(x => x.IdRangoPeso).HasColumnName("IDRANGOPESO");

        var par = modelBuilder.Entity<clsSapParametro>();
        par.ToTable("tblParametros", "dbo");
        par.HasKey(x => new { x.DataAreaId, x.IdParametro });
        par.Property(x => x.DataAreaId).HasColumnName("DATAAREAID").HasMaxLength(10);
        par.Property(x => x.IdParametro).HasColumnName("IDPARAMETRO");
        par.Property(x => x.ValorParametro).HasColumnName("VALORPARAMETRO").HasMaxLength(200);
        par.Property(x => x.NombreParametro).HasColumnName("NOMBREPARAMETRO").HasMaxLength(200);

        var cc = modelBuilder.Entity<clsSapClaseContenedorCatalogo>();
        cc.ToTable("tblSAPClaseContenedor", "dbo");
        cc.HasKey(x => x.Id);
        cc.Property(x => x.Id).HasColumnName("ID");
        cc.Property(x => x.Descripcion).HasColumnName("DESCRIPCION").HasMaxLength(50);
        cc.Property(x => x.TamanoContenedor).HasColumnName("TAMANOCONTENEDOR");
        cc.Property(x => x.PesoInfCnt).HasColumnName("PESOINFCNT");
        cc.Property(x => x.PesoSupCnt).HasColumnName("PESOSUPCNT");

        // Vigente ("de plaza") y log de cambios comparten el núcleo de columnas.
        var plaza = MapTarifaSap<clsSapTarifaPlaza>(modelBuilder, "tblSAPTarifas");
        plaza.Property(x => x.FechaModificacion).HasColumnName("FECHAMODIFICACION");
        plaza.Property(x => x.UsuarioModificacion).HasColumnName("USUARIOMODIFICACION").HasMaxLength(50);
        plaza.Property(x => x.DetallesTarifa).HasColumnName("DETALLESTARIFA").HasMaxLength(2000);
        plaza.Property(x => x.Consolidado).HasColumnName("CONSOLIDADO");
        plaza.Property(x => x.Exclusivo).HasColumnName("EXCLUSIVO");

        var log = MapTarifaSap<clsSapTarifaLog>(modelBuilder, "tblSAPTarifasLog");
        log.Property(x => x.IdTarifa).HasColumnName("ID_TARIFA");
        // En el log estos códigos llegan como varchar ('1', '4', …): conversión int <-> texto.
        log.Property(x => x.LineaNegocio).HasColumnName("LINEANEGOCIO").HasConversion<string>().HasMaxLength(50);
        log.Property(x => x.ClaseViaje).HasColumnName("CLASEVIAJE").HasConversion<string>().HasMaxLength(50);
    }

    private static Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> MapTarifaSap<T>(
        ModelBuilder modelBuilder, string tabla) where T : clsSapTarifaBase
    {
        var e = modelBuilder.Entity<T>();
        e.ToTable(tabla, "dbo");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.LineaNegocio).HasColumnName("LINEANEGOCIO");
        e.Property(x => x.Origen).HasColumnName("ORIGEN").HasMaxLength(30);
        e.Property(x => x.Destino).HasColumnName("DESTINO").HasMaxLength(30);
        e.Property(x => x.Valor).HasColumnName("VALOR").HasMaxLength(50);
        e.Property(x => x.Vigencia).HasColumnName("VIGENCIA");
        e.Property(x => x.ClaseContenedor).HasColumnName("CLASECONTENEDOR");
        e.Property(x => x.TamanoContenedor).HasColumnName("TAMANOCONTENEDOR").HasMaxLength(10);
        e.Property(x => x.RangoPesoInf).HasColumnName("RANGOPESOINF");
        e.Property(x => x.RangoPesoSup).HasColumnName("RANGOPESOSUP");
        e.Property(x => x.TipoVehiculo).HasColumnName("TIPOVEHICULO").HasMaxLength(40);
        e.Property(x => x.TipoCarroseria).HasColumnName("TIPOCARROSERIA").HasMaxLength(40);
        e.Property(x => x.PesoLimite).HasColumnName("PESOLIMITE").HasMaxLength(40);
        e.Property(x => x.FechaCreacion).HasColumnName("FECHACREACION");
        e.Property(x => x.UsuarioCreacion).HasColumnName("USUARIOCREACION").HasMaxLength(50);
        e.Property(x => x.ClaseViaje).HasColumnName("CLASEVIAJE");
        e.Property(x => x.Devolucion).HasColumnName("DEVOLUCION");
        return e;
    }

    // ---- Defensa en profundidad: este contexto jamás escribe. ----
    public override int SaveChanges() => throw ReadOnly();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() =>
        new("clsDbSolucionesTiDbContext es de solo lectura: no se permiten escrituras contra el datacenter.");
}
