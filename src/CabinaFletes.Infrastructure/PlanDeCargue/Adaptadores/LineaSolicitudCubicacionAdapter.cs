using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.PlanDeCargue.Adaptadores;

/// <summary>Implementa el puerto dedicado y de solo lectura de Plan de Cargue hacia
/// Solicitudes (ver ILineaSolicitudCubicacionPort).</summary>
public sealed class LineaSolicitudCubicacionAdapter(CabinaFletesDbContext db) : ILineaSolicitudCubicacionPort
{
    public async Task<LineaParaCubicacionDto?> ObtenerAsync(Guid lineaSolicitudId, CancellationToken ct = default)
    {
        var linea = await db.LineasSolicitud.AsNoTracking()
            .Include(l => l.Ruta)
            .FirstOrDefaultAsync(l => l.Id == lineaSolicitudId, ct);
        if (linea is null) return null;

        return new LineaParaCubicacionDto(
            linea.Id, PosicionCargaDto.Desde(linea.Dimensiones), linea.Ruta?.Origen, linea.Ruta?.Destino);
    }
}
