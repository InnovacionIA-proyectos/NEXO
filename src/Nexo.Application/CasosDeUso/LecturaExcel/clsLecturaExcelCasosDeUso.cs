using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Application.CasosDeUso.LecturaExcel.DTOs;
using Nexo.Application.CasosDeUso.LecturaExcel.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>Reglas compartidas por los casos de uso de las recetas de lectura.</summary>
public static class clsRecetasLecturaReglas
{
    public const int MaxRecetasPorCliente = 20;
    public const int MaxNombre = 100;
    public const int MaxCaracteresReceta = 60_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> LeerHuella(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, Json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string SerializarHuella(IReadOnlyList<string> tokens) => JsonSerializer.Serialize(tokens, Json);

    /// <summary>La receta guardada que mejor se parece al archivo (comparando cada una con su propia parte alta) y su parecido, de 0 a 1.</summary>
    public static (clsRecetaLecturaCliente? Receta, clsRecetaLectura? Lectura, double Similitud, IReadOnlyList<string> TokensDelArchivo) MasParecida(
        IReadOnlyList<clsRecetaLecturaCliente> guardadas, IReadOnlyList<clsHojaCruda> hojas)
    {
        clsRecetaLecturaCliente? mejor = null;
        clsRecetaLectura? mejorLectura = null;
        var mejorSimilitud = 0.0;
        IReadOnlyList<string> tokens = [];
        foreach (var g in guardadas)
        {
            var lectura = clsRecetaLectura.Deserializar(g.Receta);
            if (lectura is null || !clsValidadorDeReceta.Validar(lectura).Valida) continue;
            var delArchivo = clsHuellaDeEstructura.Tokens(hojas, lectura);
            var similitud = clsHuellaDeEstructura.Similitud(LeerHuella(g.Huella), delArchivo);
            if (mejor is null || similitud > mejorSimilitud)
                (mejor, mejorLectura, mejorSimilitud, tokens) = (g, lectura, similitud, delArchivo);
        }
        return (mejor, mejorLectura, mejorSimilitud, tokens);
    }

    public static clsRecetaGuardadaDto ADto(clsRecetaLecturaCliente r) => new(
        r.Id, r.ClienteId, r.Cliente?.Nombre, r.Nombre,
        clsRecetaLectura.Deserializar(r.Receta) ?? new clsRecetaLectura(clsRecetaLectura.VersionActual, []),
        r.Instrucciones, r.Usos, r.UltimoUsoAt, r.CreatedAt, r.UpdatedAt);
}

// ---------------------------------------------------------------- leer la estructura

/// <summary>
/// Lee un Excel y devuelve sus hojas como tablas normales, con la receta que se usó. Elige la receta así:
/// 1) con una <paramref name="RecetaJson"/> y sin instrucciones, la ejecuta tal cual (la persona la ajustó a mano);
/// 2) sin receta ni instrucciones y con un formato guardado del cliente que se parece (≥ 85 %), usa ese formato SIN IA;
/// 3) en cualquier otro caso pide una receta a la IA, tomando de base la receta enviada o la guardada más parecida (≥ 50 %) y las instrucciones de la persona;
/// 4) si la IA no está disponible o responde algo inválido, usa la base que haya o una lectura simple, y lo dice. No guarda nada: guardar es una confirmación aparte.
/// </summary>
public sealed record clsLeerEstructuraCommand(
    Stream Archivo, string NombreArchivo, Guid? ClienteId, string? ClienteNit, string? Instrucciones, string? RecetaJson)
    : ICommand<clsLecturaEstructuraDto>;

public sealed class clsLeerEstructuraCommandHandler(
    ILectorDeHojasPort lector, IRecetasLecturaRepository recetas, clsClienteDeImportacion clienteDeImportacion,
    IAsistenteIaPort ia, ICurrentUserPort usuarioActual, IUnitOfWork uow, TimeProvider reloj)
    : ICommandHandler<clsLeerEstructuraCommand, clsLecturaEstructuraDto>
{
    private sealed record Propuesta(clsRecetaLectura? Receta, string? Resumen, IReadOnlyList<string> Dudas, string? Motivo, IReadOnlyList<string> Avisos);

    public async Task<clsLecturaEstructuraDto> Handle(clsLeerEstructuraCommand command, CancellationToken ct = default)
    {
        var instrucciones = clsTexto.Limpiar(command.Instrucciones);
        if (instrucciones is { Length: > clsPromptReceta.MaxCaracteresInstrucciones })
            throw new clsValidationFailedException("INSTRUCCIONES_DEMASIADO_LARGAS", $"Las instrucciones admiten máximo {clsPromptReceta.MaxCaracteresInstrucciones} caracteres.");

        clsRecetaLectura? enviada = null;
        if (!string.IsNullOrWhiteSpace(command.RecetaJson))
        {
            if (command.RecetaJson.Length > clsRecetasLecturaReglas.MaxCaracteresReceta)
                throw new clsValidationFailedException("RECETA_INVALIDA", "La receta es demasiado grande.");
            enviada = clsRecetaLectura.Deserializar(command.RecetaJson)
                ?? throw new clsValidationFailedException("RECETA_INVALIDA", "La receta no es un JSON válido.");
        }

        var hojas = await lector.LeerAsync(command.Archivo, ct);
        var clienteId = command.ClienteId ?? await clienteDeImportacion.BuscarLocalAsync(command.ClienteNit, ct);
        var guardadas = clienteId is { } id ? await recetas.ListarAsync(id, ct) : [];
        var (guardada, guardadaLectura, similitud, _) = clsRecetasLecturaReglas.MasParecida(guardadas, hojas);

        var avisos = new List<string>();
        var dudas = (IReadOnlyList<string>)[];
        string? resumen = null;
        string? motivoSinIa = null;
        var iaUsada = false;
        clsCambiosDeFormatoDto? cambios = null;
        clsRecetaLectura receta;
        string origen;

        if (enviada is not null && instrucciones is null)
        {
            ExigirValida(enviada, hojas);
            (receta, origen, motivoSinIa) = (enviada, "manual", "SIN_INSTRUCCION");
        }
        else if (enviada is null && instrucciones is null && guardada is not null && guardadaLectura is not null && similitud >= clsHuellaDeEstructura.UmbralIgual)
        {
            (receta, origen, motivoSinIa) = (guardadaLectura, "guardada", "RECETA_GUARDADA");
            guardada = await RegistrarUsoAsync(guardada, ct);
        }
        else
        {
            var @base = enviada ?? (guardadaLectura is not null && similitud >= clsHuellaDeEstructura.UmbralParecido ? guardadaLectura : null);
            if (enviada is null && @base is not null && guardada is not null)
            {
                var (faltan, nuevos) = clsHuellaDeEstructura.Diferencias(clsRecetasLecturaReglas.LeerHuella(guardada.Huella), clsHuellaDeEstructura.Tokens(hojas, @base));
                cambios = new clsCambiosDeFormatoDto(faltan, nuevos);
            }

            var propuesta = await ProponerAsync(hojas, instrucciones, @base, enviada is null ? guardada?.Instrucciones : null, cambios, ct);
            avisos.AddRange(propuesta.Avisos);
            if (propuesta.Receta is not null)
            {
                (receta, origen, iaUsada, resumen, dudas) = (propuesta.Receta, "ia", true, propuesta.Resumen, propuesta.Dudas);
            }
            else
            {
                motivoSinIa = propuesta.Motivo;
                if (@base is not null && clsValidadorDeReceta.Validar(@base).Valida)
                {
                    receta = @base;
                    origen = enviada is not null ? "manual" : "guardada";
                    avisos.Add(instrucciones is not null
                        ? "No se pudo aplicar la instrucción con la IA: se muestra la lectura anterior. Ajusta la receta a mano o inténtalo de nuevo."
                        : "El formato cambió y la IA no está disponible: se usó el formato guardado más parecido. Revisa la vista previa.");
                }
                else
                {
                    receta = clsRecetaPorDefecto.Generar(hojas);
                    origen = "automatica";
                    avisos.Add("La IA no está disponible: se hizo una lectura simple (una tabla por hoja, con el primer encabezado que se encontró). Revísala y ajústala.");
                }
            }
        }

        var validacion = clsValidadorDeReceta.Validar(receta, hojas);
        avisos.AddRange(validacion.Avisos);
        var lectura = clsEjecutorDeReceta.Ejecutar(hojas, receta);
        avisos.AddRange(lectura.Avisos);
        if (lectura.Tablas.Count == 0) avisos.Add("La receta no dio ninguna tabla con este archivo.");

        var delArchivo = hojas.Select(h => new clsHojaDelArchivoDto(
            h.Nombre, h.Oculta, h.Filas, h.Columnas, clsEjecutorDeReceta.EntradaDe(receta, h) is { Usar: true })).ToList();

        return new clsLecturaEstructuraDto(
            command.NombreArchivo, origen, guardada is null ? null : Math.Round(similitud, 3), guardada?.Id, iaUsada, motivoSinIa, receta,
            resumen, dudas, delArchivo, lectura.Tablas, avisos.Distinct().ToList(), cambios);
    }

    private static void ExigirValida(clsRecetaLectura receta, IReadOnlyList<clsHojaCruda> hojas)
    {
        var v = clsValidadorDeReceta.Validar(receta, hojas);
        if (!v.Valida) throw new clsValidationFailedException("RECETA_INVALIDA", string.Join(" ", v.Errores.Take(3)));
    }

    private async Task<clsRecetaLecturaCliente> RegistrarUsoAsync(clsRecetaLecturaCliente vista, CancellationToken ct)
    {
        var entidad = await recetas.ObtenerAsync(vista.Id, ct);
        if (entidad is null) return vista;
        entidad.Usos++;
        entidad.UltimoUsoAt = reloj.GetUtcNow();
        await uow.SaveChangesAsync(ct);
        return entidad;
    }

    private async Task<Propuesta> ProponerAsync(
        IReadOnlyList<clsHojaCruda> hojas, string? instrucciones, clsRecetaLectura? recetaBase, string? instruccionesGuardadas,
        clsCambiosDeFormatoDto? cambios, CancellationToken ct)
    {
        if (!ia.Habilitada(clsFuncionIa.ImportacionExcel)) return new(null, null, [], "IA_NO_DISPONIBLE", []);

        var cambiosTexto = cambios is null ? null : (IReadOnlyList<string>)
            [.. cambios.Faltan.Select(t => $"ya no aparece «{t}»"), .. cambios.Nuevos.Select(t => $"nuevo «{t}»")];
        var mensajes = new List<clsMensajeIa>
        {
            new(clsMensajeIa.RolUsuario, clsPromptReceta.Mensaje(hojas, instrucciones, recetaBase, instruccionesGuardadas, cambiosTexto)),
        };

        var avisos = new List<string>();
        for (var intento = 0; intento < 2; intento++)
        {
            clsRespuestaIa respuesta;
            try
            {
                respuesta = await ia.CompletarAsync(new clsPeticionIa(
                    clsFuncionIa.ImportacionExcel, clsPromptReceta.Instrucciones(), mensajes, clsPromptReceta.EsquemaDeSalida(),
                    MaxTokensSalida: 8000, UsuarioId: usuarioActual.Id), ct);
            }
            catch (clsServiceUnavailableException ex)
            {
                return new(null, null, [], ex.Code, avisos);
            }

            var texto = respuesta.JsonEstructurado ?? respuesta.Texto;
            var interpretada = clsPromptReceta.Interpretar(texto);
            if (interpretada?.Receta is null) return new(null, null, [], "IA_RESPUESTA_INVALIDA", avisos);

            var validacion = clsValidadorDeReceta.Validar(interpretada.Value.Receta, hojas);
            if (validacion.Valida) return new(interpretada.Value.Receta, interpretada.Value.Resumen, interpretada.Value.Dudas, null, avisos);

            // Un solo intento de corrección: se le devuelven los errores concretos de lo que propuso.
            mensajes.Add(new clsMensajeIa(clsMensajeIa.RolAsistente, texto));
            mensajes.Add(new clsMensajeIa(clsMensajeIa.RolUsuario,
                "La receta tiene errores y no se puede ejecutar: " + string.Join(" ", validacion.Errores.Take(5)) + " Devuelve la receta corregida con el mismo formato."));
            avisos.Add("La primera propuesta de la IA tenía errores y se le pidió corregirla.");
        }
        return new(null, null, [], "IA_RESPUESTA_INVALIDA", avisos);
    }
}

// ---------------------------------------------------------------- guardar

/// <summary>
/// Guarda como formato del cliente la receta que la persona confirmó, con la huella del archivo con el que se confirmó. Antes de guardar la ejecuta contra ese archivo:
/// una receta que no da ninguna fila no sirve. Guardar un archivo con el mismo formato (≥ 85 %) de uno que el cliente ya tiene lo reemplaza; si no, agrega otro formato.
/// El cliente se indica con <paramref name="ClienteId"/> o con <paramref name="ClienteNit"/> (si no tiene cliente local se crea desde el DW, como en las plantillas de mapeo).
/// </summary>
public sealed record clsGuardarRecetaLecturaCommand(
    Stream Archivo, Guid? ClienteId, string? ClienteNit, string? Nombre, string? RecetaJson, string? Instrucciones)
    : ICommand<clsRecetaGuardadaResultadoDto>;

public sealed class clsGuardarRecetaLecturaCommandHandler(
    ILectorDeHojasPort lector, IRecetasLecturaRepository repositorio, clsClienteDeImportacion clienteDeImportacion,
    IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow, TimeProvider reloj)
    : ICommandHandler<clsGuardarRecetaLecturaCommand, clsRecetaGuardadaResultadoDto>
{
    public async Task<clsRecetaGuardadaResultadoDto> Handle(clsGuardarRecetaLecturaCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.RecetaJson)) throw new clsValidationFailedException("RECETA_REQUERIDA", "Envía la receta que quieres guardar.");
        if (command.RecetaJson.Length > clsRecetasLecturaReglas.MaxCaracteresReceta)
            throw new clsValidationFailedException("RECETA_INVALIDA", "La receta es demasiado grande.");
        var receta = clsRecetaLectura.Deserializar(command.RecetaJson) ?? throw new clsValidationFailedException("RECETA_INVALIDA", "La receta no es un JSON válido.");

