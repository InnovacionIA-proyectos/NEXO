using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>S6-HU-27.2/#321: diccionario de alias sobre <c>tbl_alias_elementos</c>.</summary>
public sealed class clsEfAliasCatalogoRepository(clsNexoDbContext db) : IAliasCatalogoPort
{
    public async Task<IReadOnlyList<clsAliasElemento>> ListarAsync(string? tipoElemento, CancellationToken ct)
    {
        var q = db.AliasElementos.AsNoTracking();
        if (tipoElemento is not null) q = q.Where(a => a.TipoElemento == tipoElemento);
        return await q.OrderBy(a => a.TipoElemento).ThenBy(a => a.Codigo).ThenBy(a => a.Alias).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> MapaActivoAsync(string tipoElemento, CancellationToken ct)
    {
        var filas = await db.AliasElementos.AsNoTracking()
            .Where(a => a.Activo && a.TipoElemento == tipoElemento)
            .Select(a => new { a.AliasNormalizado, a.Codigo })
            .ToListAsync(ct);
        // AliasNormalizado es único en la tabla, así que no hay colisión; el comparador ordinal es el mismo del índice único (sin distinguir
        // mayúsculas en SQL Server, y ya viene en mayúsculas desde el normalizador).
        return filas.ToDictionary(a => a.AliasNormalizado, a => a.Codigo, StringComparer.OrdinalIgnoreCase);
    }

    public Task<clsAliasElemento?> BuscarPorNormalizadoAsync(string aliasNormalizado, CancellationToken ct) =>
        db.AliasElementos.AsNoTracking().FirstOrDefaultAsync(a => a.AliasNormalizado == aliasNormalizado, ct);

    public async Task<IReadOnlyList<string>> ListarCodigosAsync(string tipoElemento, CancellationToken ct) =>
        await db.AliasElementos.AsNoTracking().Where(a => a.TipoElemento == tipoElemento)
            .Select(a => a.Codigo).Distinct().OrderBy(c => c).ToListAsync(ct);

    public void Agregar(clsAliasElemento alias) => db.AliasElementos.Add(alias);
}
