namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>
/// La receta más simple posible, sin IA: cada hoja visible es una tabla cuyo encabezado es la primera fila (de las primeras 15) con tres o más textos
/// distintos. Es el punto de partida cuando la IA no está disponible; no entiende tablas cruzadas ni secciones.
/// </summary>
public static class clsRecetaPorDefecto
{
    private const int FilasDeBusqueda = 15;

    public static clsRecetaLectura Generar(IReadOnlyList<clsHojaCruda> hojas)
    {
        var entradas = new List<clsRecetaHoja>();
        foreach (var hoja in hojas.Where(h => !h.Oculta))
        {
            var fila = FilaDeEncabezado(hoja);
            if (fila is null) continue;
            entradas.Add(new clsRecetaHoja(hoja.Nombre, FilasEncabezado: [fila.Value]));
        }
        return new clsRecetaLectura(clsRecetaLectura.VersionActual, entradas, Notas: "Lectura automática: la primera fila con varios títulos es el encabezado de cada hoja.");
    }

    private static int? FilaDeEncabezado(clsHojaCruda hoja)
    {
        for (var f = 1; f <= Math.Min(hoja.Filas, FilasDeBusqueda); f++)
        {
            var distintos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 1; c <= hoja.Columnas; c++)
            {
                var v = hoja.ValorEfectivo(f, c).Trim();
                if (v.Length > 0) distintos.Add(v);
            }
            if (distintos.Count >= 3) return f;
        }
        return null;
    }
}
