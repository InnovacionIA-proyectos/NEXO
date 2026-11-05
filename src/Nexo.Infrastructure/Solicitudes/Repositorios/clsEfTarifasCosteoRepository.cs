using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfTarifasCosteoRepository(clsNexoDbContext db, IPropuestaTarifaPort propuestas) : ITarifasCosteoPort
{
    /// <summary>Lee el piso de DWSice (solo trae el período actual): se usa solo si es el del mes y año actuales, sin mes de gracia;
    /// si no, devuelve null y <see cref="MotivoSicetacNoUsadoAsync"/> dice por qué.</summary>
    public async Task<clsPisoSicetacDto?> PisoVigenteAsync(
        string origen, string destino, string? vehiculo, TipologiaCarga? tipologia,
        string? carroceria, CancellationToken ct)
    {
        var piso = await propuestas.PisoSicetacVigenteAsync(origen, destino, vehiculo, tipologia, carroceria, ct);

        return piso is null ? null : new clsPisoSicetacDto(
            Guid.Empty, piso.ValorPiso, piso.ValorHora, piso.HorasPactadas, piso.Periodo,
            "vigente", piso.Configuracion, piso.TipoCarga, piso.UnidadTransporte, piso.CondicionCarga);
    }

    public Task<string?> MotivoSicetacNoUsadoAsync(CancellationToken ct) => propuestas.MotivoSicetacNoUsadoAsync(ct);

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

    /// <summary>Vigencia de una tarifa histórica como referencia: máximo 1 semana; los últimos 2 días se marcan "por vencer".</summary>
    private static string VigenciaHistorica(DateOnly fecha, DateTimeOffset ahora)
    {
        var dias = DateOnly.FromDateTime(ahora.UtcDateTime).DayNumber - fecha.DayNumber;
        return dias <= 5 ? "vigente" : dias <= 7 ? "por_vencer" : "vencida";
    }
}
