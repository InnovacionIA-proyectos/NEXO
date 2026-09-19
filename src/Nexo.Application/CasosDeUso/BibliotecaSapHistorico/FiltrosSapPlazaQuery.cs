using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

public sealed record FiltrosSapPlazaQuery() : IQuery<SapHistoricoFiltrosDto>;

public sealed class FiltrosSapPlazaQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<FiltrosSapPlazaQuery, SapHistoricoFiltrosDto>
{
    public Task<SapHistoricoFiltrosDto> Handle(FiltrosSapPlazaQuery query, CancellationToken ct = default) =>
        sap.FiltrosPlazaAsync(ct);
}