        var nombre = clsTexto.Limpiar(command.Nombre);
        if (nombre is { Length: > clsRecetasLecturaReglas.MaxNombre })
            throw new clsValidationFailedException("NOMBRE_DEMASIADO_LARGO", $"El nombre admite máximo {clsRecetasLecturaReglas.MaxNombre} caracteres.");
        var instrucciones = clsTexto.Limpiar(command.Instrucciones);
        if (instrucciones is { Length: > clsPromptReceta.MaxCaracteresInstrucciones })
            throw new clsValidationFailedException("INSTRUCCIONES_DEMASIADO_LARGAS", $"Las instrucciones admiten máximo {clsPromptReceta.MaxCaracteresInstrucciones} caracteres.");

        var hojas = await lector.LeerAsync(command.Archivo, ct);
        var validacion = clsValidadorDeReceta.Validar(receta, hojas);
        if (!validacion.Valida) throw new clsValidationFailedException("RECETA_INVALIDA", string.Join(" ", validacion.Errores.Take(3)));
        if (!clsEjecutorDeReceta.Ejecutar(hojas, receta).Tablas.Any(t => t.TotalFilas > 0))
            throw new clsValidationFailedException("RECETA_SIN_FILAS", "La receta no da ninguna fila con este archivo; no se guarda.");

