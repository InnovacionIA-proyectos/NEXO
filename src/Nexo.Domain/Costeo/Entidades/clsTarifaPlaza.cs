using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Domain.Costeo.Entidades;

/// <summary>
/// Tarifa de plaza gobernada por Nexo (S4-HU-10.1) — recrea el formulario de captura
/// que hoy vive en un sistema externo; NO es una copia de solo lectura de tblSAPTarifas.
/// Tabla propia (<c>tbl_tarifas_plaza</c>, creada y poblada a mano en S4-HU-10.1 con 271
/// filas reales — ver docs/Database/seeder/tarifas_plaza.csv).
///
/// Origen/Destino referencian el catálogo propio de municipios (<see cref="clsMunicipio"/>).
/// <see cref="PesoId"/> (banda, <see cref="clsRangoPeso"/>) y <see cref="TipoVehiculo"/> NO
/// son mutuamente excluyentes (verificado contra datos reales, S4-HU-10.1): las tarifas de
/// contenedor traen ambos (el tipo de vehículo se deriva de la banda de peso), las
/// nacionales solo <see cref="TipoVehiculo"/>. Lo único inválido es que falten las dos.
///
/// LineaNegocio/ClaseViaje/ClaseContenedor guardan el NOMBRE completo (no el Id) del
/// catálogo externo (tblSAPLineaNegocio/tblSAPClaseViaje/tblSAPClaseContenedor,
/// dbSolucionesTI): ese Id es de otra base de datos, en otro servidor — no significa nada
/// dentro de Nexo, así que el nombre deja la fila legible sin consultar el catálogo externo.
///
/// Única por sus 7 dimensiones (línea de negocio + clase de viaje + origen + destino +
/// clase de contenedor + rango de peso + tipo de vehículo). Valor/Vigencia/DetallesTarifa
/// es lo que cambia con el tiempo para una misma combinación — cada actualización queda
/// archivada por un trigger de BD en tbl_tarifas_plaza_historico (no se escribe desde acá).
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
