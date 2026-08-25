using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico;

public sealed record FiltrosSapHistoricoQuery() : IQuery<SapHistoricoFiltrosDto>;

public sealed class FiltrosSapHistoricoQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<FiltrosSapHistoricoQuery, SapHistoricoFiltrosDto>
{
    public Task<SapHistoricoFiltrosDto> Handle(FiltrosSapHistoricoQuery query, CancellationToken ct = default) =>
        sap.FiltrosAsync(ct);
}
