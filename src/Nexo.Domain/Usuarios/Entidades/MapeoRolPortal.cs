using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Usuarios.Entidades;

/// <summary>
/// Traduce un rol del catálogo de la administración (ajeno, mucho más amplio) a uno
/// de los 6 roles de Nexo. Resuelve los pendientes P1/P2/P3 de
/// docs/contrato-integracion/README.md: la equivalencia real la confirma negocio: esta
/// tabla es donde queda una vez acordada, sin requerir un deploy para cambiarla.
/// </summary>
public class MapeoRolPortal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>IDROLES en la tabla de la administración. Único cuando no es null: la
    /// resolución busca primero por esta columna. Null mientras el id real no está
    /// confirmado (ver DescripcionRolPortal) — nunca se rellena con un valor adivinado,
    /// porque un id equivocado podría matchear por accidente el rol de otra persona.</summary>
    public long? IdRolPortal { get; set; }

    /// <summary>DESCRIPCION del rol en la administración. Respaldo de la resolución
    /// cuando el id no matchea (p. ej. tras un cambio de base física del portal) —
    /// ver docs/contrato-integracion/README.md sección 2 (Pendiente P2).</summary>
    [MaxLength(200)]
    public string? DescripcionRolPortal { get; set; }

    public RolUsuario Rol { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
