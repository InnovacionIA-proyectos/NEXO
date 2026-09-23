using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Domain.BibliotecaTarifas.Entidades;

/// <summary>Caché del piso regulatorio SICE-TAC, importada desde el Excel exportado de la fuente.</summary>
public class clsTarifaSicetac : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ID del registro en la fuente (columna ID del Excel). Idempotencia del import.</summary>
    public long? SiceId { get; set; }

    /// <summary>Periodo AAAAMM (columna PERIODO). Base de la vigencia mensual del Semáforo.</summary>
    [Range(200001, 209912)]
    public int Periodo { get; set; }

    public Guid? RutaId { get; set; }

    [JsonIgnore]
    public clsRuta? clsRuta { get; set; }

    [Required, MaxLength(12)]
    public string OrigenDane { get; set; } = null!;

    [Required, MaxLength(12)]
    public string DestinoDane { get; set; } = null!;

    [MaxLength(300)]
    public string? NombreRuta { get; set; }

    [Required, MaxLength(50)]
    public string Configuracion { get; set; } = null!;

    [Required, MaxLength(80)]
    public string TipoCarga { get; set; } = null!;

    [Required, MaxLength(80)]
    public string UnidadTransporte { get; set; } = null!;

    public CondicionCarga CondicionCarga { get; set; }

    /// <summary>VALOR: el flete mínimo (piso). Es el costo base que se muestra al coordinador.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorPiso { get; set; }

    /// <summary>VALORHORA: hora de tiempo logístico (cargue/descargue).</summary>
    public decimal? ValorHora { get; set; }
    public decimal? HorasPactadas { get; set; }
    public decimal? HorasRecorrido { get; set; }

    /// <summary>VALORSICEHORA = ValorPiso + ValorHora * HorasPactadas (referencia).</summary>
    public decimal? ValorSiceHora { get; set; }

    public bool? ViaEstandar { get; set; }

    public DateTimeOffset? FechaCreacionSice { get; set; }
    public DateTimeOffset? FechaModificacionSice { get; set; }

    /// <summary>Cuándo nuestro sistema importó este registro (dato de dominio del import).</summary>
    public DateTimeOffset ImportadoAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ImportadoPor { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

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
