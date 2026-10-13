using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Cubicacion;

/// <summary>
/// POST /api/cubicacion — reemplaza el POST que <c>cubicar()</c>
/// (<c>captura-api.service.ts</c>) hacía directo contra Cubicador con la clave compartida.
/// S5-HU-18.1/#273: Command y no Query — el contrato nuevo (<c>POST /recomendaciones</c>)
/// persiste (append-only) del lado del Cubicador, a diferencia del viejo <c>POST
/// /api/cubicacion</c> que era cálculo puro. Ver <see cref="ICubicacionRapidaPort"/> para
/// el porqué de la excepción HTTP.
/// </summary>
public sealed record clsCrearRecomendacionCommand(clsCrearRecomendacionInput clsSolicitud) : ICommand<clsRecomendacionDto>;

public sealed class clsCrearRecomendacionCommandHandler(ICubicacionRapidaPort cubicacion)
    : ICommandHandler<clsCrearRecomendacionCommand, clsRecomendacionDto>
{
    public Task<clsRecomendacionDto> Handle(clsCrearRecomendacionCommand command, CancellationToken ct = default)
        => cubicacion.CrearAsync(command.clsSolicitud, ct);
}

/// <summary>
/// GET /api/cubicacion?idLineaSolicitud=&amp;origenSistema= — reconsulta de la última
/// corrida de una línea. A diferencia de la creación, esto SÍ es una query pura: no agrega
/// ninguna corrida nueva, solo lee la que ya existe.
/// </summary>
public sealed record clsConsultarRecomendacionQuery(string IdLineaSolicitud, string OrigenSistema) : IQuery<clsRecomendacionDto>;

public sealed class clsConsultarRecomendacionQueryHandler(ICubicacionRapidaPort cubicacion)
    : IQueryHandler<clsConsultarRecomendacionQuery, clsRecomendacionDto>
{
    public Task<clsRecomendacionDto> Handle(clsConsultarRecomendacionQuery query, CancellationToken ct = default)
        => cubicacion.ConsultarAsync(query.IdLineaSolicitud, query.OrigenSistema, ct);
}
