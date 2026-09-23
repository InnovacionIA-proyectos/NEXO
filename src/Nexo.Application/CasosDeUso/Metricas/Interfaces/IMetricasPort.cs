using Nexo.Application.CasosDeUso.Metricas.DTOs;

namespace Nexo.Application.CasosDeUso.Metricas.Interfaces;

/// <summary>KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder).</summary>
public interface IMetricasPort
{
    Task<clsCumplimientoDto> CumplimientoAsync(CancellationToken ct);

    /// <summary>Los 4 KPIs cuantitativos del piloto (documento de entendimiento, sección 14).</summary>
    Task<clsKpisPilotoDto> KpisPilotoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct);
}
