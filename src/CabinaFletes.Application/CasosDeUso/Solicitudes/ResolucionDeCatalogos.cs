using System.Globalization;
using System.Text;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Resuelve Ruta/ConfiguracionVehiculo desde texto libre contra el catálogo, creando lo
/// que falte. Extraído de CapturarSolicitudCommand (donde vivía mezclado con "crear la
/// solicitud") para que otro caso de uso —como ResolverCatalogosDeLineasCommand, que
/// alimenta a lineas:bulk desde un formulario de texto libre sin crear la solicitud
/// completa— use exactamente la misma normalización. Dos normalizadores divergentes
/// crearían rutas/vehículos duplicados según por dónde entró el texto (issue #131).
/// </summary>
public static class ResolucionDeCatalogos
{
    public static string? Limpiar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var colapsado = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return System.Text.RegularExpressions.Regex.Replace(colapsado, @"\s+([.,])", "$1");
    }

    public static string Normalizar(string? s) => (Limpiar(s) ?? string.Empty).ToUpperInvariant();

    public static string ClaveRuta(string origen, string destino) => $"{Normalizar(origen)}|{Normalizar(destino)}";

    public static string ClaveVehiculo(string tipo, string? carroceria) => $"{Normalizar(tipo)}|{Normalizar(carroceria)}";

    public static string SinTildes(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Busca la ruta en el diccionario ya cargado; si no existe, la crea y la agrega al catálogo.</summary>
    public static (Ruta Ruta, bool Creada) ResolverRuta(
        string origen, string destino, IDictionary<string, Ruta> rutasPorClave, IRutasCatalogoPort catalogo)
    {
        var clave = ClaveRuta(origen, destino);
        if (rutasPorClave.TryGetValue(clave, out var ruta)) return (ruta, false);

        ruta = new Ruta { Origen = Limpiar(origen)!, Destino = Limpiar(destino)! };
        catalogo.Agregar(ruta);
        rutasPorClave[clave] = ruta;
        return (ruta, true);
    }

    /// <summary>Igual que <see cref="ResolverRuta"/>, pero para el vehículo (opcional: sin tipo, no hay nada que resolver).</summary>
    public static (ConfiguracionVehiculo? Vehiculo, bool Creado) ResolverVehiculo(
        string? tipoVehiculo, string? carroceria,
        IDictionary<string, ConfiguracionVehiculo> vehiculosPorClave, IConfiguracionesVehiculoPort catalogo)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo)) return (null, false);

        var clave = ClaveVehiculo(tipoVehiculo, carroceria);
        if (vehiculosPorClave.TryGetValue(clave, out var vehiculo)) return (vehiculo, false);

        vehiculo = new ConfiguracionVehiculo { TipoVehiculo = Limpiar(tipoVehiculo)!, Carroceria = Limpiar(carroceria) };
        catalogo.Agregar(vehiculo);
        vehiculosPorClave[clave] = vehiculo;
        return (vehiculo, true);
    }
}
