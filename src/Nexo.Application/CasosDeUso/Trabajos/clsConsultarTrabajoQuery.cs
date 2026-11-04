using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Trabajos;

/// <summary>Estado de un trabajo por identificador: la consulta de respaldo que reconstruye todo si el aviso en vivo (SignalR) se perdió.</summary>
public sealed record clsConsultarTrabajoQuery(Guid TrabajoId) : IQuery<clsEstadoTrabajoDto>;

public sealed class clsConsultarTrabajoQueryHandler(IEstadoDeTrabajoPort trabajos)
    : IQueryHandler<clsConsultarTrabajoQuery, clsEstadoTrabajoDto>
{
    public async Task<clsEstadoTrabajoDto> Handle(clsConsultarTrabajoQuery query, CancellationToken ct = default) =>
        await trabajos.ObtenerAsync(query.TrabajoId, ct)
        ?? throw new clsNotFoundException("TRABAJO_NO_ENCONTRADO", "No existe un trabajo con ese identificador.");
}
