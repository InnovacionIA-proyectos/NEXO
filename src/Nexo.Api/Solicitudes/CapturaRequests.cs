using System.ComponentModel.DataAnnotations;
using Nexo.Domain.Enums;

namespace Nexo.Api.Solicitudes;

/// <summary>Alta de una solicitud completa (cabecera + líneas) con origen/destino/vehículo como texto libre: el servicio resuelve o crea las rutas y configuraciones. Sirve al formulario manual y a la importación de Excel.</summary>
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

    /// <summary>Notas u observaciones comerciales; distinto de <see cref="Referencia"/> (referencia/PO del cliente).</summary>
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

    /// <summary>Horas de cargue/descargue de esta línea; sobreescriben el default (4h+4h; 1h+1h si la línea es DevolucionContenedor). Marcar RetornaContenedor no las cambia: el tramo vacío de retorno se costea aparte con 1h+1h.</summary>
    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasCargue { get; set; }

    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasDescargue { get; set; }

    /// <summary>Id de tbl_municipios elegido en el buscador de ciudades; null si Origen/Destino viene de texto libre.</summary>
    public int? OrigenMunicipioId { get; set; }
    public int? DestinoMunicipioId { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? Peso { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorMercancia { get; set; }

    [MaxLength(200, ErrorMessage = "Una línea admite máximo 200 posiciones.")]
    public List<clsCapturaPosicionRequest>? Posiciones { get; set; }

    /// <summary>Id de la ruta que agrupa esta línea con las demás de una misma carga heterogénea (un camión por línea). Lo manda quien crea las líneas del grupo; NEXO lo guarda tal cual y nunca lo genera. Null si la línea nace en NEXO.</summary>
    public Guid? GrupoRutaId { get; set; }

    /// <summary>Id de la recomendación de vehículo del Cubicador usada para capturar esta línea (idRecomendacionVehiculo de POST /api/cubicacion), para cruzarla con lo despachado. Null si no pasó por el Cubicador.</summary>
    [MaxLength(64)]
    public string? IdRecomendacionVehiculo { get; set; }
}

/// <summary>Una línea de texto libre a resolver contra el catálogo, sin crear solicitud: entrada de POST /api/solicitudes/resolver-catalogos.</summary>
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

/// <summary>Un contenedor a recomendar: entrada de POST /api/solicitudes/recomendar-vehiculo-contenedor.</summary>
public class clsRecomendarVehiculoContenedorRequest
{
    /// <summary>Posición de la línea en el archivo; se devuelve tal cual para que el front la asocie.</summary>
    public int Indice { get; set; }

    /// <summary>Clase de SAP (<c>CNT40.HC</c>) o el tamaño en pies (20 / 40).</summary>
    [MaxLength(30)]
    public string? ClaseContenedor { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999", ParseLimitsInInvariantCulture = true)]
    public decimal PesoKg { get; set; }
}

/// <summary>Una pieza/bulto a transportar, con dimensiones en centímetros.</summary>
public class clsCapturaPosicionRequest
{
    [MaxLength(200)]
    public string? Descripcion { get; set; }

    /// <summary>Unidades de esta posición. Tope 10.000, el mismo del Cubicador; vale también para la suma de las posiciones de una línea (ver clsCapturarSolicitudValidador).</summary>
    [Range(1, 10000)]
    public int Cantidad { get; set; } = 1;

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal LargoCm { get; set; }

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal AnchoCm { get; set; }

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal AltoCm { get; set; }

    /// <summary>
    /// Peso de UNA unidad en kg, no el total de la posición: el Cubicador calcula peso_total = pesoKg × cantidad, y mandar el total multiplica dos veces (100 cajas de 20 kg: enviar 2000 da 200.000 kg).
    /// Obligatorio (> 0): el Cubicador rechaza una posición sin peso (PESO_NO_DECLARADO). Una línea sin posiciones puede capturarse solo con su <c>peso</c> total.
    /// </summary>
    [Required, Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? PesoKg { get; set; }

    /// <summary>Código del tipo de mercancía (tbl_tipos_mercancia.CodigoMercancia, no el uuid): es estable y sobrevive a un reseed. Null = desconocido; no se asigna default porque el tipo mueve la carrocería elegida.</summary>
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

    /// <summary>Alternativa a <c>clienteId</c>: el NIT del cliente o prospecto (con o sin puntos, espacios y dígito de verificación). Si vienen los dos manda <c>clienteId</c>.</summary>
    [MaxLength(30)]
    public string? ClienteNit { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<string> Encabezados { get; set; } = null!;

    [MaxLength(1000)]
    public List<List<string>?>? Filas { get; set; }
}

/// <summary>Un texto libre de una columna del Excel.</summary>
public class clsValorANormalizarRequest
{
    /// <summary><c>tipoVehiculo</c>, <c>carroceria</c>, <c>lineaNegocio</c> o <c>ciudad</c> (origen, destino y demás ciudades).</summary>
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

    /// <summary>Alternativa a <c>clienteId</c>: el NIT del cliente o prospecto (con o sin puntos, espacios y dígito de verificación). Si vienen los dos manda <c>clienteId</c>.</summary>
    [MaxLength(30)]
    public string? ClienteNit { get; set; }

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

    /// <summary>Alternativa a <c>clienteId</c>: el NIT del cliente o prospecto (con o sin puntos, espacios y dígito de verificación). Si vienen los dos manda <c>clienteId</c>.</summary>
    [MaxLength(30)]
    public string? ClienteNit { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<clsColumnaAplicarRequest> Columnas { get; set; } = null!;

    [Required, MaxLength(5000)]
    public List<List<string>?> Filas { get; set; } = null!;

    /// <summary>El número que tiene en el archivo la primera fila enviada (2 si hay un solo encabezado), para que los errores digan la fila de Excel.</summary>
    [Range(1, 1_000_000)]
    public int PrimeraFila { get; set; } = 2;
}
