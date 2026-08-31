using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.External;

/// <summary>Fila de tblSAPUsuariosApp: catálogo de personas del portal.</summary>
public class UsuarioPortalDw
{
    public long Id { get; set; }
    public string NombreUsuario { get; set; } = null!;
    public string? Email { get; set; }
    public string? DescripcionUsuario { get; set; }
}

/// <summary>Fila de tblSAPUsuarioRoles: relación N a N persona-rol.</summary>
public class UsuarioRolPortalDw
{
    public long IdUsuario { get; set; }
    public long IdRoles { get; set; }
}

/// <summary>Fila de tblSAPRoles: catálogo de TODOS los roles del portal (no solo los de
/// Cabina de Fletes).</summary>
public class RolPortalDw
{
    public long IdRoles { get; set; }
    public string Descripcion { get; set; } = null!;
}

/// <summary>
/// Contexto de SOLO LECTURA contra la base de identidad del portal de la Administración
/// (docs/contrato-integracion/README.md, sección 7). Nombres de tabla/columna según el
/// esquema de referencia de la integración; confirmar contra sys.columns de la base real
/// antes de desplegar (mismo criterio que los demás Data Warehouses de solo lectura: no
/// asumir, verificar). El nombre de la base física (distinta entre QA y Producción) va
/// en la cadena de conexión (ConnectionStrings:IdentidadPortal), nunca hardcodeado aquí.
/// </summary>
public class IdentidadPortalDbContext : DbContext
{
    public IdentidadPortalDbContext(DbContextOptions<IdentidadPortalDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<UsuarioPortalDw> UsuariosApp => Set<UsuarioPortalDw>();
    public DbSet<UsuarioRolPortalDw> UsuarioRoles => Set<UsuarioRolPortalDw>();
    public DbSet<RolPortalDw> Roles => Set<RolPortalDw>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var usuarios = b.Entity<UsuarioPortalDw>();
        usuarios.ToTable("tblSAPUsuariosApp", "dbo", t => t.ExcludeFromMigrations());
        usuarios.HasKey(x => x.Id);
        usuarios.Property(x => x.Id).HasColumnName("ID");
        usuarios.Property(x => x.NombreUsuario).HasColumnName("NOMBREUSUARIO");
        usuarios.Property(x => x.Email).HasColumnName("EMAIL");
        usuarios.Property(x => x.DescripcionUsuario).HasColumnName("DESCRIPCIONUSUARIO");

        var usuarioRoles = b.Entity<UsuarioRolPortalDw>();
        usuarioRoles.ToTable("tblSAPUsuarioRoles", "dbo", t => t.ExcludeFromMigrations());
        usuarioRoles.HasKey(x => new { x.IdUsuario, x.IdRoles });
        usuarioRoles.Property(x => x.IdUsuario).HasColumnName("IDUSUARIO");
        usuarioRoles.Property(x => x.IdRoles).HasColumnName("IDROLES");

        var roles = b.Entity<RolPortalDw>();
        roles.ToTable("tblSAPRoles", "dbo", t => t.ExcludeFromMigrations());
        roles.HasKey(x => x.IdRoles);
        roles.Property(x => x.IdRoles).HasColumnName("IDROLES");
        roles.Property(x => x.Descripcion).HasColumnName("DESCRIPCION");
    }

    public override int SaveChanges() => throw ReadOnly();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) => throw ReadOnly();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) => throw ReadOnly();

    private static InvalidOperationException ReadOnly() =>
        new("IdentidadPortalDbContext es de solo lectura: nunca se escribe en la tabla de la administración.");
}
