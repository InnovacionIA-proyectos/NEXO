using System.Text;

namespace Nexo.EvaluacionIa;

/// <summary>
/// El informe en markdown de una corrida del arnés. Todo lo que dice sale de las llamadas: la recomendación final la escribe una persona
/// (queda la sección vacía), el arnés solo deja una sugerencia con su regla a la vista.
/// </summary>
public static class clsInforme
{
    /// <summary>Diferencia de acierto (en puntos) por debajo de la cual dos combinaciones se consideran empatadas y gana la más barata.</summary>
    private const double EmpateAcierto = 0.01;

    public static string Escribir(
        DateTime inicio, clsOpcionesArnes opciones, IReadOnlyList<clsCaso> casos, IReadOnlyList<clsCombo> combos, IReadOnlyList<clsLlamada> llamadas)
    {
        var s = new StringBuilder();
        var resumenes = combos.Select(c => clsResumen.De(c, llamadas.Where(l => l.Combo == c).ToList())).ToList();

        s.AppendLine($"# Evaluación del mapeo de Excel con IA — {inicio:yyyy-MM-dd HH:mm}");
        s.AppendLine();
        s.AppendLine("Generado por `tests/evaluacion-ia/arnes` (S6-HU-32.5, #353). Cada caso del dataset dorado pasó por el mismo código de producción");
        s.AppendLine("(`analizar-excel` y `sugerir-mapeo`) con la IA sola: sin cliente, sin plantilla y con el diccionario vacío.");
        s.AppendLine();
        s.AppendLine($"- **Casos:** {casos.Count} ({string.Join(", ", casos.Select(c => c.Respuesta.Caso))})");
        s.AppendLine($"- **Combinaciones:** {combos.Count} ({string.Join("; ", combos)})");
        s.AppendLine($"- **Repeticiones por caso:** {opciones.Repeticiones} · **Llamadas:** {llamadas.Count} · **Costo total estimado:** {Program.Usd(llamadas.Sum(l => l.CostoUsd ?? 0))}");
        s.AppendLine();
        s.AppendLine("Cómo leerlo:");
        s.AppendLine("- **Acierto con campo:** de las columnas que la respuesta asigna a un campo, cuántas propuso bien. Es lo que importa para importar el Excel.");
        s.AppendLine("- **Falsos positivos:** columnas que no se usan y la IA asignó a un campo (por ejemplo, la capacidad del vehículo como `peso`).");
        s.AppendLine("- El acierto se calcula sobre las llamadas en las que la IA respondió; las que fallaron van en «Fallos».");
        s.AppendLine("- Costo: precios de lista de `clsPreciosIa` (2026-10), sin descuentos. Latencia: la llamada al proveedor, con sus reintentos.");
        s.AppendLine();

        s.AppendLine("## Resumen por combinación");
        s.AppendLine();
        s.AppendLine("| Modelo | Esfuerzo | Acierto con campo | Acierto total | Falsos positivos | Costo por Excel | p50 | p95 | Fallos | Tokens entrada / salida |");
        s.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in resumenes)
            s.AppendLine($"| {r.Combo.Modelo} | {r.Combo.Esfuerzo} | {Program.Pct(r.AciertoConCampo)} | {Program.Pct(r.AciertoTotal)} | " +
                         $"{r.FalsosPositivos} ({Program.Pct(r.TasaFalsosPositivos)}) | {Program.Usd(r.CostoPorExcel)} | {Program.Seg(r.P50Ms)} | " +
                         $"{Program.Seg(r.P95Ms)} | {r.Fallos} de {r.Llamadas} | {Tokens(r.TokensEntrada)} / {Tokens(r.TokensSalida)} |");
        s.AppendLine();

        Matriz(s, "Acierto con campo por caso", casos.Select(c => c.Respuesta.Caso).ToList(), combos, llamadas, l => l.Caso);
        Matriz(s, "Acierto con campo por formato", casos.Select(c => c.Respuesta.Formato).Distinct().ToList(), combos, llamadas, l => l.Formato);
        PorCampo(s, casos, combos, llamadas);

        Despivotar(s, combos, llamadas);
        Errores(s, llamadas);
        Fallos(s, llamadas);
        Sugerencia(s, resumenes);

