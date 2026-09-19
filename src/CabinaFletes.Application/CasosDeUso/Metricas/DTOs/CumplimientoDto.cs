namespace CabinaFletes.Application.CasosDeUso.Metricas.DTOs;

public record CumplimientoDto(
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
