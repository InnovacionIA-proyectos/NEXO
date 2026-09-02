using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;

/// <summary>Respuesta del 202: el patrón de trabajos pesados del contrato de
/// integración, aplicado a la cubicación (S2-HU-08.1).</summary>
public record SolicitudDeCubicacionAceptadaDto(Guid PlanCargueId, string Estado, string ConsultarEn);

/// <summary>
/// Lo que el Cubicador (microservicio Python) necesita de una línea de solicitud para
/// armar el plan de cargue 3D — puente Mesa de Costeo → Cubicador (crear-desde-línea,
/// ver cubicador/CONTRATO.md sección 7). Ruta puede venir sin resolver (Origen/Destino
/// nulos) si la línea no tiene ruta asignada.
/// </summary>
public record LineaParaCubicacionDto(
    Guid LineaId,
    IReadOnlyList<PosicionCargaDto> Posiciones,
    string? Origen,
    string? Destino);

/// <summary>Consulta de respaldo: reconstruye el estado completo sin haber recibido
/// ningún aviso — igual criterio que GET /api/trabajos/{id}.</summary>
public record PlanCargueDto(
    Guid Id,
    string Nombre,
    string Estado,
    string Estrategia,
    string Algoritmo,
    decimal Puntaje,
    decimal PesoTotalKg,
    decimal VolumenUtilizadoM3,
    decimal UtilizacionVolumen,
    decimal UtilizacionPiso,
    int TotalItems,
    int TotalColocaciones,
    DateTimeOffset UpdatedAt);

/// <summary>Lo que viaja por el canal de avisos cuando un plan cambia de estado. Sin la
/// lista de colocaciones a propósito (puede haber cientos): el aviso dice que hay
/// desenlace, la consulta trae el detalle — mismo reparto que /hubs/trabajos.</summary>
public record AvisoPlanCargueDto(Guid PlanCargueId, string Estado, decimal Puntaje, string? Motivo = null)
{
    public static AvisoPlanCargueDto Desde(PlanCargueDto p) => new(p.Id, p.Estado, p.Puntaje);
}
