using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico;

/// <summary>Histórico (log de cambios tblSAPTarifasLog) de tarifas SAP TM.</summary>
public sealed record BuscarSapHistoricoQuery(
    string? Q, int? LineaNegocio, string? TipoVehiculo, string? TipoCarroseria, int? ClaseViaje, int Top)
    : IQuery<SapHistoricoResultDto>;

public sealed class BuscarSapHistoricoQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<BuscarSapHistoricoQuery, SapHistoricoResultDto>
{
    public Task<SapHistoricoResultDto> Handle(BuscarSapHistoricoQuery query, CancellationToken ct = default) =>
        sap.BuscarAsync(query.Q, query.LineaNegocio, query.TipoVehiculo, query.TipoCarroseria, query.ClaseViaje, query.Top, ct);
}
