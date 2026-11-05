using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>Las tablas normales que salen de aplicar una receta, con lo que el código quiso avisar (una hoja cortada, una hoja que no existe).</summary>
public sealed record clsLecturaResultado(IReadOnlyList<clsHojaExcelDto> Tablas, IReadOnlyList<string> Avisos);

/// <summary>
/// Aplica una receta de lectura a las hojas de un archivo y deja cada hoja como una tabla normal (encabezados + filas) que sigue por el flujo de
/// siempre (<c>sugerir-mapeo</c>, <c>aplicar-mapeo</c>). Es determinista y no usa la IA: el mismo archivo con la misma receta da siempre lo mismo.
/// </summary>
public static class clsEjecutorDeReceta
{
    public const int MaxFilasPorTabla = 5000;
    private const int MaxFilasParaDetectarColumnas = 2000;

    /// <summary>La entrada de la receta que corresponde a una hoja: la que lleva su nombre o, si no, el comodín <c>*</c> (solo para hojas visibles).</summary>
    public static clsRecetaHoja? EntradaDe(clsRecetaLectura receta, clsHojaCruda hoja)
    {
        var nombre = clsResolucionDeCatalogos.Normalizar(hoja.Nombre);
        var propia = receta.Hojas.FirstOrDefault(h => h.Hoja.Trim() != "*" && clsResolucionDeCatalogos.Normalizar(h.Hoja) == nombre);
        if (propia is not null) return propia;
        return hoja.Oculta ? null : receta.Hojas.FirstOrDefault(h => h.Hoja.Trim() == "*");
    }

    /// <exception cref="clsValidationFailedException"><c>RECETA_INVALIDA</c> si la receta no pasa <see cref="clsValidadorDeReceta"/>.</exception>
    public static clsLecturaResultado Ejecutar(IReadOnlyList<clsHojaCruda> hojas, clsRecetaLectura receta)
    {
        var validacion = clsValidadorDeReceta.Validar(receta);
        if (!validacion.Valida) throw new clsValidationFailedException("RECETA_INVALIDA", string.Join(" ", validacion.Errores.Take(3)));

        var avisos = new List<string>();
        var tablas = new List<clsHojaExcelDto>();
        foreach (var hoja in hojas)
        {
            if (EntradaDe(receta, hoja) is not { Usar: true } entrada) continue;
            var fuente = entrada.Trasponer ? hoja.Trasponer() : hoja;
            tablas.Add(LeerHoja(fuente, entrada, hoja.Nombre, avisos));
        }

        foreach (var h in receta.Hojas.Where(h => h.Usar && h.Hoja.Trim() != "*"))
            if (!hojas.Any(x => clsResolucionDeCatalogos.Normalizar(x.Nombre) == clsResolucionDeCatalogos.Normalizar(h.Hoja)))
                avisos.Add($"La hoja «{h.Hoja}» de la receta no está en el archivo.");

        if (receta.Unir && tablas.Count > 1) tablas = [Unir(tablas, avisos)];
        return new clsLecturaResultado(tablas, avisos);
    }

