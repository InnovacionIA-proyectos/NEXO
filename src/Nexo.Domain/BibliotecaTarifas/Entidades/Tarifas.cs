using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Domain.BibliotecaTarifas.Entidades;

/// <summary>Tarifa ganada en el pasado, reutilizable (Biblioteca de Tarifas Ganadas).</summary>
public class clsTarifaHistorica : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? RutaId { get; set; }

    [JsonIgnore]
    public clsRuta? clsRuta { get; set; }

    public Guid? ConfigVehiculoId { get; set; }

    [JsonIgnore]
    public clsConfiguracionVehiculo? clsConfiguracionVehiculo { get; set; }

    public TipologiaCarga? Tipologia { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Valor { get; set; }

    public DateOnly Fecha { get; set; }

    /// <summary>Cotización ganada que originó esta tarifa (realimenta la Biblioteca).</summary>
    public Guid? CotizacionId { get; set; }

    [JsonIgnore]
    public clsCotizacion? clsCotizacion { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
