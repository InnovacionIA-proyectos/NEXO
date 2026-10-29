using Nexo.Domain.Asistente.Entidades;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>Persistencia de las conversaciones del asistente y de la auditoría de sus herramientas.</summary>
public interface IConversacionesAsistenteRepository
{
    Task<clsConversacionAsistente?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Los mensajes de la conversación, en orden.</summary>
    Task<IReadOnlyList<clsMensajeAsistente>> MensajesAsync(Guid conversacionId, CancellationToken ct = default);

    Task<IReadOnlyList<clsUsoHerramientaAsistente>> HerramientasAsync(Guid conversacionId, CancellationToken ct = default);

    /// <summary>Las conversaciones de una persona, de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<clsConversacionAsistente>> ListarDeUsuarioAsync(Guid usuarioId, int top, CancellationToken ct = default);

    Task<clsConversacionAsistente> CrearAsync(Guid usuarioId, string titulo, CancellationToken ct = default);

    /// <summary>
    /// Agrega el mensaje del usuario y una respuesta del asistente <c>en_curso</c> (vacía), y suma un turno. Devuelve el id de la
    /// respuesta, al que se enlazan las herramientas que use.
    /// </summary>
    Task<Guid> IniciarTurnoAsync(Guid conversacionId, string mensajeUsuario, CancellationToken ct = default);

    Task FinalizarMensajeAsync(Guid mensajeId, string contenido, string estado, CancellationToken ct = default);

    Task RegistrarHerramientaAsync(clsUsoHerramientaAsistente uso, CancellationToken ct = default);
}
