using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

/// <summary>
/// Persistencia del módulo Solicitudes/Captura. Portado de las consultas EF que
/// vivían en Nexo.Api/Services/SolicitudService.cs del proyecto original
/// (ObtenerBandejaAsync en particular: la proyección se queda aquí; el cálculo del
/// score con Domain.Priorizacion.Reglas se movió a Application, donde pertenece).
/// </summary>
public sealed class clsEfSolicitudesCatalogoRepository(clsNexoDbContext db) : ISolicitudesRepository
{
    public void Agregar(clsSolicitud solicitud) => db.Solicitudes.Add(solicitud);

    public void AgregarLineas(IEnumerable<clsLineaSolicitud> lineas) => db.LineasSolicitud.AddRange(lineas);

    public void AgregarCotizacion(clsCotizacion cotizacion) => db.Cotizaciones.Add(cotizacion);

    public Task<clsSolicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<clsSolicitud?> ObtenerConLineasAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes.Include(s => s.Lineas).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<clsSolicitud?> ObtenerParaCosteoAsync(Guid id, CancellationToken ct) =>
        db.Solicitudes
            .Include(x => x.clsCliente)
            .Include(x => x.Lineas).ThenInclude(l => l.clsRuta)
            .Include(x => x.Lineas).ThenInclude(l => l.clsConfiguracionVehiculo)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<clsBandejaFilaRaw>> BandejaFilasAsync(
        string? atencion, EstadoSolicitud? estado, DateTimeOffset? desde, DateTimeOffset? hasta,
        Guid? creadoPor, CancellationToken ct)
    {
        var q = db.Solicitudes.AsNoTracking().AsQueryable();

        // Aislamiento por comercial (issue #199): null para los roles que ven todo.
        if (creadoPor is { } cp) q = q.Where(s => s.CreadoPor == cp);

        if (estado is { } e)
            q = q.Where(s => s.Estado == e);
        else if (atencion == "pendientes")
            q = q.Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCosteo);

        if (desde is { } d) q = q.Where(s => s.CreatedAt >= d);
        if (hasta is { } h) q = q.Where(s => s.CreatedAt <= h);

        // Atención: sin flete = línea sin clsCotizacionLinea; auto = AutoResuelta; admin = Revisada.
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
                clsCliente = s.clsCliente!.Nombre,
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

        return filas.Select(f => new clsBandejaFilaRaw(
            f.Id, f.clsCliente, f.Tipo, f.Estado, f.Deadline, f.ValorEstimado, f.ProbabilidadCierre,
            f.CreatedAt, f.Lineas, f.FleteTotal, f.SinFlete, f.Auto, f.Revisadas)).ToList();
    }

    public async Task<clsParametrosScore> ParametrosScoreAsync(CancellationToken ct) =>
        await db.clsParametrosScore.AsNoTracking().FirstOrDefaultAsync(ct) ?? new clsParametrosScore();

}
