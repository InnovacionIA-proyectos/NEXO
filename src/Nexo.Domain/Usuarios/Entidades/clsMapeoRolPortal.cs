using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Usuarios.Entidades;

/// <summary>Traduce un rol del catálogo de la administración (ajeno y más amplio) a uno de los roles de Nexo, sin requerir un deploy para cambiarlo.</summary>
public class clsMapeoRolPortal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>IDROLES en la tabla de la administración; único si no es null y se busca primero por él. Null mientras no esté confirmado: nunca se adivina, un id equivocado podría coincidir con el rol de otra persona.</summary>
    public long? IdRolPortal { get; set; }

    /// <summary>DESCRIPCION del rol en la administración; respaldo cuando el id no coincide (p. ej. tras un cambio de base del portal).</summary>
    [MaxLength(200)]
    public string? DescripcionRolPortal { get; set; }

    public RolUsuario Rol { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
