using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// GET /api/planes-cargue/por-linea/{lineaId}/estado — completa el sondeo de <see cref="clsObtenerPlanesPorLineaQuery"/>: dice <c>procesando</c>,
/// <c>listo</c> o <c>rechazado</c> (con código y mensaje del Cubicador), todo contra la proyección local.
/// </summary>
public sealed record clsObtenerEstadoCubicacionLineaQuery(Guid LineaSolicitudId) : IQuery<clsEstadoCubicacionLineaDto>;

public sealed class clsObtenerEstadoCubicacionLineaQueryHandler(
    IPlanCargueRepository planes, IRechazoCubicacionLineaPort rechazos)
    : IQueryHandler<clsObtenerEstadoCubicacionLineaQuery, clsEstadoCubicacionLineaDto>
{
    public async Task<clsEstadoCubicacionLineaDto> Handle(clsObtenerEstadoCubicacionLineaQuery query, CancellationToken ct = default)
    {
        var flota = await planes.ObtenerPorLineaAsync(query.LineaSolicitudId, ct);
        var total = flota.Count;
        var vehiculos = flota.Select(p => new clsPlanFlotaItemDto(
            p.Id, p.NumeroVehiculo, total,
            UnidadesCargadas: p.Colocaciones.Count(c => c.Cargada),
            UnidadesTotales: p.Colocaciones.Count)).ToList();

        // Pedir el plan otra vez borra el rechazo anterior (clsCrearPlanDesdeLineaCommand): si hay uno, es del ÚLTIMO intento.
        var rechazo = await rechazos.ObtenerAsync(query.LineaSolicitudId, ct);
        if (rechazo is not null)
            return new clsEstadoCubicacionLineaDto("rechazado", rechazo.Codigo, rechazo.Mensaje, rechazo.RechazadoAt, vehiculos);

        return new clsEstadoCubicacionLineaDto(total > 0 ? "listo" : "procesando", null, null, null, vehiculos);
    }
}
