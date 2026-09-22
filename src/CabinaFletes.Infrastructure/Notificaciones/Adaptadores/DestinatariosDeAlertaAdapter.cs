using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Notificaciones.Adaptadores;

public sealed class DestinatariosDeAlertaAdapter(CabinaFletesDbContext db) : IDestinatariosDeAlertaPort
{
    public async Task<IReadOnlyList<string>> EmailsPorRolAsync(RolUsuario rol, CancellationToken ct = default) =>
        await db.Usuarios.AsNoTracking()
            .Where(u => u.Rol == rol && u.Activo)
            .Select(u => u.Email)
            .ToListAsync(ct);
}
