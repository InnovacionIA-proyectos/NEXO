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
    /// Busca la ruta en el diccionario cargado o la crea. Con los ids de municipio que el frontend resolvió con el buscador de ciudades se busca
    /// primero por municipios y después por texto; por texto solo se completan los ids que la ruta tiene vacíos, nunca se cambian los que ya tiene
    /// (un mismo nombre existe en varios departamentos). Una ruta nueva con id toma su nombre del municipio del catálogo
    /// (<paramref name="municipios"/>, por id).
    /// </summary>
    public static (clsRuta clsRuta, bool Creada) ResolverRuta(
        string origen, string destino, IDictionary<string, clsRuta> rutasPorClave, IRutasCatalogoPort catalogo,
        int? origenMunicipioId = null, int? destinoMunicipioId = null, IReadOnlyDictionary<int, clsMunicipio>? municipios = null)
    {
        if (origenMunicipioId is not null && destinoMunicipioId is not null)
        {
            var porMunicipios = rutasPorClave.Values.FirstOrDefault(r =>
                r.OrigenMunicipioId == origenMunicipioId && r.DestinoMunicipioId == destinoMunicipioId);
            if (porMunicipios is not null) return (porMunicipios, false);
        }

        var nombreOrigen = NombreDelMunicipio(origenMunicipioId, municipios) ?? Limpiar(origen)!;
        var nombreDestino = NombreDelMunicipio(destinoMunicipioId, municipios) ?? Limpiar(destino)!;

        var clave = ClaveRuta(origen, destino);
        if (!rutasPorClave.TryGetValue(clave, out var ruta))
            rutasPorClave.TryGetValue(ClaveRuta(nombreOrigen, nombreDestino), out ruta);

        if (ruta is not null)
        {
            ruta.OrigenMunicipioId ??= origenMunicipioId;
            ruta.DestinoMunicipioId ??= destinoMunicipioId;
            return (ruta, false);
        }

        ruta = new clsRuta
        {
            Origen = nombreOrigen, Destino = nombreDestino,
            OrigenMunicipioId = origenMunicipioId, DestinoMunicipioId = destinoMunicipioId,
        };
        catalogo.Agregar(ruta);
        rutasPorClave[clave] = ruta;
        rutasPorClave[ClaveRuta(nombreOrigen, nombreDestino)] = ruta;
        return (ruta, true);
    }

    private static string? NombreDelMunicipio(int? id, IReadOnlyDictionary<int, clsMunicipio>? municipios) =>
        id is { } i && municipios is not null && municipios.TryGetValue(i, out var m) ? Limpiar(m.Municipio) : null;

    /// <summary>
    /// Los municipios de los ids que traen las líneas, por id; null si ninguna trae ids. 400 <c>MUNICIPIO_NO_ENCONTRADO</c> si algún id no
    /// está en el catálogo.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, clsMunicipio>?> CargarMunicipiosAsync(
        IMunicipiosRepository repositorio, IEnumerable<(int Linea, int? OrigenId, int? DestinoId)> lineas, CancellationToken ct)
    {
        var pedidas = lineas.ToList();
        if (pedidas.All(l => l.OrigenId is null && l.DestinoId is null)) return null;

        var catalogo = (await repositorio.ListarAsync(ct)).ToDictionary(m => m.Id);
        foreach (var (linea, origenId, destinoId) in pedidas)
        {
            if (origenId is { } o && !catalogo.ContainsKey(o))
                throw new Common.Exceptions.clsValidationFailedException("MUNICIPIO_NO_ENCONTRADO", $"Línea {linea}: el municipio de origen {o} no existe en el catálogo.");
            if (destinoId is { } d && !catalogo.ContainsKey(d))
                throw new Common.Exceptions.clsValidationFailedException("MUNICIPIO_NO_ENCONTRADO", $"Línea {linea}: el municipio de destino {d} no existe en el catálogo.");
        }
        return catalogo;
    }

    /// <summary>
    /// Igual que <see cref="ResolverRuta"/> para el vehículo (opcional). Una fila nueva nace SIEMPRE en cuarentena (EsCatalogoOficial = false):
    /// la equivalencia con un código canónico del Cubicador la decide un humano. Antes se consulta <paramref name="aliasVehiculo"/>
    /// (<c>AliasNormalizado → código</c>): si el texto es un alias ("TB", "Turbo" → 2L2) se usa el CÓDIGO, la fila nace oficial y una fila en
    /// cuarentena con ese código y carrocería se promueve (no si alguien la descartó). La carrocería no se toca.
    /// </summary>
    public static (clsConfiguracionVehiculo? Vehiculo, bool Creado) ResolverVehiculo(
        string? tipoVehiculo, string? carroceria,
        IDictionary<string, clsConfiguracionVehiculo> vehiculosPorClave, IConfiguracionesVehiculoPort catalogo,
        IReadOnlyDictionary<string, string>? aliasVehiculo = null)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo)) return (null, false);

        string? codigoAlias = null;
        if (aliasVehiculo is not null && aliasVehiculo.TryGetValue(Normalizar(tipoVehiculo), out var codigo)) codigoAlias = codigo;
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
