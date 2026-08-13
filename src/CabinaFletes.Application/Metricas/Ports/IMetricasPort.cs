using CabinaFletes.Application.Metricas.Dtos;

namespace CabinaFletes.Application.Metricas.Ports;

/// <summary>KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder).</summary>
public interface IMetricasPort
{
    Task<CumplimientoDto> CumplimientoAsync(CancellationToken ct);
}
