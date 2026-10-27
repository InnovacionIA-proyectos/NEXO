using System.Text.Json;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.CasosDeUso.Asistente.Herramientas;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Nexo.Application.CasosDeUso.Asistente;

/// <summary>Las herramientas de lectura que el usuario actual puede usar.</summary>
public sealed record clsListarHerramientasAsistenteQuery : IQuery<IReadOnlyList<clsDefinicionHerramientaDto>>;

/// <summary>Ejecuta una herramienta por su nombre con la entrada que envió el modelo.</summary>
public sealed record clsEjecutarHerramientaAsistenteQuery(string Nombre, JsonElement Entrada) : IQuery<clsResultadoHerramienta>;

/// <summary>
/// Punto único por el que el asistente llega a las herramientas. El rol del usuario autenticado decide qué ve: una herramienta que
/// su rol no puede usar no se le ofrece al modelo y, si el modelo la pide igual, se rechaza con 403 antes de ejecutar nada.
/// </summary>
public sealed class clsHerramientasDelAsistente(IEnumerable<IHerramientaAsistente> herramientas, ICurrentUserPort usuario)
{
    private readonly Dictionary<string, IHerramientaAsistente> _porNombre = herramientas.ToDictionary(h => h.Nombre, StringComparer.Ordinal);

    public IReadOnlyList<clsDefinicionHerramientaDto> Disponibles() =>
        _porNombre.Values
            .Where(Permitida)
            .OrderBy(h => h.Nombre, StringComparer.Ordinal)
            .Select(h => new clsDefinicionHerramientaDto(h.Nombre, h.Descripcion, JsonDocument.Parse(h.EsquemaEntradaJson).RootElement.Clone()))
            .ToList();

    public Task<clsResultadoHerramienta> EjecutarAsync(string nombre, JsonElement entrada, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nombre) || !_porNombre.TryGetValue(nombre.Trim(), out var herramienta))
            throw new clsValidationFailedException("HERRAMIENTA_DESCONOCIDA", "La herramienta no existe.");

        if (!Permitida(herramienta))
            throw new clsForbiddenException("HERRAMIENTA_NO_PERMITIDA", "Su rol no puede usar esta herramienta.");

        return herramienta.EjecutarAsync(entrada, ct);
    }

    private bool Permitida(IHerramientaAsistente herramienta) =>
        usuario.Rol is RolUsuario rol && (herramienta.Roles.Count == 0 || herramienta.Roles.Contains(rol));
}

public sealed class clsListarHerramientasAsistenteQueryHandler(clsHerramientasDelAsistente catalogo)
    : IQueryHandler<clsListarHerramientasAsistenteQuery, IReadOnlyList<clsDefinicionHerramientaDto>>
{
    public Task<IReadOnlyList<clsDefinicionHerramientaDto>> Handle(clsListarHerramientasAsistenteQuery query, CancellationToken ct = default) =>
        Task.FromResult(catalogo.Disponibles());
}

public sealed class clsEjecutarHerramientaAsistenteQueryHandler(clsHerramientasDelAsistente catalogo)
    : IQueryHandler<clsEjecutarHerramientaAsistenteQuery, clsResultadoHerramienta>
{
    public Task<clsResultadoHerramienta> Handle(clsEjecutarHerramientaAsistenteQuery query, CancellationToken ct = default) =>
        catalogo.EjecutarAsync(query.Nombre, query.Entrada, ct);
}

public static class clsDependencyInjection
{
    public static IServiceCollection AddAsistenteUseCases(this IServiceCollection services)
    {
        services.AddScoped<IHerramientaAsistente, clsConsultarPisoSicetacHerramienta>();
        services.AddScoped<IHerramientaAsistente, clsConsultarTarifaPlazaSapHerramienta>();
        services.AddScoped<IHerramientaAsistente, clsConsultarTarifaPlazaNexoHerramienta>();
        services.AddScoped<IHerramientaAsistente, clsConsultarHistoricoSapHerramienta>();
        services.AddScoped<IHerramientaAsistente, clsConsultarAprovisionamientoHerramienta>();
        services.AddScoped<IHerramientaAsistente, clsConsultarPropuestaLineaHerramienta>();
        services.AddScoped<IHerramientaAsistente, clsConsultarServiciosConexosHerramienta>();
        services.AddScoped<IHerramientaAsistente, clsBuscarCiudadesDaneHerramienta>();
        services.AddScoped<clsHerramientasDelAsistente>();
        services.AddScoped<IQueryHandler<clsListarHerramientasAsistenteQuery, IReadOnlyList<clsDefinicionHerramientaDto>>, clsListarHerramientasAsistenteQueryHandler>();
        services.AddScoped<IQueryHandler<clsEjecutarHerramientaAsistenteQuery, clsResultadoHerramienta>, clsEjecutarHerramientaAsistenteQueryHandler>();
        services.TryAddSingleton<clsLimitesAsistente>();
        services.AddScoped<ICommandHandler<clsConversarAsistenteCommand, IAsyncEnumerable<clsEventoAsistente>>, clsConversarAsistenteCommandHandler>();
        services.AddScoped<IQueryHandler<clsListarConversacionesAsistenteQuery, IReadOnlyList<clsConversacionResumenDto>>, clsListarConversacionesAsistenteQueryHandler>();
        services.AddScoped<IQueryHandler<clsObtenerConversacionAsistenteQuery, clsConversacionDetalleDto>, clsObtenerConversacionAsistenteQueryHandler>();
        return services;
    }
}
