using CabinaFletes.Application.Metricas.Dtos;
using CabinaFletes.Application.Metricas.Ports;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Adapters;

public sealed class MetricasAdapter(CabinaFletesDbContext db) : IMetricasPort
{
    public async Task<CumplimientoDto> CumplimientoAsync(CancellationToken ct)
    {
        var porEstado = await db.Solicitudes.GroupBy(s => s.Estado)
            .Select(g => new { Estado = g.Key, N = g.Count() }).ToListAsync(ct);
        int Estado(EstadoSolicitud e) => porEstado.FirstOrDefault(x => x.Estado == e)?.N ?? 0;

        var licitacionesTotal = await db.Solicitudes.CountAsync(s => s.Tipo == TipoSolicitud.Licitacion, ct);
        var licitacionesPerdidas = await db.Cotizaciones
            .CountAsync(c => c.Resultado == ResultadoCotizacion.Perdida && c.Solicitud!.Tipo == TipoSolicitud.Licitacion, ct);

        var entregadas = await db.Cotizaciones
            .Where(c => c.EntregadaAt != null && c.Solicitud!.Deadline != null)
            .Select(c => new { Entregada = c.EntregadaAt!.Value, Deadline = c.Solicitud!.Deadline!.Value })
            .ToListAsync(ct);
        var aTiempo = entregadas.Count(x => x.Entregada <= x.Deadline);

        var total = porEstado.Sum(x => x.N);
        return new CumplimientoDto(
            total,
            Estado(EstadoSolicitud.Pendiente),
            Estado(EstadoSolicitud.EnCosteo),
            Estado(EstadoSolicitud.Entregada),
            Estado(EstadoSolicitud.Descartada),
            licitacionesTotal,
            licitacionesPerdidas,
            licitacionesTotal == 0 ? 0 : Math.Round(100.0 * licitacionesPerdidas / licitacionesTotal, 1),
            entregadas.Count,
            aTiempo,
            entregadas.Count == 0 ? 0 : Math.Round(100.0 * aTiempo / entregadas.Count, 1));
    }
}
