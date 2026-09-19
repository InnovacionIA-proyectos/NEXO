using System.Linq.Expressions;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cotizaciones.Repositorios;

public sealed class EfCotizacionRepository(NexoDbContext db) : ICotizacionRepository
{
    public Task<Cotizacion?> ObtenerConLineasYConexosAsync(Guid id, CancellationToken ct) =>
        ObtenerConLineasYConexosAsync(x => x.Id == id, ct);

    public Task<Cotizacion?> ObtenerConLineasYConexosAsync(Expression<Func<Cotizacion, bool>> filtro, CancellationToken ct) =>
        db.Cotizaciones
            .Include(c => c.Lineas)
            .Include(c => c.Conexos).ThenInclude(x => x.ServicioConexo)
            .FirstOrDefaultAsync(filtro, ct);

    public Task<Cotizacion?> ObtenerParaMarcarResultadoAsync(Guid id, CancellationToken ct) =>
        db.Cotizaciones
            .Include(x => x.Lineas).ThenInclude(l => l.LineaSolicitud)
            .Include(x => x.Conexos).ThenInclude(x => x.ServicioConexo)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Cotizacion?> ObtenerParaExportarAsync(Expression<Func<Cotizacion, bool>> filtro, CancellationToken ct) =>
        db.Cotizaciones.AsNoTracking()
            .Include(x => x.Lineas).ThenInclude(l => l.LineaSolicitud).ThenInclude(ls => ls!.Ruta)
            .Include(x => x.Conexos).ThenInclude(x => x.ServicioConexo)
            .Include(x => x.Solicitud).ThenInclude(s => s!.Cliente)
            .FirstOrDefaultAsync(filtro, ct);

    public async Task<Guid?> BuscarIdConContenidoPorSolicitudAsync(Guid solicitudId, CancellationToken ct)
    {
        var id = await db.Cotizaciones.AsNoTracking()
            .Where(x => x.SolicitudId == solicitudId && (x.Lineas.Any() || x.Conexos.Any()))
            .OrderByDescending(x => x.Estado == Domain.Enums.EstadoCotizacion.Entregada)
            .ThenByDescending(x => x.EntregadaAt)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);
        return id == Guid.Empty ? null : id;
    }

    public void Agregar(Cotizacion cotizacion) => db.Cotizaciones.Add(cotizacion);
}
