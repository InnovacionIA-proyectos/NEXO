using Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;
using Nexo.Application.CasosDeUso.TarifasGanadas.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.TarifasGanadas;

/// <summary>Issue #166: page 1-based + pageSize (default 50, mismo tope que antes
/// como default) reemplazan el Take(50) fijo que ni siquiera exponía el total real.</summary>
public sealed record BuscarBibliotecaTarifasQuery(
    string? Q, string? Mercancia, string? Vigencia, bool? DeGanada, int Page = 1, int PageSize = 50)
    : IQuery<PaginaDto<BibliotecaItemDto>>;

public sealed class BuscarBibliotecaTarifasQueryHandler(IHistoricoTarifasPort historico)
    : IQueryHandler<BuscarBibliotecaTarifasQuery, PaginaDto<BibliotecaItemDto>>
{
    public async Task<PaginaDto<BibliotecaItemDto>> Handle(BuscarBibliotecaTarifasQuery query, CancellationToken ct = default)
    {
        var (items, total) = await historico.BuscarAsync(
            query.Q, query.Mercancia, query.Vigencia, query.DeGanada, query.Page, query.PageSize, ct);
        return new PaginaDto<BibliotecaItemDto>(items, total);
    }
}
