using CabinaFletes.Application.Solicitudes.Ports;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Repositories;

/// <summary>
/// Persistencia del módulo Solicitudes/Captura. Portado de las consultas EF que
/// vivían en CabinaFletes.Api/Services/SolicitudService.cs del proyecto original
/// (ObtenerBandejaAsync en particular: la proyección se queda aquí; el cálculo del
/// score con Domain.Priorizacion.Reglas se movió a Application, donde pertenece).
/// </summary>
public sealed class EfSolicitudesCatalogoRepository(CabinaFletesDbContext db) : ISolicitudesRepository
{
    public void Agregar(Solicitud solicitud) => db.Solicitudes.Add(solicitud);

    public void AgregarLineas(IEnumerable<LineaSolicitud> lineas) => db.LineasSolicitud.AddRange(lineas);

    public void AgregarCotizacion(Cotizacion cotizacion) => db.Cotizaciones.Add(cotizacion);

    public Task<Solicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Solicitud?> ObtenerConLineasAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.Include(s => s.Lineas).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Solicitud?> ObtenerParaCosteoAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes
            .Include(x => x.Cliente)
            .Include(x => x.Lineas).ThenInclude(l => l.Ruta)
            .Include(x => x.Lineas).ThenInclude(l => l.ConfiguracionVehiculo)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<BandejaFilaRaw>> BandejaFilasAsync(
        string? atencion, EstadoSolicitud? estado, DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        var q = db.Solicitudes.AsNoTracking().AsQueryable();

        if (estado is { } e)
            q = q.Where(s => s.Estado == e);
        else if (atencion == "pendientes")
            q = q.Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCosteo);

        if (desde is { } d) q = q.Where(s => s.CreatedAt >= d);
        if (hasta is { } h) q = q.Where(s => s.CreatedAt <= h);

        // Atención: sin flete = línea sin CotizacionLinea; auto = AutoResuelta; admin = Revisada.
        q = atencion switch
        {
            "pendientes" => q.Where(s => s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))),
            "atendidas" => q.Where(s => s.Lineas.Any() && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))),
            "automatico" => q.Where(s => s.Lineas.Any()
                && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))
                && !s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada)
                && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.AutoResuelta)),
            "administrador" => q.Where(s => s.Lineas.Any()
                && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))
                && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada)),
            _ => q, // "todas"
        };

        var filas = await q
            .Select(s => new
            {
                s.Id,
                Cliente = s.Cliente!.Nombre,
                s.Tipo,
                s.Estado,
                s.Deadline,
                s.ValorEstimado,
                s.ProbabilidadCierre,
                s.CreatedAt,
                Lineas = s.Lineas.Count,
                FleteTotal = s.Cotizaciones.SelectMany(c => c.Lineas).Sum(l => (decimal?)l.ValorFlete),
                SinFlete = s.Lineas.Count(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)),
                Auto = s.Lineas.Count(l => l.EstadoLinea == EstadoLinea.AutoResuelta),
                Revisadas = s.Lineas.Count(l => l.EstadoLinea == EstadoLinea.Revisada),
            })
            .ToListAsync(ct);

        return filas.Select(f => new BandejaFilaRaw(
            f.Id, f.Cliente, f.Tipo, f.Estado, f.Deadline, f.ValorEstimado, f.ProbabilidadCierre,
            f.CreatedAt, f.Lineas, f.FleteTotal, f.SinFlete, f.Auto, f.Revisadas)).ToList();
    }

    public async Task<ParametrosScore> ParametrosScoreAsync(CancellationToken ct) =>
        await db.ParametrosScore.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametrosScore();

    public async Task<IReadOnlyList<Solicitud>> ObtenerActivasAsync(CancellationToken ct) =>
        await db.Solicitudes
            .Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCosteo)
            .ToListAsync(ct);
}
