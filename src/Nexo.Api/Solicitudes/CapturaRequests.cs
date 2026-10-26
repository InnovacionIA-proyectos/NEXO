using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Enums;

namespace Nexo.Api.Solicitudes;

/// <summary>
/// Alta de una solicitud completa (cabecera + líneas) con origen/destino/vehículo
/// como TEXTO LIBRE: el servicio resuelve o crea las rutas y configuraciones de
/// vehículo del catálogo. Sirve tanto al formulario manual como a la importación
/// de Excel (el frontend mapea columnas → líneas antes de enviar).
/// </summary>
public class clsCapturaSolicitudRequest
{
    public Guid? ClienteId { get; set; }

    [MaxLength(20)]
    public string? ClienteBp { get; set; }

    [Required]
    [RegularExpression("(?i)^(spot|licitacion)$", ErrorMessage = "Tipo debe ser 'spot' o 'licitacion'.")]
    public string Tipo { get; set; } = null!;

    public string? Modalidad { get; set; }

    public DateTimeOffset? Deadline { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorEstimado { get; set; }

    public string? Referencia { get; set; }

    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasCargue { get; set; }

    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasDescargue { get; set; }

    /// <summary>Notas u observaciones comerciales (S4-HU-18.10/#227) — distinto de
    /// <see cref="Referencia"/> (referencia/PO del cliente).</summary>
    [MaxLength(2000)]
    public string? Notas { get; set; }

    [Required, MinLength(1, ErrorMessage = "La solicitud debe traer al menos una línea.")]
    public List<clsCapturaLineaRequest> Lineas { get; set; } = [];
}

public class clsCapturaLineaRequest
{
    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    [MaxLength(100)]
    public string? TipoVehiculo { get; set; }

    [MaxLength(100)]
    public string? Carroceria { get; set; }

    public string? Tipologia { get; set; }

    [MaxLength(100)]
    public string? LineaNegocio { get; set; }

    [MaxLength(50)]
    public string? ClaseViaje { get; set; }

    [MaxLength(20)]
    public string? ClaseContenedor { get; set; }

    public bool? Exclusivo { get; set; }
    public bool? Consolidado { get; set; }
    public bool? RetornaContenedor { get; set; }

    [MaxLength(150)]
    public string? CiudadDevolucionContenedor { get; set; }

    public bool? LevanteContenedorDiferente { get; set; }

