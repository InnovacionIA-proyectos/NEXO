using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Auditoria.Entidades;
using CabinaFletes.Infrastructure.Persistence;

namespace CabinaFletes.Infrastructure.Repositories;

/// <summary>
/// Registra en el MISMO DbContext que el resto del caso de uso (comparte el
/// SaveChangesAsync/transacción de la Unit of Work): auditar y persistir el
/// cambio de negocio son, a propósito, una sola operación atómica.
/// </summary>
public sealed class EfAuditoriaRepository(CabinaFletesDbContext db) : IAuditoriaPort
{
    public void Registrar(Guid? usuarioId, string entidad, Guid entidadId, string accion, string diff) =>
        db.AuditLogs.Add(new AuditLog { UsuarioId = usuarioId, Entidad = entidad, EntidadId = entidadId, Accion = accion, Diff = diff });
}
