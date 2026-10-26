using System.Text.Json;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Propone a qué campo de NEXO corresponde cada columna del Excel de un cliente. Recibe los encabezados y las filas que ya devolvió el
/// análisis del archivo; no vuelve a leer el archivo ni guarda nada.
/// </summary>
/// <param name="ClienteId">El cliente dueño del archivo: su diccionario es la mejor pista. Sin él solo se usa el diccionario general.</param>
public sealed record clsSugerirMapeoExcelQuery(Guid? ClienteId, IReadOnlyList<string>? Encabezados, IReadOnlyList<IReadOnlyList<string>>? Filas)
    : IQuery<clsSugerenciaMapeoDto>;

/// <summary>
/// El orden de las cosas:
/// 1) Lo que el diccionario del cliente ya sabe se resuelve sin la IA y manda siempre sobre ella.
/// 2) Si quedan columnas sin resolver y la IA está disponible, se le piden SOLO con datos mínimos: encabezados, hasta 20 filas, enmascaradas
///    (NIT, nombres, correos, teléfonos y valores nunca salen) y recortadas. Sin ninguna columna pendiente no se llama.
/// 3) Lo que responde el modelo se valida como cualquier entrada externa: solo cuentan índices que existen, campos y transformaciones de las
///    listas cerradas y confianzas entre 0 y 1; el resto se descarta.
/// Si la IA falla o está apagada, se devuelve igual lo que resolvió el diccionario y el resto queda para mapear a mano. Un tope de uso superado
/// (429) sí se informa, no se esconde.
/// </summary>
public sealed class clsSugerirMapeoExcelQueryHandler(
    IAsistenteIaPort ia, IDiccionarioClienteRepository diccionario, ICurrentUserPort usuarioActual)
    : IQueryHandler<clsSugerirMapeoExcelQuery, clsSugerenciaMapeoDto>
{
    public const int MaxFilasDeMuestra = 20;
    public const int MaxColumnas = 100;
    public const int MaxTerminosDelDiccionario = 150;
    public const int MaxDudas = 10;
    public const int MaxCaracteresDuda = 300;
    private const string Ignorar = "ignorar";

    public async Task<clsSugerenciaMapeoDto> Handle(clsSugerirMapeoExcelQuery query, CancellationToken ct = default)
    {
        var encabezados = LimpiarEncabezados(query.Encabezados);
        var terminos = await TerminosDeColumnaAsync(query.ClienteId, ct);

        // 1) Lo que ya sabe el diccionario.
        var resueltas = new Dictionary<int, clsColumnaMapeadaDto>();
        for (var i = 0; i < encabezados.Count; i++)
        {
            var normalizado = clsResolucionDeCatalogos.Normalizar(encabezados[i]);
            var termino = terminos.FirstOrDefault(t => t.ClienteId is not null && t.TerminoNormalizado == normalizado)
                ?? terminos.FirstOrDefault(t => t.ClienteId is null && t.TerminoNormalizado == normalizado);
            if (termino is not null)
                resueltas[i] = new(i, encabezados[i], termino.Concepto, clsTransformacionesMapeo.Ninguna, 1.0, "diccionario", "Está en el diccionario del cliente.");
        }

        // 2) La IA, solo para lo pendiente.
        var propuestaIa = new Dictionary<int, clsColumnaMapeadaDto>();
        var dudasIa = new List<string>();
        bool iaUsada = false;
        string? motivoSinIa = null;

        if (resueltas.Count == encabezados.Count)
            motivoSinIa = "DICCIONARIO_COMPLETO";
        else if (!ia.Habilitada(clsFuncionIa.ImportacionExcel))
            motivoSinIa = "IA_NO_DISPONIBLE";
        else
        {
            try
            {
                var respuesta = await ia.CompletarAsync(Peticion(query, encabezados, terminos), ct);
                (propuestaIa, dudasIa) = InterpretarRespuesta(respuesta.JsonEstructurado, encabezados);
                iaUsada = true;
            }
            catch (JsonException)
            {
                motivoSinIa = "IA_RESPUESTA_INVALIDA";
            }
            catch (clsServiceUnavailableException ex)
            {
                // La IA no es indispensable: el usuario sigue con lo del diccionario y mapea el resto a mano.
                motivoSinIa = ex.Code;
            }
        }

        // 3) La propuesta final: una entrada por columna, en el orden del archivo.
        var columnas = new List<clsColumnaMapeadaDto>();
        var dudas = new List<string>();
        for (var i = 0; i < encabezados.Count; i++)
        {
            propuestaIa.TryGetValue(i, out var delModelo);
            if (resueltas.TryGetValue(i, out var delDiccionario))
            {
                if (delModelo is not null && delModelo.Campo != delDiccionario.Campo)
                    dudas.Add($"El diccionario del cliente manda sobre la IA en «{encabezados[i]}» ({delDiccionario.Campo}); la IA proponía {delModelo.Campo}.");
                // Si la IA coincide, se conserva su transformación (el diccionario no sabe de unidades); si no, ninguna.
                var transformacion = delModelo is not null && delModelo.Campo == delDiccionario.Campo ? delModelo.Transformacion : clsTransformacionesMapeo.Ninguna;
                columnas.Add(delDiccionario with { Transformacion = transformacion });
            }
            else
                columnas.Add(delModelo ?? new(i, encabezados[i], Ignorar, clsTransformacionesMapeo.Ninguna, 0, "ninguno", "Sin propuesta: revisa esta columna."));
        }

        dudas.AddRange(DudasDelMapeo(columnas));
        dudas.AddRange(dudasIa);
        return new clsSugerenciaMapeoDto(iaUsada, motivoSinIa, columnas, dudas.Take(MaxDudas).ToList());
    }

    // ---------------------------------------------------------------- entrada

    private static IReadOnlyList<string> LimpiarEncabezados(IReadOnlyList<string>? encabezados)
    {
        if (encabezados is null || encabezados.Count == 0)
            throw new clsValidationFailedException("ENCABEZADOS_REQUERIDOS", "Envía los encabezados de las columnas del archivo.");
        if (encabezados.Count > MaxColumnas)
            throw new clsValidationFailedException("DEMASIADAS_COLUMNAS", $"El archivo trae más de {MaxColumnas} columnas; no se puede proponer un mapeo.");

        return encabezados.Select((e, i) =>
        {
            var texto = clsTexto.Limpiar(e);
            return string.IsNullOrEmpty(texto) ? $"(sin encabezado {i + 1})" : texto.Length <= clsEnmascaradorDeCeldas.MaxCaracteresPorCelda ? texto : texto[..clsEnmascaradorDeCeldas.MaxCaracteresPorCelda];
        }).ToList();
    }

    private async Task<IReadOnlyList<clsTerminoCliente>> TerminosDeColumnaAsync(Guid? clienteId, CancellationToken ct)
    {
        var todos = await diccionario.ListarAsync(clienteId, incluirGenerales: true, tipo: null, buscar: null, soloActivos: true, ct);
        // Sin cliente, "todos" son los de todos los clientes: solo valen los generales.
        return todos.Where(t => clienteId is not null || t.ClienteId is null).Take(MaxTerminosDelDiccionario).ToList();
    }

    private clsPeticionIa Peticion(clsSugerirMapeoExcelQuery query, IReadOnlyList<string> encabezados, IReadOnlyList<clsTerminoCliente> terminos)
    {
        var marcadores = encabezados.Select(clsEnmascaradorDeCeldas.MarcadorDeColumna).ToList();
        var filas = query.Filas ?? [];
        var muestra = filas.Take(MaxFilasDeMuestra)
            .Select(fila => (IReadOnlyList<string>)encabezados.Select((_, i) => clsEnmascaradorDeCeldas.Celda(i < fila?.Count ? fila[i] : null, marcadores[i])).ToList())
            .ToList();

        // Las pistas del cliente son su vocabulario (columnas, unidades y valores), no datos de sus solicitudes.
        return new clsPeticionIa(
            clsFuncionIa.ImportacionExcel,
            clsPromptMapeoExcel.Instrucciones(),
            [new clsMensajeIa(clsMensajeIa.RolUsuario, clsPromptMapeoExcel.Mensaje(encabezados, muestra, filas.Count, terminos))],
            clsPromptMapeoExcel.EsquemaDeSalida(),
            UsuarioId: usuarioActual.Id);
    }

    // ---------------------------------------------------------------- salida del modelo

    /// <summary>Lo que el modelo propuso, validado: solo índices existentes, campos y transformaciones de las listas y confianza entre 0 y 1.</summary>
    internal static (Dictionary<int, clsColumnaMapeadaDto> Columnas, List<string> Dudas) InterpretarRespuesta(string? json, IReadOnlyList<string> encabezados)
    {
        var columnas = new Dictionary<int, clsColumnaMapeadaDto>();
        var dudas = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return (columnas, dudas);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("columnas", out var lista) && lista.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in lista.EnumerateArray())
            {
                if (!c.TryGetProperty("indice", out var iEl) || !iEl.TryGetInt32(out var indice) || indice < 0 || indice >= encabezados.Count) continue;
                if (columnas.ContainsKey(indice)) continue; // la primera respuesta para un índice es la que vale

                var campo = clsTerminoCliente.Canonico(clsTerminoCliente.CamposDestino, Texto(c, "campo")) ?? Ignorar;
                var transformacion = clsTerminoCliente.Canonico(clsTransformacionesMapeo.Todas, Texto(c, "transformacion")) ?? clsTransformacionesMapeo.Ninguna;
                // Una conversión que no corresponde al campo (o una columna que no se usa) no tiene sentido: se descarta.
                if (!clsTransformacionesMapeo.Aplica(campo, transformacion)) transformacion = clsTransformacionesMapeo.Ninguna;
                var confianza = c.TryGetProperty("confianza", out var cEl) && cEl.TryGetDouble(out var cd) ? Math.Round(Math.Clamp(cd, 0, 1), 2) : 0;
                var nota = clsTexto.Limpiar(Texto(c, "nota"));
                columnas[indice] = new(indice, encabezados[indice], campo, transformacion, confianza, "ia",
                    string.IsNullOrEmpty(nota) ? null : Recortar(nota));
            }
        }

        if (doc.RootElement.TryGetProperty("dudas", out var d) && d.ValueKind == JsonValueKind.Array)
            foreach (var duda in d.EnumerateArray())
                if (duda.ValueKind == JsonValueKind.String && clsTexto.Limpiar(duda.GetString()) is { Length: > 0 } texto)
                    dudas.Add(Recortar(texto));

        return (columnas, dudas);
    }

    private static string? Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string Recortar(string texto) => texto.Length <= MaxCaracteresDuda ? texto : texto[..MaxCaracteresDuda];

    // ---------------------------------------------------------------- dudas que salen del propio mapeo

    private static IEnumerable<string> DudasDelMapeo(IReadOnlyList<clsColumnaMapeadaDto> columnas)
    {
        foreach (var grupo in columnas.Where(c => c.Campo != Ignorar).GroupBy(c => c.Campo).Where(g => g.Count() > 1))
            yield return $"Varias columnas apuntan a «{grupo.Key}»: {string.Join(", ", grupo.Select(c => $"«{c.Columna}»"))}.";

        foreach (var obligatorio in new[] { "origen", "destino" })
            if (!columnas.Any(c => c.Campo == obligatorio) && !columnas.Any(c => c.Transformacion == clsTransformacionesMapeo.DividirOrigenDestino))
                yield return $"No se encontró una columna para «{obligatorio}», que es obligatorio.";
    }
}

public static class clsDependencyInjection
{
    public static IServiceCollection AddMapeoExcelUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<clsSugerirMapeoExcelQuery, clsSugerenciaMapeoDto>, clsSugerirMapeoExcelQueryHandler>()
        .AddScoped<IQueryHandler<clsAplicarMapeoExcelQuery, clsResultadoAplicarMapeoDto>, clsAplicarMapeoExcelQueryHandler>();
}
