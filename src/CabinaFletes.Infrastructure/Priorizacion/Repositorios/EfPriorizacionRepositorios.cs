using CabinaFletes.Application.CasosDeUso.Priorizacion.Interfaces;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Priorizacion.Repositorios;

/// <summary>
/// La fila única de parámetros, con seguimiento porque el caso de uso la va a modificar.
///
/// Si no existe se crea con los valores por defecto en vez de fallar. La siembra la crea,
/// pero una base migrada a mano podría no tenerla, y en ese caso el Admin se encontraría
/// un 500 al abrir la pantalla de recalibración — cuando lo que corresponde es mostrarle
/// la configuración por defecto, que es exactamente con la que el sistema está corriendo.
/// </summary>
public sealed class EfParametrosScoreRepository(CabinaFletesDbContext db) : IParametrosScorePort
{
    public async Task<ParametrosScore> ParaEditarAsync(CancellationToken ct = default)
    {
        var fila = await db.ParametrosScore.FirstOrDefaultAsync(ct);
        if (fila is not null) return fila;

        fila = new ParametrosScore();
        db.ParametrosScore.Add(fila);
        return fila;
    }
}

/// <summary>
/// Las activas por lotes, paginando por identificador.
///
/// Por identificador y no por Skip/Take: el recálculo ESCRIBE sobre las mismas filas que
/// recorre. Con desplazamiento, una fila que cambia de posición entre dos lotes se salta
/// sin que nadie se entere, y el resultado es una solicitud con score viejo que nadie
/// sabe explicar.
///
/// Con seguimiento porque el caso de uso modifica el score y lo guarda por lote.
/// </summary>
public sealed class EfSolicitudesParaRecalcularRepository(CabinaFletesDbContext db)
    : ISolicitudesParaRecalcularPort
{
    public async Task<IReadOnlyList<Solicitud>> ActivasAsync(
        Guid? despuesDe, int tamano, CancellationToken ct = default) =>
        await db.Solicitudes
            .Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCosteo)
            .Where(s => despuesDe == null || s.Id.CompareTo(despuesDe.Value) > 0)
            .OrderBy(s => s.Id)
            .Take(tamano)
            .ToListAsync(ct);
}
