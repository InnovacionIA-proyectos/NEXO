using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Costeo.Repositorios;

public sealed class clsEfLineasCosteadasRepository(clsNexoDbContext db) : ILineasCosteadasPort
{
    /// <summary>
    /// Deja la línea como resuelta por el servicio.
    ///
    /// No se guarda el valor en la línea: el valor propuesto se aplica a la cotización
    /// en su propio caso de uso, y duplicarlo aquí crearía dos verdades sobre el mismo
    /// número. Lo que sí queda es que el servicio ya la resolvió, para que la bandeja
    /// no la muestre como pendiente de costear.
    /// </summary>
    public async Task MarcarAutoResueltaAsync(Guid lineaId, decimal valor, string fuente, CancellationToken ct = default)
    {
        var linea = await db.LineasSolicitud.FirstOrDefaultAsync(l => l.Id == lineaId, ct);
        if (linea is null) return;
        linea.EstadoLinea = EstadoLinea.AutoResuelta;
        linea.MotivoPendiente = null;
    }

    public async Task MarcarPendienteAsync(Guid lineaId, string motivo, CancellationToken ct = default)
    {
        var linea = await db.LineasSolicitud.FirstOrDefaultAsync(l => l.Id == lineaId, ct);
        if (linea is null) return;
        linea.EstadoLinea = EstadoLinea.PorCostear;
        linea.MotivoPendiente = motivo.Length <= 500 ? motivo : motivo[..500];
    }
}
