using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.CasosDeUso.Priorizacion.Interfaces;
using Nexo.Application.CasosDeUso.Priorizacion.Mappers;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Priorizacion;

/// <summary>Los parámetros vigentes del score. Lo que el Admin ve antes de recalibrar.</summary>
public sealed record ObtenerParametrosScoreQuery : IQuery<ParametrosScoreDto>;

public sealed class ObtenerParametrosScoreQueryHandler(IParametrosScorePort parametros)
    : IQueryHandler<ObtenerParametrosScoreQuery, ParametrosScoreDto>
{
    public async Task<ParametrosScoreDto> Handle(ObtenerParametrosScoreQuery query, CancellationToken ct = default) =>
        (await parametros.ParaEditarAsync(ct)).ADto();
}
