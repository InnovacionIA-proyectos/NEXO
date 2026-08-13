namespace CabinaFletes.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Contrato del motor de propuesta de tarifa (módulo Biblioteca): compara el
// flete de plaza SAP, el piso SICE-TAC y el histórico SAP, y decide el valor
// propuesto. Vive en Common porque lo consumen dos módulos: Cotizaciones
// (AplicarPropuestasCommand, vía IPropuestaTarifaPort) y Biblioteca (que lo
// produce). Antes vivía en Cotizaciones/Dtos — se movió aquí por la misma
// razón que IAuditoriaPort (ver Common/Ports/IAuditoriaPort.cs).
// ---------------------------------------------------------------------------

public record CandidatoTarifaDto(
    string Fuente, decimal Valor, string Detalle, string Criterio, int Coincidencias);

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
    CandidatoTarifaDto? RetornoContenedor = null);

public record PropuestaSolicitudDto(
    Guid SolicitudId,
    IReadOnlyList<PropuestaLineaDto> Lineas,
    IReadOnlyList<string> Advertencias);
