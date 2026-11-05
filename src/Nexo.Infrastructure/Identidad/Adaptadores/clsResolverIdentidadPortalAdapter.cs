using Nexo.Application.Common.Ports;
using Nexo.Domain.Usuarios.Entidades;
using Nexo.Domain.Usuarios.Reglas;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Identidad.Adaptadores;

/// <summary>Consulta la tabla de la administración en cada llamada (sin caché), traduce sus roles a uno de los 6 de Nexo vía
/// <see cref="clsMapeoRolPortal"/> y sincroniza la cuenta local usada como autoría de clsAuditLog.
/// Guarda de inmediato: corre en la autenticación, sin pasar por el UnitOfWork del caso de uso.</summary>
public sealed class clsResolverIdentidadPortalAdapter(IIdentidadPortalPort identidadPortal, clsNexoDbContext db)
    : IResolverIdentidadPortalPort
{
    public async Task<clsIdentidadResueltaDto?> ResolverAsync(long idPortal, CancellationToken ct = default)
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
        var rolEfectivo = clsResolucionRolPortal.ElegirRolEfectivo(candidatos);
        if (rolEfectivo is null) return new clsIdentidadResueltaDto(null, nombre, null);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.IdentidadPortalId == idPortal, ct);
        if (usuario is null)
        {
            usuario = new clsUsuario
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

        return new clsIdentidadResueltaDto(usuario.Id, usuario.Nombre, rolEfectivo);
    }

    /// <summary>Primero por id (estable mientras no cambie la base física del portal); si no matchea, respaldo por nombre.</summary>
    private async Task<Domain.Enums.RolUsuario?> ResolverRolAsync(RolPortalDto rolPortal, CancellationToken ct)
    {
        var porId = await db.MapeosRolPortal.FirstOrDefaultAsync(m => m.IdRolPortal != null && m.IdRolPortal == rolPortal.Id, ct);
        if (porId is not null) return porId.Rol;

        var porNombre = await db.MapeosRolPortal
            .FirstOrDefaultAsync(m => m.DescripcionRolPortal == rolPortal.Descripcion, ct);
        return porNombre?.Rol;
    }
}
