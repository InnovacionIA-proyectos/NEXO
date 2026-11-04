namespace Nexo.Application.Common.Ports;

/// <summary>
/// Lectura de SOLO LECTURA de usuarios y permisos de la administración. Ante una falla de conexión devuelve null (no lanza),
/// para tratarla como identificador inexistente (401) y no exponer detalle interno con un 500.
/// </summary>
public interface IIdentidadPortalPort
{
    Task<PersonaPortalDto?> ObtenerPersonaConRolesAsync(long id, CancellationToken ct = default);
}

public readonly record struct RolPortalDto(long Id, string Descripcion);

public readonly record struct PersonaPortalDto(
    long Id, string Username, string? Email, string? NombreCompleto, IReadOnlyList<RolPortalDto> Roles);
