using System.Linq.Expressions;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cotizaciones.Repositorios;

public sealed class clsEfCotizacionRepository(clsNexoDbContext db) : ICotizacionRepository
{
    public Task<clsCotizacion?> ObtenerConLineasYConexosAsync(Guid id, CancellationToken ct) =>
        ObtenerConLineasYConexosAsync(x => x.Id == id, ct);

    public Task<clsCotizacion?> ObtenerConLineasYConexosAsync(Expression<Func<clsCotizacion, bool>> filtro, CancellationToken ct) =>
        db.Cotizaciones
            .Include(c => c.Lineas)
            .Include(c => c.Conexos).ThenInclude(x => x.clsServicioConexo)
            .FirstOrDefaultAsync(filtro, ct);

    public Task<clsCotizacion?> ObtenerParaMarcarResultadoAsync(Guid id, CancellationToken ct) =>
        db.Cotizaciones
            .Include(x => x.Lineas).ThenInclude(l => l.clsLineaSolicitud)
            .Include(x => x.Conexos).ThenInclude(x => x.clsServicioConexo)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<clsCotizacion?> ObtenerParaExportarAsync(Expression<Func<clsCotizacion, bool>> filtro, CancellationToken ct) =>
        db.Cotizaciones.AsNoTracking()
            .Include(x => x.Lineas).ThenInclude(l => l.clsLineaSolicitud).ThenInclude(ls => ls!.clsRuta)
            .Include(x => x.Conexos).ThenInclude(x => x.clsServicioConexo)
            .Include(x => x.clsSolicitud).ThenInclude(s => s!.clsCliente)
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

    public void Agregar(clsCotizacion cotizacion) => db.Cotizaciones.Add(cotizacion);
}
