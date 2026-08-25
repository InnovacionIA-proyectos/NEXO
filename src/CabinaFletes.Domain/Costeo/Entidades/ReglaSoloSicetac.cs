using System.ComponentModel.DataAnnotations;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Common;

namespace CabinaFletes.Domain.Costeo.Entidades;

/// <summary>
/// Regla "solo SICE-TAC" demarcada por el Administrador de Fletes: para una combinación
/// de origen + destino + tipo de vehículo, la propuesta se resuelve EXCLUSIVAMENTE con el
/// piso SICE-TAC (sin consultar ni comparar contra plaza/tarifario). Las solicitudes que
/// hagan match se auto-costean y quedan atendidas para el comercial de inmediato.
///
/// Los valores se guardan normalizados (mayúsculas, sin espacios sobrantes) para que la
/// coincidencia sea estable; el índice único evita duplicados.
/// </summary>
public class ReglaSoloSicetac : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    /// <summary>Tipo/configuración de vehículo (p. ej. 3S3, 2L3), como en ConfiguracionVehiculo.TipoVehiculo.</summary>
    [Required, MaxLength(40)]
    public string TipoVehiculo { get; set; } = null!;

    public Guid? CreadoPor { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
