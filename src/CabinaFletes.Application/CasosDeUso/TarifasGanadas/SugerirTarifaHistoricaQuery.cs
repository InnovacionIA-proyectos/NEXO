using CabinaFletes.Application.CasosDeUso.TarifasGanadas.DTOs;
using CabinaFletes.Application.CasosDeUso.TarifasGanadas.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.TarifasGanadas;

public sealed record SugerirTarifaHistoricaQuery(Guid? RutaId, Guid? VehiculoId, string? Mercancia)
    : IQuery<IReadOnlyList<BibliotecaItemDto>>;

public sealed class SugerirTarifaHistoricaQueryHandler(IHistoricoTarifasPort historico)
    : IQueryHandler<SugerirTarifaHistoricaQuery, IReadOnlyList<BibliotecaItemDto>>
{
    public Task<IReadOnlyList<BibliotecaItemDto>> Handle(SugerirTarifaHistoricaQuery query, CancellationToken ct = default) =>
        historico.SugerirAsync(query.RutaId, query.VehiculoId, query.Mercancia, ct);
}
