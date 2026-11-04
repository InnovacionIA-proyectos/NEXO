namespace Nexo.Application.Common.Dtos;

/// <summary>
/// Advertencia no nula = fallback conservador ("mayor valor vigente") en vez de match exacto; la línea sigue cotizándose.
/// TablaOrigen: "SAP" (tablas externas) o "NEXO" (tablas propias). ValorPisoBase + ValorTiemposLogisticos == Valor; solo los trae el candidato SICE-TAC.
/// </summary>
public record clsCandidatoTarifaDto(
    string Fuente, decimal Valor, string Detalle, string Criterio, int Coincidencias,
    string? Advertencia = null, string TablaOrigen = "SAP",
    decimal? ValorPisoBase = null, decimal? ValorTiemposLogisticos = null,
    /// <summary>Configuración SICE-TAC de la fila (2L2, C2, 3S3...) cuando el candidato es SICE-TAC.</summary>
    string? Configuracion = null,
    /// <summary>true cuando la fuente no tiene filas del vehículo pedido: se muestra como referencia, no gobierna ni entra al recomendado.</summary>
    bool VehiculoRelajado = false);

/// <summary>Piso SICE-TAC vigente con el desglose de horas, para Mesa de Costeo.</summary>
public record clsPisoSicetacVigenteDto(
    int Periodo, decimal ValorPiso, decimal? ValorHora, decimal? HorasPactadas,
    string Configuracion, string TipoCarga, string UnidadTransporte, string CondicionCarga);

/// <summary>
/// ModoDecision: "SoloSicetac" | "PlazaVigenteMayor" | "ConsultaCompleta" (texto, no el enum de Domain).
/// MotivoPendiente != null impide el autocosteo aunque haya valor propuesto.
/// </summary>
public record clsPropuestaLineaDto(
    Guid LineaId,
    string? clsRuta,
    string? Vehiculo,
    decimal? Peso,
    clsCandidatoTarifaDto? Sicetac,
    clsCandidatoTarifaDto? Plaza,
    clsCandidatoTarifaDto? HistoricoSap,
    decimal? ValorPropuesto,
    string? FuentePropuesta,
    string Justificacion,
    clsCandidatoTarifaDto? RetornoContenedor = null,
    bool Vencida = false,
    string ModoDecision = "ConsultaCompleta",
    string? MotivoPendiente = null,
    clsCandidatoTarifaDto? AprovisionamientoContenedor = null)
{
    public bool Autocosteable => ValorPropuesto is not null && MotivoPendiente is null;
}

public record clsPropuestaSolicitudDto(
    Guid SolicitudId,
    IReadOnlyList<clsPropuestaLineaDto> Lineas,
    IReadOnlyList<string> Advertencias);
