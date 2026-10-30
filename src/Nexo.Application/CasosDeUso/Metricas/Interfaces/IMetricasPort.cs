using Nexo.Application.CasosDeUso.Metricas.DTOs;

namespace Nexo.Application.CasosDeUso.Metricas.Interfaces;

/// <summary>KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder).</summary>
public interface IMetricasPort
{
    /// <summary>S5-HU-21.1/#287: con <paramref name="desde"/>/<paramref name="hasta"/>, las tarjetas de
    /// la cola cuentan solicitudes CREADAS en el rango y Cumplimiento SLA cotizaciones ENTREGADAS en él.</summary>
    Task<clsCumplimientoDto> CumplimientoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct);

    /// <summary>Los 4 KPIs cuantitativos del piloto (documento de entendimiento, sección 14).
    /// <paramref name="tamano"/> recorta solo el tiempo de costeo por TotalLineas de la saga.</summary>
    Task<clsKpisPilotoDto> KpisPilotoAsync(DateTimeOffset? desde, DateTimeOffset? hasta, TamanoPliego? tamano, CancellationToken ct);
}
