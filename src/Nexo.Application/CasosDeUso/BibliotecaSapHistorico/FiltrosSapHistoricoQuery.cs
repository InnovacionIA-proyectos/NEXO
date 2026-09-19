using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

public sealed record FiltrosSapHistoricoQuery() : IQuery<SapHistoricoFiltrosDto>;

public sealed class FiltrosSapHistoricoQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<FiltrosSapHistoricoQuery, SapHistoricoFiltrosDto>
{
    public Task<SapHistoricoFiltrosDto> Handle(FiltrosSapHistoricoQuery query, CancellationToken ct = default) =>
        sap.FiltrosAsync(ct);
}
