namespace Nexo.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Contrato del motor de propuesta de tarifa (módulo Biblioteca): compara el
// flete de plaza SAP, el piso SICE-TAC y el histórico SAP, y decide el valor
// propuesto. Vive en Common porque lo consumen dos módulos: Cotizaciones
// (clsAplicarPropuestasCommand, vía IPropuestaTarifaPort) y Biblioteca (que lo
// produce). Antes vivía en Cotizaciones/Dtos — se movió aquí por la misma
// razón que IAuditoriaPort (ver Common/Ports/IAuditoriaPort.cs).
// ---------------------------------------------------------------------------

/// <summary>
/// <paramref name="Advertencia"/> es no-nulo cuando el candidato NO es un
/// match exacto de las reglas de negocio (A/B/C de clsTarifarioSapAdapter) sino el
/// fallback conservador ("mayor valor vigente") — ej. el peso de la línea excede el
/// límite de todas las tarifas vigentes para ese vehículo, o el vehículo no resolvió en
/// el catálogo SAP. La línea SIGUE cotizándose con este candidato (no se descarta), pero
/// el mensaje es explícito para que el frontend se lo muestre al Administrador de
/// Fletes/cliente antes de confirmar.
/// </summary>
public record clsCandidatoTarifaDto(
    string Fuente, decimal Valor, string Detalle, string Criterio, int Coincidencias, string? Advertencia = null);

/// <summary>
/// Piso SICE-TAC vigente con el desglose de horas, para Mesa de Costeo
/// (<see cref="IPropuestaTarifaPort.PisoSicetacVigenteAsync"/>). Separado de
/// <see cref="clsCandidatoTarifaDto"/> porque ese colapsa el desglose en un string
/// (<c>Detalle</c>) para la justificación de la propuesta automática — aquí Mesa de
/// Costeo necesita los campos por separado para mostrarlos en su propia UI.
/// </summary>
public record clsPisoSicetacVigenteDto(
    int Periodo, decimal ValorPiso, decimal? ValorHora, decimal? HorasPactadas,
    string Configuracion, string TipoCarga, string UnidadTransporte, string CondicionCarga);

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
    bool Vencida = false);

public record clsPropuestaSolicitudDto(
    Guid SolicitudId,
    IReadOnlyList<clsPropuestaLineaDto> Lineas,
    IReadOnlyList<string> Advertencias);
