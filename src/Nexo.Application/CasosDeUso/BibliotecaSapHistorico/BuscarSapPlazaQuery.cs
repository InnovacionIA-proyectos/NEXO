using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

/// <summary>Tarifas de plaza (vigentes, tblSAPTarifas) de SAP TM.</summary>
public sealed record BuscarSapPlazaQuery(
    string? Q, int? LineaNegocio, string? TipoVehiculo, string? TipoCarroseria, int? ClaseViaje, int Top,
    int Page = 1) : IQuery<SapHistoricoResultDto>;

public sealed class BuscarSapPlazaQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<BuscarSapPlazaQuery, SapHistoricoResultDto>
{
    public Task<SapHistoricoResultDto> Handle(BuscarSapPlazaQuery query, CancellationToken ct = default) =>
        sap.BuscarPlazaAsync(query.Q, query.LineaNegocio, query.TipoVehiculo, query.TipoCarroseria, query.ClaseViaje, query.Top, query.Page, ct);
}
