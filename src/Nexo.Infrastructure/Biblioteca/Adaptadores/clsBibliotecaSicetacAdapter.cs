using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Consulta de SOLO LECTURA a la matriz oficial SICE-TAC del Data Warehouse
/// (DWSice.dbo.tblRutasSicetac, ~3,8 M de filas). Sin periodo explícito se usa el
/// más reciente. Portado 1:1 desde
/// Nexo.Api/Services/BibliotecaSicetacService.cs del proyecto original.
/// </summary>
public sealed class clsBibliotecaSicetacAdapter(clsDwSiceDbContext dw, IMemoryCache cache) : IBibliotecaSicetacPort
{
    private const int MaxTop = 200;
    // PeriodosAsync dispara un DISTINCT sobre Periodo en las ~2.14M filas de
    // tblRutasSicetac (sin índice - ver DependencyInjection.cs), probable causa
    // principal de los ~33s reportados en frío "sin filtros" (BuscarAsync resuelve
    // el periodo más reciente por acá antes de filtrar la matriz). Los periodos
    // SICE-TAC cambian con baja frecuencia (mensual), así que subir el TTL de
    // 15 min a 6 horas reduce el costo sin arriesgar servir un periodo
    // desactualizado por más que unas horas.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    public async Task<clsMatrizSicetacResultDto> BuscarAsync(
        string? q, int? periodo, string? configuracion, string? tipoCarga, string? unidadTransporte,
        string? condicionCarga, string? viaEstandar, int top, int page = 1, CancellationToken ct = default)
    {
        top = Math.Clamp(top, 1, MaxTop);
        page = Math.Max(1, page);
        var periodoEfectivo = periodo ?? await PeriodoMasRecienteAsync(ct);

        var query = dw.RutasSicetac.Where(r => r.Periodo == periodoEfectivo);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r => r.NombreRuta.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(configuracion))
            query = query.Where(r => r.Configuracion == configuracion.Trim());
        if (!string.IsNullOrWhiteSpace(tipoCarga))
            query = query.Where(r => r.NombreTipoCarga == tipoCarga.Trim());
        if (!string.IsNullOrWhiteSpace(unidadTransporte))
            query = query.Where(r => r.NombreUnidadTransporte == unidadTransporte.Trim());
        if (!string.IsNullOrWhiteSpace(condicionCarga))
            query = query.Where(r => r.CondicionCarga == condicionCarga.Trim());
        if (!string.IsNullOrWhiteSpace(viaEstandar))
            query = query.Where(r => r.ViaEstandar == viaEstandar.Trim());

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.NombreRuta).ThenBy(r => r.Configuracion).ThenBy(r => r.NombreUnidadTransporte)
            .Skip((page - 1) * top)
            .Take(top)
            .Select(r => new clsRutaSicetacItemDto(
                r.Id, r.Periodo, r.NombreRuta, r.Origen, r.Destino,
                r.Configuracion, r.NombreTipoCarga, r.NombreUnidadTransporte, r.CondicionCarga,
                r.Valor, r.ValorHora, r.ValorSiceConHora, r.HorasRecorrido, r.HorasPactadas,
                r.ViaEstandar, r.FechaCreacion, r.FechaModificacion))
            .ToListAsync(ct);

        return new clsMatrizSicetacResultDto(periodoEfectivo, total, items);
    }

    public async Task<IReadOnlyList<int>> PeriodosAsync(CancellationToken ct = default)
    {
        var periodos = await cache.GetOrCreateAsync("dwsice:periodos", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await dw.RutasSicetac
                .Where(r => r.Periodo > 0)
                .Select(r => r.Periodo)
                .Distinct()
                .OrderByDescending(p => p)
                .Take(12)
                .ToListAsync(ct);
        });
        return periodos!;
    }

    public async Task<clsSicetacFiltrosDto> FiltrosAsync(CancellationToken ct = default)
    {
        var filtros = await cache.GetOrCreateAsync("dwsice:filtros", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var periodos = await PeriodosAsync(ct);
            var p = periodos.Count > 0 ? periodos[0] : 0;
            var baseQuery = dw.RutasSicetac.Where(r => r.Periodo == p);

            var configuraciones = await baseQuery.Select(r => r.Configuracion).Distinct().OrderBy(x => x).ToListAsync(ct);
            var tiposCarga = await baseQuery.Select(r => r.NombreTipoCarga).Distinct().OrderBy(x => x).ToListAsync(ct);
            var unidades = await baseQuery.Select(r => r.NombreUnidadTransporte).Distinct().OrderBy(x => x).ToListAsync(ct);

            return new clsSicetacFiltrosDto(periodos, configuraciones, tiposCarga, unidades);
        });
        return filtros!;
    }

    private async Task<int> PeriodoMasRecienteAsync(CancellationToken ct)
    {
        var periodos = await PeriodosAsync(ct);
        if (periodos.Count == 0)
            throw new clsServiceUnavailableException("DWSICE_SIN_DATOS", "La matriz SICE-TAC del Data Warehouse no tiene periodos cargados.");
        return periodos[0];
    }
}
