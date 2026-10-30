using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

/// <summary>Histórico (log de cambios tblSAPTarifasLog) de tarifas SAP TM.</summary>
public sealed record clsBuscarSapHistoricoQuery(
    string? Q, int? LineaNegocio, string? TipoVehiculo, string? TipoCarroseria, int? ClaseViaje, int Top,
    int Page = 1) : IQuery<clsSapHistoricoResultDto>;

public sealed class clsBuscarSapHistoricoQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<clsBuscarSapHistoricoQuery, clsSapHistoricoResultDto>
{
    public Task<clsSapHistoricoResultDto> Handle(clsBuscarSapHistoricoQuery query, CancellationToken ct = default) =>
        sap.BuscarAsync(query.Q, query.LineaNegocio, query.TipoVehiculo, query.TipoCarroseria, query.ClaseViaje, query.Top, query.Page, ct);
}
