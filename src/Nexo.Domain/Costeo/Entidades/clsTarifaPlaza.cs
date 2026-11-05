using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Domain.Costeo.Entidades;

/// <summary>
/// Tarifa de plaza gobernada por Nexo (tabla propia, no copia de tblSAPTarifas). Única por sus 7 dimensiones
/// (línea de negocio, clase de viaje, origen, destino, clase de contenedor, rango de peso, tipo de vehículo);
/// Valor/Vigencia/DetallesTarifa cambian con el tiempo y un trigger de BD archiva cada actualización en
/// tbl_tarifas_plaza_historico.
/// <see cref="PesoId"/> y <see cref="TipoVehiculo"/> no son excluyentes (las de contenedor traen ambos); solo es inválido que falten los dos.
/// LineaNegocio/ClaseViaje/ClaseContenedor guardan el nombre, no el Id, porque el Id es de otra base de datos.
/// </summary>
public class clsTarifaPlaza : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string LineaNegocio { get; set; } = null!;

    [Required, MaxLength(50)]
    public string ClaseViaje { get; set; } = null!;

    public int OrigenMunicipioId { get; set; }
    public clsMunicipio? Origen { get; set; }

    public int DestinoMunicipioId { get; set; }
    public clsMunicipio? Destino { get; set; }

    [MaxLength(50)]
    public string? ClaseContenedor { get; set; }

    public int? PesoId { get; set; }
    public clsRangoPeso? Peso { get; set; }

    [MaxLength(40)]
    public string? TipoVehiculo { get; set; }

    [Required]
    public decimal Valor { get; set; }

    [Required]
    public DateOnly Vigencia { get; set; }

    [Required, MaxLength(2000)]
    public string DetallesTarifa { get; set; } = null!;

    public Guid? CreadoPor { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
