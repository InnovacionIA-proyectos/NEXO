namespace Nexo.Domain.Catalogos.Entidades;

/// <summary>
/// Cómo se traduce una palabra que usa un cliente en sus archivos a lo que entiende NEXO (<c>tbl_diccionario_cliente</c>). Tres tipos:
/// <list type="bullet">
/// <item><see cref="TipoColumna"/>: un encabezado del Excel del cliente → el campo de NEXO al que corresponde (<see cref="CamposDestino"/>).</item>
/// <item><see cref="TipoUnidad"/>: una unidad del cliente → la unidad canónica (<see cref="Unidades"/>).</item>
/// <item><see cref="TipoValor"/>: un valor de una columna categórica → el valor de NEXO; <see cref="Contexto"/> dice de qué campo es.</item>
/// </list>
/// Sin cliente (<see cref="ClienteId"/> null) el término aplica a todos. Complementa al diccionario global de sinónimos de
/// vehículos y contenedores (<see cref="clsAliasElemento"/>): lo propio de un cliente vive aquí, y el concepto de un valor no se valida
/// contra el catálogo oficial porque un cliente puede usar un valor que el catálogo no tiene.
/// <see cref="TerminoNormalizado"/> se calcula con el mismo normalizador de la captura, para que "mismo texto" signifique lo mismo al
/// guardar y al comparar.
/// </summary>
public class clsTerminoCliente
{
    public const string TipoColumna = "columna";
    public const string TipoUnidad = "unidad";
    public const string TipoValor = "valor";

    public static readonly IReadOnlyList<string> Tipos = [TipoColumna, TipoUnidad, TipoValor];

    /// <summary>Los campos de la captura de una línea (más <c>ignorar</c>, para una columna que no se usa) a los que puede apuntar una columna.</summary>
    public static readonly IReadOnlyList<string> CamposDestino =
    [
        "origen", "destino", "tipoVehiculo", "carroceria", "tipologia", "lineaNegocio", "claseViaje", "claseContenedor",
        "exclusivo", "consolidado", "retornaContenedor", "ciudadDevolucionContenedor", "levanteContenedorDiferente",
        "ciudadLevanteContenedor", "horasCargue", "horasDescargue", "peso", "valorMercancia", "referencia",
        "descripcion", "cantidad", "largoCm", "anchoCm", "altoCm", "pesoKg", "codigoTipoMercancia", "ignorar",
    ];

    /// <summary>Unidades de peso canónicas a las que se traduce la del cliente.</summary>
    public static readonly IReadOnlyList<string> Unidades = ["kg", "t"];

    /// <summary>Campos categóricos a los que puede pertenecer un valor.</summary>
    public static readonly IReadOnlyList<string> Contextos =
        ["tipoVehiculo", "carroceria", "tipologia", "lineaNegocio", "claseViaje", "claseContenedor", "codigoTipoMercancia"];

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null = aplica a todos los clientes.</summary>
    public Guid? ClienteId { get; set; }
    public clsCliente? Cliente { get; set; }

    /// <summary>La palabra tal como la escribe el cliente (limpia de espacios), para mostrarla.</summary>
    public string Termino { get; set; } = null!;

    public string TerminoNormalizado { get; set; } = null!;

    /// <summary><see cref="TipoColumna"/>, <see cref="TipoUnidad"/> o <see cref="TipoValor"/>.</summary>
    public string Tipo { get; set; } = null!;

    /// <summary>Columna: campo de NEXO · unidad: kg o t · valor: valor de NEXO.</summary>
    public string Concepto { get; set; } = null!;

    /// <summary>Solo para <see cref="TipoValor"/>: el campo al que pertenece el valor.</summary>
    public string? Contexto { get; set; }

    public string? Ejemplo { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>El valor de <paramref name="candidatos"/> que coincide con <paramref name="texto"/> sin distinguir mayúsculas; null si ninguno.</summary>
    public static string? Canonico(IReadOnlyList<string> candidatos, string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : candidatos.FirstOrDefault(c => c.Equals(texto.Trim(), StringComparison.OrdinalIgnoreCase));
}
