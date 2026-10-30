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
/// <summary>
/// <paramref name="TablaOrigen"/> (S5-HU-19.1/#281): "SAP" si el candidato salió de una
/// tabla externa (tblSAPTarifas/tblSAPTarifasLog/DWSice) o "NEXO" si salió de una tabla
/// propia (tbl_tarifas_plaza/tbl_tarifas_plaza_historico). Antes de este campo, la única
/// forma de distinguirlo era parseando el texto libre de <see cref="Criterio"/> — esto lo
/// hace un dato estructurado en vez de un detalle de presentación.
/// </summary>
/// <summary>
/// <paramref name="ValorPisoBase"/> y <paramref name="ValorTiemposLogisticos"/>
/// (S5-HU-20.1/#286): solo los trae el candidato SICE-TAC. Descomponen <c>Valor</c> en el
/// piso base de la fila (<c>fila.Valor</c>) y lo que suman las horas logísticas reales de
/// la línea (<c>Valor - fila.Valor</c>, una diferencia a propósito: sirve igual cuando el
/// total salió del <c>ValorSiceConHora</c> de respaldo y no de <c>valorHora × horas</c>).
/// Invariante: <c>ValorPisoBase + ValorTiemposLogisticos == Valor</c>. Plaza e histórico los
/// dejan en null. El desglose se calcula acá y no en pantalla porque el frontend solo ve el
/// catálogo crudo (valorHora/horasPactadas por defecto) y reconstruirlo con las horas de la
/// solicitud —no las de la línea— reviviría el bug que corrigió S4-HU-14.1/#221.
/// </summary>
public record clsCandidatoTarifaDto(
    string Fuente, decimal Valor, string Detalle, string Criterio, int Coincidencias,
    string? Advertencia = null, string TablaOrigen = "SAP",
    decimal? ValorPisoBase = null, decimal? ValorTiemposLogisticos = null,
    /// <summary>#300: configuración SICE-TAC de la fila (2L2, C2, 3S3...) cuando el candidato es SICE-TAC. Permite que el
    /// tramo vacío de retorno use el MISMO vehículo del viaje cargado aunque la línea no traiga uno (contenedor).</summary>
    string? Configuracion = null,
    /// <summary>true cuando la línea pide un vehículo y la fuente no tiene filas de ese vehículo: el valor se muestra como referencia,
    /// pero no gobierna solo ni entra al recomendado.</summary>
    bool VehiculoRelajado = false);

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

/// <summary>
/// Propuesta de una línea. <paramref name="ModoDecision"/> es el camino de decisión del flete principal: "SoloSicetac" |
/// "PlazaVigenteMayor" | "ConsultaCompleta" — texto, no el número del enum de Domain (ver
/// <see cref="Nexo.Domain.Costeo.Reglas.ModoDecision"/>).
///
/// <paramref name="RetornoContenedor"/> y <paramref name="AprovisionamientoContenedor"/> son los tramos vacíos de retorno y de levante
/// del contenedor, calculados con SICE-TAC (viaje vacío, 1 h + 1 h, mismo vehículo y misma carrocería que la línea). Si la línea los
/// pide y no se encuentra el piso, <paramref name="MotivoPendiente"/> lo dice y la línea no se guarda sola.
///
/// <paramref name="MotivoPendiente"/>: motivo con el que la línea no se autocostea aunque haya un valor propuesto. null = sin motivo.
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
    /// <summary>
    /// true cuando el flete se puede guardar solo: hay un valor propuesto y ningún motivo de pendiente. Aplica a los tres caminos
    /// de decisión; sin valor (ningún candidato) o con motivo, la línea queda para que el Administrador de Tarifas la resuelva.
    /// </summary>
    public bool Autocosteable => ValorPropuesto is not null && MotivoPendiente is null;
}

public record clsPropuestaSolicitudDto(
    Guid SolicitudId,
    IReadOnlyList<clsPropuestaLineaDto> Lineas,
    IReadOnlyList<string> Advertencias);
