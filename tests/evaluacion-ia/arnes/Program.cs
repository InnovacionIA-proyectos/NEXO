using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.MapeoExcel;
using Nexo.Application.CasosDeUso.PlantillasMapeo.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.IA;
using Nexo.Infrastructure.Solicitudes.Adaptadores;

namespace Nexo.EvaluacionIa;

/// <summary>
/// Arnés de evaluación del mapeo de Excel con IA (S6-HU-32.5, #353).
///
/// A cada caso del dataset dorado (tests/evaluacion-ia/dataset) le pide la propuesta de mapeo con el MISMO código de producción: el
/// lector de <c>analizar-excel</c> (clsExcelSolicitudesAdapter) y el handler de <c>sugerir-mapeo</c> (clsSugerirMapeoExcelQueryHandler)
/// con el adaptador real de Anthropic. Solo cambian el modelo y el esfuerzo, que en producción son configuración.
///
/// Mide a la IA sola: sin cliente, sin plantilla y con el diccionario vacío, para que nada resuelva columnas por ella.
/// Compara la propuesta con la respuesta correcta y escribe un informe en markdown y los resultados crudos en JSON.
///
/// Uso (desde la raíz del repo):
///   dotnet run --project tests/evaluacion-ia/arnes -- [--modelos a,b] [--esfuerzos low,medium,high] [--repeticiones 3]
///                                                     [--casos caso_01,caso_05] [--paralelo 3] [--timeout 60] [--seco] [--autoprueba]
///                                                     [--combinaciones claude-sonnet-5-5:low,claude-opus-5-5:high]
/// --combinaciones reemplaza a --modelos × --esfuerzos cuando no hace falta el cruce completo.
/// La llave sale de Anthropic__ApiKey (variable de entorno o .env de la raíz); nunca se escribe en el informe ni en consola.
/// <c>--autoprueba</c> prueba el propio arnés sin llamar a la IA: una IA falsa devuelve la respuesta correcta y el informe debe dar 100 %.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    public static async Task<int> Main(string[] args)
    {
        var opciones = clsOpcionesArnes.Leer(args);
        var raiz = RaizDelRepo();
        var dataset = Path.Combine(raiz, "tests", "evaluacion-ia", "dataset");

        var casos = await CargarCasosAsync(dataset, opciones.Casos);
        if (casos.Count == 0)
        {
            Console.Error.WriteLine("No hay casos que coincidan con --casos.");
            return 1;
        }

        var combos = opciones.Combinaciones.Count > 0
            ? opciones.Combinaciones.ToList()
            : opciones.Modelos.SelectMany(m => opciones.Esfuerzos.Select(e => new clsCombo(m, e))).ToList();
        var total = combos.Count * casos.Count * opciones.Repeticiones;
        Console.WriteLine($"{casos.Count} casos × {combos.Count} combinaciones × {opciones.Repeticiones} repeticiones = {total} llamadas.");
        if (opciones.Seco)
        {
            Console.WriteLine("Modo seco: los encabezados de las réplicas coinciden con las respuestas. No se llamó a la IA.");
            return 0;
        }

        var entorno = LeerEntorno(raiz);
        if (!opciones.Autoprueba && string.IsNullOrWhiteSpace(entorno.GetValueOrDefault("Anthropic__ApiKey")))
        {
            Console.Error.WriteLine("Falta Anthropic__ApiKey (variable de entorno o .env de la raíz del repo).");
            return 1;
        }

        var inicio = DateTime.Now;
        var llamadas = new List<clsLlamada>();
        foreach (var combo in combos)
        {
            IAsistenteIaPort? ia = opciones.Autoprueba ? null : new clsAnthropicAdapter(
                Options.Create(OpcionesAnthropic(entorno, combo, opciones.TimeoutSegundos)), new clsAvisosEnConsola<clsAnthropicAdapter>());
            var trabajos = Enumerable.Range(1, opciones.Repeticiones).SelectMany(r => casos.Select(c => (Caso: c, Repeticion: r)));
            using var cupo = new SemaphoreSlim(opciones.Paralelo);
            var delCombo = await Task.WhenAll(trabajos.Select(async t =>
            {
                await cupo.WaitAsync();
                try { return await EvaluarAsync(ia ?? new clsIaPerfecta(t.Caso.Respuesta), combo, t.Caso, t.Repeticion); }
                finally { cupo.Release(); }
            }));
            llamadas.AddRange(delCombo);
            var r = clsResumen.De(combo, delCombo);
            Console.WriteLine($"{combo,-28} acierto con campo {Pct(r.AciertoConCampo),7}  falsos positivos {r.FalsosPositivos,3}  " +
                              $"costo/Excel {Usd(r.CostoPorExcel)}  p95 {Seg(r.P95Ms)}  fallos {r.Fallos}");
        }

        var carpeta = Path.Combine(raiz, "tests", "evaluacion-ia", "informes");
        Directory.CreateDirectory(carpeta);
        var nombre = $"{inicio:yyyy-MM-dd_HHmm}_mapeo" + (opciones.Autoprueba ? "_autoprueba" : "");
        await File.WriteAllTextAsync(Path.Combine(carpeta, nombre + ".json"), JsonSerializer.Serialize(llamadas, Json));
        await File.WriteAllTextAsync(Path.Combine(carpeta, nombre + ".md"), clsInforme.Escribir(inicio, opciones, casos, combos, llamadas));
        Console.WriteLine($"Informe: tests/evaluacion-ia/informes/{nombre}.md");
        return 0;
    }

    private static async Task<clsLlamada> EvaluarAsync(IAsistenteIaPort ia, clsCombo combo, clsCaso caso, int repeticion)
    {
        // Un medidor y un handler por llamada: así el uso de tokens de cada una no se mezcla con las que corren en paralelo.
        var medidor = new clsIaMedida(ia);
        var handler = new clsSugerirMapeoExcelQueryHandler(medidor, new clsDiccionarioVacio(), new clsUsuarioDelArnes(), new clsSinPlantillas());
        var reloj = Stopwatch.StartNew();
        var propuesta = await handler.Handle(new clsSugerirMapeoExcelQuery(null, caso.Encabezados, caso.Filas));
        reloj.Stop();

        var uso = medidor.Uso;
        var costo = uso is null ? null : clsPreciosIa.Estimar(uso.Modelo, uso.Entrada, uso.Salida, uso.CacheLectura, uso.CacheEscritura)
                                         ?? clsPreciosIa.Estimar(combo.Modelo, uso.Entrada, uso.Salida, uso.CacheLectura, uso.CacheEscritura);
        var columnas = propuesta.Columnas.Select(c => new clsColumnaPropuesta(c.Indice, c.Campo, c.Transformacion, c.Confianza)).ToList();
        return new clsLlamada(combo.Modelo, combo.Esfuerzo, caso.Respuesta.Caso, caso.Respuesta.Formato, repeticion, propuesta.IaUsada,
            propuesta.MotivoSinIa, uso?.Modelo, uso?.Entrada ?? 0, uso?.Salida ?? 0, uso?.CacheLectura ?? 0, uso?.CacheEscritura ?? 0, costo,
            medidor.Milisegundos ?? reloj.ElapsedMilliseconds, columnas, clsCalificacion.De(caso.Respuesta, columnas));
    }

    private static clsOpcionesAnthropic OpcionesAnthropic(IReadOnlyDictionary<string, string> entorno, clsCombo combo, int? timeout) => new()
    {
        ApiKey = entorno["Anthropic__ApiKey"],
        WorkspaceId = entorno.GetValueOrDefault("Anthropic__WorkspaceId"),
        Model = combo.Modelo,
        Effort = combo.Esfuerzo,
        TimeoutSegundos = timeout ?? (int.TryParse(entorno.GetValueOrDefault("Anthropic__TimeoutSegundos"), out var t) ? t : 60),
        MaxReintentos = int.TryParse(entorno.GetValueOrDefault("Anthropic__MaxReintentos"), out var r) ? r : 2,
        MaxTokensSalida = int.TryParse(entorno.GetValueOrDefault("Anthropic__MaxTokensSalida"), out var m) ? m : 4096,
        LimiteTokensPorUsuarioDia = 0,
        Funciones = new(StringComparer.OrdinalIgnoreCase) { [nameof(clsFuncionIa.ImportacionExcel)] = true },
    };

    // ---------------------------------------------------------------- casos

    private static async Task<List<clsCaso>> CargarCasosAsync(string dataset, IReadOnlyList<string> filtro)
    {
        var lector = new clsExcelSolicitudesAdapter();
        var casos = new List<clsCaso>();
        foreach (var ruta in Directory.GetFiles(Path.Combine(dataset, "respuestas"), "caso_*.json").Order())
        {
            var respuesta = JsonSerializer.Deserialize<clsRespuestaCaso>(await File.ReadAllTextAsync(ruta), Json)!;
            if (filtro.Count > 0 && !filtro.Any(f => respuesta.Caso.StartsWith(f, StringComparison.OrdinalIgnoreCase))) continue;

            await using var archivo = File.OpenRead(Path.Combine(dataset, respuesta.Archivo));
            var analisis = await lector.AnalizarAsync(archivo, Path.GetFileName(respuesta.Archivo), CancellationToken.None);
            var hoja = analisis.Hojas.Single(h => h.Nombre == respuesta.Hoja);

            // La respuesta se escribió sobre los encabezados que produce analizar-excel; si no coinciden, calificar no tiene sentido.
            // Solo se ignoran los saltos de línea dentro de la celda: ClosedXML y openpyxl los devuelven distinto (\r\n o \n).
            var esperados = respuesta.Columnas.Select(c => c.Columna.ReplaceLineEndings("\n")).ToList();
            var leidos = hoja.Encabezados.Select(e => e.ReplaceLineEndings("\n")).ToList();
            if (!leidos.SequenceEqual(esperados))
            {
                var i = Enumerable.Range(0, Math.Max(leidos.Count, esperados.Count))
                    .First(i => i >= leidos.Count || i >= esperados.Count || leidos[i] != esperados[i]);
                throw new InvalidOperationException(
                    $"{respuesta.Caso}: los encabezados de analizar-excel no coinciden con la respuesta (columna {i}: " +
                    $"«{leidos.ElementAtOrDefault(i)}» contra «{esperados.ElementAtOrDefault(i)}»). Vuelve a correr generar_dataset.py.");
            }
            // Y debe usar las listas cerradas vigentes de sugerir-mapeo: una respuesta imposible de dar no se puede acertar.
            foreach (var c in respuesta.Columnas)
                foreach (var (campo, transformacion) in c.AlternativasAceptadas.Select(a => (a.Campo, a.Transformacion)).Prepend((c.Campo, c.Transformacion)))
                    if (!clsTerminoCliente.CamposDestino.Contains(campo) || !clsTransformacionesMapeo.Todas.Contains(transformacion)
                        || !clsTransformacionesMapeo.Aplica(campo, transformacion))
                        throw new InvalidOperationException(
                            $"{respuesta.Caso}, columna {c.Indice}: «{campo}» con «{transformacion}» no es una respuesta válida de sugerir-mapeo.");
            casos.Add(new clsCaso(respuesta, hoja.Encabezados, hoja.Filas));
        }
        return casos;
    }

    // ---------------------------------------------------------------- entorno

    private static string RaizDelRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Nexo.Clean.sln"))) return dir.FullName;
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Nexo.Clean.sln"))) return dir.FullName;
        throw new InvalidOperationException("No se encontró la raíz del repo (Nexo.Clean.sln).");
    }

    /// <summary>Las variables Anthropic__* del .env de la raíz; las variables de entorno mandan sobre el archivo.</summary>
    private static Dictionary<string, string> LeerEntorno(string raiz)
    {
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var env = Path.Combine(raiz, ".env");
        if (File.Exists(env))
            foreach (var linea in File.ReadAllLines(env))
            {
                var l = linea.Trim();
                var igual = l.IndexOf('=');
                if (l.StartsWith('#') || igual <= 0) continue;
                var clave = l[..igual].Trim();
                if (clave.StartsWith("Anthropic__", StringComparison.OrdinalIgnoreCase)) valores[clave] = l[(igual + 1)..].Trim().Trim('"', '\'');
            }
        foreach (var clave in valores.Keys.ToList().Concat(["Anthropic__ApiKey", "Anthropic__WorkspaceId"]).Distinct())
            if (Environment.GetEnvironmentVariable(clave) is { Length: > 0 } v) valores[clave] = v;
        return valores;
    }

    internal static string Pct(double? x) => x is null ? "—" : (x.Value * 100).ToString("0.0", Es) + " %";
    internal static string Usd(decimal? x) => x is null ? "—" : "US$ " + x.Value.ToString("0.0000", Es);
    internal static string Seg(double? ms) => ms is null ? "—" : (ms.Value / 1000).ToString("0.0", Es) + " s";
}

