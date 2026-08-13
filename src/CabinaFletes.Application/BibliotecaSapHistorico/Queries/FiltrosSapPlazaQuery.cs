using CabinaFletes.Application.BibliotecaSapHistorico.Dtos;
using CabinaFletes.Application.BibliotecaSapHistorico.Ports;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.BibliotecaSapHistorico.Queries;

public sealed record FiltrosSapPlazaQuery() : IQuery<SapHistoricoFiltrosDto>;

public sealed class FiltrosSapPlazaQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<FiltrosSapPlazaQuery, SapHistoricoFiltrosDto>
{
    public Task<SapHistoricoFiltrosDto> Handle(FiltrosSapPlazaQuery query, CancellationToken ct = default) =>
        sap.FiltrosPlazaAsync(ct);
}
