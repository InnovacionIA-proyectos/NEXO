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

    /// <summary>Identidad de la persona en el portal de la Administración. Dato interno: no se expone en el API.</summary>
    [JsonIgnore]
    public long? IdentidadPortalId { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
