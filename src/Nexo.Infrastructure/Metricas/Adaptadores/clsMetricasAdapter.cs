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
        // S5-HU-21.1/#287: las 5 tarjetas de la cola cuentan solicitudes CREADAS en el rango.
        // Para Descartadas no existe fecha de descarte: se aproxima por la de creación.
        var solicitudes = db.Solicitudes.AsQueryable();
        if (desde is not null) solicitudes = solicitudes.Where(s => s.CreatedAt >= desde);
        if (hasta is not null) solicitudes = solicitudes.Where(s => s.CreatedAt <= hasta);

        var porEstado = await solicitudes.GroupBy(s => s.Estado)
            .Select(g => new { Estado = g.Key, N = g.Count() }).ToListAsync(ct);
        int Estado(EstadoSolicitud e) => porEstado.FirstOrDefault(x => x.Estado == e)?.N ?? 0;

        // Licitaciones SIN filtro: el Tablero ni siquiera las muestra (decisión de negocio).
        var licitacionesTotal = await db.Solicitudes.CountAsync(s => s.Tipo == TipoSolicitud.Licitacion, ct);
        var licitacionesPerdidas = await db.Cotizaciones
            .CountAsync(c => c.Resultado == ResultadoCotizacion.Perdida && c.clsSolicitud!.Tipo == TipoSolicitud.Licitacion, ct);

        // Cumplimiento SLA entra al filtro, pero por EntregadaAt (no por la creación de la
        // solicitud): % de cotizaciones entregadas con Deadline informado que llegaron a tiempo.
        // La fórmula no cambia (no cuenta las vencidas sin entregar: aceptado por negocio).
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

    /// <summary>
    /// Los 4 KPIs del piloto (documento de entendimiento, sección 14). El tiempo de
    /// resolución sale de clsEstadoAutoCosteo (la saga que ya persiste SolicitadoEn/
    /// TerminadoEn para todo pliego que pasó por auto-costeo, sea vía captura diferida
    /// o vía lineas:bulk); no hay otra fuente con ambos timestamps.
    ///
    /// S5-HU-21.1/#287: TerminadoEn se marca cuando TODAS las líneas tienen desenlace, incluso las
    /// que quedan esperando a una persona — mide cuánto tardó el robot, no cuándo la cotización
    /// está lista. Por eso: saga Completado (100% automático) = TerminadoEn - SolicitadoEn; saga
    /// Parcial (hubo intervención manual) = Cotizacion.EntregadaAt - Solicitud.CreatedAt, y si
    /// todavía no se entregó, ese pliego no entra. Desde S5-HU-19.3 la entrega exige
    /// el pliego completo; las entregas anteriores pudieron subestimar el tiempo.
    /// El filtro Desde/Hasta sigue siendo por SolicitadoEn de la saga.
    /// </summary>
    public async Task<clsKpisPilotoDto> KpisPilotoAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, TamanoPliego? tamano, CancellationToken ct)
    {
        var sagas = await db.Set<clsEstadoAutoCosteo>()
            .Where(s => s.TerminadoEn != null)
            .Where(s => desde == null || s.SolicitadoEn >= desde)
            .Where(s => hasta == null || s.SolicitadoEn <= hasta)
            .Select(s => new { s.CurrentState, s.SolicitudId, s.SolicitadoEn, s.TotalLineas, Terminado = s.TerminadoEn!.Value })
            .ToListAsync(ct);

        // Tamaño por TotalLineas de la saga: el tiempo NO es proporcional al número de líneas
        // (se procesan en paralelo, un mensaje por línea), así que se corta por tamaño en vez
        // de dividir por línea.
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
        var totalLineas = await lineasQuery.CountAsync(ct);
        var autoCosteadas = await lineasQuery.CountAsync(l => l.EstadoLinea == EstadoLinea.AutoResuelta, ct);

        var cotizacionesQuery = db.Cotizaciones.Where(c => c.EntregadaAt != null);
        if (desde is not null) cotizacionesQuery = cotizacionesQuery.Where(c => c.EntregadaAt >= desde);
        if (hasta is not null) cotizacionesQuery = cotizacionesQuery.Where(c => c.EntregadaAt <= hasta);
        var cotizacionesEmitidas = await cotizacionesQuery.CountAsync(ct);

        // No "0 fletes bajo el piso" literal: eso exigiría el piso SICE-TAC vigente EN EL
        // MOMENTO en que se fijó cada flete, y esa foto histórica no se persiste por línea.
        // Fuente=Manual es la señal barata y honesta: líneas fuera de la regla automática
        // (RN-01/RN-02) que alguien debe verificar a mano contra el piso (ver clsKpisPilotoDto).
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