    private static clsHojaExcelDto LeerHoja(clsHojaCruda g, clsRecetaHoja r, string nombreHoja, List<string> avisos)
    {
        var filasEnc = (r.FilasEncabezado ?? []).Where(f => f >= 1).Distinct().Order().ToList();
        var primera = r.PrimeraFila ?? ((filasEnc.Count > 0 ? filasEnc.Max() : 0) + 1);
        var ultima = Math.Min(r.UltimaFila ?? g.Filas, g.Filas);

        var constantes = (r.Constantes ?? [])
            .Select(c => (Nombre: c.Nombre.Trim(), Valor: c.Hoja ? nombreHoja : ValorDeCelda(g, c.Celda)))
            .ToList();

        var seccionCol = r.Seccion is null ? (int?)null : clsReferenciasA1.ColumnaANumero(r.Seccion.Columna);
        var encabezados = new List<string>();
        var filas = new List<IReadOnlyList<string>>();
        var total = 0;

        void Agregar(List<string> fila, string? seccion)
        {
            if (seccionCol is not null) fila.Add(seccion ?? string.Empty);
            foreach (var c in constantes) fila.Add(c.Valor);
            total++;
            if (filas.Count < MaxFilasPorTabla) filas.Add(fila);
        }

        if (r.Valores is null)
        {
            var cols = r.Columnas is not null ? clsReferenciasA1.Columnas(r.Columnas)! : ColumnasConContenido(g, filasEnc, primera, ultima);
            encabezados.AddRange(cols.Select(c => NombreDeColumna(g, filasEnc, c)));
            var datosYSeccion = seccionCol is { } sc && !cols.Contains(sc) ? cols.Append(sc).ToList() : cols;

            string? seccion = null;
            for (var f = primera; f <= ultima; f++)
            {
                if (g.FilaVacia(f))
                {
                    if (r.HastaPrimeraFilaVacia && total > 0) break;
                    continue;
                }
                if (seccionCol is { } s && EsTituloDeSeccion(g, f, s, datosYSeccion)) { seccion = g.ValorEfectivo(f, s).Trim(); continue; }
                var fila = cols.Select(c => g.ValorDeDato(f, c).Trim()).ToList();
                if (fila.All(string.IsNullOrEmpty)) continue;
                Agregar(fila, seccion);
            }
        }
        else
        {
            var v = r.Valores;
            var etiquetas = (r.Etiquetas ?? [])
                .Select(e =>
                {
                    var col = clsReferenciasA1.ColumnaANumero(e.Columna)!.Value;
                    var filasTitulo = filasEnc.Count > 0 ? filasEnc : v.Dimensiones.Select(d => d.Fila).ToList();
                    return (Col: col, Nombre: string.IsNullOrWhiteSpace(e.Nombre) ? NombreDeColumna(g, filasTitulo, col, "Etiqueta") : e.Nombre.Trim());
                })
                .ToList();
            var valorCols = clsReferenciasA1.Columnas(v.Columnas)!;
            encabezados.AddRange(etiquetas.Select(e => e.Nombre));
            encabezados.AddRange(v.Dimensiones.Select(d => d.Nombre.Trim()));
            encabezados.Add(v.NombreValor.Trim());

            // No se lee una columna de valores sin ningún título (cálculos o relleno) ni la continuación de un título combinado (la segunda columna de «CARTAGENA» E:F),
            // así que un rango amplio como «E:T» sirve aunque cada hoja tenga un número distinto de orígenes.
            var titulos = valorCols
                .Select(c => (Col: c, Dims: v.Dimensiones.Select(d => g.ValorEfectivo(d.Fila, c).Trim()).ToList()))
                .Where(x => x.Dims.Any(t => t.Length > 0) && !v.Dimensiones.All(d => g.EsContinuacionDeCombinada(d.Fila, x.Col)))
                .ToList();

            var todas = etiquetas.Select(e => e.Col).Concat(valorCols).ToList();
            if (seccionCol is { } scv && !todas.Contains(scv)) todas.Add(scv);

            string? seccion = null;
            for (var f = primera; f <= ultima; f++)
            {
                if (g.FilaVacia(f))
                {
                    if (r.HastaPrimeraFilaVacia && total > 0) break;
                    continue;
                }
                if (seccionCol is { } s && EsTituloDeSeccion(g, f, s, todas)) { seccion = g.ValorEfectivo(f, s).Trim(); continue; }
                var etiquetasDeFila = etiquetas.Select(e => g.ValorDeDato(f, e.Col).Trim()).ToList();
                if (etiquetasDeFila.All(string.IsNullOrEmpty)) continue;

                foreach (var (col, dims) in titulos)
                {
                    var valor = g.ValorDeDato(f, col).Trim();
                    if (v.OmitirVacios && valor.Length == 0) continue;
                    var fila = new List<string>(etiquetasDeFila);
                    fila.AddRange(dims);
                    fila.Add(valor);
                    Agregar(fila, seccion);
                }
            }
        }

        if (seccionCol is not null) encabezados.Add(r.Seccion!.Nombre.Trim());
        encabezados.AddRange(constantes.Select(c => c.Nombre));
        if (total > MaxFilasPorTabla) avisos.Add($"La hoja «{nombreHoja}» da {total:N0} filas; se devuelven las primeras {MaxFilasPorTabla:N0}.");
        return new clsHojaExcelDto(nombreHoja, filasEnc.Count > 0 ? filasEnc[0] : 0, encabezados, filas, total);
    }