        Guid clienteId;
        if (command.ClienteId is { } id)
        {
            if (!await repositorio.ClienteExisteAsync(id, ct)) throw new clsValidationFailedException("CLIENTE_NO_ENCONTRADO", "El cliente no existe.");
            clienteId = id;
        }
        else if (!string.IsNullOrWhiteSpace(command.ClienteNit))
            clienteId = await clienteDeImportacion.ObtenerOCrearAsync(command.ClienteNit, ct);
        else
            throw new clsValidationFailedException("CLIENTE_REQUERIDO", "Indica el cliente dueño del formato (clienteId o clienteNit).");

        var existentes = await repositorio.ListarAsync(clienteId, ct);
        var (parecida, _, similitud, tokens) = clsRecetasLecturaReglas.MasParecida(existentes, hojas);
        var reemplaza = parecida is not null && similitud >= clsHuellaDeEstructura.UmbralIgual ? await repositorio.ObtenerAsync(parecida.Id, ct) : null;
        var huella = clsRecetasLecturaReglas.SerializarHuella(clsHuellaDeEstructura.Tokens(hojas, receta));

        var creada = reemplaza is null;
        var entidad = reemplaza ?? new clsRecetaLecturaCliente { ClienteId = clienteId, CreadoPor = usuarioActual.Id };
        entidad.Nombre = nombre ?? entidad.Nombre;
        entidad.Receta = receta.ASerializado();
        entidad.Huella = huella;
        entidad.Instrucciones = instrucciones ?? entidad.Instrucciones;
        if (creada)
        {
            // Más de 20 formatos por cliente no ayudan: se descarta el que hace más tiempo que no se usa.
            if (existentes.Count >= clsRecetasLecturaReglas.MaxRecetasPorCliente)
            {
                var masAntigua = existentes.OrderBy(r => r.UltimoUsoAt ?? r.UpdatedAt ?? r.CreatedAt).First();
                if (await repositorio.ObtenerAsync(masAntigua.Id, ct) is { } aBorrar) repositorio.Eliminar(aBorrar);
            }
            repositorio.Agregar(entidad);
        }
        else entidad.UpdatedAt = reloj.GetUtcNow();

