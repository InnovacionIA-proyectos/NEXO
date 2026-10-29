using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Domain.Asistente.Entidades;

namespace Nexo.Application.Tests.Asistente;

/// <summary>El repositorio de conversaciones del asistente en memoria, para probar los casos de uso sin base.</summary>
public sealed class clsRepositorioConversacionesEnMemoria : IConversacionesAsistenteRepository
{
    public List<clsConversacionAsistente> Conversaciones { get; } = [];
    public List<clsMensajeAsistente> Mensajes { get; } = [];
    public List<clsUsoHerramientaAsistente> Herramientas { get; } = [];
    public List<clsAccionAsistente> Acciones { get; } = [];

    public Task<clsConversacionAsistente?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Conversaciones.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<clsMensajeAsistente>> MensajesAsync(Guid conversacionId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<clsMensajeAsistente>>(Mensajes.Where(m => m.ConversacionId == conversacionId).OrderBy(m => m.Orden).ToList());

    public Task<IReadOnlyList<clsUsoHerramientaAsistente>> HerramientasAsync(Guid conversacionId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<clsUsoHerramientaAsistente>>(Herramientas.Where(h => h.ConversacionId == conversacionId).ToList());

    public Task<IReadOnlyList<clsConversacionAsistente>> ListarDeUsuarioAsync(Guid usuarioId, int top, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<clsConversacionAsistente>>(Conversaciones.Where(c => c.UsuarioId == usuarioId).Take(top).ToList());

    public Task<clsConversacionAsistente> CrearAsync(Guid usuarioId, string titulo, CancellationToken ct = default)
    {
        var c = new clsConversacionAsistente { UsuarioId = usuarioId, Titulo = titulo };
        Conversaciones.Add(c);
        return Task.FromResult(c);
    }

    public Task<Guid> IniciarTurnoAsync(Guid conversacionId, string mensajeUsuario, CancellationToken ct = default)
    {
        var siguiente = Mensajes.Count(m => m.ConversacionId == conversacionId) + 1;
        Mensajes.Add(new clsMensajeAsistente { ConversacionId = conversacionId, Orden = siguiente, Rol = clsMensajeAsistente.RolUsuario, Contenido = mensajeUsuario });
        var respuesta = new clsMensajeAsistente
        {
            ConversacionId = conversacionId, Orden = siguiente + 1, Rol = clsMensajeAsistente.RolAsistente, Estado = clsMensajeAsistente.EstadoEnCurso,
        };
        Mensajes.Add(respuesta);
        Conversaciones.First(c => c.Id == conversacionId).Turnos++;
        return Task.FromResult(respuesta.Id);
    }

    public Task FinalizarMensajeAsync(Guid mensajeId, string contenido, string estado, CancellationToken ct = default)
    {
        var m = Mensajes.First(x => x.Id == mensajeId);
        m.Contenido = contenido;
        m.Estado = estado;
        return Task.CompletedTask;
    }

    public Task RegistrarHerramientaAsync(clsUsoHerramientaAsistente uso, CancellationToken ct = default)
    {
        Herramientas.Add(uso);
        return Task.CompletedTask;
    }

    public Task AgregarMensajeAsistenteAsync(Guid conversacionId, string contenido, CancellationToken ct = default)
    {
        Mensajes.Add(new clsMensajeAsistente
        {
            ConversacionId = conversacionId, Orden = Mensajes.Count(m => m.ConversacionId == conversacionId) + 1,
            Rol = clsMensajeAsistente.RolAsistente, Contenido = contenido, Estado = clsMensajeAsistente.EstadoCompleto,
        });
        return Task.CompletedTask;
    }

    public Task<clsAccionAsistente> CrearAccionAsync(clsAccionAsistente accion, CancellationToken ct = default)
    {
        Acciones.Add(accion);
        return Task.FromResult(accion);
    }

    public Task<clsAccionAsistente?> ObtenerAccionAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Acciones.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<clsAccionAsistente>> AccionesAsync(Guid conversacionId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<clsAccionAsistente>>(Acciones.Where(a => a.ConversacionId == conversacionId).ToList());

    public Task<int> ContarAccionesDelMensajeAsync(Guid mensajeId, CancellationToken ct = default) =>
        Task.FromResult(Acciones.Count(a => a.MensajeId == mensajeId));

    public Task<bool> TomarAccionAsync(Guid id, DateTimeOffset ahora, CancellationToken ct = default)
    {
        var a = Acciones.FirstOrDefault(x => x.Id == id);
        if (a is null || a.Estado != clsAccionAsistente.EstadoPendiente || a.VenceAt <= ahora) return Task.FromResult(false);
        a.Estado = clsAccionAsistente.EstadoEjecutando;
        return Task.FromResult(true);
    }

    public Task<bool> CancelarAccionAsync(Guid id, DateTimeOffset ahora, CancellationToken ct = default)
    {
        var a = Acciones.FirstOrDefault(x => x.Id == id);
        if (a is null || a.Estado != clsAccionAsistente.EstadoPendiente) return Task.FromResult(false);
        a.Estado = clsAccionAsistente.EstadoCancelada;
        a.ResueltaAt = ahora;
        return Task.FromResult(true);
    }

    public Task ResolverAccionAsync(Guid id, string estado, string? codigo, string? mensaje, DateTimeOffset ahora, CancellationToken ct = default)
    {
        var a = Acciones.First(x => x.Id == id);
        a.Estado = estado;
        a.ResultadoCodigo = codigo;
        a.ResultadoMensaje = mensaje;
        a.ResueltaAt = ahora;
        return Task.CompletedTask;
    }
}
