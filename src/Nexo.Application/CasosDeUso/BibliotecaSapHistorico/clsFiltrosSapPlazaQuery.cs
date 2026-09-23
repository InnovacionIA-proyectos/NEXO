using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

public sealed record clsFiltrosSapPlazaQuery() : IQuery<clsSapHistoricoFiltrosDto>;

public sealed class clsFiltrosSapPlazaQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<clsFiltrosSapPlazaQuery, clsSapHistoricoFiltrosDto>
{
    public Task<clsSapHistoricoFiltrosDto> Handle(clsFiltrosSapPlazaQuery query, CancellationToken ct = default) =>
        sap.FiltrosPlazaAsync(ct);
}
