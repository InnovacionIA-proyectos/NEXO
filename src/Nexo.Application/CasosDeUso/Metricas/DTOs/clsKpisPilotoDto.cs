namespace Nexo.Application.CasosDeUso.Metricas.DTOs;

/// <summary>
/// Los 4 indicadores cuantitativos de éxito del piloto. El tiempo de costeo es el ciclo completo: pliego 100% automático (saga Completado) = TerminadoEn - SolicitadoEn;
/// con intervención manual (saga Parcial) = desde la creación de la solicitud hasta Cotizacion.EntregadaAt; un Parcial sin entregar no entra.
/// </summary>
/// <param name="LineasConFleteManual">
/// Líneas con Fuente=Manual. No equivale a "por debajo del piso" (el piso vigente al fijarlo no se persiste): es la señal de cuántas líneas requieren verificar RN-02.
/// </param>
public record clsKpisPilotoDto(
    DateTimeOffset? Desde,
    DateTimeOffset? Hasta,
    int PliegosResueltos,
    double? TiempoPromedioResolucionMinutos,
    double? TiempoMedianoResolucionMinutos,
    int PliegosBajo10Minutos,
    double? PorcentajePliegosBajo10Minutos,
    int TotalLineas,
    int LineasAutoCosteadas,
    double? PorcentajeLineasAutoCosteadas,
    int CotizacionesEmitidas,
    int LineasConFleteManual,
    /// <summary>Eco del corte por tamaño aplicado al tiempo de costeo ("1-10", "11-100", "+100"); null = todos.</summary>
    string? Tamano = null);
