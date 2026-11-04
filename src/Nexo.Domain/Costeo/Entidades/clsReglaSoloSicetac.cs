using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Domain.Costeo.Entidades;

/// <summary>
/// Regla "solo SICE-TAC" del Administrador de Fletes: para un origen + destino + tipo de vehículo la propuesta se
/// resuelve solo con el piso SICE-TAC, sin comparar contra plaza. Los valores se guardan normalizados para que la coincidencia sea estable.
/// </summary>
public class clsReglaSoloSicetac : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    /// <summary>Tipo/configuración de vehículo (p. ej. 3S3, 2L3), como en clsConfiguracionVehiculo.TipoVehiculo.</summary>
    [Required, MaxLength(40)]
    public string TipoVehiculo { get; set; } = null!;

    public Guid? CreadoPor { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
