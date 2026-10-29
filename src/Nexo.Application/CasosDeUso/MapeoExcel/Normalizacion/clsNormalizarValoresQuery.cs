using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.MapeoExcel.Normalizacion;

/// <summary>
/// Normaliza los textos libres distintos de un Excel (tipo de vehículo, carrocería y ciudad) a los valores oficiales de NEXO. No persiste nada: el
/// usuario revisa el resultado y la captura sigue como siempre (lo que no se resuelva se envía tal cual y la captura lo manda a la cuarentena de
/// vehículos).
/// </summary>
/// <param name="ClienteId">El cliente dueño del archivo: su diccionario manda. Sin él solo se usa el diccionario general.</param>
public sealed record clsNormalizarValoresQuery(Guid? ClienteId, IReadOnlyList<clsValorANormalizar>? Valores)
    : IQuery<clsResultadoNormalizacionDto>;

/// <summary>
/// El orden de las cosas:
/// 1) Reglas exactas, sin IA: el diccionario del cliente, el de alias de vehículos, el catálogo oficial (código o descripción idénticos) y, para
///    las ciudades, un código DANE o un nombre de municipio con las mismas palabras (sin tildes, puntuación ni «D.C.»), usando el departamento
///    si el texto lo trae («Santa Rosa, Cauca»).
/// 2) Con lo que queda sin resolver, UNA sola llamada a la IA por todos los textos distintos (no una por fila), solo con textos que no traen datos
///    sensibles y con listas cerradas: el catálogo oficial para vehículos y carrocerías, y un puñado de municipios parecidos para cada ciudad.
/// 3) Nada se asigna por lo que diga el modelo: su respuesta solo vale si la clave elegida está en la lista que se le dio para ese texto y su
///    confianza llega al mínimo. Lo demás queda sin resolver.
/// Si la IA falla o está apagada se devuelve igual lo que resolvieron las reglas. Un tope de uso superado (429) sí se informa.
/// </summary>
public sealed class clsNormalizarValoresQueryHandler(
    IAsistenteIaPort ia,
    IDiccionarioClienteRepository diccionario,
    IAliasCatalogoPort alias,
    ITiposVehiculoPort tiposVehiculo,
    IConfiguracionesVehiculoPort vehiculos,
    IMunicipiosRepository municipios,
    ICurrentUserPort usuarioActual)
    : IQueryHandler<clsNormalizarValoresQuery, clsResultadoNormalizacionDto>
{
    public const string CampoVehiculo = "tipoVehiculo";
    public const string CampoCarroceria = "carroceria";
    public const string CampoCiudad = "ciudad";
    public static readonly IReadOnlyList<string> Campos = [CampoVehiculo, CampoCarroceria, CampoCiudad];

    public const string Resuelto = "resuelto";
    public const string Ambiguo = "ambiguo";
    public const string SinResolver = "sin_resolver";

    public const int MaxValores = 1000;
    public const int MaxCaracteresPorValor = 150;
    public const int MaxPendientesIa = 150;
    public const int MaxCandidatosPorCiudad = 8;
    public const int MaxOpcionesDeCatalogo = 150;
    public const double MinConfianzaIa = 0.7;
    private const double MinPuntajeCandidato = 0.6;

    private sealed record Resolucion(
        string Estado, string? Codigo, string? Descripcion, int? MunicipioId, string Origen, double Confianza, string? Nota,
        IReadOnlyList<clsCandidatoCiudadDto>? Candidatos = null)
    {
        public static Resolucion Sin(string? nota = null) => new(SinResolver, null, null, null, "ninguno", 0, nota);
    }

    /// <summary>Un texto que las reglas no resolvieron y que puede ir a la IA.</summary>
    private sealed record Pendiente(int Indice, string Campo, string Texto, IReadOnlyList<clsPromptNormalizacion.Opcion>? Candidatos, IReadOnlyList<clsMunicipio>? Municipios);

    public async Task<clsResultadoNormalizacionDto> Handle(clsNormalizarValoresQuery query, CancellationToken ct = default)
    {
        var entradas = Validar(query.Valores);
        var campos = entradas.Select(e => e.Campo).ToHashSet();

        // Los catálogos se cargan una vez por llamada y solo los que hacen falta.
        var valoresDelCliente = await clsValoresDelCliente.CargarAsync(diccionario, query.ClienteId, ct);
        var catalogoVehiculos = campos.Contains(CampoVehiculo) ? await CatalogoDeVehiculosAsync(ct) : [];
        var aliasVehiculo = campos.Contains(CampoVehiculo) ? await alias.MapaActivoAsync(clsAliasElemento.TipoVehiculo, ct) : new Dictionary<string, string>();
        var carrocerias = campos.Contains(CampoCarroceria) ? await CatalogoDeCarroceriasAsync(ct) : new Dictionary<string, string>();
        var catalogoCiudades = campos.Contains(CampoCiudad) ? await CatalogoDeCiudadesAsync(ct) : null;

        // 1) Reglas exactas, una vez por valor distinto (campo + texto normalizado).
        var porClave = new Dictionary<(string Campo, string Clave), Resolucion>();
        var pendientes = new List<Pendiente>();
        var pendientePorClave = new Dictionary<(string, string), Pendiente>();

        foreach (var e in entradas)
        {
            var clave = (e.Campo, e.Normalizado);
            if (porClave.ContainsKey(clave)) continue;

            var (resolucion, opciones, candidatosCiudad) = e.Campo switch
            {
                CampoVehiculo => ReglasDeVehiculo(e.Normalizado, valoresDelCliente, aliasVehiculo, catalogoVehiculos),
                CampoCarroceria => ReglasDeCarroceria(e.Normalizado, valoresDelCliente, carrocerias),
                _ => ReglasDeCiudad(e.Texto, catalogoCiudades!),
            };
            porClave[clave] = resolucion;
            if (resolucion.Estado != SinResolver) continue;

            // 2) Lo que sigue sin resolver puede ir a la IA, si hay una lista cerrada de dónde elegir y el texto no trae datos sensibles.
            var tieneLista = e.Campo switch { CampoVehiculo => catalogoVehiculos.Count > 0, CampoCarroceria => carrocerias.Count > 0, _ => candidatosCiudad is { Count: > 0 } };
            if (!tieneLista) continue;
            if (clsEnmascaradorDeCeldas.Celda(e.Texto, null) != e.Texto)
            {
                porClave[clave] = Resolucion.Sin("El texto trae datos que no se envían a la IA.");
                continue;
            }
            if (pendientes.Count >= MaxPendientesIa)
            {
                porClave[clave] = Resolucion.Sin($"Hay más de {MaxPendientesIa} textos por resolver en esta llamada; se resolvieron los primeros.");
                continue;
            }

            var pendiente = new Pendiente(
                pendientes.Count, e.Campo, e.Texto,
                candidatosCiudad?.Select(m => new clsPromptNormalizacion.Opcion(m.Id.ToString(CultureInfo.InvariantCulture), $"{m.Municipio}, {m.Departamento}")).ToList(),
                candidatosCiudad);
            pendientes.Add(pendiente);
            pendientePorClave[clave] = pendiente;
        }

        // 3) Una sola llamada por todo lo pendiente.
        bool iaUsada = false;
        string? motivoSinIa = null;
        if (pendientes.Count == 0)
            motivoSinIa = "SIN_PENDIENTES";
        else if (!ia.Habilitada(clsFuncionIa.ImportacionExcel))
            motivoSinIa = "IA_NO_DISPONIBLE";
        else
        {
            try
            {
                var respuesta = await ia.CompletarAsync(Peticion(pendientes, catalogoVehiculos, carrocerias), ct);
                var elegidas = InterpretarRespuesta(respuesta.JsonEstructurado, pendientes.Count);
                iaUsada = true;

                foreach (var (clave, pendiente) in pendientePorClave)
                    if (elegidas.TryGetValue(pendiente.Indice, out var e))
                        porClave[clave] = Validar(pendiente, e, catalogoVehiculos, carrocerias);
            }
            catch (JsonException)
            {
                motivoSinIa = "IA_RESPUESTA_INVALIDA";
            }
            catch (clsServiceUnavailableException ex)
            {
                // La IA no es indispensable: las reglas ya resolvieron lo que pudieron y el resto se revisa a mano.
                motivoSinIa = ex.Code;
            }
        }

        // 4) Una entrada por texto distinto, en el orden recibido.
        var salida = entradas
            .DistinctBy(e => (e.Campo, e.Texto))
            .Select(e =>
            {
                var r = porClave[(e.Campo, e.Normalizado)];
                return new clsValorNormalizadoDto(e.Campo, e.Texto, r.Estado, r.Codigo, r.Descripcion, r.MunicipioId, r.Origen, r.Confianza, r.Nota, r.Candidatos);
            })
            .ToList();

        return new clsResultadoNormalizacionDto(iaUsada, motivoSinIa, porClave.Count, salida);
    }

    // ---------------------------------------------------------------- entrada

    private sealed record Entrada(string Campo, string Texto, string Normalizado);

    private static List<Entrada> Validar(IReadOnlyList<clsValorANormalizar>? valores)
    {
        if (valores is null || valores.Count == 0)
            throw new clsValidationFailedException("VALORES_REQUERIDOS", "Envía los textos a normalizar.");
        if (valores.Count > MaxValores)
            throw new clsValidationFailedException("DEMASIADOS_VALORES", $"Se pueden normalizar hasta {MaxValores:N0} textos por llamada.");

        var resultado = new List<Entrada>();
        foreach (var v in valores)
        {
            var campo = clsTerminoCliente.Canonico(Campos, v.Campo)
                ?? throw new clsValidationFailedException("CAMPO_INVALIDO", $"«{v.Campo}» no se puede normalizar; usa: {string.Join(", ", Campos)}.");
            var texto = clsTexto.Limpiar(v.Valor);
            if (string.IsNullOrEmpty(texto)) continue;
            if (texto.Length > MaxCaracteresPorValor)
                throw new clsValidationFailedException("VALOR_DEMASIADO_LARGO", $"Un texto a normalizar admite máximo {MaxCaracteresPorValor} caracteres.");
            resultado.Add(new(campo, texto, clsResolucionDeCatalogos.Normalizar(texto)));
        }
        return resultado;
    }

    // ---------------------------------------------------------------- catálogos

    private async Task<IReadOnlyList<clsTipoVehiculoDto>> CatalogoDeVehiculosAsync(CancellationToken ct) =>
        (await tiposVehiculo.ListarAsync(ct))
            .Where(t => !string.IsNullOrWhiteSpace(t.Codigo))
            .GroupBy(t => t.Codigo.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First() with { Codigo = g.Key })
            .ToList();

    /// <summary>Las carrocerías de las configuraciones oficiales (no descartadas): <c>texto normalizado → nombre guardado</c>, ganando la más antigua.</summary>
    private async Task<Dictionary<string, string>> CatalogoDeCarroceriasAsync(CancellationToken ct) =>
        (await vehiculos.ListarAsync(ct))
            .Where(v => v.EsCatalogoOficial && !v.Descartada && !string.IsNullOrWhiteSpace(v.Carroceria))
            .OrderBy(v => v.CreatedAt)
            .GroupBy(v => clsResolucionDeCatalogos.Normalizar(v.Carroceria))
            .ToDictionary(g => g.Key, g => clsTexto.Limpiar(g.First().Carroceria)!);

    private sealed record CatalogoCiudades(IReadOnlyList<clsMunicipio> Todos, ILookup<string, clsMunicipio> PorClave, Dictionary<string, clsMunicipio> PorCodigoDane);

    private async Task<CatalogoCiudades> CatalogoDeCiudadesAsync(CancellationToken ct)
    {
        var colombia = (await municipios.ListarAsync(ct)).Where(m => m.Pais == "CO").ToList();
        return new(
            colombia,
            colombia.ToLookup(m => clsSimilitudDeTexto.Clave(m.Municipio)),
            colombia.GroupBy(m => m.CodigoDane.Trim()).ToDictionary(g => g.Key, g => g.First()));
    }

    // ---------------------------------------------------------------- reglas exactas

    private static (Resolucion, IReadOnlyList<clsPromptNormalizacion.Opcion>?, IReadOnlyList<clsMunicipio>?) ReglasDeVehiculo(
        string normalizado, Dictionary<(string Campo, string Termino), string> valoresDelCliente,
        IReadOnlyDictionary<string, string> aliasVehiculo, IReadOnlyList<clsTipoVehiculoDto> catalogo)
    {
        string? DescripcionDe(string codigo) => catalogo.FirstOrDefault(t => t.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase))?.Descripcion;

        if (valoresDelCliente.TryGetValue((CampoVehiculo, normalizado), out var delCliente))
            return (new(Resuelto, delCliente, DescripcionDe(delCliente), null, "diccionario", 1, null), null, null);
        if (aliasVehiculo.TryGetValue(normalizado, out var delAlias))
            return (new(Resuelto, delAlias, DescripcionDe(delAlias), null, "alias", 1, null), null, null);

        var oficial = catalogo.FirstOrDefault(t =>
            clsResolucionDeCatalogos.Normalizar(t.Codigo) == normalizado || clsResolucionDeCatalogos.Normalizar(t.Descripcion) == normalizado);
        return oficial is not null
            ? (new(Resuelto, oficial.Codigo, oficial.Descripcion, null, "catalogo", 1, null), null, null)
            : (Resolucion.Sin(), null, null);
    }

    private static (Resolucion, IReadOnlyList<clsPromptNormalizacion.Opcion>?, IReadOnlyList<clsMunicipio>?) ReglasDeCarroceria(
        string normalizado, Dictionary<(string Campo, string Termino), string> valoresDelCliente, Dictionary<string, string> catalogo)
    {
        if (valoresDelCliente.TryGetValue((CampoCarroceria, normalizado), out var delCliente))
            return (new(Resuelto, delCliente, null, null, "diccionario", 1, null), null, null);
        return catalogo.TryGetValue(normalizado, out var oficial)
            ? (new(Resuelto, oficial, null, null, "catalogo", 1, null), null, null)
            : (Resolucion.Sin(), null, null);
    }

    private static readonly Regex SeparadoresDeDepartamento = new(@"[,()/\-]", RegexOptions.Compiled);
    private static readonly Regex CodigoDane = new(@"^\d{5}$|^\d{8}$", RegexOptions.Compiled);

    private static clsCandidatoCiudadDto ACandidato(clsMunicipio m) => new(m.Id, m.CodigoDane, m.Municipio, m.Departamento);

    private static Resolucion DeMunicipio(clsMunicipio m, string origen) =>
        new(Resuelto, m.CodigoDane, $"{m.Municipio}, {m.Departamento}", m.Id, origen, 1, null);

    private static (Resolucion, IReadOnlyList<clsPromptNormalizacion.Opcion>?, IReadOnlyList<clsMunicipio>?) ReglasDeCiudad(
        string texto, CatalogoCiudades catalogo)
    {
        // Un código DANE (el de 5 dígitos es el del municipio; el del catálogo lleva «000» de centro poblado).
        var solo = texto.Replace(" ", string.Empty).Replace(".", string.Empty);
        if (CodigoDane.IsMatch(solo))
            return catalogo.PorCodigoDane.TryGetValue(solo.Length == 5 ? solo + "000" : solo, out var porCodigo)
                ? (DeMunicipio(porCodigo, "dane"), null, null)
                : (Resolucion.Sin("Ese código DANE no está en el catálogo de municipios de Colombia."), null, null);

        // El nombre completo y, si trae departamento («Santa Rosa, Cauca»), el nombre antes del separador.
        var partes = SeparadoresDeDepartamento.Split(texto).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        var coincidencias = catalogo.PorClave[clsSimilitudDeTexto.Clave(texto)].DistinctBy(m => m.Id).ToList();
        var nombre = texto;
        if (coincidencias.Count == 0 && partes.Count > 1)
        {
            nombre = partes[0];
            coincidencias = catalogo.PorClave[clsSimilitudDeTexto.Clave(nombre)].DistinctBy(m => m.Id).ToList();
            if (coincidencias.Count > 1)
            {
                var departamento = partes.Skip(1).Select(clsSimilitudDeTexto.Clave).ToHashSet();
                var delDepartamento = coincidencias.Where(m => departamento.Contains(clsSimilitudDeTexto.Clave(m.Departamento))).ToList();
                if (delDepartamento.Count > 0) coincidencias = delDepartamento;
            }
        }

        if (coincidencias.Count == 1) return (DeMunicipio(coincidencias[0], "catalogo"), null, null);
        if (coincidencias.Count > 1)
            return (new(Ambiguo, null, null, null, "ninguno", 0, "Hay varios municipios con ese nombre; indica el departamento.",
                coincidencias.Take(MaxCandidatosPorCiudad).Select(ACandidato).ToList()), null, null);

        // Sin coincidencia exacta: los municipios más parecidos, para que el modelo escoja entre ellos.
        var candidatos = clsSimilitudDeTexto.MejoresCandidatos(nombre, catalogo.Todos, m => m.Municipio, MaxCandidatosPorCiudad, MinPuntajeCandidato);
        return (Resolucion.Sin(), null, candidatos);
    }

    // ---------------------------------------------------------------- la IA

    private clsPeticionIa Peticion(
        IReadOnlyList<Pendiente> pendientes, IReadOnlyList<clsTipoVehiculoDto> catalogoVehiculos, Dictionary<string, string> carrocerias)
    {
        var valores = pendientes.Select(p => new clsPromptNormalizacion.Valor(p.Indice, p.Campo, p.Texto, p.Candidatos)).ToList();
        var opcionesVehiculo = pendientes.Any(p => p.Campo == CampoVehiculo)
            ? catalogoVehiculos.Take(MaxOpcionesDeCatalogo).Select(t => new clsPromptNormalizacion.Opcion(t.Codigo, t.Descripcion)).ToList()
            : [];
        var opcionesCarroceria = pendientes.Any(p => p.Campo == CampoCarroceria)
            ? carrocerias.Values.Take(MaxOpcionesDeCatalogo).Select(c => new clsPromptNormalizacion.Opcion(c, c)).ToList()
            : [];

        return new clsPeticionIa(
            clsFuncionIa.ImportacionExcel,
            clsPromptNormalizacion.Instrucciones(),
            [new clsMensajeIa(clsMensajeIa.RolUsuario, clsPromptNormalizacion.Mensaje(valores, opcionesVehiculo, opcionesCarroceria))],
            clsPromptNormalizacion.EsquemaDeSalida(),
            UsuarioId: usuarioActual.Id);
    }

    /// <summary>Lo que el modelo eligió por índice, sin validar contra las listas: solo índices que se pidieron; el primero que llega es el que vale.</summary>
    internal static Dictionary<int, (string Clave, double Confianza, string? Nota)> InterpretarRespuesta(string? json, int totalPendientes)
    {
        var resultado = new Dictionary<int, (string, double, string?)>();
        if (string.IsNullOrWhiteSpace(json)) return resultado;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("resultados", out var lista) || lista.ValueKind != JsonValueKind.Array) return resultado;

        foreach (var r in lista.EnumerateArray())
        {
            if (!r.TryGetProperty("indice", out var iEl) || !iEl.TryGetInt32(out var indice) || indice < 0 || indice >= totalPendientes) continue;
            if (resultado.ContainsKey(indice)) continue;
            if (!r.TryGetProperty("clave", out var cEl) || cEl.ValueKind != JsonValueKind.String) continue;

            var confianza = r.TryGetProperty("confianza", out var fEl) && fEl.TryGetDouble(out var f) ? Math.Round(Math.Clamp(f, 0, 1), 2) : 0;
            var nota = r.TryGetProperty("nota", out var nEl) && nEl.ValueKind == JsonValueKind.String ? clsTexto.Limpiar(nEl.GetString()) : null;
            resultado[indice] = (cEl.GetString()!.Trim(), confianza, string.IsNullOrEmpty(nota) ? null : (nota.Length <= 300 ? nota : nota[..300]));
        }
        return resultado;
    }

    /// <summary>La regla que decide si lo que eligió el modelo se acepta: la clave debe estar en la lista que se le dio para ese texto y la confianza llegar al mínimo.</summary>
    private static Resolucion Validar(
        Pendiente p, (string Clave, double Confianza, string? Nota) e,
        IReadOnlyList<clsTipoVehiculoDto> catalogoVehiculos, Dictionary<string, string> carrocerias)
    {
        if (e.Clave.Equals(clsPromptNormalizacion.Ninguna, StringComparison.OrdinalIgnoreCase)) return Resolucion.Sin(e.Nota);

        Resolucion? aceptada = p.Campo switch
        {
            CampoVehiculo => catalogoVehiculos.FirstOrDefault(t => t.Codigo.Equals(e.Clave, StringComparison.OrdinalIgnoreCase)) is { } t
                ? new(Resuelto, t.Codigo, t.Descripcion, null, "ia", e.Confianza, e.Nota) : null,
            CampoCarroceria => carrocerias.Values.FirstOrDefault(c => c.Equals(e.Clave, StringComparison.OrdinalIgnoreCase)) is { } c
                ? new(Resuelto, c, null, null, "ia", e.Confianza, e.Nota) : null,
            _ => int.TryParse(e.Clave, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && p.Municipios!.FirstOrDefault(m => m.Id == id) is { } m
                ? new(Resuelto, m.CodigoDane, $"{m.Municipio}, {m.Departamento}", m.Id, "ia", e.Confianza, e.Nota) : null,
        };

        if (aceptada is null) return Resolucion.Sin("La IA propuso un valor que no está en el catálogo; se descartó.");
        if (e.Confianza < MinConfianzaIa) return Resolucion.Sin($"La IA no está segura (confianza {e.Confianza:0.##}); revisa a mano.");
        return aceptada;
    }
}
