using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>Las conversaciones de quien consulta, de la más reciente a la más antigua.</summary>
public sealed record clsListarConversacionesAsistenteQuery(int Top = 50) : IQuery<IReadOnlyList<clsConversacionResumenDto>>;

public sealed class clsListarConversacionesAsistenteQueryHandler(IConversacionesAsistenteRepository conversaciones, ICurrentUserPort usuario)
    : IQueryHandler<clsListarConversacionesAsistenteQuery, IReadOnlyList<clsConversacionResumenDto>>
{
    public async Task<IReadOnlyList<clsConversacionResumenDto>> Handle(clsListarConversacionesAsistenteQuery query, CancellationToken ct = default) =>
        (await conversaciones.ListarDeUsuarioAsync(usuario.Id, Math.Clamp(query.Top, 1, 200), ct))
        .Select(c => new clsConversacionResumenDto(c.Id, c.Titulo, c.Turnos, c.CreatedAt, c.UpdatedAt))
        .ToList();
}

/// <summary>
/// Una conversación completa con las herramientas que se usaron en cada respuesta. La ve su dueño y el Admin (que puede auditarla);
/// el Admin no escribe en conversaciones ajenas.
/// </summary>
public sealed record clsObtenerConversacionAsistenteQuery(Guid Id) : IQuery<clsConversacionDetalleDto>;

public sealed class clsObtenerConversacionAsistenteQueryHandler(IConversacionesAsistenteRepository conversaciones, ICurrentUserPort usuario)
    : IQueryHandler<clsObtenerConversacionAsistenteQuery, clsConversacionDetalleDto>
{
    public async Task<clsConversacionDetalleDto> Handle(clsObtenerConversacionAsistenteQuery query, CancellationToken ct = default)
    {
        var c = await conversaciones.ObtenerAsync(query.Id, ct)
            ?? throw new clsNotFoundException("CONVERSACION_NO_ENCONTRADA", "La conversación no existe.");

        if (c.UsuarioId != usuario.Id && usuario.Rol != RolUsuario.Admin)
            throw new clsForbiddenException("CONVERSACION_AJENA", "Esa conversación es de otra persona.");

        var mensajes = await conversaciones.MensajesAsync(c.Id, ct);
        var herramientas = await conversaciones.HerramientasAsync(c.Id, ct);
        var acciones = await conversaciones.AccionesAsync(c.Id, ct);

        return new clsConversacionDetalleDto(
            c.Id, c.Titulo, c.Turnos, c.UsuarioId, c.CreatedAt, c.UpdatedAt,
            mensajes.Select(m => new clsMensajeAsistenteDto(m.Id, m.Orden, m.Rol, m.Contenido, m.Estado, m.CreatedAt)).ToList(),
            herramientas.Select(h => new clsUsoHerramientaDto(
                h.MensajeId, h.Herramienta, h.Entrada, h.Encontrado, h.Fuente, h.Resultado, h.LatenciaMs, h.CreatedAt)).ToList(),
            acciones.Select(clsAccionAsistenteDto.De).ToList());
    }
}
