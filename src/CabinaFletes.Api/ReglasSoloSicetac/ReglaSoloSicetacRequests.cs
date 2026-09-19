using System.ComponentModel.DataAnnotations;

namespace CabinaFletes.Api.ReglasSoloSicetac;

public class CrearReglaSoloSicetacRequest
{
    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    [Required, MaxLength(40)]
    public string TipoVehiculo { get; set; } = null!;
}
