using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CabinaFletes.Domain.Common;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Domain.Usuarios.Entidades;

public class Usuario : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Nombre { get; set; } = null!;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    public RolUsuario Rol { get; set; }

    /// <summary>Vínculo con el directorio corporativo. Dato interno: no se expone en el API.</summary>
    [MaxLength(100)]
    [JsonIgnore]
    public string? AdObjectId { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
