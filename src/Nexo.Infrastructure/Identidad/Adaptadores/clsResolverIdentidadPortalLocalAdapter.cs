using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Identidad.Adaptadores;

/// <summary>Solo Desarrollo/QA: se registra únicamente sin ConnectionStrings:IdentidadPortal (clsProgram.cs impide arrancar así en Producción).
/// No confía en nada que declare el cliente: resuelve el rol contra un registro REAL de la tabla Usuarios local;
/// para probar otro rol se edita esa fila, no hay encabezado que lo declare.</summary>
public sealed class clsResolverIdentidadPortalLocalAdapter(clsNexoDbContext db) : IResolverIdentidadPortalPort
{
    public async Task<clsIdentidadResueltaDto?> ResolverAsync(long idPortal, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.IdentidadPortalId == idPortal, ct);
        return usuario is null ? null : new clsIdentidadResueltaDto(usuario.Id, usuario.Nombre, usuario.Rol);
    }
}
