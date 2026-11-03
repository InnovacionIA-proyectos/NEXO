using Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;
using Nexo.Application.CasosDeUso.TarifasGanadas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasGanadas;

public sealed record clsSugerirTarifaHistoricaQuery(Guid? RutaId, Guid? VehiculoId, string? Mercancia)
    : IQuery<IReadOnlyList<clsBibliotecaItemDto>>;

public sealed class clsSugerirTarifaHistoricaQueryHandler(IHistoricoTarifasPort historico)
    : IQueryHandler<clsSugerirTarifaHistoricaQuery, IReadOnlyList<clsBibliotecaItemDto>>
{
    public Task<IReadOnlyList<clsBibliotecaItemDto>> Handle(clsSugerirTarifaHistoricaQuery query, CancellationToken ct = default) =>
        historico.SugerirAsync(query.RutaId, query.VehiculoId, query.Mercancia, ct);
}
