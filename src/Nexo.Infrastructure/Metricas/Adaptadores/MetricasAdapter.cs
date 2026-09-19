using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Metricas.Adaptadores;

public sealed class MetricasAdapter(NexoDbContext db) : IMetricasPort
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

    /// <summary>
    /// Los 4 KPIs del piloto (documento de entendimiento, sección 14). El tiempo de
    /// resolución sale de EstadoAutoCosteo (la saga que ya persiste SolicitadoEn/
    /// TerminadoEn para todo pliego que pasó por auto-costeo, sea vía captura diferida
    /// o vía lineas:bulk); no hay otra fuente con ambos timestamps.
    /// </summary>
    public async Task<KpisPilotoDto> KpisPilotoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        var sagas = await db.Set<EstadoAutoCosteo>()
            .Where(s => s.TerminadoEn != null)
            .Where(s => desde == null || s.SolicitadoEn >= desde)
            .Where(s => hasta == null || s.SolicitadoEn <= hasta)
            .Select(s => new { s.SolicitadoEn, Terminado = s.TerminadoEn!.Value })
            .ToListAsync(ct);

        var minutos = sagas.Select(s => (s.Terminado - s.SolicitadoEn).TotalMinutes).OrderBy(m => m).ToList();
        double? promedio = minutos.Count == 0 ? null : Math.Round(minutos.Average(), 1);
        double? mediana = minutos.Count == 0 ? null : Math.Round(Mediana(minutos), 1);
        var bajo10 = minutos.Count(m => m <= 10.0);

        var lineasQuery = db.LineasSolicitud.AsQueryable();
        if (desde is not null) lineasQuery = lineasQuery.Where(l => l.CreatedAt >= desde);
        if (hasta is not null) lineasQuery = lineasQuery.Where(l => l.CreatedAt <= hasta);
        var totalLineas = await lineasQuery.CountAsync(ct);
        var autoCosteadas = await lineasQuery.CountAsync(l => l.EstadoLinea == EstadoLinea.AutoResuelta, ct);

        var cotizacionesQuery = db.Cotizaciones.Where(c => c.EntregadaAt != null);
        if (desde is not null) cotizacionesQuery = cotizacionesQuery.Where(c => c.EntregadaAt >= desde);
        if (hasta is not null) cotizacionesQuery = cotizacionesQuery.Where(c => c.EntregadaAt <= hasta);
        var cotizacionesEmitidas = await cotizacionesQuery.CountAsync(ct);

        // No "0 fletes bajo el piso" literal: eso exigiría el piso SICE-TAC vigente EN EL
        // MOMENTO en que se fijó cada flete, y esa foto histórica no se persiste por línea.
        // Fuente=Manual es la señal barata y honesta: líneas fuera de la regla automática
        // (RN-01/RN-02) que alguien debe verificar a mano contra el piso (ver KpisPilotoDto).
        var lineasManualQuery = db.Cotizaciones.SelectMany(c => c.Lineas).Where(l => l.Fuente == FuenteFlete.Manual);
        if (desde is not null) lineasManualQuery = lineasManualQuery.Where(l => l.CreatedAt >= desde);
        if (hasta is not null) lineasManualQuery = lineasManualQuery.Where(l => l.CreatedAt <= hasta);
        var lineasConFleteManual = await lineasManualQuery.CountAsync(ct);

        return new KpisPilotoDto(
            desde, hasta, sagas.Count, promedio, mediana, bajo10,
            sagas.Count == 0 ? null : Math.Round(100.0 * bajo10 / sagas.Count, 1),
            totalLineas, autoCosteadas,
            totalLineas == 0 ? null : Math.Round(100.0 * autoCosteadas / totalLineas, 1),
            cotizacionesEmitidas, lineasConFleteManual);
    }

    private static double Mediana(List<double> ordenadoAscendente)
    {
        var n = ordenadoAscendente.Count;
        return n % 2 == 1
            ? ordenadoAscendente[n / 2]
            : (ordenadoAscendente[n / 2 - 1] + ordenadoAscendente[n / 2]) / 2.0;
    }
}
