using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Costeo.Repositorios;

public sealed class clsEfLineasCosteadasRepository(clsNexoDbContext db) : ILineasCosteadasPort
{
    /// <summary>Deja la línea como resuelta por el servicio sin guardar el valor aquí (se aplica a la cotización en su propio caso de uso;
    /// duplicarlo crearía dos verdades), para que la bandeja no la muestre pendiente.</summary>
    public async Task MarcarAutoResueltaAsync(Guid lineaId, decimal valor, string fuente, CancellationToken ct = default)
    {
        var linea = await db.LineasSolicitud.FirstOrDefaultAsync(l => l.Id == lineaId, ct);
        if (linea is null || linea.EstadoLinea == EstadoLinea.Rechazada) return;
        linea.EstadoLinea = EstadoLinea.AutoResuelta;
        linea.MotivoPendiente = null;
    }

    public Task<bool> EstaRechazadaAsync(Guid lineaId, CancellationToken ct = default) =>
        db.LineasSolicitud.AsNoTracking().AnyAsync(l => l.Id == lineaId && l.EstadoLinea == EstadoLinea.Rechazada, ct);

    public async Task MarcarPendienteAsync(Guid lineaId, string motivo, CancellationToken ct = default)
    {
        var linea = await db.LineasSolicitud.FirstOrDefaultAsync(l => l.Id == lineaId, ct);
        if (linea is null || linea.EstadoLinea == EstadoLinea.Rechazada) return;
        linea.EstadoLinea = EstadoLinea.PorCostear;
        linea.MotivoPendiente = motivo.Length <= 500 ? motivo : motivo[..500];
    }
}
