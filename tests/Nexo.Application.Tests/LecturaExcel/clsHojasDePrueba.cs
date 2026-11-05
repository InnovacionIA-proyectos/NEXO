using Nexo.Application.CasosDeUso.LecturaExcel;

namespace Nexo.Application.Tests.LecturaExcel;

/// <summary>Arma hojas de prueba a partir de líneas de texto: cada línea es una fila y las celdas van separadas por «|» (A, B, C…).</summary>
internal static class clsHojasDePrueba
{
    public static clsHojaCruda Hoja(string nombre, string[] filas, string[]? combinadas = null, bool oculta = false) =>
        new(nombre, oculta, filas.Select(f => f.Split('|')).ToArray(), (combinadas ?? []).Select(Rango).ToList());

    private static clsRangoCombinado Rango(string a1)
    {
        var partes = a1.Split(':');
        var desde = clsReferenciasA1.ACelda(partes[0])!.Value;
        var hasta = clsReferenciasA1.ACelda(partes.Length > 1 ? partes[1] : partes[0])!.Value;
        return new clsRangoCombinado(desde.Fila, desde.Col, hasta.Fila, hasta.Col);
    }

    public static string[] Celdas(IReadOnlyList<string> fila) => [.. fila];
}
