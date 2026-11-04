using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.External;

/// <summary>Fila de tblSAPUsuariosApp: catálogo de personas del portal.</summary>
public class clsUsuarioPortalDw
{
    public long Id { get; set; }
    public string NombreUsuario { get; set; } = null!;
    public string? Email { get; set; }
    public string? DescripcionUsuario { get; set; }
}

/// <summary>Fila de tblSAPUsuarioRoles: relación N a N persona-rol.</summary>
public class clsUsuarioRolPortalDw
{
    public long IdUsuario { get; set; }
    public long IdRoles { get; set; }
}

/// <summary>Fila de tblSAPRoles: catálogo de TODOS los roles del portal (no solo los de
/// Nexo).</summary>
public class clsRolPortalDw
{
    public long IdRoles { get; set; }
    public string Descripcion { get; set; } = null!;
}

/// <summary>Contexto de SOLO LECTURA contra la base de identidad del portal. El nombre de la base física
/// (distinto en QA y Producción) va en ConnectionStrings:IdentidadPortal.</summary>
public class clsIdentidadPortalDbContext : DbContext
{
    public clsIdentidadPortalDbContext(DbContextOptions<clsIdentidadPortalDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<clsUsuarioPortalDw> UsuariosApp => Set<clsUsuarioPortalDw>();
    public DbSet<clsUsuarioRolPortalDw> UsuarioRoles => Set<clsUsuarioRolPortalDw>();
    public DbSet<clsRolPortalDw> Roles => Set<clsRolPortalDw>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Tipos reales: tblSAPUsuariosApp.ID es numeric(18,0) y los IDROLES/IDUSUARIO son int, no bigint.
        // Sin HasConversion + HasColumnType explícitos, SqlClient lanza InvalidCastException al leer.
        var usuarios = b.Entity<clsUsuarioPortalDw>();
        usuarios.ToTable("tblSAPUsuariosApp", "dbo", t => t.ExcludeFromMigrations());
        usuarios.HasKey(x => x.Id);
        usuarios.Property(x => x.Id).HasColumnName("ID")
            .HasConversion(v => (decimal)v, v => (long)v).HasColumnType("numeric(18,0)");
        usuarios.Property(x => x.NombreUsuario).HasColumnName("NOMBREUSUARIO");
        usuarios.Property(x => x.Email).HasColumnName("EMAIL");
        usuarios.Property(x => x.DescripcionUsuario).HasColumnName("DESCRIPCIONUSUARIO");

        var usuarioRoles = b.Entity<clsUsuarioRolPortalDw>();
        usuarioRoles.ToTable("tblSAPUsuarioRoles", "dbo", t => t.ExcludeFromMigrations());
        usuarioRoles.HasKey(x => new { x.IdUsuario, x.IdRoles });
        usuarioRoles.Property(x => x.IdUsuario).HasColumnName("IDUSUARIO")
            .HasConversion(v => (int)v, v => (long)v).HasColumnType("int");
        usuarioRoles.Property(x => x.IdRoles).HasColumnName("IDROLES")
            .HasConversion(v => (int)v, v => (long)v).HasColumnType("int");

        var roles = b.Entity<clsRolPortalDw>();
        roles.ToTable("tblSAPRoles", "dbo", t => t.ExcludeFromMigrations());
        roles.HasKey(x => x.IdRoles);
        roles.Property(x => x.IdRoles).HasColumnName("IDROLES")
            .HasConversion(v => (int)v, v => (long)v).HasColumnType("int");
        roles.Property(x => x.Descripcion).HasColumnName("DESCRIPCION");
    }

    public override int SaveChanges() => throw ReadOnly();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() =>
        new("clsIdentidadPortalDbContext es de solo lectura: nunca se escribe en la tabla de la administración.");
}
