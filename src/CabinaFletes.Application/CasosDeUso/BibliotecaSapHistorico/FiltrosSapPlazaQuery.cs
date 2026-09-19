using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico;

public sealed record FiltrosSapPlazaQuery() : IQuery<SapHistoricoFiltrosDto>;

public sealed class FiltrosSapPlazaQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<FiltrosSapPlazaQuery, SapHistoricoFiltrosDto>
{
    public Task<SapHistoricoFiltrosDto> Handle(FiltrosSapPlazaQuery query, CancellationToken ct = default) =>
        sap.FiltrosPlazaAsync(ct);
}
