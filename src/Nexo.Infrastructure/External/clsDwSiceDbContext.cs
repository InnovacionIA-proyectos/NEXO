using Nexo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.External;

/// <summary>
/// Contexto de SOLO LECTURA contra el Data Warehouse corporativo (base DWSice).
/// El usuario de conexión solo tiene permisos de lectura; además este contexto
/// deshabilita el tracking y bloquea SaveChanges como defensa en profundidad.
/// </summary>
public class clsDwSiceDbContext : DbContext
{
    public clsDwSiceDbContext(DbContextOptions<clsDwSiceDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<clsRutaSicetacDw> RutasSicetac => Set<clsRutaSicetacDw>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<clsRutaSicetacDw>();
        e.ToTable("tblRutasSicetac", "dbo");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.Periodo).HasColumnName("PERIODO");
        e.Property(x => x.Configuracion).HasColumnName("CONFIGURACION").HasMaxLength(10);
        e.Property(x => x.Valor).HasColumnName("VALOR").HasColumnType("numeric(18,5)");
        e.Property(x => x.NombreRuta).HasColumnName("NOMBRERUTA").HasMaxLength(500);
        e.Property(x => x.NombreTipoCarga).HasColumnName("NOMBRETIPOCARGA").HasMaxLength(50);
        e.Property(x => x.NombreUnidadTransporte).HasColumnName("NOMBREUNIDADTRANSPORTE").HasMaxLength(50);
        e.Property(x => x.FechaCreacion).HasColumnName("FECHACREACION");
        e.Property(x => x.FechaModificacion).HasColumnName("FECHAMODIFICACION");
        e.Property(x => x.Origen).HasColumnName("ORIGEN").HasMaxLength(50);
        e.Property(x => x.Destino).HasColumnName("DESTINO").HasMaxLength(50);
        e.Property(x => x.ValorHora).HasColumnName("VALORHORA").HasColumnType("numeric(18,5)");
        e.Property(x => x.ValorSiceConHora).HasColumnName("VALORSICECONHORA").HasColumnType("numeric(18,5)");
        e.Property(x => x.HorasRecorrido).HasColumnName("HORASRECORRIDO").HasColumnType("numeric(18,2)");
        e.Property(x => x.HorasPactadas).HasColumnName("HORASPACTADAS");
        e.Property(x => x.CondicionCarga).HasColumnName("CONDICIONCARGA").HasMaxLength(50);
        e.Property(x => x.ViaEstandar).HasColumnName("VIAESTANDAR").HasMaxLength(3);
    }

    public override int SaveChanges() => throw ReadOnly();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() =>
        new("clsDwSiceDbContext es de solo lectura: no se permiten escrituras contra el Data Warehouse.");
}
