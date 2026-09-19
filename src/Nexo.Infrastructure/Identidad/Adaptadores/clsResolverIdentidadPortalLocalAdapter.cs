using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Identidad.Adaptadores;

/// <summary>
/// Solo para Desarrollo/QA sin acceso todavía a la base de identidad real del portal.
/// Se registra ÚNICAMENTE cuando ConnectionStrings:clsIdentidadPortal no está configurada
/// (clsProgram.cs impide arrancar así en Producción — fail-closed). No es un
/// esquema de rol simulado: no confía en nada que declare el cliente, resuelve el rol
/// contra un registro REAL ya sembrado en la base local (mismo invariante de seguridad
/// que el adapter real, solo que la fuente de verdad es la tabla Usuarios local en vez
/// de la del portal). Para probar como otro rol, un desarrollador edita/siembra esa fila
/// — no hay forma de declarar el rol por encabezado.
/// </summary>
public sealed class clsResolverIdentidadPortalLocalAdapter(clsNexoDbContext db) : IResolverIdentidadPortalPort
{
    public async Task<clsIdentidadResueltaDto?> ResolverAsync(long idPortal, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.IdentidadPortalId == idPortal, ct);
        return usuario is null ? null : new clsIdentidadResueltaDto(usuario.Id, usuario.Nombre, usuario.Rol);
    }
}
