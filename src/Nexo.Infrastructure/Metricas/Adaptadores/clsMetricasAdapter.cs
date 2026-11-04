using Nexo.Application.CasosDeUso.Metricas;
using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Metricas.Adaptadores;

public sealed class clsMetricasAdapter(clsNexoDbContext db) : IMetricasPort
{
    public async Task<clsCumplimientoDto> CumplimientoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        // Las 5 tarjetas de la cola cuentan solicitudes CREADAS en el rango; Descartadas no tiene fecha de descarte, se aproxima por la de creación.
        var solicitudes = db.Solicitudes.AsQueryable();
        if (desde is not null) solicitudes = solicitudes.Where(s => s.CreatedAt >= desde);
        if (hasta is not null) solicitudes = solicitudes.Where(s => s.CreatedAt <= hasta);

        var porEstado = await solicitudes.GroupBy(s => s.Estado)
            .Select(g => new { Estado = g.Key, N = g.Count() }).ToListAsync(ct);
        int Estado(EstadoSolicitud e) => porEstado.FirstOrDefault(x => x.Estado == e)?.N ?? 0;

        // Licitaciones SIN filtro: el Tablero no las muestra.
        var licitacionesTotal = await db.Solicitudes.CountAsync(s => s.Tipo == TipoSolicitud.Licitacion, ct);
        var licitacionesPerdidas = await db.Cotizaciones
            .CountAsync(c => c.Resultado == ResultadoCotizacion.Perdida && c.clsSolicitud!.Tipo == TipoSolicitud.Licitacion, ct);

        // Cumplimiento SLA filtra por EntregadaAt: % de cotizaciones entregadas con Deadline que llegaron a tiempo;
        // no cuenta las vencidas sin entregar.
        var cotizaciones = db.Cotizaciones
            .Where(c => c.EntregadaAt != null && c.clsSolicitud!.Deadline != null);
        if (desde is not null) cotizaciones = cotizaciones.Where(c => c.EntregadaAt >= desde);
        if (hasta is not null) cotizaciones = cotizaciones.Where(c => c.EntregadaAt <= hasta);
        var entregadas = await cotizaciones
            .Select(c => new { Entregada = c.EntregadaAt!.Value, Deadline = c.clsSolicitud!.Deadline!.Value })
            .ToListAsync(ct);
        var aTiempo = entregadas.Count(x => x.Entregada <= x.Deadline);

