namespace Nexo.Application.CasosDeUso.Metricas.DTOs;

/// <summary>
/// Los 4 indicadores cuantitativos que el documento de entendimiento define como
/// criterio de éxito del piloto de 2 semanas (sección 14): tiempo de costeo del pliego,
/// % de líneas auto-costeadas, cotizaciones emitidas desde la app y fletes manuales a
/// vigilar frente al piso. Antes no existía ninguna métrica calculada para esto — el
/// piloto solo podía evaluarse de forma cualitativa/manual.
///
/// S5-HU-21.1/#287: el tiempo de costeo ya no es "cuánto tardó el robot en revisar todo"
/// (TerminadoEn de la saga se marca cuando todas las líneas tienen desenlace, incluso las que
/// quedan esperando a una persona) sino el ciclo completo: pliego 100% automático (saga
/// Completado) = TerminadoEn - SolicitadoEn; pliego con intervención manual (saga Parcial) =
/// desde que se creó la solicitud hasta Cotizacion.EntregadaAt. Un Parcial todavía sin entregar
/// no entra (mismo criterio que Cumplimiento SLA).
/// </summary>
/// <param name="LineasConFleteManual">
/// Líneas cuyo flete quedó en Fuente=Manual (fuera de la regla automática MAYOR(plaza,
/// piso)). No es lo mismo que "por debajo del piso": eso exigiría comparar cada una
/// contra el piso SICE-TAC vigente EN EL MOMENTO en que se fijó, y esa foto histórica no
/// se persiste por línea. Este número es la señal barata y honesta: cuántas líneas
/// requieren que alguien verifique manualmente el cumplimiento de RN-02.
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
    /// <summary>S5-HU-21.1/#287: eco del corte por tamaño aplicado al tiempo de costeo ("1-10", "11-100", "+100"); null = todos.</summary>
    string? Tamano = null);
