namespace Nexo.Application.CasosDeUso.Metricas.DTOs;

/// <param name="Desde">Eco del filtro (S5-HU-21.1/#287), igual que en <see cref="clsKpisPilotoDto"/>.
/// null = sin límite. Las 5 tarjetas de la cola filtran por <c>Solicitud.CreatedAt</c> (para
/// Descartadas no existe fecha de descarte: se aproxima por la de creación); Cumplimiento SLA, por
/// <c>Cotizacion.EntregadaAt</c>. Las licitaciones no se filtran (ni siquiera se muestran).</param>
public record clsCumplimientoDto(
    DateTimeOffset? Desde,
    DateTimeOffset? Hasta,
    int TotalSolicitudes,
    int Pendientes,
    int EnCosteo,
    int Entregadas,
    int Descartadas,
    int LicitacionesTotal,
    int LicitacionesPerdidas,
    double PorcentajeLicitacionesPerdidas,
    int EntregadasConDeadline,
    int EntregadasATiempo,
    double CumplimientoSla);
