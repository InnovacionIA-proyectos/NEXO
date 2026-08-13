using CabinaFletes.Application.BibliotecaSapHistorico.Dtos;
using CabinaFletes.Application.BibliotecaSapHistorico.Ports;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.BibliotecaSapHistorico.Queries;

public sealed record FiltrosSapHistoricoQuery() : IQuery<SapHistoricoFiltrosDto>;

public sealed class FiltrosSapHistoricoQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<FiltrosSapHistoricoQuery, SapHistoricoFiltrosDto>
{
    public Task<SapHistoricoFiltrosDto> Handle(FiltrosSapHistoricoQuery query, CancellationToken ct = default) =>
        sap.FiltrosAsync(ct);
}
