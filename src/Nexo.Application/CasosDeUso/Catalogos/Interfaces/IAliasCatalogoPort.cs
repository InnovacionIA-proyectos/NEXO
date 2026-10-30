using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Catalogos.Interfaces;

/// <summary>
/// S6-HU-27.2/#321: el diccionario de alias de vehículos y de clases de contenedor (<c>tbl_alias_elementos</c>). Lo usa la captura
/// (ResolverVehiculo y la clase de contenedor), los endpoints de alias y la bandeja de normalización.
/// </summary>
public interface IAliasCatalogoPort
{
    /// <summary>Sin seguimiento, por tipo (null = todos) y ordenado por código y alias. Incluye los inactivos.</summary>
    Task<IReadOnlyList<clsAliasElemento>> ListarAsync(string? tipoElemento, CancellationToken ct);

    /// <summary>Los alias ACTIVOS del tipo como diccionario <c>AliasNormalizado → Codigo</c>, para resolver sin ir a la base por cada línea.</summary>
    Task<IReadOnlyDictionary<string, string>> MapaActivoAsync(string tipoElemento, CancellationToken ct);

    /// <summary>El alias con ese texto normalizado (único en toda la tabla, de cualquier tipo), esté activo o no; null si no existe.</summary>
    Task<clsAliasElemento?> BuscarPorNormalizadoAsync(string aliasNormalizado, CancellationToken ct);

    /// <summary>Los códigos distintos que ya usa el diccionario para ese tipo (los que se aceptan al agregar alias).</summary>
    Task<IReadOnlyList<string>> ListarCodigosAsync(string tipoElemento, CancellationToken ct);

    void Agregar(clsAliasElemento alias);
}
