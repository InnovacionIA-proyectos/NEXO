using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>Errores (la receta no se puede ejecutar) y avisos (se ejecuta, pero algo no cuadra con el archivo).</summary>
public sealed record clsResultadoValidacionReceta(IReadOnlyList<string> Errores, IReadOnlyList<string> Avisos)
{
    public bool Valida => Errores.Count == 0;
}

/// <summary>
/// Revisa una receta antes de ejecutarla. Lo que propone la IA nunca se ejecuta sin pasar por aquí: posiciones fuera de rango, referencias mal
/// escritas o formas contradictorias se rechazan, y lo que el archivo ya no tiene (una hoja que cambió de nombre) se avisa.
/// </summary>
public static class clsValidadorDeReceta
{
    public const int MaxHojas = 60;
    public const int MaxFila = 100_000;
    public const int MaxNombre = 100;
    public const int MaxElementos = 100;

    public static clsResultadoValidacionReceta Validar(clsRecetaLectura? receta, IReadOnlyList<clsHojaCruda>? hojas = null)
    {
        var errores = new List<string>();
        var avisos = new List<string>();

        if (receta is null) return new([ "La receta no es un JSON válido." ], []);
        if (receta.Version != clsRecetaLectura.VersionActual) errores.Add($"La versión {receta.Version} de la receta no se conoce (se espera {clsRecetaLectura.VersionActual}).");
        if (receta.Hojas is null || receta.Hojas.Count == 0) errores.Add("La receta no trae ninguna hoja.");
        else if (receta.Hojas.Count > MaxHojas) errores.Add($"La receta trae más de {MaxHojas} hojas.");

        foreach (var h in receta.Hojas ?? [])
        {
            var cual = $"Hoja «{h.Hoja}»";
            if (string.IsNullOrWhiteSpace(h.Hoja) || h.Hoja.Length > 200) { errores.Add("Hay una hoja sin nombre (o con un nombre demasiado largo)."); continue; }
            if (!h.Usar) continue;

            foreach (var f in h.FilasEncabezado ?? []) if (f < 1 || f > MaxFila) errores.Add($"{cual}: la fila de encabezado {f} no es válida.");
            if ((h.FilasEncabezado?.Count ?? 0) > 10) errores.Add($"{cual}: demasiadas filas de encabezado.");
            if (h.PrimeraFila is < 1 or > MaxFila) errores.Add($"{cual}: la primera fila de datos no es válida.");
            if (h.UltimaFila is < 1 or > MaxFila) errores.Add($"{cual}: la última fila de datos no es válida.");
            if (h.PrimeraFila is { } pf && h.UltimaFila is { } uf && uf < pf) errores.Add($"{cual}: la última fila es anterior a la primera.");

            if (h.Columnas is not null && (h.Columnas.Count > MaxElementos || clsReferenciasA1.Columnas(h.Columnas) is null))
                errores.Add($"{cual}: las columnas no son válidas (usa letras como «C» o «C:F»).");

            if (h.Valores is null)
            {
                if (h.Etiquetas is { Count: > 0 }) errores.Add($"{cual}: las etiquetas solo se usan junto con valores (tabla cruzada).");
            }
            else
            {
                var v = h.Valores;
                if (v.Columnas is null || v.Columnas.Count == 0 || v.Columnas.Count > MaxElementos || clsReferenciasA1.Columnas(v.Columnas) is null)
                    errores.Add($"{cual}: las columnas de valores no son válidas.");
                if (v.Dimensiones is null || v.Dimensiones.Count == 0 || v.Dimensiones.Count > 5)
                    errores.Add($"{cual}: indica de 1 a 5 filas de títulos para las columnas de valores.");
                else
                    foreach (var d in v.Dimensiones)
                    {
                        if (d.Fila < 1 || d.Fila > MaxFila) errores.Add($"{cual}: la fila de títulos {d.Fila} no es válida.");
                        if (string.IsNullOrWhiteSpace(d.Nombre) || d.Nombre.Length > MaxNombre) errores.Add($"{cual}: cada fila de títulos necesita un nombre de hasta {MaxNombre} caracteres.");
                    }
                if (string.IsNullOrWhiteSpace(v.NombreValor) || v.NombreValor.Length > MaxNombre) errores.Add($"{cual}: el valor necesita un nombre de hasta {MaxNombre} caracteres.");
                if (h.Etiquetas is null or { Count: 0 }) errores.Add($"{cual}: una tabla cruzada necesita al menos una columna de etiquetas.");
                else
                    foreach (var e in h.Etiquetas)
                    {
                        if (clsReferenciasA1.ColumnaANumero(e.Columna) is null) errores.Add($"{cual}: la columna de etiquetas «{e.Columna}» no es válida.");
                        if (e.Nombre is { Length: > MaxNombre }) errores.Add($"{cual}: el nombre de una etiqueta es demasiado largo.");
                    }
                if (h.Etiquetas is not null && v.Columnas is not null && clsReferenciasA1.Columnas(v.Columnas) is { } vc
                    && h.Etiquetas.Select(e => clsReferenciasA1.ColumnaANumero(e.Columna)).Any(c => c is not null && vc.Contains(c.Value)))
                    errores.Add($"{cual}: una columna no puede ser etiqueta y valor a la vez.");
            }

            if (h.Seccion is not null)
            {
                if (clsReferenciasA1.ColumnaANumero(h.Seccion.Columna) is null) errores.Add($"{cual}: la columna de las secciones no es válida.");
                if (string.IsNullOrWhiteSpace(h.Seccion.Nombre) || h.Seccion.Nombre.Length > MaxNombre) errores.Add($"{cual}: las secciones necesitan un nombre.");
            }

            foreach (var c in h.Constantes ?? [])
            {
                if (string.IsNullOrWhiteSpace(c.Nombre) || c.Nombre.Length > MaxNombre) errores.Add($"{cual}: una constante no tiene nombre.");
                if (!c.Hoja && clsReferenciasA1.ACelda(c.Celda) is null) errores.Add($"{cual}: la celda «{c.Celda}» de la constante «{c.Nombre}» no es válida.");
            }
        }

        if (hojas is not null && errores.Count == 0) Contrastar(receta, hojas, avisos);
        return new(errores, avisos);
    }