        var total = porEstado.Sum(x => x.N);
        return new clsCumplimientoDto(
            desde, hasta,
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

    /// <summary>KPIs del piloto. El tiempo de resolución sale de clsEstadoAutoCosteo: saga Completado = TerminadoEn - SolicitadoEn;
    /// saga Parcial (hubo intervención manual) = Cotizacion.EntregadaAt - Solicitud.CreatedAt, y sin entrega no entra.
    /// El filtro Desde/Hasta es por SolicitadoEn.</summary>
    public async Task<clsKpisPilotoDto> KpisPilotoAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, TamanoPliego? tamano, CancellationToken ct)
    {
        var sagas = await db.Set<clsEstadoAutoCosteo>()
            .Where(s => s.TerminadoEn != null)
            .Where(s => desde == null || s.SolicitadoEn >= desde)
            .Where(s => hasta == null || s.SolicitadoEn <= hasta)
            .Select(s => new { s.CurrentState, s.SolicitudId, s.SolicitadoEn, s.TotalLineas, Terminado = s.TerminadoEn!.Value })
            .ToListAsync(ct);

        // Tamaño por TotalLineas de la saga: el tiempo no es proporcional al número de líneas (se procesan en paralelo).
        if (tamano is { } t)
            sagas = sagas.Where(s => s.TotalLineas is { } n && clsTamanoPliego.Contiene(t, n)).ToList();

        var parciales = sagas
            .Where(s => s.CurrentState == nameof(clsAutoCosteoSaga.Parcial))
            .Select(s => s.SolicitudId).Distinct().ToList();

        var entregadaPorSolicitud = new Dictionary<Guid, DateTimeOffset>();
        var creadaPorSolicitud = new Dictionary<Guid, DateTimeOffset>();
        if (parciales.Count > 0)
        {
            entregadaPorSolicitud = (await db.Cotizaciones
                    .Where(c => parciales.Contains(c.SolicitudId) && c.EntregadaAt != null)
                    .Select(c => new { c.SolicitudId, Entregada = c.EntregadaAt!.Value })
                    .ToListAsync(ct))
                .GroupBy(c => c.SolicitudId)
                .ToDictionary(g => g.Key, g => g.Min(c => c.Entregada));
            creadaPorSolicitud = await db.Solicitudes
                .Where(s => parciales.Contains(s.Id))
                .Select(s => new { s.Id, s.CreatedAt })
                .ToDictionaryAsync(s => s.Id, s => s.CreatedAt, ct);
        }

        var lista = new List<double>();
        foreach (var s in sagas)
        {
            if (s.CurrentState == nameof(clsAutoCosteoSaga.Completado))
                lista.Add((s.Terminado - s.SolicitadoEn).TotalMinutes);
            else if (s.CurrentState == nameof(clsAutoCosteoSaga.Parcial)
                     && entregadaPorSolicitud.TryGetValue(s.SolicitudId, out var entregada)
                     && creadaPorSolicitud.TryGetValue(s.SolicitudId, out var creada))
                lista.Add((entregada - creada).TotalMinutes);
            // Parcial sin entregar (o sin solicitud): todavía no hay "cotización lista", no entra.
        }

        var minutos = lista.OrderBy(m => m).ToList();
        double? promedio = minutos.Count == 0 ? null : Math.Round(minutos.Average(), 1);
        double? mediana = minutos.Count == 0 ? null : Math.Round(Mediana(minutos), 1);
        var bajo10 = minutos.Count(m => m <= 10.0);

        var lineasQuery = db.LineasSolicitud.AsQueryable();
        if (desde is not null) lineasQuery = lineasQuery.Where(l => l.CreatedAt >= desde);
        if (hasta is not null) lineasQuery = lineasQuery.Where(l => l.CreatedAt <= hasta);
        // Una línea RECHAZADA se dejó sin costear a propósito: no cuenta como "por costear" ni baja el porcentaje de autocosteo.
        lineasQuery = lineasQuery.Where(l => l.EstadoLinea != EstadoLinea.Rechazada);
        var totalLineas = await lineasQuery.CountAsync(ct);
        var autoCosteadas = await lineasQuery.CountAsync(l => l.EstadoLinea == EstadoLinea.AutoResuelta, ct);

        var cotizacionesQuery = db.Cotizaciones.Where(c => c.EntregadaAt != null);
        if (desde is not null) cotizacionesQuery = cotizacionesQuery.Where(c => c.EntregadaAt >= desde);
        if (hasta is not null) cotizacionesQuery = cotizacionesQuery.Where(c => c.EntregadaAt <= hasta);
        var cotizacionesEmitidas = await cotizacionesQuery.CountAsync(ct);

        // No se mide "fletes bajo el piso" literal: el piso SICE-TAC vigente al fijar cada flete no se persiste por línea.
        // Fuente=Manual (fuera de la regla automática RN-01/RN-02) es la señal para verificar a mano.
        var lineasManualQuery = db.Cotizaciones.SelectMany(c => c.Lineas).Where(l => l.Fuente == FuenteFlete.Manual);
        if (desde is not null) lineasManualQuery = lineasManualQuery.Where(l => l.CreatedAt >= desde);
        if (hasta is not null) lineasManualQuery = lineasManualQuery.Where(l => l.CreatedAt <= hasta);
        var lineasConFleteManual = await lineasManualQuery.CountAsync(ct);

        return new clsKpisPilotoDto(
            desde, hasta, minutos.Count, promedio, mediana, bajo10,
            minutos.Count == 0 ? null : Math.Round(100.0 * bajo10 / minutos.Count, 1),
            totalLineas, autoCosteadas,
            totalLineas == 0 ? null : Math.Round(100.0 * autoCosteadas / totalLineas, 1),
            cotizacionesEmitidas, lineasConFleteManual,
            tamano is { } tm ? clsTamanoPliego.Etiqueta(tm) : null);
    }

    private static double Mediana(List<double> ordenadoAscendente)
    {
        var n = ordenadoAscendente.Count;
        return n % 2 == 1
            ? ordenadoAscendente[n / 2]
            : (ordenadoAscendente[n / 2 - 1] + ordenadoAscendente[n / 2]) / 2.0;
    }
}