    [MaxLength(150)]
    public string? CiudadLevanteContenedor { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAprovisionamiento { get; set; }

    /// <summary>
    /// S4-HU-15.5/#251: horas de cargue/descargue de ESTA línea — sobreescribe el
    /// default (4h+4h; 1h+1h si la línea ES una devolución de contenedor, lineaNegocio=DevolucionContenedor)
    /// cuando el Comercial las conoce de antemano. Sin esto, cae al default de la línea. #300: marcar
    /// RetornaContenedor ya NO cambia estas horas — el tramo vacío de retorno se costea aparte con 1h+1h.
    /// </summary>
    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasCargue { get; set; }

    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasDescargue { get; set; }

    /// <summary>S5-HU-16.1/#254: Id de tbl_municipios elegido en el buscador de ciudades —
    /// null si Origen/Destino viene de texto libre (formulario viejo, Excel sin ese dato).</summary>
    public int? OrigenMunicipioId { get; set; }
    public int? DestinoMunicipioId { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? Peso { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorMercancia { get; set; }

    [MaxLength(200, ErrorMessage = "Una línea admite máximo 200 posiciones.")]
    public List<clsCapturaPosicionRequest>? Posiciones { get; set; }

    /// <summary>
    /// S5-HU-18.3/#275: id de la ruta que agrupa esta línea con las demás de la misma
    /// carga heterogénea (un camión por línea). Lo manda quien crea las líneas del grupo (Cotifly o el
    /// portal; NEXO lo guarda tal cual y nunca lo genera, S5-HU-17.8) — null si la
    /// línea nace directo en NEXO (formulario manual, Excel).
    /// </summary>
    public Guid? GrupoRutaId { get; set; }

    /// <summary>
    /// S5-HU-18.2/#274: id de la recomendación de vehículo del Cubicador que se usó para
    /// capturar esta línea (idRecomendacionVehiculo de POST /api/cubicacion) — permite
    /// cruzar lo recomendado contra lo realmente despachado más adelante. Null si la línea
    /// se capturó sin pasar por el Cubicador.
    /// </summary>
    [MaxLength(64)]
    public string? IdRecomendacionVehiculo { get; set; }
}

/// <summary>
/// Una línea de texto libre a resolver contra el catálogo (sin crear ninguna solicitud):
/// entrada de POST /api/solicitudes/resolver-catalogos.
/// </summary>
public class clsResolverCatalogoLineaRequest
{
    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    [MaxLength(100)]
    public string? TipoVehiculo { get; set; }

    [MaxLength(100)]
    public string? Carroceria { get; set; }
}

/// <summary>Una pieza/bulto a transportar, con dimensiones en centímetros.</summary>
public class clsCapturaPosicionRequest
{
    [MaxLength(200)]
    public string? Descripcion { get; set; }

    /// <summary>
    /// Unidades de esta posición. #304: tope 10.000 — el MISMO que el Cubicador (10.000 unidades totales por
    /// petición); antes admitía 100.000 y una posición así se capturaba y se guardaba, y después el Cubicador rechazaba
    /// la recomendación entera con <c>CUBICACION_DEMASIADO_GRANDE</c>, lejos de donde nació el error. El tope vale
    /// también para la SUMA de las posiciones de una línea (ver clsCapturarSolicitudValidador).
    /// </summary>
    [Range(1, 10000)]
    public int Cantidad { get; set; } = 1;

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal LargoCm { get; set; }

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal AnchoCm { get; set; }

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal AltoCm { get; set; }

    /// <summary>
    /// Peso de UNA SOLA UNIDAD en kg — <b>no el total de la posición</b>. El motor del Cubicador hace
    /// <c>peso_total = pesoKg × cantidad</c> (y la regla de peso de la captura suma <c>cantidad × pesoKg</c>): si aquí
    /// llega el total, se multiplica otra vez por la cantidad y se piden camiones de más (100 cajas de 20 kg: mandar
    /// 2000 en vez de 20 produce 200.000 kg y una flota absurda).
    ///
    /// #304: OBLIGATORIO (> 0). Antes era opcional, pero una posición sin peso no sirve para su único consumidor: el
    /// Cubicador la rechaza (<c>PESO_NO_DECLARADO</c>) porque el peso decide la capacidad del vehículo y ya no se inventa
    /// 1 kg. Exigirlo aquí da el error donde está el error, no más tarde al generar el plan de cargue. Una línea SIN
    /// posiciones no se ve afectada (sigue pudiendo capturarse solo con su <c>peso</c> total).
    /// </summary>
    [Required, Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? PesoKg { get; set; }

    /// <summary>
    /// S5-HU-18.2/#274: código del tipo de mercancía (tbl_tipos_mercancia.CodigoMercancia,
    /// NO el uuid) para ESTA posición — se guarda el código porque es estable, sobrevive un
    /// reseed del catálogo y es legible en logs. Null = tipo desconocido; no se asigna un
    /// default (el tipo mueve la carrocería elegida, un default silencioso produciría una
    /// recomendación con apariencia correcta pero mal fundamentada).
    /// </summary>
    [MaxLength(40)]
    public string? CodigoTipoMercancia { get; set; }
}

/// <summary>
/// Pide una propuesta de mapeo de columnas para UNA hoja de un Excel ya analizado: los encabezados y las filas que devolvió
/// <c>analizar-excel</c>. Solo se usan las primeras 20 filas.
/// </summary>
public class clsSugerirMapeoRequest
{
    /// <summary>El cliente dueño del archivo; su diccionario es la mejor pista. Sin él solo se usa el diccionario general.</summary>
    public Guid? ClienteId { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<string> Encabezados { get; set; } = null!;

    [MaxLength(1000)]
    public List<List<string>?>? Filas { get; set; }
}

/// <summary>Un texto libre de una columna del Excel.</summary>
public class clsValorANormalizarRequest
{
    /// <summary><c>tipoVehiculo</c>, <c>carroceria</c> o <c>ciudad</c> (origen, destino y demás ciudades).</summary>
    [Required, MaxLength(20)]
    public string Campo { get; set; } = null!;

    [MaxLength(150)]
    public string? Valor { get; set; }
}

/// <summary>Los textos libres distintos de un Excel (vehículo, carrocería, ciudad) para llevarlos a los valores oficiales de NEXO.</summary>
public class clsNormalizarValoresRequest
{
    /// <summary>El cliente dueño del archivo; su diccionario manda. Sin él solo se usa el diccionario general.</summary>
    public Guid? ClienteId { get; set; }

    [Required, MinLength(1), MaxLength(1000)]
    public List<clsValorANormalizarRequest> Valores { get; set; } = null!;
}

/// <summary>Una columna del archivo y lo que se hace con ella, tal como la confirmó el usuario.</summary>
public class clsColumnaAplicarRequest
{
    /// <summary>Posición de la columna en las filas (base 0).</summary>
    [Range(0, 99)]
    public int Indice { get; set; }

    /// <summary>Campo de NEXO al que va, o <c>ignorar</c>.</summary>
    [Required, MaxLength(40)]
    public string Campo { get; set; } = null!;

    /// <summary>Una de las conversiones de la sugerencia de mapeo; vacía equivale a <c>ninguna</c>.</summary>
    [MaxLength(40)]
    public string? Transformacion { get; set; }
}

/// <summary>
/// Aplica un mapeo ya confirmado a TODAS las filas de un Excel analizado: las columnas con su campo y conversión, y las filas que devolvió
/// <c>analizar-excel</c> (sin el encabezado).
/// </summary>
public class clsAplicarMapeoRequest
{
    /// <summary>El cliente dueño del archivo; su diccionario traduce los valores. Sin él solo se usa el diccionario general.</summary>
    public Guid? ClienteId { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<clsColumnaAplicarRequest> Columnas { get; set; } = null!;

    [Required, MaxLength(5000)]
    public List<List<string>?> Filas { get; set; } = null!;

    /// <summary>El número que tiene en el archivo la primera fila enviada (2 si hay un solo encabezado), para que los errores digan la fila de Excel.</summary>
    [Range(1, 1_000_000)]
    public int PrimeraFila { get; set; } = 2;
}
