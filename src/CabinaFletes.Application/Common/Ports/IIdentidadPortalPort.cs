namespace CabinaFletes.Application.Common.Ports;

/// <summary>
/// Lectura de SOLO LECTURA contra la tabla de usuarios y permisos de la administración
/// (docs/contrato-integracion/README.md, sección 7: "el único punto de contacto es la
/// lectura de la tabla de usuarios y permisos... es de solo lectura"). Nunca lanza ante
/// una falla de conexión: la traduce a null, para que quien resuelve la identidad la
/// trate igual que un identificador inexistente (401) en vez de un 500 que exponga
/// detalle interno.
/// </summary>
public interface IIdentidadPortalPort
{
    Task<PersonaPortalDto?> ObtenerPersonaConRolesAsync(long id, CancellationToken ct = default);
}

public readonly record struct RolPortalDto(long Id, string Descripcion);

public readonly record struct PersonaPortalDto(
    long Id, string Username, string? Email, string? NombreCompleto, IReadOnlyList<RolPortalDto> Roles);
