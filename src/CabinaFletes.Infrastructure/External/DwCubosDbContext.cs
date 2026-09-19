using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.External;

/// <summary>
/// Business partner solicitante (cliente real) del DW corporativo
/// (DWCubosOperacionesEBS.dbo.DimBPSolicitante). SOLO LECTURA.
/// </summary>
public class BpSolicitante
{
    public string Bp { get; set; } = null!;
    public string Nit { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Tipo { get; set; } = null!;
}

/// <summary>
/// Contexto de SOLO LECTURA contra el DW de cubos de operaciones (base
/// DWCubosOperacionesEBS, misma instancia que DWSice). NoTracking y SaveChanges
/// bloqueado, igual que los demás contextos externos.
/// </summary>
public class DwCubosDbContext : DbContext
{
    public DwCubosDbContext(DbContextOptions<DwCubosDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<BpSolicitante> Solicitantes => Set<BpSolicitante>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<BpSolicitante>();
        e.ToTable("DimBPSolicitante", "dbo");
        e.HasKey(x => x.Bp);
        e.Property(x => x.Bp).HasColumnName("BP").HasMaxLength(20);
        e.Property(x => x.Nit).HasColumnName("NIT").HasMaxLength(20);
        e.Property(x => x.Nombre).HasColumnName("NOMBRE").HasMaxLength(100);
        e.Property(x => x.Tipo).HasColumnName("TIPO").HasMaxLength(100);
    }

    public override int SaveChanges() => throw ReadOnly();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() =>
        new("DwCubosDbContext es de solo lectura: no se permiten escrituras contra el Data Warehouse.");
}
