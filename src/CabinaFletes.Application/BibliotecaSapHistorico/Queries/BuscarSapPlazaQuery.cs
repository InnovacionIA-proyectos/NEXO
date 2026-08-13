using CabinaFletes.Application.BibliotecaSapHistorico.Dtos;
using CabinaFletes.Application.BibliotecaSapHistorico.Ports;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.BibliotecaSapHistorico.Queries;

/// <summary>Tarifas de plaza (vigentes, tblSAPTarifas) de SAP TM.</summary>
public sealed record BuscarSapPlazaQuery(
    string? Q, int? LineaNegocio, string? TipoVehiculo, string? TipoCarroseria, int? ClaseViaje, int Top)
    : IQuery<SapHistoricoResultDto>;

public sealed class BuscarSapPlazaQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<BuscarSapPlazaQuery, SapHistoricoResultDto>
{
    public Task<SapHistoricoResultDto> Handle(BuscarSapPlazaQuery query, CancellationToken ct = default) =>
        sap.BuscarPlazaAsync(query.Q, query.LineaNegocio, query.TipoVehiculo, query.TipoCarroseria, query.ClaseViaje, query.Top, ct);
}
