using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.Catalogos;

/// <summary>
/// Los códigos de la tabla de traducción (<c>tbl_mapeo_vehiculo_tarifario</c>) son códigos oficiales de NEXO: un tipo de vehículo que coincide con
/// uno se trata como alias de sí mismo, siempre que la carrocería esté vacía o sea una del catálogo.
/// </summary>
public sealed class clsOficialidadDeVehiculos(
    IReadOnlyDictionary<string, string> codigosPorClave, IReadOnlySet<string> carroceriasPorClave)
{
    public static readonly clsOficialidadDeVehiculos Vacia = new(new Dictionary<string, string>(), new HashSet<string>());

    public static async Task<clsOficialidadDeVehiculos> CargarAsync(IVehiculosNexoPort puerto, CancellationToken ct)
    {
        var codigos = (await puerto.ListarCodigosAsync(ct))
            .Select(c => c.Codigo.Trim())
            .Where(c => c.Length > 0)
            .GroupBy(clsResolucionDeCatalogos.Normalizar)
            .ToDictionary(g => g.Key, g => g.First());
        var carrocerias = (await puerto.ListarCarroceriasOficialesAsync(ct)).Select(clsResolucionDeCatalogos.Normalizar).Where(c => c.Length > 0).ToHashSet();
        return new clsOficialidadDeVehiculos(codigos, carrocerias);
    }

    /// <summary>El código de la tabla al que corresponde el tipo, o null si no es uno de ellos o su carrocería no es del catálogo.</summary>
    public string? CodigoOficial(string? tipoVehiculo, string? carroceria)
    {
        if (!codigosPorClave.TryGetValue(clsResolucionDeCatalogos.Normalizar(tipoVehiculo), out var codigo)) return null;
        var carroceriaLimpia = clsResolucionDeCatalogos.Limpiar(carroceria);
        return carroceriaLimpia is null || carroceriasPorClave.Contains(clsResolucionDeCatalogos.Normalizar(carroceriaLimpia)) ? codigo : null;
    }

    /// <summary>El código de la tabla con ese texto, sin mirar la carrocería (para validar a qué código se puede apuntar un alias).</summary>
    public string? CodigoDeLaTabla(string? texto) =>
        codigosPorClave.TryGetValue(clsResolucionDeCatalogos.Normalizar(texto), out var codigo) ? codigo : null;
}
