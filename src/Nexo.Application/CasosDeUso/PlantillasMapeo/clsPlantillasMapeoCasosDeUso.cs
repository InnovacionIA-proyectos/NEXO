using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Application.CasosDeUso.MapeoExcel;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
using Nexo.Application.CasosDeUso.PlantillasMapeo.DTOs;
using Nexo.Application.CasosDeUso.PlantillasMapeo.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.PlantillasMapeo;

/// <summary>Reglas compartidas por los casos de uso de las plantillas de mapeo.</summary>
internal static class clsPlantillasMapeoReglas
{
    public const int MaxEncabezados = 100;
    public const int MaxCaracteresEncabezado = 200;
    public const int MaxNombre = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string SerializarEncabezados(IReadOnlyList<string> encabezados) => JsonSerializer.Serialize(encabezados, Json);

    /// <summary>
    /// La forma con la que se guardan las columnas: <c>{ "version": 1, "columnas": [...] }</c>. La versión permite cambiar esa forma más adelante
    /// sin que las plantillas ya guardadas se lean mal: una versión que este código no conoce se trata como plantilla vieja (se vuelve a sugerir).
    /// </summary>
    public const int VersionDeColumnas = 1;

    private sealed record ColumnasGuardadas(int Version, List<clsColumnaPlantillaDto>? Columnas);

    public static string SerializarColumnas(IReadOnlyList<clsColumnaPlantillaDto> columnas) =>
        JsonSerializer.Serialize(new ColumnasGuardadas(VersionDeColumnas, [.. columnas]), Json);

