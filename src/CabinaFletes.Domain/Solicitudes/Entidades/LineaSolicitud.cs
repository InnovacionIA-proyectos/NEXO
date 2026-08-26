using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Common;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Domain.Solicitudes.Entidades;

/// <summary>Cada combinación a costear: 1 para spot, ~500 para un pliego de licitación.</summary>
public class LineaSolicitud : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SolicitudId { get; set; }

    [JsonIgnore]
    public Solicitud? Solicitud { get; set; }

    public Guid? RutaId { get; set; }

    [JsonIgnore]
    public Ruta? Ruta { get; set; }

    public Guid? ConfigVehiculoId { get; set; }

    [JsonIgnore]
    public ConfiguracionVehiculo? ConfiguracionVehiculo { get; set; }

    public TipologiaCarga? Tipologia { get; set; }

    /// <summary>Línea de negocio SAP TM (formulario Consultar Tarifa).</summary>
    public LineaNegocio? LineaNegocio { get; set; }

    /// <summary>Clase de viaje SAP TM: Contenedor, Cupo, Carga Suelta o Extradimensionado.</summary>
    public ClaseViaje? ClaseViaje { get; set; }

    /// <summary>Clase de contenedor SAP (CNT20.IT/RE/ST, CNT40.HC/RE). Aplica cuando ClaseViaje = Contenedor.</summary>
    [MaxLength(20)]
    public string? ClaseContenedor { get; set; }

    /// <summary>Servicio con vehículo exclusivo para el cliente.</summary>
    public bool Exclusivo { get; set; }

    /// <summary>Carga consolidada (aplica a devolución de contenedor, línea 6 SAP).</summary>
    public bool Consolidado { get; set; }

    /// <summary>El viaje incluye retornar el contenedor vacío (flujo paralelo línea 6).</summary>
    public bool RetornaContenedor { get; set; }

    /// <summary>Ciudad a la que se devuelve el contenedor vacío al finalizar el viaje.</summary>
    [MaxLength(150)]
    public string? CiudadDevolucionContenedor { get; set; }

    /// <summary>Levante de contenedor en un punto diferente al origen (SAP TM).</summary>
    public bool LevanteContenedorDiferente { get; set; }

    /// <summary>
    /// Ciudad donde se levanta el contenedor vacío (exportación / interior a puerto).
    /// Nula o igual al origen: no hay aprovisionamiento.
    /// </summary>
    [MaxLength(150)]
    public string? CiudadLevanteContenedor { get; set; }

    /// <summary>
    /// Valor del aprovisionamiento: reposicionar el contenedor vacío desde la ciudad
    /// de levante hasta el origen del cargue. El viaje vacío tiene piso regulado
    /// (Resolución 16075, matriz SICE-TAC condición VACIO).
    /// </summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAprovisionamiento { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? Peso { get; set; }

    /// <summary>Dimensiones serializadas como JSON.</summary>
    public string? Dimensiones { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorMercancia { get; set; }

    public EstadoLinea EstadoLinea { get; set; } = EstadoLinea.PorCostear;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
