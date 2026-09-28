using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfTarifasCosteoRepository(clsNexoDbContext db, IPropuestaTarifaPort propuestas) : ITarifasCosteoPort
{
    /// <summary>
    /// Issue #179: antes leía una caché local (tbl_tarifas_sicetac) alimentada solo a
    /// mano por Excel, sin evidencia de uso real y desactualizada (nunca cubría el
    /// período vigente). Ahora lee la misma fuente DWSice que ya usa el motor
    /// automático de costeo (<see cref="IPropuestaTarifaPort.PisoSicetacVigenteAsync"/>),
    /// así que "vencida" ya no aplica: DWSice solo trae el período actual.
    /// </summary>
    public async Task<clsPisoSicetacDto?> PisoVigenteAsync(
        string origen, string destino, string? vehiculo, TipologiaCarga? tipologia,
        string? carroceria, CancellationToken ct)
    {
        var piso = await propuestas.PisoSicetacVigenteAsync(origen, destino, vehiculo, tipologia, carroceria, ct);

        return piso is null ? null : new clsPisoSicetacDto(
            Guid.Empty, piso.ValorPiso, piso.ValorHora, piso.HorasPactadas, piso.Periodo,
            "vigente", piso.Configuracion, piso.TipoCarga, piso.UnidadTransporte, piso.CondicionCarga);
    }

    public async Task<IReadOnlyList<clsHistoricaSugeridaDto>> HistoricasSugeridasAsync(
        Guid rutaId, TipologiaCarga? tipologia, DateTimeOffset ahora, int top, CancellationToken ct)
    {
        var hist = await db.TarifasHistoricas.AsNoTracking()
            .Where(h => h.RutaId == rutaId && (h.Tipologia == tipologia || h.Tipologia == null))
            .OrderByDescending(h => h.Fecha)
            .Take(top)
            .ToListAsync(ct);

        return hist.Select(h => new clsHistoricaSugeridaDto(h.Id, h.Valor, h.Fecha, h.Tipologia, VigenciaHistorica(h.Fecha, ahora))).ToList();
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
