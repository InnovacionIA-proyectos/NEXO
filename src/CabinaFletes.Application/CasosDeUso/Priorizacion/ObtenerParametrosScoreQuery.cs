using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Interfaces;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Mappers;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Priorizacion;

/// <summary>Los parámetros vigentes del score. Lo que el Admin ve antes de recalibrar.</summary>
public sealed record ObtenerParametrosScoreQuery : IQuery<ParametrosScoreDto>;

public sealed class ObtenerParametrosScoreQueryHandler(IParametrosScorePort parametros)
    : IQueryHandler<ObtenerParametrosScoreQuery, ParametrosScoreDto>
{
    public async Task<ParametrosScoreDto> Handle(ObtenerParametrosScoreQuery query, CancellationToken ct = default) =>
        (await parametros.ParaEditarAsync(ct)).ADto();
}
