using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// [S3-HU-04.3, issue #78] GET /api/planes-cargue/por-linea/{lineaId} — sondeo de la flota
/// que crea <see cref="CrearPlanDesdeLineaCommand"/>, contra la proyección local (mismo
/// criterio "sin llamada a Cubicador" que el resto de S3-HU-03). Responde lista VACÍA, no
/// 404, mientras el comando "crear_desde_linea" sigue en proceso: no hay forma de
/// distinguir acá "la línea no existe" de "todavía no llegó el primer plan" sin volver a
/// consultar a Cubicador, y el contrato del frontend (<c>porLinea()</c> en
/// cubicador-api.service.ts, y su caller en mesa-costeo-page.component.ts) ya asume que una
/// lista vacía significa "seguir esperando" — misma semántica que tenía el endpoint
/// síncrono de Cubicador que este reemplaza.
///
/// Reutiliza el mismo mapeo a <see cref="PlanFlotaItemDto"/> que
/// <see cref="ObtenerFlotaPlanQuery"/> (TotalVehiculos = tamaño de la lista, no un campo
/// propio): la diferencia entre ambas queries es solo CÓMO se llega a la flota — por un
/// PlanId ya conocido (<see cref="IPlanCargueRepository.ObtenerFlotaAsync"/>) o directo por
/// LineaSolicitudId (<see cref="IPlanCargueRepository.ObtenerPorLineaAsync"/>), que es lo
/// único que hay disponible mientras se sondea y ningún PlanId existe todavía.
///
/// Ver la nota de limitación conocida en <see cref="CrearPlanDesdeLineaCommand"/>: hoy solo
/// el primer vehículo de una flota multi-vehículo llega a proyectarse acá.
/// </summary>
public sealed record ObtenerPlanesPorLineaQuery(Guid LineaSolicitudId) : IQuery<IReadOnlyList<PlanFlotaItemDto>>;

public sealed class ObtenerPlanesPorLineaQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ObtenerPlanesPorLineaQuery, IReadOnlyList<PlanFlotaItemDto>>
{
    public async Task<IReadOnlyList<PlanFlotaItemDto>> Handle(ObtenerPlanesPorLineaQuery query, CancellationToken ct = default)
    {
        var flota = await planes.ObtenerPorLineaAsync(query.LineaSolicitudId, ct);
        var total = flota.Count;

        return flota.Select(p => new PlanFlotaItemDto(
            p.Id, p.NumeroVehiculo, total,
            UnidadesCargadas: p.Colocaciones.Count(c => c.Cargada),
            UnidadesTotales: p.Colocaciones.Count)).ToList();
    }
}
