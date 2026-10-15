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

    /// <summary>
    /// Valor del aprovisionamiento: reposicionar el contenedor vacío desde la ciudad
    /// de levante hasta el origen del cargue. El viaje vacío tiene piso regulado
    /// (Resolución 16075, matriz SICE-TAC condición VACIO).
    /// </summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAprovisionamiento { get; set; }

    /// <summary>
    /// S4-HU-15.5/#251: horas de cargue/descargue EFECTIVAS de esta línea — ya
    /// resueltas (clsResolucionHorasLinea.Resolver) al momento de la captura, no el
    /// valor crudo que mandó el Comercial. Quedan trazadas acá para auditoría/consulta
    /// posterior de qué horas se usaron realmente en el cálculo SICE de esta línea
    /// (clsPisoConHoras.Calcular). Nulas en líneas capturadas ANTES de este cambio.
    /// </summary>
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

    /// <summary>
    /// S5-HU-18.3/#275: id de la ruta que Cotifly asigna cuando una carga heterogénea se
    /// reparte en varios camiones — todas las líneas de la misma ruta llegan con el mismo
    /// valor. NEXO lo guarda tal cual y NUNCA lo genera: lo pone quien crea las líneas del grupo
    /// (Cotifly, o el portal al crear una línea por camión — S5-HU-17.8); líneas nacidas directo en NEXO
    /// quedan en null (no agrupan con nada, y eso no rompe nada existente).
    /// </summary>
    public Guid? GrupoRutaId { get; set; }

    /// <summary>
    /// S5-HU-18.3/#275: orden de captura dentro de la solicitud (1, 2, 3...) — permite
    /// saber cuál línea de un GrupoRutaId es "la primera" (donde se cuelgan los servicios
    /// conexos de la ruta). Nulo en líneas capturadas antes de este cambio.
    /// </summary>
    public int? NumeroLinea { get; set; }

    /// <summary>
    /// S5-HU-18.2/#274: id de la recomendación de vehículo del Cubicador que corresponde a
    /// esta línea — permite cruzar lo recomendado contra lo realmente despachado más
    /// adelante. Nulo si la línea nunca se cubicó.
    /// </summary>
    [MaxLength(64)]
    public string? IdRecomendacionVehiculo { get; set; }

    public EstadoLinea EstadoLinea { get; set; } = EstadoLinea.PorCostear;

    /// <summary>
    /// Por qué la línea sigue en PorCostear: sin tarifa válida (requiere al Administrador
    /// de Tarifas) o una fuente que falló. Se muestra al usuario, no es para depurar. Nulo
    /// mientras no se haya intentado costear, y se limpia cuando la línea queda resuelta.
    /// </summary>
    [MaxLength(500)]
    public string? MotivoPendiente { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
