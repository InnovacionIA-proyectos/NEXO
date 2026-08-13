using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.TarifasGanadas.Dtos;
using CabinaFletes.Application.TarifasGanadas.Ports;

namespace CabinaFletes.Application.TarifasGanadas.Queries;

public sealed record SugerirTarifaHistoricaQuery(Guid? RutaId, Guid? VehiculoId, string? Mercancia)
    : IQuery<IReadOnlyList<BibliotecaItemDto>>;

public sealed class SugerirTarifaHistoricaQueryHandler(IHistoricoTarifasPort historico)
    : IQueryHandler<SugerirTarifaHistoricaQuery, IReadOnlyList<BibliotecaItemDto>>
{
    public Task<IReadOnlyList<BibliotecaItemDto>> Handle(SugerirTarifaHistoricaQuery query, CancellationToken ct = default) =>
        historico.SugerirAsync(query.RutaId, query.VehiculoId, query.Mercancia, ct);
}
