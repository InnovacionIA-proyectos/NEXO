using Microsoft.EntityFrameworkCore;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.PlanDeCargue.Entidades;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.PlanDeCargue.Adaptadores;

/// <summary>#304: ver <see cref="IRechazoCubicacionLineaPort"/>.</summary>
public sealed class clsRechazoCubicacionLineaAdapter(clsNexoDbContext db) : IRechazoCubicacionLineaPort
{
    public Task<clsRechazoCubicacionLinea?> ObtenerAsync(Guid lineaSolicitudId, CancellationToken ct = default) =>
        db.RechazosCubicacionLinea.AsNoTracking().FirstOrDefaultAsync(r => r.LineaSolicitudId == lineaSolicitudId, ct);

    public async Task<bool> RegistrarAsync(Guid lineaSolicitudId, string? codigo, string? mensaje, CancellationToken ct = default)
    {
        if (!await db.LineasSolicitud.AnyAsync(l => l.Id == lineaSolicitudId, ct)) return false;

        var fila = await db.RechazosCubicacionLinea.FirstOrDefaultAsync(r => r.LineaSolicitudId == lineaSolicitudId, ct);
        if (fila is null)
        {
            fila = new clsRechazoCubicacionLinea { LineaSolicitudId = lineaSolicitudId };
            db.RechazosCubicacionLinea.Add(fila);
        }
        fila.Codigo = Recortar(codigo, 60);
        fila.Mensaje = Recortar(mensaje, 1000);
        fila.RechazadoAt = DateTimeOffset.UtcNow;
        return true;
    }

    private static string Recortar(string? texto, int max) =>
        string.IsNullOrWhiteSpace(texto) ? "(sin detalle)" : texto.Length <= max ? texto : texto[..max];

    public async Task LimpiarAsync(Guid lineaSolicitudId, CancellationToken ct = default)
    {
        var previo = await db.RechazosCubicacionLinea.FirstOrDefaultAsync(r => r.LineaSolicitudId == lineaSolicitudId, ct);
        if (previo is not null) db.RechazosCubicacionLinea.Remove(previo);
    }
}
