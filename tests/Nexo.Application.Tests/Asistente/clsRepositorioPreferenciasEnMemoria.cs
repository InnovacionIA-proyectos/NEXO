using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Domain.Asistente.Entidades;

namespace Nexo.Application.Tests.Asistente;

/// <summary>El repositorio de preferencias del asistente en memoria, para probar los casos de uso sin base.</summary>
public sealed class clsRepositorioPreferenciasEnMemoria : IPreferenciasAsistenteRepository
{
    public List<clsPreferenciaAsistente> Preferencias { get; } = [];

    public Task<IReadOnlyList<clsPreferenciaAsistente>> ListarAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<clsPreferenciaAsistente>>(Preferencias.Where(p => p.UsuarioId == usuarioId).OrderBy(p => p.CreatedAt).ToList());

    public Task<clsPreferenciaAsistente> CrearAsync(clsPreferenciaAsistente preferencia, CancellationToken ct = default)
    {
        preferencia.CreatedAt = DateTimeOffset.UtcNow.AddMilliseconds(Preferencias.Count);
        Preferencias.Add(preferencia);
        return Task.FromResult(preferencia);
    }

    public Task<bool> EliminarAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult(Preferencias.RemoveAll(p => p.Id == id && p.UsuarioId == usuarioId) == 1);
}
