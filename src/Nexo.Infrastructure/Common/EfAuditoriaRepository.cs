using Nexo.Application.Common.Ports;
using Nexo.Domain.Auditoria.Entidades;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Common;

/// <summary>
/// Registra en el MISMO DbContext que el resto del caso de uso (comparte el
/// SaveChangesAsync/transacción de la Unit of Work): auditar y persistir el
/// cambio de negocio son, a propósito, una sola operación atómica.
/// </summary>
public sealed class EfAuditoriaRepository(NexoDbContext db) : IAuditoriaPort
{
    public void Registrar(Guid? usuarioId, string entidad, Guid entidadId, string accion, string diff) =>
        db.AuditLogs.Add(new AuditLog { UsuarioId = usuarioId, Entidad = entidad, EntidadId = entidadId, Accion = accion, Diff = diff });
}
