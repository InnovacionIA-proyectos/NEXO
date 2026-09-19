namespace CabinaFletes.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Contrato del motor de propuesta de tarifa (módulo Biblioteca): compara el
// flete de plaza SAP, el piso SICE-TAC y el histórico SAP, y decide el valor
// propuesto. Vive en Common porque lo consumen dos módulos: Cotizaciones
// (AplicarPropuestasCommand, vía IPropuestaTarifaPort) y Biblioteca (que lo
// produce). Antes vivía en Cotizaciones/Dtos — se movió aquí por la misma
// razón que IAuditoriaPort (ver Common/Ports/IAuditoriaPort.cs).
// ---------------------------------------------------------------------------

/// <summary>
/// Issue #169: <paramref name="Advertencia"/> es no-nulo cuando el candidato NO es un
/// match exacto de las reglas de negocio (A/B/C de TarifarioSapAdapter) sino el
/// fallback conservador ("mayor valor vigente") — ej. el peso de la línea excede el
/// límite de todas las tarifas vigentes para ese vehículo, o el vehículo no resolvió en
/// el catálogo SAP. La línea SIGUE cotizándose con este candidato (no se descarta), pero
/// el mensaje es explícito para que el frontend se lo muestre al Administrador de
/// Fletes/cliente antes de confirmar.
/// </summary>
public record CandidatoTarifaDto(
    string Fuente, decimal Valor, string Detalle, string Criterio, int Coincidencias, string? Advertencia = null);

public record PropuestaLineaDto(
    Guid LineaId,
    string? Ruta,
    string? Vehiculo,
    decimal? Peso,
    CandidatoTarifaDto? Sicetac,
    CandidatoTarifaDto? Plaza,
    CandidatoTarifaDto? HistoricoSap,
    decimal? ValorPropuesto,
    string? FuentePropuesta,
    string Justificacion,
    CandidatoTarifaDto? RetornoContenedor = null,
    bool Vencida = false);

public record PropuestaSolicitudDto(
    Guid SolicitudId,
    IReadOnlyList<PropuestaLineaDto> Lineas,
    IReadOnlyList<string> Advertencias);
