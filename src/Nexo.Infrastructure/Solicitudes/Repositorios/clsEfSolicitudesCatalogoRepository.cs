using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

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
            .Include(x => x.Lineas).ThenInclude(l => l.clsRuta).ThenInclude(r => r!.OrigenMunicipio)
            .Include(x => x.Lineas).ThenInclude(l => l.clsRuta).ThenInclude(r => r!.DestinoMunicipio)
            .Include(x => x.Lineas).ThenInclude(l => l.clsConfiguracionVehiculo)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<clsBandejaFilaRaw>> BandejaFilasAsync(
        string? atencion, EstadoSolicitud? estado, DateTimeOffset? desde, DateTimeOffset? hasta,
        Guid? creadoPor, string? resultado, CancellationToken ct)
    {
        var q = db.Solicitudes.AsNoTracking().AsQueryable();

        // Aislamiento por comercial: null para los roles que ven todo. Va antes de cualquier otro filtro:
        // un Comercial nunca ve filas de otro, aunque la rama "pendientes" tenga un OR.
        if (creadoPor is { } cp) q = q.Where(s => s.CreadoPor == cp);

        if (estado is { } e)
            q = q.Where(s => s.Estado == e);
        else if (atencion == "pendientes")
            q = q.Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCosteo);

        if (desde is { } d) q = q.Where(s => s.CreatedAt >= d);
        if (hasta is { } h) q = q.Where(s => s.CreatedAt <= h);

        // Atención: sin flete = línea sin clsCotizacionLinea; auto = AutoResuelta; admin = Revisada.
        // "pendientes" = hay línea sin flete O (todas con flete y alguna Revisada): revisada entera pero aún no entregada.
        // También sigue apareciendo en "administrador"/"atendidas"; no aplica a "automatico". El filtro de estado ya excluye Entregada y Descartada.
        q = atencion switch
        {
            "pendientes" => q.Where(s => s.Lineas.Any(l => (l.EstadoLinea != EstadoLinea.Rechazada && !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)))
                || (s.Lineas.Any()
                    && !s.Lineas.Any(l => (l.EstadoLinea != EstadoLinea.Rechazada && !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)))
                    && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada))),
            "atendidas" => q.Where(s => s.Lineas.Any() && !s.Lineas.Any(l => (l.EstadoLinea != EstadoLinea.Rechazada && !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)))),
            "automatico" => q.Where(s => s.Lineas.Any()
                && !s.Lineas.Any(l => (l.EstadoLinea != EstadoLinea.Rechazada && !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)))
                && !s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada)
                && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.AutoResuelta)),
            "administrador" => q.Where(s => s.Lineas.Any()
                && !s.Lineas.Any(l => (l.EstadoLinea != EstadoLinea.Rechazada && !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)))
                && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada)),
            _ => q, // "todas"
        };

        // Resultado: esperando = Entregada (o auto-costeada en Borrador) sin NINGUNA cotización con resultado y solicitud no Descartada;
        // ganada/perdida: solo esas. Así, si hay una Ganada la fila no sale en "esperando" por otra cotización en borrador.
        q = resultado switch
        {
            clsBandejaResultado.Esperando => q.Where(s => s.Estado != EstadoSolicitud.Descartada
                && !s.Cotizaciones.Any(c => c.Resultado != ResultadoCotizacion.SinDefinir)
                && s.Cotizaciones.Any(c => c.Estado == EstadoCotizacion.Entregada
                    || (c.Estado == EstadoCotizacion.Borrador && c.EsAutoCosteo))),
            clsBandejaResultado.Ganada => q.Where(s => s.Cotizaciones.Any(c => c.Resultado == ResultadoCotizacion.Ganada)),
            clsBandejaResultado.Perdida => q.Where(s => s.Cotizaciones.Any(c => c.Resultado == ResultadoCotizacion.Perdida)),
            _ => q,
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
                SinFlete = s.Lineas.Count(l => (l.EstadoLinea != EstadoLinea.Rechazada && !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))),
                Auto = s.Lineas.Count(l => l.EstadoLinea == EstadoLinea.AutoResuelta),
                Revisadas = s.Lineas.Count(l => l.EstadoLinea == EstadoLinea.Revisada),
                // Cotización representativa: la que tenga resultado distinto de SinDefinir (la más
                // reciente si hay varias); si ninguna, la más reciente. Sin cotización -> null.
                Resultado = s.Cotizaciones
                    .OrderByDescending(c => c.Resultado != ResultadoCotizacion.SinDefinir)
                    .ThenByDescending(c => c.CreatedAt)
                    .Select(c => (ResultadoCotizacion?)c.Resultado)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return filas.Select(f => new clsBandejaFilaRaw(
            f.Id, f.clsCliente, f.Tipo, f.Estado, f.Deadline, f.ValorEstimado, f.ProbabilidadCierre,
            f.CreatedAt, f.Lineas, f.FleteTotal, f.SinFlete, f.Auto, f.Revisadas, f.Resultado)).ToList();
    }

    public async Task<clsParametrosScore> ParametrosScoreAsync(CancellationToken ct) =>
        await db.clsParametrosScore.AsNoTracking().FirstOrDefaultAsync(ct) ?? new clsParametrosScore();

}
