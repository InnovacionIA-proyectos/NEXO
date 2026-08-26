using CabinaFletes.Application.CasosDeUso.TarifasGanadas.DTOs;
using CabinaFletes.Application.CasosDeUso.TarifasGanadas.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.TarifasGanadas;

public sealed record BuscarBibliotecaTarifasQuery(string? Q, string? Mercancia, string? Vigencia, bool? DeGanada)
    : IQuery<IReadOnlyList<BibliotecaItemDto>>;

public sealed class BuscarBibliotecaTarifasQueryHandler(IHistoricoTarifasPort historico)
    : IQueryHandler<BuscarBibliotecaTarifasQuery, IReadOnlyList<BibliotecaItemDto>>
{
    public Task<IReadOnlyList<BibliotecaItemDto>> Handle(BuscarBibliotecaTarifasQuery query, CancellationToken ct = default) =>
        historico.BuscarAsync(query.Q, query.Mercancia, query.Vigencia, query.DeGanada, ct);
}