// ---------------------------------------------------------------- opciones

public sealed record clsOpcionesArnes(
    IReadOnlyList<string> Modelos, IReadOnlyList<string> Esfuerzos, int Repeticiones, IReadOnlyList<string> Casos, int Paralelo,
    int? TimeoutSegundos, bool Seco, bool Autoprueba, IReadOnlyList<clsCombo> Combinaciones)
{
    public static clsOpcionesArnes Leer(string[] args)
    {
        string? Valor(string nombre)
        {
            var i = Array.IndexOf(args, "--" + nombre);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        IReadOnlyList<string> Lista(string nombre, string porDefecto) =>
            (Valor(nombre) ?? porDefecto).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new(
            Lista("modelos", "claude-sonnet-4-6,claude-sonnet-5-5"),
            Lista("esfuerzos", "low,medium,high"),
            int.TryParse(Valor("repeticiones"), out var r) ? Math.Max(1, r) : 3,
            Lista("casos", ""),
            int.TryParse(Valor("paralelo"), out var p) ? Math.Clamp(p, 1, 8) : 3,
            int.TryParse(Valor("timeout"), out var t) ? t : null,
            args.Contains("--seco"),
            args.Contains("--autoprueba"),
            Lista("combinaciones", "").Select(c => c.Split(':', 2)).Where(c => c.Length == 2).Select(c => new clsCombo(c[0].Trim(), c[1].Trim())).ToList());
    }
}

public sealed record clsCombo(string Modelo, string Esfuerzo)
{
    public override string ToString() => $"{Modelo} · {Esfuerzo}";
}

// ---------------------------------------------------------------- dataset

public sealed record clsAlternativa(string Campo, string Transformacion);

public sealed record clsColumnaEsperada(
    int Indice, string Columna, string Campo, string Transformacion, bool RequiereDespivotar, List<clsAlternativa> AlternativasAceptadas, string? Nota);

public sealed record clsRespuestaCaso(string Caso, string Archivo, string Hoja, string Cliente, string Formato, List<clsColumnaEsperada> Columnas);

public sealed record clsCaso(clsRespuestaCaso Respuesta, IReadOnlyList<string> Encabezados, IReadOnlyList<IReadOnlyList<string>> Filas);

// ---------------------------------------------------------------- resultados

public sealed record clsColumnaPropuesta(int Indice, string Campo, string Transformacion, double Confianza);

/// <summary>Cómo le fue a una propuesta contra la respuesta correcta.</summary>
/// <param name="ConCampo">Columnas que la respuesta asigna a un campo (no <c>ignorar</c>): lo que de verdad importa para importar.</param>
/// <param name="FalsosPositivos">Columnas que la respuesta deja en <c>ignorar</c> y la propuesta asigna a un campo.</param>
/// <param name="Errores">Las columnas mal propuestas, para leer qué se equivoca.</param>
public sealed record clsCalificacion(
    int Columnas, int Aciertos, int ConCampo, int AciertosConCampo, int Ignorar, int FalsosPositivos,
    IReadOnlyList<clsError> Errores, IReadOnlyList<clsDespivotar> Despivotar, IReadOnlyList<clsPorCampo> PorCampo)
{
    public static clsCalificacion De(clsRespuestaCaso respuesta, IReadOnlyList<clsColumnaPropuesta> propuesta)
    {
        int aciertos = 0, conCampo = 0, aciertosConCampo = 0, ignorar = 0, falsos = 0;
        var errores = new List<clsError>();
        var despivotar = new List<clsDespivotar>();
        var porCampo = new List<clsPorCampo>();
        foreach (var esperada in respuesta.Columnas)
        {
            var p = propuesta.FirstOrDefault(c => c.Indice == esperada.Indice);
            var campo = p?.Campo ?? "ignorar";
            var transformacion = p?.Transformacion ?? "ninguna";
            var bien = Acierta(esperada, campo, transformacion);
            var esIgnorar = Igual(esperada.Campo, "ignorar");

            if (bien) aciertos++;
            else errores.Add(new clsError(esperada.Indice, esperada.Columna, Texto(esperada.Campo, esperada.Transformacion), Texto(campo, transformacion)));
            if (esIgnorar)
            {
                ignorar++;
                if (!bien) falsos++;
            }
            else
            {
                conCampo++;
                if (bien) aciertosConCampo++;
                porCampo.Add(new clsPorCampo(esperada.Campo, bien));
            }
            if (esperada.RequiereDespivotar) despivotar.Add(new clsDespivotar(esperada.Columna, campo));
        }
        return new(respuesta.Columnas.Count, aciertos, conCampo, aciertosConCampo, ignorar, falsos, errores, despivotar, porCampo);
    }

    /// <summary>
    /// Bien si coincide con la respuesta o con una alternativa aceptada. En <c>ignorar</c> la transformación no importa: la columna no se usa.
    /// </summary>
    private static bool Acierta(clsColumnaEsperada esperada, string campo, string transformacion)
    {
        bool Coincide(string c, string t) => Igual(c, campo) && (Igual(c, "ignorar") || Igual(t, transformacion));
        return Coincide(esperada.Campo, esperada.Transformacion) || esperada.AlternativasAceptadas.Any(a => Coincide(a.Campo, a.Transformacion));
    }

    private static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static string Texto(string campo, string transformacion) =>
        Igual(campo, "ignorar") || Igual(transformacion, "ninguna") ? campo : $"{campo} ({transformacion})";
}

public sealed record clsError(int Indice, string Columna, string Esperado, string Propuesto);

public sealed record clsDespivotar(string Columna, string Propuesto);

/// <summary>Una columna con campo de la respuesta (origen, destino…) y si la propuesta la acertó: el acierto por campo de NEXO.</summary>
public sealed record clsPorCampo(string Campo, bool Acierto);

public sealed record clsLlamada(
    string Modelo, string Esfuerzo, string Caso, string Formato, int Repeticion, bool IaUsada, string? MotivoSinIa, string? ModeloRespondido,
    long TokensEntrada, long TokensSalida, long TokensCacheLectura, long TokensCacheEscritura, decimal? CostoUsd, long Milisegundos,
    IReadOnlyList<clsColumnaPropuesta> Propuesta, clsCalificacion Calificacion)
{
    public clsCombo Combo => new(Modelo, Esfuerzo);
}

/// <summary>
/// Los números de una combinación de modelo y esfuerzo. El acierto se calcula solo sobre las llamadas en las que la IA respondió: una
/// llamada fallida devuelve todo en <c>ignorar</c> y acertaría las columnas que no se usan sin haber hecho nada. Los fallos van aparte.
/// </summary>
public sealed record clsResumen(
    clsCombo Combo, int Llamadas, int Fallos, double? AciertoConCampo, double? AciertoTotal, int FalsosPositivos, double? TasaFalsosPositivos,
    decimal? CostoPorExcel, double? P50Ms, double? P95Ms, double? TokensEntrada, double? TokensSalida)
{
    public static clsResumen De(clsCombo combo, IReadOnlyCollection<clsLlamada> llamadas)
    {
        var buenas = llamadas.Where(l => l.IaUsada).ToList();
        var cal = buenas.Select(l => l.Calificacion).ToList();
        double? Razon(int a, int b) => b == 0 ? null : (double)a / b;
        var costos = llamadas.Where(l => l.CostoUsd is not null).Select(l => l.CostoUsd!.Value).ToList();
        var ms = buenas.Select(l => (double)l.Milisegundos).Order().ToList();
        return new(combo, llamadas.Count, llamadas.Count - buenas.Count,
            Razon(cal.Sum(c => c.AciertosConCampo), cal.Sum(c => c.ConCampo)),
            Razon(cal.Sum(c => c.Aciertos), cal.Sum(c => c.Columnas)),
            cal.Sum(c => c.FalsosPositivos),
            Razon(cal.Sum(c => c.FalsosPositivos), cal.Sum(c => c.Ignorar)),
            costos.Count == 0 ? null : Math.Round(costos.Sum() / llamadas.Count, 6),
            Percentil(ms, 0.50), Percentil(ms, 0.95),
            buenas.Count == 0 ? null : buenas.Average(l => (double)l.TokensEntrada),
            buenas.Count == 0 ? null : buenas.Average(l => (double)l.TokensSalida));
    }

    private static double? Percentil(IReadOnlyList<double> ordenados, double p) =>
        ordenados.Count == 0 ? null : ordenados[Math.Max(0, (int)Math.Ceiling(p * ordenados.Count) - 1)];
}

// ---------------------------------------------------------------- dobles

/// <summary>Envuelve al adaptador real y guarda lo que gastó la llamada, también cuando la respuesta no sirvió.</summary>
public sealed class clsIaMedida(IAsistenteIaPort ia) : IAsistenteIaPort
{
    public clsUsoDeTokens? Uso { get; private set; }
    public long? Milisegundos { get; private set; }

    public bool Habilitada(clsFuncionIa funcion) => ia.Habilitada(funcion);

    public async Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default)
    {
        var reloj = Stopwatch.StartNew();
        try
        {
            var r = await ia.CompletarAsync(peticion, ct);
            Uso = new clsUsoDeTokens(r.Modelo, r.TokensEntrada, r.TokensSalida, r.TokensCacheLectura, r.TokensCacheEscritura);
            return r;
        }
        catch (clsIaRespuestaNoUtilException ex)
        {
            Uso = ex.Uso;
            throw;
        }
        finally
        {
            Milisegundos = reloj.ElapsedMilliseconds;
        }
    }
}

