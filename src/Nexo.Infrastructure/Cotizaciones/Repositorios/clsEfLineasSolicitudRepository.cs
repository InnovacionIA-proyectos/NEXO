using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cotizaciones.Repositorios;

public sealed class clsEfLineasSolicitudRepository(clsNexoDbContext db) : ILineasSolicitudPort
{
    public Task<clsLineaSolicitud?> ObtenerConRutaYVehiculoAsync(Guid lineaId, Guid solicitudId, CancellationToken ct) =>
        db.LineasSolicitud.AsNoTracking()
            .Include(l => l.clsRuta).ThenInclude(r => r!.OrigenMunicipio)
            .Include(l => l.clsRuta).ThenInclude(r => r!.DestinoMunicipio)
            .Include(l => l.clsConfiguracionVehiculo)
            .FirstOrDefaultAsync(l => l.Id == lineaId && l.SolicitudId == solicitudId, ct);

    public Task<clsLineaSolicitud?> ObtenerDeSolicitudAsync(Guid lineaId, Guid solicitudId, CancellationToken ct) =>
        db.LineasSolicitud.FirstOrDefaultAsync(l => l.Id == lineaId && l.SolicitudId == solicitudId, ct);

    public async Task<IReadOnlyDictionary<Guid, clsLineaSolicitud>> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
        await db.LineasSolicitud.AsNoTracking()
            .Where(l => l.SolicitudId == solicitudId)
            .ToDictionaryAsync(l => l.Id, ct);

    public Task MarcarRevisadasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        db.LineasSolicitud.Where(l => ids.Contains(l.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(l => l.EstadoLinea, EstadoLinea.Revisada), ct);
}
