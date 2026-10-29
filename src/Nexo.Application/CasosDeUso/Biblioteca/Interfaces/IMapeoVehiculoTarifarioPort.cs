namespace Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>
/// El mapeo de un vehículo de NEXO a los códigos con los que lo conocen SICE-TAC y Plaza/SAP.
/// <paramref name="CodigoPlazaSap"/> en null significa que el vehículo no tiene tarifa de plaza.
/// </summary>
public sealed record clsMapeoVehiculoTarifarioDto(string TipoVehiculo, string CodigoSicetac, string? CodigoPlazaSap);

/// <summary>
/// Lectura del mapeo de vehículos (<c>tbl_mapeo_vehiculo_tarifario</c>). Es una tabla chica que cambia poco: la implementación la guarda en
/// memoria unos minutos. Si la tabla no está disponible (por ejemplo el script aún no se aplicó) responde "sin mapeo" y las consultas se
/// comportan como antes de que existiera, en lugar de dejar de costear.
/// </summary>
public interface IMapeoVehiculoTarifarioPort
{
    /// <summary>El mapeo del vehículo, o null si no hay (vehículo vacío, no mapeado o tabla no disponible).</summary>
    Task<clsMapeoVehiculoTarifarioDto?> ObtenerAsync(string? tipoVehiculo, CancellationToken ct = default);
}

/// <summary>El código de plaza con el que buscar un vehículo, o la constancia de que no tiene tarifa de plaza.</summary>
public readonly record struct clsCodigoDePlaza(string? Codigo, bool SinTarifaDePlaza);

/// <summary>Cómo se pide cada código a partir del vehículo de NEXO; un vehículo sin fila de mapeo se busca con su propio código, como antes.</summary>
public static class clsCodigosDeVehiculo
{
    /// <summary>Mayúsculas y sin espacios: la forma en que se comparan los códigos de vehículo.</summary>
    public static string Normalizar(string vehiculo) => vehiculo.Trim().ToUpperInvariant();

    /// <summary>
    /// El valor de <c>CONFIGURACION</c> con el que buscar el vehículo en SICE-TAC. <paramref name="alternativaSinMapeo"/> es lo que se usa si el
    /// vehículo no está mapeado (por defecto, su propio código).
    /// </summary>
    public static async Task<string?> SicetacAsync(
        this IMapeoVehiculoTarifarioPort mapeo, string? vehiculo, Func<string, string>? alternativaSinMapeo = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vehiculo)) return null;
        var m = await mapeo.ObtenerAsync(vehiculo, ct);
        return m?.CodigoSicetac ?? (alternativaSinMapeo ?? Normalizar)(vehiculo);
    }

    /// <summary>
    /// El código con el que buscar el vehículo en Plaza/SAP. Con el vehículo mapeado y sin código de plaza, <see cref="clsCodigoDePlaza.SinTarifaDePlaza"/>
    /// es true: la fuente no tiene tarifa de ese vehículo y no se debe buscar otro en su lugar.
    /// </summary>
    public static async Task<clsCodigoDePlaza> PlazaAsync(this IMapeoVehiculoTarifarioPort mapeo, string? vehiculo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vehiculo)) return new clsCodigoDePlaza(null, false);
        var m = await mapeo.ObtenerAsync(vehiculo, ct);
        if (m is null) return new clsCodigoDePlaza(Normalizar(vehiculo), false);
        return string.IsNullOrWhiteSpace(m.CodigoPlazaSap) ? new clsCodigoDePlaza(null, true) : new clsCodigoDePlaza(Normalizar(m.CodigoPlazaSap), false);
    }
}
