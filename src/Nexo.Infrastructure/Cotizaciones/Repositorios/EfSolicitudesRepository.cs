using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cotizaciones.Repositorios;

public sealed class EfSolicitudesRepository(NexoDbContext db) : ISolicitudesPort
{
    public Task<Solicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Solicitud?> ObtenerConClienteAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.Include(s => s.Cliente).Include(s => s.Creador).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<int> ContarLineasAsync(Guid solicitudId, CancellationToken ct) =>
        db.LineasSolicitud.CountAsync(l => l.SolicitudId == solicitudId, ct);
}
