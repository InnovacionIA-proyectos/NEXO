using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CabinaFletes.Domain.Entities;

/// <summary>Registro de auditoría append-only: quién hizo qué y cuándo. No implementa IAuditable
/// (no se actualiza); solo lleva CreatedAt.</summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UsuarioId { get; set; }

    [JsonIgnore]
    public Usuario? Usuario { get; set; }

    [MaxLength(100)]
    public string? Entidad { get; set; }

    public Guid? EntidadId { get; set; }

    [MaxLength(50)]
    public string? Accion { get; set; }

    /// <summary>Diferencia (antes/después) serializada como JSON.</summary>
    public string? Diff { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