        s.AppendLine("## Recomendación");
        s.AppendLine();
        s.AppendLine("_La escribe una persona después de leer el informe: modelo y esfuerzo para `Anthropic__Model` y `Anthropic__Effort`, y por qué._");
        s.AppendLine();
        return s.ToString();
    }

    private static void Matriz(
        StringBuilder s, string titulo, IReadOnlyList<string> filas, IReadOnlyList<clsCombo> combos, IReadOnlyList<clsLlamada> llamadas,
        Func<clsLlamada, string> clave)
    {
        s.AppendLine($"## {titulo}");
        s.AppendLine();
        s.AppendLine("| | " + string.Join(" | ", combos) + " |");
        s.AppendLine("|---|" + string.Concat(combos.Select(_ => "---|")));
        foreach (var fila in filas)
        {
            var celdas = combos.Select(c =>
            {
                var cal = llamadas.Where(l => l.Combo == c && l.IaUsada && clave(l) == fila).Select(l => l.Calificacion).ToList();
                var total = cal.Sum(x => x.ConCampo);
                return total == 0 ? "—" : Program.Pct((double)cal.Sum(x => x.AciertosConCampo) / total);
            });
            s.AppendLine($"| {fila} | " + string.Join(" | ", celdas) + " |");
        }
        s.AppendLine();
    }

    /// <summary>El acierto por campo de NEXO: de las columnas que la respuesta asigna a cada campo, cuántas acertó cada combinación.</summary>
    private static void PorCampo(StringBuilder s, IReadOnlyList<clsCaso> casos, IReadOnlyList<clsCombo> combos, IReadOnlyList<clsLlamada> llamadas)
    {
        var campos = casos.SelectMany(c => c.Respuesta.Columnas).Where(c => c.Campo != "ignorar").Select(c => c.Campo).Distinct().Order().ToList();
        s.AppendLine("## Acierto por campo de NEXO");
        s.AppendLine();
        s.AppendLine("Entre paréntesis, cuántas columnas de ese campo hay en el dataset.");
        s.AppendLine();
        s.AppendLine("| Campo | " + string.Join(" | ", combos) + " |");
        s.AppendLine("|---|" + string.Concat(combos.Select(_ => "---|")));
        foreach (var campo in campos)
        {
            var enDataset = casos.Sum(c => c.Respuesta.Columnas.Count(x => x.Campo == campo));
            var celdas = combos.Select(c =>
            {
                var x = llamadas.Where(l => l.Combo == c && l.IaUsada).SelectMany(l => l.Calificacion.PorCampo).Where(p => p.Campo == campo).ToList();
                return x.Count == 0 ? "—" : Program.Pct((double)x.Count(p => p.Acierto) / x.Count);
            });
            s.AppendLine($"| {campo} ({enDataset}) | " + string.Join(" | ", celdas) + " |");
        }
        s.AppendLine();
    }

    /// <summary>Las columnas con un dato en el encabezado (vehículo, origen…): hoy la única respuesta válida es ignorar.</summary>
    private static void Despivotar(StringBuilder s, IReadOnlyList<clsCombo> combos, IReadOnlyList<clsLlamada> llamadas)
    {
        s.AppendLine("## Columnas que requieren despivotar");
        s.AppendLine();
        s.AppendLine("Traen en el encabezado un dato que debería ser un campo (vehículo, clase de viaje, origen). Con el contrato actual la única");
        s.AppendLine("respuesta válida es `ignorar`; esto mide cuánto de lo que llega no se puede representar todavía y qué intenta la IA.");
        s.AppendLine();
        s.AppendLine("| Combinación | Columnas evaluadas | La dejó en ignorar | Qué propuso en las demás |");
        s.AppendLine("|---|---|---|---|");
        foreach (var c in combos)
        {
            var d = llamadas.Where(l => l.Combo == c && l.IaUsada).SelectMany(l => l.Calificacion.Despivotar).ToList();
            if (d.Count == 0) continue;
            var ignoradas = d.Count(x => x.Propuesto.Equals("ignorar", StringComparison.OrdinalIgnoreCase));
            var otras = d.Where(x => !x.Propuesto.Equals("ignorar", StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Propuesto).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} ({g.Count()})");
            s.AppendLine($"| {c} | {d.Count} | {Program.Pct((double)ignoradas / d.Count)} | {string.Join(", ", otras).OrDash()} |");
        }
        s.AppendLine();
    }

    private static void Errores(StringBuilder s, IReadOnlyList<clsLlamada> llamadas)
    {
        s.AppendLine("## Errores más frecuentes");
        s.AppendLine();
        s.AppendLine("Sumando todas las combinaciones. «Veces» es en cuántas llamadas de ese caso pasó.");
        s.AppendLine();
        var porCaso = llamadas.Where(l => l.IaUsada).GroupBy(l => l.Caso).ToDictionary(g => g.Key, g => g.Count());
        var errores = llamadas.Where(l => l.IaUsada)
            .SelectMany(l => l.Calificacion.Errores.Select(e => (l.Caso, e.Indice, e.Columna, e.Esperado, e.Propuesto)))
            .GroupBy(x => x).OrderByDescending(g => g.Count()).Take(20).ToList();
        if (errores.Count == 0)
        {
            s.AppendLine("Ninguno.");
            s.AppendLine();
            return;
        }
        s.AppendLine("| Caso | Columna | Esperado | Propuesto | Veces |");
        s.AppendLine("|---|---|---|---|---|");
        foreach (var g in errores)
            s.AppendLine($"| {g.Key.Caso} | {g.Key.Indice}: {Escapar(g.Key.Columna)} | {g.Key.Esperado} | {g.Key.Propuesto} | {g.Count()} de {porCaso[g.Key.Caso]} |");
        s.AppendLine();
    }

    private static void Fallos(StringBuilder s, IReadOnlyList<clsLlamada> llamadas)
    {
        var fallos = llamadas.Where(l => !l.IaUsada).GroupBy(l => (l.Combo, l.MotivoSinIa)).ToList();
        if (fallos.Count == 0) return;
        s.AppendLine("## Fallos");
        s.AppendLine();
        s.AppendLine("| Combinación | Motivo | Llamadas |");
        s.AppendLine("|---|---|---|");
        foreach (var g in fallos)
            s.AppendLine($"| {g.Key.Combo} | {g.Key.MotivoSinIa ?? "—"} | {g.Count()} |");
        s.AppendLine();
    }

    /// <summary>
    /// Regla a la vista: entre las combinaciones sin fallos, la de mayor acierto con campo; si otras quedan a menos de un punto, la más barata
    /// de ellas. Es un punto de partida, no la recomendación.
    /// </summary>
    private static void Sugerencia(StringBuilder s, IReadOnlyList<clsResumen> resumenes)
    {
        s.AppendLine("## Sugerencia automática");
        s.AppendLine();
        var candidatas = resumenes.Where(r => r.Fallos == 0 && r.AciertoConCampo is not null).ToList();
        if (candidatas.Count == 0)
        {
            s.AppendLine("Ninguna combinación respondió en todas sus llamadas: revisar «Fallos» antes de recomendar.");
            s.AppendLine();
            return;
        }
        var mejor = candidatas.Max(r => r.AciertoConCampo!.Value);
        var elegida = candidatas.Where(r => mejor - r.AciertoConCampo!.Value < EmpateAcierto)
            .OrderBy(r => r.CostoPorExcel ?? decimal.MaxValue).ThenBy(r => r.P95Ms).First();
        s.AppendLine($"**{elegida.Combo}**: acierto con campo {Program.Pct(elegida.AciertoConCampo)}, {elegida.FalsosPositivos} falsos positivos, " +
                     $"{Program.Usd(elegida.CostoPorExcel)} por Excel y p95 de {Program.Seg(elegida.P95Ms)}.");
        s.AppendLine();
        s.AppendLine("Regla: entre las combinaciones sin fallos, la de mayor acierto con campo; si otras quedan a menos de 1 punto, la más barata.");
        s.AppendLine();
    }

    private static string Tokens(double? x) => x is null ? "—" : Math.Round(x.Value).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
    private static string Escapar(string texto) => texto.Replace("|", "\\|").ReplaceLineEndings(" ");
    private static string OrDash(this string texto) => texto.Length == 0 ? "—" : texto;
}
