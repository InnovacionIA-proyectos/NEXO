using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;
using Nexo.Application.CasosDeUso.TarifasGanadas.Interfaces;
using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.TarifasGanadas.Repositorios;

public sealed class clsEfTarifasHistoricasRepository(clsNexoDbContext db, TimeProvider clock) : ITarifasHistoricasPort, IHistoricoTarifasPort
{
    public Task<bool> HayVigenteAsync(Guid rutaId, TipologiaCarga? tipologia, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        db.TarifasHistoricas.AsNoTracking()
            .Where(h => h.RutaId == rutaId && (h.Tipologia == tipologia || h.Tipologia == null))
            .AnyAsync(h => h.Fecha >= desde && h.Fecha <= hasta, ct);

    public Task<bool> ExistenParaCotizacionAsync(Guid cotizacionId, CancellationToken ct) =>
        db.TarifasHistoricas.AnyAsync(h => h.CotizacionId == cotizacionId, ct);

    public void Agregar(clsTarifaHistorica tarifa) => db.TarifasHistoricas.Add(tarifa);

    // ------------------------------------------------------- Biblioteca de Tarifas Ganadas (lectura)

    public async Task<IReadOnlyList<clsBibliotecaItemDto>> SugerirAsync(Guid? rutaId, Guid? vehiculoId, string? mercancia, CancellationToken ct)
    {
        TipologiaCarga? tip = null;
        if (!string.IsNullOrWhiteSpace(mercancia) && Enum.TryParse<TipologiaCarga>(mercancia, true, out var t)) tip = t;

        var q = Base()
            .Where(h => (rutaId == null || h.RutaId == rutaId)
                     && (vehiculoId == null || h.ConfigVehiculoId == vehiculoId)
                     && (tip == null || h.Tipologia == tip));

        return await Materializar(q.OrderByDescending(h => h.Fecha).Take(10), ct);
    }

    public async Task<(IReadOnlyList<clsBibliotecaItemDto> Items, int Total)> BuscarAsync(
        string? q, string? mercancia, string? vigencia, bool? deGanada, int page, int pageSize, CancellationToken ct)
    {
        var query = Base();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(h =>
                h.clsRuta!.Origen.Contains(term) ||
                h.clsRuta!.Destino.Contains(term) ||
                (h.clsConfiguracionVehiculo != null && h.clsConfiguracionVehiculo.TipoVehiculo.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(mercancia) && Enum.TryParse<TipologiaCarga>(mercancia, true, out var tip))
            query = query.Where(h => h.Tipologia == tip);

        if (!string.IsNullOrWhiteSpace(vigencia))
        {
            // Los cortes replican la semántica de Vigencia() (diferencia de meses, ignora el día):
            // vigente <= 6 meses; por_vencer 7-12; vencida > 12.
            var hoy = clock.GetUtcNow();
            var mesActual = new DateOnly(hoy.Year, hoy.Month, 1);
            var corte6 = mesActual.AddMonths(-6);
            var corte12 = mesActual.AddMonths(-12);
            query = vigencia.Trim().ToLowerInvariant() switch
            {
                "vigente" => query.Where(h => h.Fecha >= corte6),
                "por_vencer" => query.Where(h => h.Fecha >= corte12 && h.Fecha < corte6),
                "vencida" => query.Where(h => h.Fecha < corte12),
                _ => query,
            };
        }

        if (deGanada.HasValue)
            query = query.Where(h => (h.CotizacionId != null) == deGanada.Value);

        var total = await query.CountAsync(ct);
        var pagina = query.OrderByDescending(h => h.Fecha).Skip((page - 1) * pageSize).Take(pageSize);
        return (await Materializar(pagina, ct), total);
    }

    private IQueryable<clsTarifaHistorica> Base() =>
        db.TarifasHistoricas.AsNoTracking().Include(h => h.clsRuta).Include(h => h.clsConfiguracionVehiculo);

    private async Task<IReadOnlyList<clsBibliotecaItemDto>> Materializar(IQueryable<clsTarifaHistorica> q, CancellationToken ct)
    {
        var ahora = clock.GetUtcNow();
        var items = await q.ToListAsync(ct);
        return items.Select(h => new clsBibliotecaItemDto(
            h.Id,
            h.clsRuta == null ? null : $"{h.clsRuta.Origen} → {h.clsRuta.Destino}",
            h.clsConfiguracionVehiculo == null ? null : $"{h.clsConfiguracionVehiculo.TipoVehiculo}{(h.clsConfiguracionVehiculo.Carroceria == null ? "" : " / " + h.clsConfiguracionVehiculo.Carroceria)}",
            h.Tipologia,
            h.Valor,
            h.Fecha,
            Vigencia(h.Fecha, ahora),
            h.CotizacionId != null)).ToList();
    }

    private static string Vigencia(DateOnly fecha, DateTimeOffset ahora)
    {
        var meses = (ahora.Year - fecha.Year) * 12 + (ahora.Month - fecha.Month);
        return meses <= 6 ? "vigente" : meses <= 12 ? "por_vencer" : "vencida";
    }
}
