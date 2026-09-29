using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Catalogos;

/// <summary>Catálogo de tipos de vehículo para los desplegables de Reglas Solo-SICETAC
/// (S4-HU-09.2) — ya no texto libre.</summary>
public sealed record clsListarTiposVehiculoQuery : IQuery<IReadOnlyList<clsTipoVehiculoDto>>;

public sealed class clsListarTiposVehiculoQueryHandler(ITiposVehiculoPort tiposVehiculo)
    : IQueryHandler<clsListarTiposVehiculoQuery, IReadOnlyList<clsTipoVehiculoDto>>
{
    public Task<IReadOnlyList<clsTipoVehiculoDto>> Handle(clsListarTiposVehiculoQuery query, CancellationToken ct = default) =>
        tiposVehiculo.ListarAsync(ct);
}