    private static string ValorDeCelda(clsHojaCruda g, string? celda) =>
        clsReferenciasA1.ACelda(celda) is { } c ? g.ValorEfectivo(c.Fila, c.Col).Trim() : string.Empty;

    private static string NombreDeColumna(clsHojaCruda g, IReadOnlyList<int> filasEncabezado, int col, string prefijoSinTitulo = "Columna")
    {
        var partes = filasEncabezado.Select(f => g.ValorEfectivo(f, col).Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
        var nombre = string.Join(" · ", partes);
        return nombre.Length > 0 ? nombre : $"{prefijoSinTitulo} {clsReferenciasA1.NumeroAColumna(col)}";
    }

    private static IReadOnlyList<int> ColumnasConContenido(clsHojaCruda g, IReadOnlyList<int> filasEnc, int primera, int ultima)
    {
        int min = int.MaxValue, max = 0;
        var filas = filasEnc.Concat(Enumerable.Range(Math.Max(1, primera), Math.Max(0, Math.Min(ultima, primera + MaxFilasParaDetectarColumnas) - primera + 1)));
        foreach (var f in filas)
            for (var c = 1; c <= Math.Min(g.Columnas, clsReferenciasA1.MaxColumnas); c++)
                if (g.ValorEfectivo(f, c).Length > 0 || g.Valor(f, c).Length > 0) { min = Math.Min(min, c); max = Math.Max(max, c); }
        return max == 0 ? [] : Enumerable.Range(min, max - min + 1).ToList();
    }

    /// <summary>Una fila es un título de sección si su columna de sección tiene texto y ninguna otra de las columnas de la tabla lo tiene.</summary>
    private static bool EsTituloDeSeccion(clsHojaCruda g, int fila, int seccionCol, IEnumerable<int> columnas) =>
        g.ValorEfectivo(fila, seccionCol).Trim().Length > 0 && columnas.Where(c => c != seccionCol).All(c => g.Valor(fila, c).Trim().Length == 0);

    private static clsHojaExcelDto Unir(IReadOnlyList<clsHojaExcelDto> tablas, List<string> avisos)
    {
        var encabezados = new List<string>();
        var indice = new Dictionary<string, int>();
        foreach (var t in tablas)
            foreach (var e in t.Encabezados)
            {
                var clave = clsResolucionDeCatalogos.Normalizar(e);
                if (indice.TryAdd(clave, encabezados.Count)) encabezados.Add(e);
            }

        var filas = new List<IReadOnlyList<string>>();
        var total = 0;
        foreach (var t in tablas)
        {
            var destino = t.Encabezados.Select(e => indice[clsResolucionDeCatalogos.Normalizar(e)]).ToList();
            foreach (var fila in t.Filas)
            {
                if (filas.Count >= MaxFilasPorTabla) break;
                var nueva = new string[encabezados.Count];
                Array.Fill(nueva, string.Empty);
                for (var i = 0; i < fila.Count && i < destino.Count; i++) nueva[destino[i]] = fila[i];
                filas.Add(nueva);
            }
            total += t.TotalFilas;
        }
        if (total > MaxFilasPorTabla) avisos.Add($"Las hojas unidas dan {total:N0} filas; se devuelven las primeras {MaxFilasPorTabla:N0}.");
        return new clsHojaExcelDto("Todas las hojas", tablas[0].FilaEncabezado, encabezados, filas, total);
    }
}
