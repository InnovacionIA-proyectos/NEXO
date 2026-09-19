using Nexo.Domain.Enums;

namespace Nexo.Application.Common.Ports;

/// <summary>
/// Orquesta la resolución de sesión que exige S2-HU-03.1/03.2 en un solo puerto, para
/// que el esquema de autenticación (Api) no conozca ni la tabla externa ni la de mapeo
/// de roles. Se llama en CADA petición, sin caché de sesión (docs/contrato-integracion,
/// D5/ADR-001): la persona pudo perder el permiso desde la última vez.
/// </summary>
public interface IResolverIdentidadPortalPort
{
    Task<clsIdentidadResueltaDto?> ResolverAsync(long idPortal, CancellationToken ct = default);
}

/// <summary>
/// Resultado de resolver un id del portal. <paramref name="Rol"/> null distingue "existe
/// en la tabla pero sin permiso sobre Nexo" (403, <paramref name="UsuarioId"/>
/// también null porque no se aprovisiona cuenta local para quien nunca entra) de "no se
/// pudo resolver identidad alguna" (401, cuando el método devuelve null en vez de esta
/// clase).
/// </summary>
public sealed record clsIdentidadResueltaDto(Guid? UsuarioId, string Nombre, RolUsuario? Rol);