/// <summary>
/// La IA falsa de <c>--autoprueba</c>: responde, en el formato que pide sugerir-mapeo, exactamente la respuesta correcta del caso. Sirve
/// para probar que el arnés califica bien (debe dar 100 % y 0 falsos positivos) sin llamar al proveedor.
/// </summary>
public sealed class clsIaPerfecta(clsRespuestaCaso respuesta) : IAsistenteIaPort
{
    public bool Habilitada(clsFuncionIa funcion) => true;

    public Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new
        {
            columnas = respuesta.Columnas.Select(c => new { indice = c.Indice, campo = c.Campo, transformacion = c.Transformacion, confianza = 1.0, nota = "" }),
            dudas = Array.Empty<string>(),
        });
        return Task.FromResult(new clsRespuestaIa(json, json, "autoprueba", 1000, 100, TimeSpan.FromMilliseconds(1)));
    }
}

/// <summary>Diccionario vacío: se mide a la IA sola, sin que el diccionario resuelva columnas por ella.</summary>
public sealed class clsDiccionarioVacio : IDiccionarioClienteRepository
{
    public Task<IReadOnlyList<clsTerminoCliente>> ListarAsync(Guid? clienteId, bool incluirGenerales, string? tipo, string? buscar, bool soloActivos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<clsTerminoCliente>>([]);
    public Task<clsTerminoCliente?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult<clsTerminoCliente?>(null);
    public Task<clsTerminoCliente?> BuscarDuplicadoAsync(Guid? clienteId, string terminoNormalizado, string tipo, string? contexto, Guid? excluirId, CancellationToken ct) =>
        Task.FromResult<clsTerminoCliente?>(null);
    public Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct) => Task.FromResult(false);
    public void Agregar(clsTerminoCliente termino) => throw new NotSupportedException();
    public void Eliminar(clsTerminoCliente termino) => throw new NotSupportedException();
}

