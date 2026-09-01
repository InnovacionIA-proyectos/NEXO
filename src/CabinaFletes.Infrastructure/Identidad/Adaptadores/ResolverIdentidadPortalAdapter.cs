using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Usuarios.Entidades;
using CabinaFletes.Domain.Usuarios.Reglas;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Identidad.Adaptadores;

/// <summary>
/// Implementación real (Producción/QA) de S2-HU-03.1/03.2: consulta la tabla de la
/// administración en cada llamada (sin caché de sesión), traduce sus roles a uno de los
/// 6 de Nexo vía <see cref="MapeoRolPortal"/>, y mantiene sincronizada la cuenta local
/// que sirve de autoría real para AuditLog (docs/contrato-integracion, sección 1 y
/// ADR-001). Guarda inmediatamente (no espera al UnitOfWork del caso de uso): esto corre
/// en la autenticación, antes de saber si la petición es una consulta que nunca llama a
/// SaveChanges.
/// </summary>
public sealed class ResolverIdentidadPortalAdapter(IIdentidadPortalPort identidadPortal, CabinaFletesDbContext db)
    : IResolverIdentidadPortalPort
{
    public async Task<IdentidadResueltaDto?> ResolverAsync(long idPortal, CancellationToken ct = default)
    {
        var persona = await identidadPortal.ObtenerPersonaConRolesAsync(idPortal, ct);
        if (persona is null) return null;

        var candidatos = new List<Domain.Enums.RolUsuario>();
        foreach (var rolPortal in persona.Value.Roles)
        {
            var rolNexo = await ResolverRolAsync(rolPortal, ct);
            if (rolNexo is not null) candidatos.Add(rolNexo.Value);
        }

        var nombre = persona.Value.NombreCompleto ?? persona.Value.Username;
        var rolEfectivo = ResolucionRolPortal.ElegirRolEfectivo(candidatos);
        if (rolEfectivo is null) return new IdentidadResueltaDto(null, nombre, null);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.IdentidadPortalId == idPortal, ct);
        if (usuario is null)
        {
            usuario = new Usuario
            {
                IdentidadPortalId = idPortal,
                Nombre = nombre,
                Email = persona.Value.Email ?? $"{persona.Value.Username}@boterosoto.com.co",
                Rol = rolEfectivo.Value,
            };
            db.Usuarios.Add(usuario);
        }
        else if (usuario.Nombre != nombre || usuario.Rol != rolEfectivo.Value ||
                 (persona.Value.Email is not null && usuario.Email != persona.Value.Email))
        {
            usuario.Nombre = nombre;
            usuario.Rol = rolEfectivo.Value;
            if (persona.Value.Email is not null) usuario.Email = persona.Value.Email;
            usuario.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        return new IdentidadResueltaDto(usuario.Id, usuario.Nombre, rolEfectivo);
    }

    /// <summary>Primero por id (estable mientras no cambie la base física del portal);
    /// si no matchea, respaldo por nombre (Pendiente P2 en el contrato) — ver
    /// docs/contrato-integracion/README.md sección 2.</summary>
    private async Task<Domain.Enums.RolUsuario?> ResolverRolAsync(RolPortalDto rolPortal, CancellationToken ct)
    {
        var porId = await db.MapeosRolPortal.FirstOrDefaultAsync(m => m.IdRolPortal != null && m.IdRolPortal == rolPortal.Id, ct);
        if (porId is not null) return porId.Rol;

        var porNombre = await db.MapeosRolPortal
            .FirstOrDefaultAsync(m => m.DescripcionRolPortal == rolPortal.Descripcion, ct);
        return porNombre?.Rol;
    }
}
