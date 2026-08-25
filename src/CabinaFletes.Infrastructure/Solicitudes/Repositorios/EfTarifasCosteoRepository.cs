using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Solicitudes.Repositorios;

public sealed class EfTarifasCosteoRepository(CabinaFletesDbContext db) : ITarifasCosteoPort
{
    public async Task<PisoSicetacDto?> PisoVigenteAsync(Guid rutaId, int periodoActual, CancellationToken ct)
    {
        // Orden determinista: periodo más reciente y, a igualdad, por Id, para no
        // devolver una fila arbitraria. CARGADO (el VACÍO es el aprovisionamiento, otro concepto).
        var piso = await db.TarifasSicetac.AsNoTracking()
            .Where(t => t.RutaId == rutaId && t.CondicionCarga == CondicionCarga.Cargado)
            .OrderByDescending(t => t.Periodo)
            .ThenBy(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return piso is null ? null : new PisoSicetacDto(
            piso.Id, piso.ValorPiso, piso.ValorHora, piso.HorasPactadas, piso.Periodo,
            piso.Periodo >= periodoActual ? "vigente" : "vencida",
            piso.Configuracion, piso.TipoCarga, piso.UnidadTransporte, piso.CondicionCarga.ToString());
    }

    public async Task<IReadOnlyList<HistoricaSugeridaDto>> HistoricasSugeridasAsync(
        Guid rutaId, TipologiaCarga? tipologia, DateTimeOffset ahora, int top, CancellationToken ct)
    {
        var hist = await db.TarifasHistoricas.AsNoTracking()
            .Where(h => h.RutaId == rutaId && (h.Tipologia == tipologia || h.Tipologia == null))
            .OrderByDescending(h => h.Fecha)
            .Take(top)
            .ToListAsync(ct);

        return hist.Select(h => new HistoricaSugeridaDto(h.Id, h.Valor, h.Fecha, h.Tipologia, VigenciaHistorica(h.Fecha, ahora))).ToList();
    }

    /// <summary>
    /// Vigencia de una tarifa histórica como referencia reutilizable: MÁXIMO 1 semana
    /// (los fletes cambian semanalmente). Los últimos 2 días se marcan "por vencer".
    /// </summary>
    private static string VigenciaHistorica(DateOnly fecha, DateTimeOffset ahora)
    {
        var dias = DateOnly.FromDateTime(ahora.UtcDateTime).DayNumber - fecha.DayNumber;
        return dias <= 5 ? "vigente" : dias <= 7 ? "por_vencer" : "vencida";
    }
}