        auditoria.Registrar(usuarioActual.Id, "receta_lectura_cliente", entidad.Id, creada ? "crear_receta" : "reemplazar_receta",
            JsonSerializer.Serialize(new { clienteId, nombre = entidad.Nombre, hojas = receta.Hojas.Count(h => h.Usar) }));
        await uow.SaveChangesAsync(ct);

        var guardada = (await repositorio.ListarAsync(clienteId, ct)).FirstOrDefault(r => r.Id == entidad.Id) ?? entidad;
        return new clsRecetaGuardadaResultadoDto(clsRecetasLecturaReglas.ADto(guardada), creada);
    }
}

// ---------------------------------------------------------------- listar y eliminar

/// <summary>Los formatos guardados: los de un cliente (por id o NIT) o, sin indicar ninguno, los de todos. Un NIT sin cliente local no tiene formatos: lista vacía.</summary>
public sealed record clsListarRecetasLecturaQuery(Guid? ClienteId, string? ClienteNit) : IQuery<IReadOnlyList<clsRecetaGuardadaDto>>;

public sealed class clsListarRecetasLecturaQueryHandler(IRecetasLecturaRepository repositorio, clsClienteDeImportacion clienteDeImportacion)
    : IQueryHandler<clsListarRecetasLecturaQuery, IReadOnlyList<clsRecetaGuardadaDto>>
{
    public async Task<IReadOnlyList<clsRecetaGuardadaDto>> Handle(clsListarRecetasLecturaQuery query, CancellationToken ct = default)
    {
        var clienteId = query.ClienteId;
        if (clienteId is null && !string.IsNullOrWhiteSpace(query.ClienteNit))
        {
            clienteId = await clienteDeImportacion.BuscarLocalAsync(query.ClienteNit, ct);
            if (clienteId is null) return [];
        }
        return (await repositorio.ListarAsync(clienteId, ct)).Select(clsRecetasLecturaReglas.ADto).ToList();
    }
}

