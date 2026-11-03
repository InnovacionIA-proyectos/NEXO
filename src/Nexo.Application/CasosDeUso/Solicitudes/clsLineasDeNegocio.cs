using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Una línea de negocio de NEXO: su código (<c>puertoAInterior</c>) y su nombre legible (<c>Puerto a interior</c>).</summary>
public sealed record clsLineaDeNegocio(LineaNegocio Valor, string Codigo, string Nombre);

/// <summary>Las seis líneas de negocio que entiende la captura y cómo se reconoce un texto como una de ellas.</summary>
public static class clsLineasDeNegocio
{
    public static readonly IReadOnlyList<clsLineaDeNegocio> Todas =
    [
        new(LineaNegocio.Nacional, "nacional", "Nacional"),
        new(LineaNegocio.PuertoAInterior, "puertoAInterior", "Puerto a interior"),
        new(LineaNegocio.InteriorAPuerto, "interiorAPuerto", "Interior a puerto"),
        new(LineaNegocio.DevolucionContenedor, "devolucionContenedor", "Devolución de contenedor"),
        new(LineaNegocio.Andino, "andino", "Andino"),
        new(LineaNegocio.Licitaciones, "licitaciones", "Licitaciones"),
    ];

    /// <summary>Los nombres legibles, para decirle al usuario con qué valores se puede escribir.</summary>
    public static string NombresSeparados => string.Join(", ", Todas.Select(l => l.Nombre));

    public static clsLineaDeNegocio De(LineaNegocio valor) => Todas.First(l => l.Valor == valor);

    /// <summary>
    /// La línea que el texto nombra, con o sin tildes, espacios, guiones ni mayúsculas (<c>puertoAInterior</c>, <c>Puerto a interior</c>,
    /// <c>PUERTO-A-INTERIOR</c>); null si no es ninguna de las seis. Vacío también da null: quien lo llama decide si eso es un error.
    /// </summary>
    public static LineaNegocio? Reconocer(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var n = clsResolucionDeCatalogos.SinTildes(valor).Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "").ToUpperInvariant();
        if (n.Contains("DEVOLUCION")) return LineaNegocio.DevolucionContenedor;
        if (n.Contains("ANDINO")) return LineaNegocio.Andino;
        if (n.Contains("LICITACION")) return LineaNegocio.Licitaciones;
        if (n.Contains("PUERTOAINTERIOR")) return LineaNegocio.PuertoAInterior;
        if (n.Contains("INTERIORAPUERTO")) return LineaNegocio.InteriorAPuerto;
        if (n.Contains("NACIONAL")) return LineaNegocio.Nacional;
        return null;
    }
}
