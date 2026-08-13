using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CabinaFletes.Domain.Entities;

public class Cliente : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Nombre { get; set; } = null!;

    /// <summary>NIT colombiano: 5–15 dígitos con dígito de verificación opcional.</summary>
    [Required, MaxLength(20)]
    [RegularExpression(@"^\d{5,15}(-\d)?$", ErrorMessage = "NIT inválido (formato esperado: dígitos, opcionalmente -DV).")]
    public string Nit { get; set; } = null!;

    [MaxLength(250)]
    public string? RazonSocial { get; set; }

    /// <summary>Lista de teléfonos serializada como JSON (jsonb en el modelo, nvarchar(max) en SQL Server).</summary>
    public string? Telefonos { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Relación inversa — no se serializa (evita ciclos; se expone vía DTO cuando hace falta).
    [JsonIgnore]
    public ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();
}
