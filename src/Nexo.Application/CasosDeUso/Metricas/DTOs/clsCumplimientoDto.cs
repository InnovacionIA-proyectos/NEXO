namespace Nexo.Application.CasosDeUso.Metricas.DTOs;

/// <param name="Desde">Eco del filtro, igual que en <see cref="clsKpisPilotoDto"/>; null = sin límite. Las tarjetas de la cola filtran por <c>Solicitud.CreatedAt</c>
/// (Descartadas se aproxima por la creación); Cumplimiento SLA, por <c>Cotizacion.EntregadaAt</c>. Las licitaciones no se filtran.</param>
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
