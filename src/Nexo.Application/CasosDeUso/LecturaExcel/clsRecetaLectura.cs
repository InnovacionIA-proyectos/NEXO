using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>
/// Cómo leer el Excel de un cliente: por cada hoja, dónde están los encabezados y los datos, qué columnas identifican la fila y cuáles
/// se despivotan en filas. Es la «receta» que propone la IA, confirma la persona y ejecuta el código sin IA. Filas y columnas desde 1 (A = 1).
/// </summary>
public sealed record clsRecetaLectura(
    int Version,
    IReadOnlyList<clsRecetaHoja> Hojas,
    bool Unir = false,
    string? Notas = null)
{
    public const int VersionActual = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public string ASerializado() => JsonSerializer.Serialize(this, Json);

    /// <summary>La receta de un texto JSON, o null si no se puede leer.</summary>
    public static clsRecetaLectura? Deserializar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<clsRecetaLectura>(json, Json); }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// La lectura de una hoja. <paramref name="Hoja"/> es el nombre de la hoja (sin importar mayúsculas ni tildes) o <c>*</c> para las demás hojas visibles.
/// Sin <paramref name="Valores"/> la hoja es una tabla (una fila de datos = una fila de salida); con <paramref name="Valores"/> es una tabla cruzada
/// y cada celda de valor pasa a ser una fila de salida.
/// </summary>
/// <param name="Trasponer">Intercambia filas y columnas antes de leer (todas las posiciones de esta receta son sobre la hoja ya traspuesta).</param>
/// <param name="FilasEncabezado">Filas con los títulos de las columnas; varias filas se combinan («Volumen · Turbo»). Una celda combinada vale para todo su ancho.</param>
/// <param name="PrimeraFila">Primera fila de datos; sin ella, la siguiente a los encabezados.</param>
/// <param name="UltimaFila">Última fila de datos; sin ella, hasta el final de la hoja.</param>
/// <param name="HastaPrimeraFilaVacia">Los datos terminan en la primera fila vacía (hay notas o totales debajo de la tabla).</param>
/// <param name="Columnas">Solo en una tabla simple: las columnas a leer («A», «C:F»). Sin ellas, todas las que tengan contenido.</param>
/// <param name="Etiquetas">Solo en una tabla cruzada: las columnas que identifican cada fila (servicio, destino…).</param>
/// <param name="Seccion">Filas que son un título que aplica a las filas de abajo (un tipo de viaje, un escenario).</param>
/// <param name="Constantes">Valores que se repiten en todas las filas: una celda fija o el nombre de la hoja.</param>
public sealed record clsRecetaHoja(
    string Hoja,
    bool Usar = true,
    bool Trasponer = false,
    IReadOnlyList<int>? FilasEncabezado = null,
    int? PrimeraFila = null,
    int? UltimaFila = null,
    bool HastaPrimeraFilaVacia = false,
    IReadOnlyList<string>? Columnas = null,
    IReadOnlyList<clsRecetaEtiqueta>? Etiquetas = null,
    clsRecetaValores? Valores = null,
    clsRecetaSeccion? Seccion = null,
    IReadOnlyList<clsRecetaConstante>? Constantes = null);

/// <summary>Una columna que identifica la fila en una tabla cruzada. Sin <paramref name="Nombre"/> se usa el título que tenga en los encabezados.</summary>
public sealed record clsRecetaEtiqueta(string Columna, string? Nombre = null);

/// <summary>
/// Las columnas que se despivotan. Cada fila de datos sale una vez por columna de valor, con el título de esa columna en cada
/// <paramref name="Dimensiones"/> (por ejemplo el origen de la fila 20 y el vehículo de la fila 21) y el contenido de la celda en <paramref name="NombreValor"/>.
/// </summary>
public sealed record clsRecetaValores(
    IReadOnlyList<string> Columnas, IReadOnlyList<clsRecetaDimension> Dimensiones, string NombreValor = "Valor", bool OmitirVacios = true);

/// <summary>Una fila de títulos de las columnas de valor y el nombre de la columna que genera.</summary>
public sealed record clsRecetaDimension(int Fila, string Nombre);

/// <summary>Una fila en la que solo esa columna tiene texto es un título de sección; el texto va a la columna <paramref name="Nombre"/> de las filas siguientes.</summary>
public sealed record clsRecetaSeccion(string Columna, string Nombre);

/// <summary>Un valor fijo para todas las filas: el de una celda (<c>E12</c>) o el nombre de la hoja.</summary>
public sealed record clsRecetaConstante(string Nombre, string? Celda = null, bool Hoja = false);
