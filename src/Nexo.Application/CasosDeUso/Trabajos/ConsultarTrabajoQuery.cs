using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Trabajos;

/// <summary>
/// Estado de un trabajo por su identificador: la consulta de respaldo del contrato.
///
/// El aviso en vivo por SignalR es una mejora; esto es la garantía. Un cliente que
/// perdió la conexión, cambió de pestaña o recargó reconstruye todo con esta llamada
/// sin haber recibido un solo aviso.
/// </summary>
public sealed record ConsultarTrabajoQuery(Guid TrabajoId) : IQuery<EstadoTrabajoDto>;

public sealed class ConsultarTrabajoQueryHandler(IEstadoDeTrabajoPort trabajos)
    : IQueryHandler<ConsultarTrabajoQuery, EstadoTrabajoDto>
{
    public async Task<EstadoTrabajoDto> Handle(ConsultarTrabajoQuery query, CancellationToken ct = default) =>
        await trabajos.ObtenerAsync(query.TrabajoId, ct)
        ?? throw new NotFoundException("TRABAJO_NO_ENCONTRADO", "No existe un trabajo con ese identificador.");
}