    private static void Contrastar(clsRecetaLectura receta, IReadOnlyList<clsHojaCruda> hojas, List<string> avisos)
    {
        var nombres = hojas.Select(h => clsResolucionDeCatalogos.Normalizar(h.Nombre)).ToHashSet();
        foreach (var rh in receta.Hojas.Where(h => h.Usar && h.Hoja != "*"))
            if (!nombres.Contains(clsResolucionDeCatalogos.Normalizar(rh.Hoja)))
                avisos.Add($"La receta usa la hoja «{rh.Hoja}», que este archivo no tiene.");

        foreach (var hoja in hojas)
        {
            var rh = clsEjecutorDeReceta.EntradaDe(receta, hoja);
            if (rh is not { Usar: true }) continue;
            var filas = hoja.Filas;
            var cols = hoja.Columnas;
            if (rh.Trasponer) (filas, cols) = (cols, filas);

            foreach (var f in (rh.FilasEncabezado ?? []).Concat(rh.Valores?.Dimensiones?.Select(d => d.Fila) ?? []))
                if (f > filas) avisos.Add($"Hoja «{hoja.Nombre}»: la fila {f} no existe (la hoja tiene {filas} filas).");
            if (rh.PrimeraFila is { } p && p > filas) avisos.Add($"Hoja «{hoja.Nombre}»: la primera fila de datos ({p}) está después del final de la hoja.");
            foreach (var c in rh.Constantes ?? [])
                if (!c.Hoja && clsReferenciasA1.ACelda(c.Celda) is { } cel && (cel.Fila > filas || cel.Col > cols))
                    avisos.Add($"Hoja «{hoja.Nombre}»: la celda {c.Celda} (constante «{c.Nombre}») está fuera de la hoja.");
        }
    }
}
