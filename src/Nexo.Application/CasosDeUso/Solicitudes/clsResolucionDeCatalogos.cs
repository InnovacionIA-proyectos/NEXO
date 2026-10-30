using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Resuelve clsRuta/clsConfiguracionVehiculo desde texto libre contra el catálogo, creando lo
/// que falte. Extraído de clsCapturarSolicitudCommand (donde vivía mezclado con "crear la
/// solicitud") para que otro caso de uso —como clsResolverCatalogosDeLineasCommand, que
/// alimenta a lineas:bulk desde un formulario de texto libre sin crear la solicitud
/// completa— use exactamente la misma normalización. Dos normalizadores divergentes
/// crearían rutas/vehículos duplicados según por dónde entró el texto.
/// </summary>
public static class clsResolucionDeCatalogos
{
    public static string? Limpiar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var colapsado = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return System.Text.RegularExpressions.Regex.Replace(colapsado, @"\s+([.,])", "$1");
    }

    /// <summary>
    /// Clave de comparación (NO lo que se guarda: eso sale de <see cref="Limpiar"/>). S5-HU-20.5/#297:
    /// sin tildes y con los separadores <c>_</c> y <c>-</c> unificados a espacio, para que "Bogotá" y
    /// "Bogota", o "Furgon Refrigerado" y "FURGON_REFRIGERADO", resuelvan a la MISMA fila en vez de crear
    /// una nueva. Las claves de lo ya guardado se calculan con este mismo método al cargar el catálogo,
    /// así que no hace falta migrar nada; si dos filas viejas colapsan en una clave gana la primera.
    /// A propósito NO se unifican otras formas ("T.MULA" vs "Tractomula"): adivinar equivalencias es lo
    /// que decide un humano en la cuarentena (S5-HU-18.4).
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
    /// Indexa las configuraciones de vehículo por clave normalizada. Varias filas pueden compartir clave ("Furgón" y "FURGON"):
    /// gana siempre la misma, primero una oficial que no esté descartada y, entre iguales, la más antigua. Así el vehículo que
    /// se asigna a una línea no depende del orden en que la base devuelva las filas.
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
    /// Busca la ruta en el diccionario ya cargado; si no existe, la crea y la agrega al
    /// catálogo. S5-HU-16.1/#254: cuando el frontend ya resolvió Origen/Destino contra el
    /// buscador de ciudades (tbl_municipios), <paramref name="origenMunicipioId"/>/
    /// <paramref name="destinoMunicipioId"/> quedan guardados en la ruta — nuevos o sobre
    /// una ya existente (se sobrescribe; el mismo texto normalizado siempre debería
    /// resolver al mismo municipio). Null si la línea no los trae (formulario viejo, texto
    /// libre que no pasó por el buscador).
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
    /// Igual que <see cref="ResolverRuta"/>, pero para el vehículo (opcional: sin tipo, no
    /// hay nada que resolver). S5-HU-18.4/#276: una fila nueva SIEMPRE nace en cuarentena
    /// (EsCatalogoOficial = false) — comparar texto normalizado no es suficiente para saber
    /// si corresponde a un código canónico del Cubicador ("TMULA"/"T.MULA"/"Tractomula" no
    /// calzan entre sí hoy, y adivinar la equivalencia aquí perpetuaría el mismo problema).
    /// Esa decisión la toma un humano en la bandeja de normalización (S6-HU-27.3/#322).
    ///
    /// S6-HU-27.2/#321: antes de caer en cuarentena se consulta el diccionario de alias
    /// (<paramref name="aliasVehiculo"/>: <c>AliasNormalizado → código</c>, de <c>tbl_alias_elementos</c>). Solo aplica a la columna "tipo de
    /// vehículo": si el texto es un alias ("TB", "T.B", "Turbo", "2L2" → 2L2), el tipo que se busca/guarda es el CÓDIGO, así que todas
    /// las variantes llegan a la MISMA fila; la carrocería no se toca (viaja tal cual, con la normalización de siempre). Un alias lo
    /// puso un humano, así que la fila que se cree con el código nace oficial, y una fila (código + carrocería) que estaba en
    /// cuarentena se promueve a oficial (el tracking de EF la guarda con el SaveChanges del caso de uso). Una fila que alguien
    /// descartó no se promueve. Sin alias, nada cambia: texto desconocido → cuarentena.
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
    /// Código de la clase de contenedor a partir del texto que llegó. S6-HU-27.2/#321, en este orden: (1) el diccionario de alias
    /// (<paramref name="aliasContenedor"/>: "40 HC", "40HC", "20 STD" → CNT40.HC, CNT20.ST); (2) un código real de SAP dentro del texto
    /// ("CNT40.HC", "cnt 40 hc"); (3) el texto tal cual (máximo 20), como antes. Null si no vino nada.
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