    /// <summary>Los encabezados guardados, o null si el JSON no se puede leer.</summary>
    public static IReadOnlyList<string>? LeerEncabezados(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Las columnas guardadas, o null si el JSON no se puede leer, no es de la versión que se conoce o no trae las columnas.</summary>
    public static IReadOnlyList<clsColumnaPlantillaDto>? LeerColumnas(string json)
    {
        try
        {
            var guardadas = JsonSerializer.Deserialize<ColumnasGuardadas>(json, Json);
            return guardadas is { Version: VersionDeColumnas, Columnas: { } columnas } && columnas.All(c => c is not null) ? columnas : null;
        }
        catch (JsonException) { return null; }
    }

    public static clsPlantillaMapeoDto ADto(clsPlantillaMapeoCliente p) => new(
        p.Id, p.ClienteId, p.Cliente?.Nombre, p.Nombre, LeerEncabezados(p.Encabezados) ?? [], LeerColumnas(p.Columnas) ?? [], p.CreatedAt, p.UpdatedAt);
}

// ---------------------------------------------------------------- guardar

/// <summary>
/// Guarda el mapeo que el usuario confirmó como la plantilla de un cliente. Es un «guardar o reemplazar»: la plantilla se identifica por los
/// encabezados (su firma), así que guardar otra vez con los mismos encabezados actualiza la que ya había y no crea una segunda. El mapeo se
/// valida igual que al aplicarlo (campos y conversiones de las listas, sin campos repetidos y con origen y destino): una plantilla que
/// luego no se pudiera aplicar no sirve. El cliente se indica con <paramref name="ClienteId"/> o con <paramref name="ClienteNit"/> (si vienen los
/// dos manda el id): con el NIT se usa el cliente local y, si aún no existe, se crea con los datos del DW. 400 si el cliente no existe, el NIT
/// no es de ningún cliente ni prospecto, los encabezados no son válidos o el mapeo no es válido.
/// </summary>
public sealed record clsGuardarPlantillaMapeoCommand(
    Guid? ClienteId, string? Nombre, IReadOnlyList<string?>? Encabezados, IReadOnlyList<clsColumnaAAplicar>? Columnas, string? ClienteNit = null)
    : ICommand<clsPlantillaGuardadaDto>;

public sealed class clsGuardarPlantillaMapeoCommandHandler(
    IPlantillasMapeoRepository repositorio, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow, TimeProvider reloj,
    clsClienteDeImportacion clienteDeImportacion)
    : ICommandHandler<clsGuardarPlantillaMapeoCommand, clsPlantillaGuardadaDto>
{
    public Task<clsPlantillaGuardadaDto> Handle(clsGuardarPlantillaMapeoCommand command, CancellationToken ct = default) =>
        Guardar(command, reintento: false, ct);

    private async Task<clsPlantillaGuardadaDto> Guardar(clsGuardarPlantillaMapeoCommand command, bool reintento, CancellationToken ct)
    {
        Guid clienteId;
        if (command.ClienteId is { } id)
        {
            if (!await repositorio.ClienteExisteAsync(id, ct))
                throw new clsValidationFailedException("CLIENTE_NO_ENCONTRADO", "El cliente no existe.");
            clienteId = id;
        }
        else if (!string.IsNullOrWhiteSpace(command.ClienteNit))
            clienteId = await clienteDeImportacion.ObtenerOCrearAsync(command.ClienteNit, ct);
        else
            throw new clsValidationFailedException("CLIENTE_REQUERIDO", "Indica el cliente dueño de la plantilla (clienteId o clienteNit).");

        var nombre = clsTexto.Limpiar(command.Nombre);
        if (nombre is { Length: > clsPlantillasMapeoReglas.MaxNombre })
            throw new clsValidationFailedException("NOMBRE_DEMASIADO_LARGO", $"El nombre admite máximo {clsPlantillasMapeoReglas.MaxNombre} caracteres.");

        var encabezados = ValidarEncabezados(command.Encabezados);

        clsAplicarMapeoExcelQueryHandler.ValidarColumnas(command.Columnas);
        var columnas = command.Columnas!
            .Select(c => new clsColumnaPlantillaDto(
                c.Indice,
                clsTerminoCliente.Canonico(clsTerminoCliente.CamposDestino, c.Campo)!,
                string.IsNullOrWhiteSpace(c.Transformacion) ? clsTransformacionesMapeo.Ninguna : clsTerminoCliente.Canonico(clsTransformacionesMapeo.Todas, c.Transformacion)!))
            .OrderBy(c => c.Indice)
            .ToList();
        if (columnas.Any(c => c.Indice >= encabezados.Count))
            throw new clsValidationFailedException("COLUMNA_FUERA_DE_RANGO", "El mapeo tiene una columna que no existe en los encabezados.");

        var firma = clsFirmaDeEncabezados.Calcular(encabezados);
        var existente = await repositorio.ObtenerParaActualizarAsync(clienteId, firma, ct);
        var creada = existente is null;

        var plantilla = existente ?? new clsPlantillaMapeoCliente { ClienteId = clienteId, FirmaEncabezados = firma };
        plantilla.Nombre = nombre;
        plantilla.Encabezados = clsPlantillasMapeoReglas.SerializarEncabezados(encabezados);
        plantilla.Columnas = clsPlantillasMapeoReglas.SerializarColumnas(columnas);
        if (creada) repositorio.Agregar(plantilla);
        else plantilla.UpdatedAt = reloj.GetUtcNow();

        auditoria.Registrar(usuarioActual.Id, "plantilla_mapeo_cliente", plantilla.Id, creada ? "crear_plantilla" : "reemplazar_plantilla",
            JsonSerializer.Serialize(new { clienteId, nombre, columnas = columnas.Count, encabezados = encabezados.Count }));
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (creada && !reintento && repositorio.EsChoqueDeFirma(ex))
        {
            // Dos confirmaciones del mismo formato a la vez (un doble clic): la otra insertó entre mi búsqueda y mi inserción. No es un error del
            // usuario; se vuelve a empezar, y ahora la plantilla ya existe, así que esta vez se reemplaza.
            repositorio.DescartarCambiosPendientes();
            return await Guardar(command, reintento: true, ct);
        }

        var guardada = await repositorio.BuscarPorFirmaAsync(clienteId, firma, ct) ?? plantilla;
        return new clsPlantillaGuardadaDto(clsPlantillasMapeoReglas.ADto(guardada), creada);
    }

    private static List<string> ValidarEncabezados(IReadOnlyList<string?>? entrada)
    {
        if (entrada is null || entrada.Count == 0)
            throw new clsValidationFailedException("ENCABEZADOS_REQUERIDOS", "Envía los encabezados del archivo.");
        if (entrada.Count > clsPlantillasMapeoReglas.MaxEncabezados)
            throw new clsValidationFailedException("DEMASIADAS_COLUMNAS", $"El archivo trae más de {clsPlantillasMapeoReglas.MaxEncabezados} columnas.");

        var limpios = new List<string>(entrada.Count);
        foreach (var e in entrada)
        {
            var texto = clsTexto.Limpiar(e) ?? string.Empty;
            if (texto.Length > clsPlantillasMapeoReglas.MaxCaracteresEncabezado)
                throw new clsValidationFailedException("ENCABEZADO_DEMASIADO_LARGO", $"Un encabezado admite máximo {clsPlantillasMapeoReglas.MaxCaracteresEncabezado} caracteres.");
            limpios.Add(texto);
        }
        // Un archivo sin ningún encabezado no tiene nada con qué reconocerse: todos los archivos "vacíos" compartirían la misma plantilla.
        if (clsFirmaDeEncabezados.Cantidad(limpios) == 0)
            throw new clsValidationFailedException("ENCABEZADOS_REQUERIDOS", "Envía al menos un encabezado con texto.");
        return limpios;
    }
}

// ---------------------------------------------------------------- listar

/// <summary>Las plantillas guardadas: las de un cliente, o las de todos si no se indica.</summary>
public sealed record clsListarPlantillasMapeoQuery(Guid? ClienteId) : IQuery<IReadOnlyList<clsPlantillaMapeoDto>>;

public sealed class clsListarPlantillasMapeoQueryHandler(IPlantillasMapeoRepository repositorio)
    : IQueryHandler<clsListarPlantillasMapeoQuery, IReadOnlyList<clsPlantillaMapeoDto>>
{
    public async Task<IReadOnlyList<clsPlantillaMapeoDto>> Handle(clsListarPlantillasMapeoQuery query, CancellationToken ct = default) =>
        (await repositorio.ListarAsync(query.ClienteId, ct)).Select(clsPlantillasMapeoReglas.ADto).ToList();
}

// ---------------------------------------------------------------- eliminar

/// <summary>Borra una plantilla: el siguiente archivo de ese cliente con esos encabezados se vuelve a analizar. 404 si no existe.</summary>
public sealed record clsEliminarPlantillaMapeoCommand(Guid Id) : ICommand<Unit>;

public sealed class clsEliminarPlantillaMapeoCommandHandler(
    IPlantillasMapeoRepository repositorio, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsEliminarPlantillaMapeoCommand, Unit>
{
    public async Task<Unit> Handle(clsEliminarPlantillaMapeoCommand command, CancellationToken ct = default)
    {
        var plantilla = await repositorio.ObtenerAsync(command.Id, ct)
            ?? throw new clsNotFoundException("PLANTILLA_NO_ENCONTRADA", "La plantilla no existe.");

        repositorio.Eliminar(plantilla);
        auditoria.Registrar(usuarioActual.Id, "plantilla_mapeo_cliente", plantilla.Id, "eliminar_plantilla",
            JsonSerializer.Serialize(new { clienteId = plantilla.ClienteId, nombre = plantilla.Nombre }));
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public static class clsPlantillasMapeoDependencyInjection
{
    public static IServiceCollection AddPlantillasMapeoUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsGuardarPlantillaMapeoCommand, clsPlantillaGuardadaDto>, clsGuardarPlantillaMapeoCommandHandler>()
        .AddScoped<IQueryHandler<clsListarPlantillasMapeoQuery, IReadOnlyList<clsPlantillaMapeoDto>>, clsListarPlantillasMapeoQueryHandler>()
        .AddScoped<ICommandHandler<clsEliminarPlantillaMapeoCommand, Unit>, clsEliminarPlantillaMapeoCommandHandler>()
        .AddScoped<clsClienteDeImportacion>();
}
