using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Cubicacion;

/// <summary>POST /api/cubicacion. Es Command porque <c>POST /recomendaciones</c> persiste (append-only) en el Cubicador; ver <see cref="ICubicacionRapidaPort"/>.</summary>
public sealed record clsCrearRecomendacionCommand(clsCrearRecomendacionInput clsSolicitud) : ICommand<clsRecomendacionDto>;

public sealed class clsCrearRecomendacionCommandHandler(ICubicacionRapidaPort cubicacion)
    : ICommandHandler<clsCrearRecomendacionCommand, clsRecomendacionDto>
{
    public Task<clsRecomendacionDto> Handle(clsCrearRecomendacionCommand command, CancellationToken ct = default)
        => cubicacion.CrearAsync(command.clsSolicitud, ct);
}

/// <summary>GET /api/cubicacion?idLineaSolicitud=&amp;origenSistema= — query pura: lee la última corrida de la línea sin crear otra.</summary>
public sealed record clsConsultarRecomendacionQuery(string IdLineaSolicitud, string OrigenSistema) : IQuery<clsRecomendacionDto>;

/// <summary>
/// GET /api/cubicacion/explicacion?idLineaSolicitud=&amp;origenSistema= — el texto del Cubicador que explica la recomendación de esa línea.
/// Opcional y sin errores: ver <see cref="ICubicadorExplicacionPort"/>.
/// </summary>
public sealed record clsObtenerExplicacionRecomendacionQuery(string IdLineaSolicitud, string OrigenSistema) : IQuery<clsExplicacionRecomendacionDto>;

public sealed class clsObtenerExplicacionRecomendacionQueryHandler(ICubicadorExplicacionPort explicacion)
    : IQueryHandler<clsObtenerExplicacionRecomendacionQuery, clsExplicacionRecomendacionDto>
{
    public async Task<clsExplicacionRecomendacionDto> Handle(clsObtenerExplicacionRecomendacionQuery query, CancellationToken ct = default)
        => new(await explicacion.ObtenerAsync(query.IdLineaSolicitud, query.OrigenSistema, ct));
}

public sealed class clsConsultarRecomendacionQueryHandler(ICubicacionRapidaPort cubicacion)
    : IQueryHandler<clsConsultarRecomendacionQuery, clsRecomendacionDto>
{
    public Task<clsRecomendacionDto> Handle(clsConsultarRecomendacionQuery query, CancellationToken ct = default)
        => cubicacion.ConsultarAsync(query.IdLineaSolicitud, query.OrigenSistema, ct);
}
