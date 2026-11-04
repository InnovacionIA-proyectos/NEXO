using Nexo.Domain.Enums;

namespace Nexo.Application.Common.Ports;

/// <summary>
/// Resuelve la sesión de identidad del portal. Se llama en CADA petición, sin caché de sesión: la persona pudo perder el permiso desde la última vez.
/// </summary>
public interface IResolverIdentidadPortalPort
{
    Task<clsIdentidadResueltaDto?> ResolverAsync(long idPortal, CancellationToken ct = default);
}

/// <summary>
/// <paramref name="Rol"/> null = existe pero sin permiso sobre Nexo (403, <paramref name="UsuarioId"/> también null);
/// si el método devuelve null no se resolvió identidad alguna (401).
/// </summary>
public sealed record clsIdentidadResueltaDto(Guid? UsuarioId, string Nombre, RolUsuario? Rol);
