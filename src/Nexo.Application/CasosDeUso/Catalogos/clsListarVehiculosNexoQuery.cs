using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Catalogos;

/// <summary>
/// Los códigos de vehículo de NEXO (<c>tbl_mapeo_vehiculo_tarifario</c>, activos) para el selector «Cambiar vehículo» de la Mesa de Costeo.
/// <c>EnSap</c> dice si el código también existe en el catálogo de SAP, que es el de Reglas Solo-SICETAC y Tarifas de Plaza.
/// </summary>
public sealed record clsListarVehiculosNexoQuery : IQuery<IReadOnlyList<clsVehiculoNexoDto>>;

public sealed class clsListarVehiculosNexoQueryHandler(IVehiculosNexoPort vehiculosNexo, ITiposVehiculoPort tiposVehiculo)
    : IQueryHandler<clsListarVehiculosNexoQuery, IReadOnlyList<clsVehiculoNexoDto>>
{
    public async Task<IReadOnlyList<clsVehiculoNexoDto>> Handle(clsListarVehiculosNexoQuery query, CancellationToken ct = default)
    {
        var enSap = (await tiposVehiculo.ListarAsync(ct)).Select(t => t.Codigo.Trim().ToUpperInvariant()).ToHashSet();
        return (await vehiculosNexo.ListarCodigosAsync(ct))
            .OrderBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
            .Select(c => new clsVehiculoNexoDto(c.Codigo, c.Descripcion, enSap.Contains(c.Codigo.Trim().ToUpperInvariant())))
            .ToList();
    }
}
