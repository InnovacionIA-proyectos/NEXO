using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Usuarios.Entidades;

public class clsUsuario : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Nombre { get; set; } = null!;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    public RolUsuario Rol { get; set; }

    /// <summary>
    /// Vínculo con la identidad de la persona en la tabla de usuarios/permisos del
    /// portal de la Administración (encabezado X-Portal-clsUsuario-Id, ver
    /// docs/contrato-integracion). Reemplaza al antiguo AdObjectId de Azure AD,
    /// retirado en S2-HU-03.3. Dato interno: no se expone en el API.
    /// </summary>
    [JsonIgnore]
    public long? IdentidadPortalId { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
