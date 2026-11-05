using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Trabajos.DTOs;

/// <summary>Avance de un trabajo; <c>Pendientes</c> es la suma de los dos tipos desglosados (reintentando vs. esperando al Administrador de Tarifas), que la pantalla explica distinto.</summary>
public record clsAvanceTrabajoDto(
    int? Total,
    int Resueltas,
    int Pendientes,
    int PendientesTecnicas,
    int RequierenTarifaManual);

/// <summary>Una línea que quedó sin costear, con su tipo y motivo (si se resuelve sola o necesita que alguien actúe).</summary>
public record clsPendienteDeCosteoDto(
    Guid LineaId, TipoPendienteDeCosteo Tipo, string Motivo, DateTimeOffset PendienteDesde);

/// <summary>
/// Estado consultable de un trabajo. <c>Secuencia</c> permite reconectar: el cliente ignora avisos con número menor o igual al de su consulta,
/// para que un aviso rezagado no pise datos nuevos con viejos.
/// </summary>
public record clsEstadoTrabajoDto(
    Guid TrabajoId,
    string Estado,
    clsAvanceTrabajoDto Avance,
    DateTimeOffset SolicitadoEn,
    DateTimeOffset? TerminadoEn,
    IReadOnlyList<clsPendienteDeCosteoDto> Pendientes,
    int Secuencia);

/// <summary>
/// Aviso del canal en vivo: el estado SIN pendientes (se construye desde <see cref="clsEstadoTrabajoDto"/> para que los campos compartidos no diverjan).
/// No lleva pendientes porque se avisa por cada línea y el detalle pesaría megabytes por pliego; la consulta lo trae cuando se necesita.
/// <c>Secuencia</c> solo crece: dos avisos concurrentes pueden llegar al revés y el cliente descarta los de número menor o igual al último pintado.
/// </summary>
public record clsAvisoDeTrabajo(
    Guid TrabajoId,
    string Estado,
    clsAvanceTrabajoDto Avance,
    DateTimeOffset? TerminadoEn,
    int Secuencia)
{
    public static clsAvisoDeTrabajo Desde(clsEstadoTrabajoDto estado) =>
        new(estado.TrabajoId, estado.Estado, estado.Avance, estado.TerminadoEn, estado.Secuencia);
}
