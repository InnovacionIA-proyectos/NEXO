using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Resuelve ruta y configuración de vehículo desde texto libre contra el catálogo, creando lo que falte; es la normalización única para no duplicar rutas ni vehículos según la entrada.</summary>
public static class clsResolucionDeCatalogos
{
    public static string? Limpiar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var colapsado = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return System.Text.RegularExpressions.Regex.Replace(colapsado, @"\s+([.,])", "$1");
    }

    /// <summary>
    /// Clave de comparación (no lo que se guarda: eso sale de <see cref="Limpiar"/>): sin tildes y con <c>_</c> y <c>-</c> unificados a espacio, para que
    /// "Bogotá"/"Bogota" o "Furgon Refrigerado"/"FURGON_REFRIGERADO" resuelvan a la MISMA fila; si dos filas viejas colapsan gana la primera.
    /// Otras formas ("T.MULA" vs "Tractomula") NO se unifican a propósito: esa equivalencia la decide un humano en la cuarentena.
    /// </summary>
    public static string Normalizar(string? s)
    {
        var limpio = Limpiar(s);
        if (limpio is null) return string.Empty;
        var sinSeparadores = limpio.Replace('_', ' ').Replace('-', ' ');
        return (Limpiar(SinTildes(sinSeparadores)) ?? string.Empty).ToUpperInvariant();
    }

    public static string ClaveRuta(string origen, string destino) => $"{Normalizar(origen)}|{Normalizar(destino)}";

    public static string ClaveVehiculo(string tipo, string? carroceria) => $"{Normalizar(tipo)}|{Normalizar(carroceria)}";

    /// <summary>
    /// Indexa las configuraciones de vehículo por clave normalizada. Si varias comparten clave ("Furgón" y "FURGON") gana una oficial no
    /// descartada y, entre iguales, la más antigua, para que el resultado no dependa del orden de la base.
    /// </summary>
    public static Dictionary<string, clsConfiguracionVehiculo> IndexarVehiculosPorClave(IEnumerable<clsConfiguracionVehiculo> vehiculos) =>
        vehiculos.GroupBy(v => ClaveVehiculo(v.TipoVehiculo, v.Carroceria))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.EsCatalogoOficial && !v.Descartada).ThenBy(v => v.CreatedAt).First());

    public static string SinTildes(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Busca la ruta en el diccionario cargado o la crea. Si el frontend resolvió Origen/Destino con el buscador de ciudades, los ids de
    /// municipio se guardan en la ruta (también sobre una existente); null si la línea no los trae.
    /// </summary>
    public static (clsRuta clsRuta, bool Creada) ResolverRuta(
        string origen, string destino, IDictionary<string, clsRuta> rutasPorClave, IRutasCatalogoPort catalogo,
        int? origenMunicipioId = null, int? destinoMunicipioId = null)
    {
        var clave = ClaveRuta(origen, destino);
        if (rutasPorClave.TryGetValue(clave, out var ruta))
        {
            if (origenMunicipioId is not null) ruta.OrigenMunicipioId = origenMunicipioId;
            if (destinoMunicipioId is not null) ruta.DestinoMunicipioId = destinoMunicipioId;
            return (ruta, false);
        }

        ruta = new clsRuta
        {
            Origen = Limpiar(origen)!, Destino = Limpiar(destino)!,
            OrigenMunicipioId = origenMunicipioId, DestinoMunicipioId = destinoMunicipioId,
        };
        catalogo.Agregar(ruta);
        rutasPorClave[clave] = ruta;
        return (ruta, true);
    }

    /// <summary>
    /// Igual que <see cref="ResolverRuta"/> para el vehículo (opcional). Una fila nueva nace SIEMPRE en cuarentena (EsCatalogoOficial = false):
    /// la equivalencia con un código canónico del Cubicador la decide un humano. Antes se consulta <paramref name="aliasVehiculo"/>
    /// (<c>AliasNormalizado → código</c>): si el texto es un alias ("TB", "Turbo" → 2L2) se usa el CÓDIGO, la fila nace oficial y una fila en
    /// cuarentena con ese código y carrocería se promueve (no si alguien la descartó). La carrocería no se toca.
    /// Un tipo que es un código de la tabla de traducción (<paramref name="oficialidad"/>) cuenta igual que un alias, pero solo con la carrocería
    /// vacía o del catálogo: con otra, la configuración nace en cuarentena como un texto libre.
    /// </summary>
    public static (clsConfiguracionVehiculo? Vehiculo, bool Creado) ResolverVehiculo(
        string? tipoVehiculo, string? carroceria,
        IDictionary<string, clsConfiguracionVehiculo> vehiculosPorClave, IConfiguracionesVehiculoPort catalogo,
        IReadOnlyDictionary<string, string>? aliasVehiculo = null, Catalogos.clsOficialidadDeVehiculos? oficialidad = null)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo)) return (null, false);

        string? codigoAlias = null;
        if (aliasVehiculo is not null && aliasVehiculo.TryGetValue(Normalizar(tipoVehiculo), out var codigo)) codigoAlias = codigo;
        codigoAlias ??= oficialidad?.CodigoOficial(tipoVehiculo, carroceria);
        var tipo = codigoAlias ?? tipoVehiculo;

        var clave = ClaveVehiculo(tipo, carroceria);
        if (vehiculosPorClave.TryGetValue(clave, out var vehiculo))
        {
            if (codigoAlias is not null && !vehiculo.EsCatalogoOficial && !vehiculo.Descartada) vehiculo.EsCatalogoOficial = true;
            return (vehiculo, false);
        }

        vehiculo = new clsConfiguracionVehiculo
        {
            TipoVehiculo = Limpiar(tipo)!, Carroceria = Limpiar(carroceria), EsCatalogoOficial = codigoAlias is not null,
        };
        catalogo.Agregar(vehiculo);
        vehiculosPorClave[clave] = vehiculo;
        return (vehiculo, true);
    }

    /// <summary>Las clases de contenedor de SAP (tblSAPClaseContenedor) que la captura reconoce dentro de un texto libre.</summary>
    private static readonly string[] ClasesContenedor = ["CNT20.IT", "CNT20.RE", "CNT20.ST", "CNT40.HC", "CNT40.RE"];

    /// <summary>
    /// Código de la clase de contenedor del texto, en este orden: alias (<paramref name="aliasContenedor"/>: "40 HC" → CNT40.HC), código SAP
    /// dentro del texto ("cnt 40 hc") y el texto tal cual (máx. 20). Null si no vino nada.
    /// </summary>
    public static string? ResolverClaseContenedor(string? valor, IReadOnlyDictionary<string, string>? aliasContenedor = null)
    {
        var limpio = Limpiar(valor);
        if (limpio is null) return null;

        if (aliasContenedor is not null && aliasContenedor.TryGetValue(Normalizar(limpio), out var codigo)) return codigo;

        var n = limpio.ToUpperInvariant().Replace(" ", "").Replace(".", "").Replace("-", "").Replace("_", "");
        foreach (var c in ClasesContenedor)
            if (n.Contains(c.Replace(".", ""))) return c;
        return limpio.Length <= 20 ? limpio : limpio[..20];
    }

    /// <summary>Los códigos de clase de contenedor de SAP — el diccionario de alias también los acepta aunque aún no tengan ningún alias.</summary>
    public static IReadOnlyList<string> CodigosClaseContenedor => ClasesContenedor;
}
