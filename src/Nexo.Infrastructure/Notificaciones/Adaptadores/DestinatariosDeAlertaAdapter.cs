using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

public sealed class DestinatariosDeAlertaAdapter(NexoDbContext db) : IDestinatariosDeAlertaPort
{
    public async Task<IReadOnlyList<string>> EmailsPorRolAsync(RolUsuario rol, CancellationToken ct = default) =>
        await db.Usuarios.AsNoTracking()
            .Where(u => u.Rol == rol && u.Activo)
            .Select(u => u.Email)
            .ToListAsync(ct);
}
