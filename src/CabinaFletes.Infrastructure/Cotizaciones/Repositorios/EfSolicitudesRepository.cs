using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Cotizaciones.Repositorios;

public sealed class EfSolicitudesRepository(CabinaFletesDbContext db) : ISolicitudesPort
{
    public Task<Solicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Solicitud?> ObtenerConClienteAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.Include(s => s.Cliente).Include(s => s.Creador).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<int> ContarLineasAsync(Guid solicitudId, CancellationToken ct) =>
        db.LineasSolicitud.CountAsync(l => l.SolicitudId == solicitudId, ct);
}