/// <summary>Sin plantillas: sin cliente el handler ni las consulta, pero el constructor las pide.</summary>
public sealed class clsSinPlantillas : IPlantillasMapeoRepository
{
    public Task<IReadOnlyList<clsPlantillaMapeoCliente>> ListarAsync(Guid? clienteId, CancellationToken ct) => Task.FromResult<IReadOnlyList<clsPlantillaMapeoCliente>>([]);
    public Task<clsPlantillaMapeoCliente?> BuscarPorFirmaAsync(Guid clienteId, string firma, CancellationToken ct) => Task.FromResult<clsPlantillaMapeoCliente?>(null);
    public Task<clsPlantillaMapeoCliente?> ObtenerParaActualizarAsync(Guid clienteId, string firma, CancellationToken ct) => Task.FromResult<clsPlantillaMapeoCliente?>(null);
    public Task<clsPlantillaMapeoCliente?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult<clsPlantillaMapeoCliente?>(null);
    public Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct) => Task.FromResult(false);
    public void Agregar(clsPlantillaMapeoCliente plantilla) => throw new NotSupportedException();
    public void Eliminar(clsPlantillaMapeoCliente plantilla) => throw new NotSupportedException();
    public bool EsChoqueDeFirma(Exception error) => false;
    public void DescartarCambiosPendientes() { }
}

public sealed class clsUsuarioDelArnes : ICurrentUserPort
{
    public Guid Id => Guid.Empty;
    public string? Nombre => "arnes-evaluacion-ia";
    public RolUsuario? Rol => RolUsuario.Comercial;
}

/// <summary>Los avisos del adaptador (por ejemplo, por qué el proveedor rechazó una petición) a la consola. Nunca traen la llave.</summary>
public sealed class clsAvisosEnConsola<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel)) Console.Error.WriteLine($"  [{logLevel}] {formatter(state, exception)}");
    }
}