/// <summary>Borra un formato guardado: el siguiente archivo de ese cliente vuelve a pasar por la IA. 404 si no existe.</summary>
public sealed record clsEliminarRecetaLecturaCommand(Guid Id) : ICommand<Unit>;

public sealed class clsEliminarRecetaLecturaCommandHandler(
    IRecetasLecturaRepository repositorio, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsEliminarRecetaLecturaCommand, Unit>
{
    public async Task<Unit> Handle(clsEliminarRecetaLecturaCommand command, CancellationToken ct = default)
    {
        var receta = await repositorio.ObtenerAsync(command.Id, ct)
            ?? throw new clsNotFoundException("RECETA_NO_ENCONTRADA", "El formato guardado no existe.");

        repositorio.Eliminar(receta);
        auditoria.Registrar(usuarioActual.Id, "receta_lectura_cliente", receta.Id, "eliminar_receta",
            JsonSerializer.Serialize(new { clienteId = receta.ClienteId, nombre = receta.Nombre }));
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public static class clsLecturaExcelDependencyInjection
{
    public static IServiceCollection AddLecturaExcelUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsLeerEstructuraCommand, clsLecturaEstructuraDto>, clsLeerEstructuraCommandHandler>()
        .AddScoped<ICommandHandler<clsGuardarRecetaLecturaCommand, clsRecetaGuardadaResultadoDto>, clsGuardarRecetaLecturaCommandHandler>()
        .AddScoped<IQueryHandler<clsListarRecetasLecturaQuery, IReadOnlyList<clsRecetaGuardadaDto>>, clsListarRecetasLecturaQueryHandler>()
        .AddScoped<ICommandHandler<clsEliminarRecetaLecturaCommand, Unit>, clsEliminarRecetaLecturaCommandHandler>();
}
