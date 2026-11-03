using Nexo.Application.CasosDeUso.IA;
using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Domain.IA.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.IA.Repositorios;

public sealed class clsEfUsoIaRepository(clsNexoDbContext db) : IUsoIaRepository
{
    public async Task RegistrarAsync(clsRegistroUsoIa r, CancellationToken ct = default)
    {
        db.UsosIa.Add(new clsUsoIa
        {
            Funcion = r.Funcion, UsuarioId = r.UsuarioId, Modelo = r.Modelo,
            TokensEntrada = Limitar(r.TokensEntrada), TokensSalida = Limitar(r.TokensSalida),
            TokensCacheLectura = Limitar(r.TokensCacheLectura), TokensCacheEscritura = Limitar(r.TokensCacheEscritura),
            CostoEstimadoUsd = r.CostoEstimadoUsd, LatenciaMs = Math.Max(0, r.LatenciaMs),
            Resultado = r.Resultado, CreatedAt = r.Fecha,
        });
        await db.SaveChangesAsync(ct);
    }

    public Task<long> TokensConsumidosDesdeAsync(Guid usuarioId, DateTimeOffset desde, CancellationToken ct = default) =>
        db.UsosIa.AsNoTracking()
            .Where(u => u.UsuarioId == usuarioId && u.CreatedAt >= desde)
            .SumAsync(u => (long)u.TokensEntrada + u.TokensSalida + u.TokensCacheLectura + u.TokensCacheEscritura, ct);

    public async Task<IReadOnlyList<clsGastoIaDto>> GastoPorFuncionYMesAsync(
        DateTimeOffset desde, DateTimeOffset hasta, string? funcion, CancellationToken ct = default)
    {
        var consulta = db.UsosIa.AsNoTracking().Where(u => u.CreatedAt >= desde && u.CreatedAt < hasta);
        if (funcion is not null) consulta = consulta.Where(u => u.Funcion == funcion);

        // El mes se cuenta en hora de Colombia, no en UTC: una llamada de las 8 p. m. del último día del mes ya es del mes
        // siguiente en UTC. Se agrupa en memoria sobre columnas mínimas; el rango es de pocos meses.
        var filas = await consulta
            .Select(u => new
            {
                u.Funcion, u.CreatedAt, u.Resultado, u.TokensEntrada, u.TokensSalida, u.TokensCacheLectura, u.TokensCacheEscritura,
                u.CostoEstimadoUsd, u.LatenciaMs,
            })
            .ToListAsync(ct);

        return filas
            .GroupBy(f => (Mes: f.CreatedAt.ToOffset(clsConsultarGastoIaQueryHandler.DesfaseColombia).ToString("yyyy-MM"), f.Funcion))
            .OrderByDescending(g => g.Key.Mes).ThenBy(g => g.Key.Funcion, StringComparer.Ordinal)
            .Select(g => new clsGastoIaDto(
                g.Key.Mes, g.Key.Funcion, g.Count(), g.Count(f => f.Resultado != clsUsoIa.ResultadoOk),
                g.Sum(f => (long)f.TokensEntrada), g.Sum(f => (long)f.TokensSalida),
                g.Sum(f => (long)f.TokensCacheLectura), g.Sum(f => (long)f.TokensCacheEscritura),
                g.Sum(f => f.CostoEstimadoUsd ?? 0m), g.Count(f => f.CostoEstimadoUsd is null),
                (int)Math.Round(g.Average(f => (double)f.LatenciaMs))))
            .ToList();
    }

    /// <summary>La tabla guarda enteros de 32 bits; una cifra fuera de rango se recorta en vez de fallar el registro.</summary>
    private static int Limitar(long valor) => (int)Math.Clamp(valor, 0, int.MaxValue);
}
