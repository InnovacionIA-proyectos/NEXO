using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Asistente;

public sealed class clsEfConversacionesAsistenteRepository(clsNexoDbContext db, TimeProvider reloj) : IConversacionesAsistenteRepository
{
    public Task<clsConversacionAsistente?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        db.ConversacionesAsistente.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<clsMensajeAsistente>> MensajesAsync(Guid conversacionId, CancellationToken ct = default) =>
        await db.MensajesAsistente.AsNoTracking().Where(m => m.ConversacionId == conversacionId).OrderBy(m => m.Orden).ToListAsync(ct);

    public async Task<IReadOnlyList<clsUsoHerramientaAsistente>> HerramientasAsync(Guid conversacionId, CancellationToken ct = default) =>
        await db.UsosHerramientaAsistente.AsNoTracking().Where(h => h.ConversacionId == conversacionId).OrderBy(h => h.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<clsConversacionAsistente>> ListarDeUsuarioAsync(Guid usuarioId, int top, CancellationToken ct = default) =>
        await db.ConversacionesAsistente.AsNoTracking().Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.UpdatedAt).Take(top).ToListAsync(ct);

    public async Task<clsConversacionAsistente> CrearAsync(Guid usuarioId, string titulo, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var conversacion = new clsConversacionAsistente { UsuarioId = usuarioId, Titulo = titulo, CreatedAt = ahora, UpdatedAt = ahora };
        db.ConversacionesAsistente.Add(conversacion);
        await db.SaveChangesAsync(ct);
        return conversacion;
    }

    public async Task<Guid> IniciarTurnoAsync(Guid conversacionId, string mensajeUsuario, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var conversacion = await db.ConversacionesAsistente.FirstAsync(c => c.Id == conversacionId, ct);
        var siguiente = (await db.MensajesAsistente.Where(m => m.ConversacionId == conversacionId).MaxAsync(m => (int?)m.Orden, ct) ?? 0) + 1;

        db.MensajesAsistente.Add(new clsMensajeAsistente
        {
            ConversacionId = conversacionId, Orden = siguiente, Rol = clsMensajeAsistente.RolUsuario,
            Contenido = mensajeUsuario, Estado = clsMensajeAsistente.EstadoCompleto, CreatedAt = ahora,
        });
        var respuesta = new clsMensajeAsistente
        {
            ConversacionId = conversacionId, Orden = siguiente + 1, Rol = clsMensajeAsistente.RolAsistente,
            Contenido = string.Empty, Estado = clsMensajeAsistente.EstadoEnCurso, CreatedAt = ahora,
        };
        db.MensajesAsistente.Add(respuesta);

        conversacion.Turnos++;
        conversacion.UpdatedAt = ahora;
        await db.SaveChangesAsync(ct);
        return respuesta.Id;
    }

    public async Task FinalizarMensajeAsync(Guid mensajeId, string contenido, string estado, CancellationToken ct = default)
    {
        var mensaje = await db.MensajesAsistente.FirstAsync(m => m.Id == mensajeId, ct);
        mensaje.Contenido = contenido;
        mensaje.Estado = estado;
        await db.SaveChangesAsync(ct);
    }

    public async Task RegistrarHerramientaAsync(clsUsoHerramientaAsistente uso, CancellationToken ct = default)
    {
        uso.CreatedAt = reloj.GetUtcNow();
        db.UsosHerramientaAsistente.Add(uso);
        await db.SaveChangesAsync(ct);
    }

    public async Task AgregarMensajeAsistenteAsync(Guid conversacionId, string contenido, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var siguiente = (await db.MensajesAsistente.Where(m => m.ConversacionId == conversacionId).MaxAsync(m => (int?)m.Orden, ct) ?? 0) + 1;
        db.MensajesAsistente.Add(new clsMensajeAsistente
        {
            ConversacionId = conversacionId, Orden = siguiente, Rol = clsMensajeAsistente.RolAsistente,
            Contenido = contenido, Estado = clsMensajeAsistente.EstadoCompleto, CreatedAt = ahora,
        });
        var conversacion = await db.ConversacionesAsistente.FirstAsync(c => c.Id == conversacionId, ct);
        conversacion.UpdatedAt = ahora;
        await db.SaveChangesAsync(ct);
    }

    public async Task<clsAccionAsistente> CrearAccionAsync(clsAccionAsistente accion, CancellationToken ct = default)
    {
        db.AccionesAsistente.Add(accion);
        await db.SaveChangesAsync(ct);
        return accion;
    }

    public Task<clsAccionAsistente?> ObtenerAccionAsync(Guid id, CancellationToken ct = default) =>
        db.AccionesAsistente.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<clsAccionAsistente>> AccionesAsync(Guid conversacionId, CancellationToken ct = default) =>
        await db.AccionesAsistente.AsNoTracking().Where(a => a.ConversacionId == conversacionId).OrderBy(a => a.CreatedAt).ToListAsync(ct);

    public Task<int> ContarAccionesDelMensajeAsync(Guid mensajeId, CancellationToken ct = default) =>
        db.AccionesAsistente.CountAsync(a => a.MensajeId == mensajeId, ct);

    public async Task<bool> TomarAccionAsync(Guid id, DateTimeOffset ahora, CancellationToken ct = default) =>
        await db.AccionesAsistente
            .Where(a => a.Id == id && a.Estado == clsAccionAsistente.EstadoPendiente && a.VenceAt > ahora)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, clsAccionAsistente.EstadoEjecutando), ct) == 1;

    public async Task<bool> CancelarAccionAsync(Guid id, DateTimeOffset ahora, CancellationToken ct = default) =>
        await db.AccionesAsistente
            .Where(a => a.Id == id && a.Estado == clsAccionAsistente.EstadoPendiente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Estado, clsAccionAsistente.EstadoCancelada)
                .SetProperty(a => a.ResueltaAt, ahora), ct) == 1;

    public async Task ResolverAccionAsync(Guid id, string estado, string? codigo, string? mensaje, DateTimeOffset ahora, CancellationToken ct = default)
    {
        var recortado = mensaje is { Length: > 500 } ? mensaje[..500] : mensaje;
        await db.AccionesAsistente.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.Estado, estado)
            .SetProperty(a => a.ResultadoCodigo, codigo)
            .SetProperty(a => a.ResultadoMensaje, recortado)
            .SetProperty(a => a.ResueltaAt, ahora), ct);
    }
}
