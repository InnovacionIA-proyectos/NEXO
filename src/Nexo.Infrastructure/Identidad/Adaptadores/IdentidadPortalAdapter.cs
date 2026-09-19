using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Identidad.Adaptadores;

/// <summary>
/// Adapter de solo lectura contra la base de identidad real del portal. Nunca lanza:
/// una falla de conexión se traduce en null, igual que "el id no existe" — quien resuelve
/// la identidad no puede distinguir (ni le sirve distinguir) entre "no existe" y "la
/// fuente no respondió", porque en los dos casos la respuesta correcta es fail-closed
/// (401), no un 500 que exponga que hay una base externa caída.
/// </summary>
public sealed class IdentidadPortalAdapter(IdentidadPortalDbContext db, ILogger<IdentidadPortalAdapter> logger)
    : IIdentidadPortalPort
{
    public async Task<PersonaPortalDto?> ObtenerPersonaConRolesAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var persona = await db.UsuariosApp.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (persona is null) return null;

            var roles = await (
                from ur in db.UsuarioRoles
                join r in db.Roles on ur.IdRoles equals r.IdRoles
                where ur.IdUsuario == id
                select new RolPortalDto(r.IdRoles, r.Descripcion)
            ).ToListAsync(ct);

            return new PersonaPortalDto(persona.Id, persona.NombreUsuario, persona.Email, persona.DescripcionUsuario, roles);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo consultar la identidad del portal para el id {IdPortal}", id);
            return null;
        }
    }
}
