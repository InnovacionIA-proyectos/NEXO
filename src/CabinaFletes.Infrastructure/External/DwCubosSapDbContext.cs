using CabinaFletes.Domain.Catalogos.Entidades;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.External;

/// <summary>
/// Cliente con su comercial responsable en el DW de cubos SAP
/// (DWCubosEBSSAP.dbo.DimCliente). SOLO LECTURA. Es la fuente del
/// CORREORESPONSABLE al que se envía la cotización formal.
/// </summary>
public class DimClienteSap
{
    public string Bp { get; set; } = null!;
    public string Nit { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string IdResponsable { get; set; } = null!;
    public string NombreResponsable { get; set; } = null!;
    public string CorreoResponsable { get; set; } = null!;
}

/// <summary>
/// Contexto de SOLO LECTURA contra DWCubosEBSSAP (misma instancia que DWSice).
/// Igual que los demás contextos externos: NoTracking y SaveChanges bloqueado.
/// </summary>
public class DwCubosSapDbContext : DbContext
{
    public DwCubosSapDbContext(DbContextOptions<DwCubosSapDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<DimClienteSap> ClientesSap => Set<DimClienteSap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<DimClienteSap>();
        e.ToTable("DimCliente", "dbo");
        e.HasKey(x => x.Bp);
        e.Property(x => x.Bp).HasColumnName("BP").HasMaxLength(20);
        e.Property(x => x.Nit).HasColumnName("NIT").HasMaxLength(20);
        e.Property(x => x.Nombre).HasColumnName("NOMBRE").HasMaxLength(100);
        e.Property(x => x.IdResponsable).HasColumnName("IDRESPONSABLE").HasMaxLength(20);
        e.Property(x => x.NombreResponsable).HasColumnName("NOMBRERESPONSABLE").HasMaxLength(80);
        e.Property(x => x.CorreoResponsable).HasColumnName("CORREORESPONSABLE").HasMaxLength(100);
    }

    // ---- Defensa en profundidad: este contexto jamás escribe. ----
    public override int SaveChanges() => throw ReadOnly();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() =>
        new("DwCubosSapDbContext es de solo lectura: no se permiten escrituras contra el Data Warehouse.");
}
