using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Solicitudes.Entidades;

/// <summary>Cada combinación a costear: 1 para spot, ~500 para un pliego de licitación.</summary>
public class clsLineaSolicitud : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SolicitudId { get; set; }

    [JsonIgnore]
    public clsSolicitud? clsSolicitud { get; set; }

    public Guid? RutaId { get; set; }

    [JsonIgnore]
    public clsRuta? clsRuta { get; set; }

    public Guid? ConfigVehiculoId { get; set; }

    [JsonIgnore]
    public clsConfiguracionVehiculo? clsConfiguracionVehiculo { get; set; }

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

    /// <summary>Valor del aprovisionamiento: reposicionar el contenedor vacío desde la ciudad de levante hasta el origen (piso regulado SICE-TAC, condición VACIO).</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAprovisionamiento { get; set; }

    /// <summary>Horas de cargue efectivas, ya resueltas al capturar (no el valor crudo del Comercial); son las que usó el cálculo SICE. Nulas en líneas antiguas.</summary>
    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasCargue { get; set; }

    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasDescargue { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? Peso { get; set; }

    /// <summary>Dimensiones serializadas como JSON.</summary>
    public string? Dimensiones { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorMercancia { get; set; }

    /// <summary>Id de la ruta que asigna Cotifly cuando una carga heterogénea se reparte en varios camiones; NEXO no lo genera. Null: la línea no agrupa con otras.</summary>
    public Guid? GrupoRutaId { get; set; }

    /// <summary>Orden de captura dentro de la solicitud (1, 2, 3...); la primera línea de un GrupoRutaId es donde se cuelgan los conexos de la ruta.</summary>
    public int? NumeroLinea { get; set; }

    /// <summary>Id de la recomendación de vehículo del Cubicador para esta línea. Nulo si nunca se cubicó.</summary>
    [MaxLength(64)]
    public string? IdRecomendacionVehiculo { get; set; }

    public EstadoLinea EstadoLinea { get; set; } = EstadoLinea.PorCostear;

    /// <summary>Por qué el Administrador de Tarifas rechazó la línea. Solo con <c>EstadoLinea = Rechazada</c>; se limpia al deshacer el rechazo.</summary>
    [MaxLength(500)]
    public string? MotivoRechazo { get; set; }

    /// <summary>Quién rechazó la línea y cuándo.</summary>
    public Guid? RechazadaPor { get; set; }
    public DateTimeOffset? RechazadaAt { get; set; }

    /// <summary>Por qué la línea sigue en PorCostear (sin tarifa válida o una fuente falló). Se muestra al usuario; se limpia al resolverse.</summary>
    [MaxLength(500)]
    public string? MotivoPendiente { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
