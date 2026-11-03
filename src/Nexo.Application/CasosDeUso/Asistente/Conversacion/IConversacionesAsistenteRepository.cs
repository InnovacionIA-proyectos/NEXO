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

    /// <summary>Agrega al final de la conversación un mensaje del asistente ya completo (por ejemplo el resultado de una acción confirmada).</summary>
    Task AgregarMensajeAsistenteAsync(Guid conversacionId, string contenido, CancellationToken ct = default);

    Task<clsAccionAsistente> CrearAccionAsync(clsAccionAsistente accion, CancellationToken ct = default);

    Task<clsAccionAsistente?> ObtenerAccionAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<clsAccionAsistente>> AccionesAsync(Guid conversacionId, CancellationToken ct = default);

    Task<int> ContarAccionesDelMensajeAsync(Guid mensajeId, CancellationToken ct = default);

    /// <summary>
    /// Pasa la acción de <c>pendiente</c> a <c>ejecutando</c> en un solo paso atómico y solo si no ha vencido: dos confirmaciones
    /// simultáneas no pueden aplicar el cambio dos veces. Devuelve false si la acción ya no estaba pendiente o venció.
    /// </summary>
    Task<bool> TomarAccionAsync(Guid id, DateTimeOffset ahora, CancellationToken ct = default);

    /// <summary>Pasa la acción de <c>pendiente</c> a <c>cancelada</c> (atómico). False si ya no estaba pendiente.</summary>
    Task<bool> CancelarAccionAsync(Guid id, DateTimeOffset ahora, CancellationToken ct = default);

    Task ResolverAccionAsync(Guid id, string estado, string? codigo, string? mensaje, DateTimeOffset ahora, CancellationToken ct = default);
}
