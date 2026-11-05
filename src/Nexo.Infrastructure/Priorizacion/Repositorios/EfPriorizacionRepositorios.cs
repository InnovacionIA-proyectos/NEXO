using Nexo.Application.CasosDeUso.Priorizacion.Interfaces;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Priorizacion.Repositorios;

/// <summary>La fila única de parámetros, con seguimiento. Si no existe se crea con los valores por defecto
/// (una base migrada a mano podría no tenerla) en vez de fallar con 500.</summary>
public sealed class clsEfParametrosScoreRepository(clsNexoDbContext db) : IParametrosScorePort
{
    public async Task<clsParametrosScore> ParaEditarAsync(CancellationToken ct = default)
    {
        var fila = await db.clsParametrosScore.FirstOrDefaultAsync(ct);
        if (fila is not null) return fila;

        fila = new clsParametrosScore();
        db.clsParametrosScore.Add(fila);
        return fila;
    }
}

/// <summary>Las activas por lotes, paginando por identificador y no por Skip/Take: el recálculo escribe sobre las filas que recorre
/// y con desplazamiento se saltarían. Con seguimiento porque se modifica el score.</summary>
public sealed class clsEfSolicitudesParaRecalcularRepository(clsNexoDbContext db)
    : ISolicitudesParaRecalcularPort
{
    public async Task<IReadOnlyList<clsSolicitud>> ActivasAsync(
        Guid? despuesDe, int tamano, CancellationToken ct = default) =>
        await db.Solicitudes
            .Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCosteo)
            .Where(s => despuesDe == null || s.Id.CompareTo(despuesDe.Value) > 0)
            .OrderBy(s => s.Id)
            .Take(tamano)
            .ToListAsync(ct);
}
