using Nexo.Application.Common.Ports;
using Nexo.Domain.Auditoria.Entidades;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Common;

/// <summary>Usa el mismo DbContext del caso de uso: auditar y persistir el cambio son una sola operación atómica.</summary>
public sealed class clsEfAuditoriaRepository(clsNexoDbContext db) : IAuditoriaPort
{
    public void Registrar(Guid? usuarioId, string entidad, Guid entidadId, string accion, string diff) =>
        db.AuditLogs.Add(new clsAuditLog { UsuarioId = usuarioId, Entidad = entidad, EntidadId = entidadId, Accion = accion, Diff = diff });
}
