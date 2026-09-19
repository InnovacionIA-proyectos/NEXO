using CabinaFletes.Application.CasosDeUso.Metricas.DTOs;

namespace CabinaFletes.Application.CasosDeUso.Metricas.Interfaces;

/// <summary>KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder).</summary>
public interface IMetricasPort
{
    Task<CumplimientoDto> CumplimientoAsync(CancellationToken ct);

    /// <summary>Los 4 KPIs cuantitativos del piloto (documento de entendimiento, sección 14).</summary>
    Task<KpisPilotoDto> KpisPilotoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct);
}
