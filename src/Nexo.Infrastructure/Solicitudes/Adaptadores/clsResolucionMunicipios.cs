using Nexo.Domain.Common;

namespace Nexo.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Reglas puras (sin acceso a datos) de la siembra única del catálogo de municipios
/// (S4-HU-09.1, ver <c>clsSembrarMunicipiosCommandHandler</c>). Todo verificado contra
/// datos reales de <c>tblSAPDANE</c>/<c>tblZonasTarificacionRelacion</c>/
/// <c>tblSAPTarifas</c>+<c>tblSAPTarifasLog</c> antes de escribirse aquí — no son
/// supuestos.
/// </summary>
public static class clsResolucionMunicipios
{
    /// <summary>
    /// País a partir de <c>NOMBREMOSTRAR</c> de <c>tblSAPDANE</c>. Verificado: de las
    /// 10.695 filas reales, solo 3 países existen — Venezuela y Ecuador SIEMPRE traen
    /// el país después de la última coma ("Ciudad,Depto,Pais"); Colombia NUNCA usa ese
    /// formato (usa "Ciudad (DEPTO_ABREV)", sin comas) — ningún caso quedó ambiguo.
    /// </summary>
    public static string DetectarPais(string? nombreMostrar)
    {
        if (string.IsNullOrWhiteSpace(nombreMostrar)) return "CO";
        var partes = nombreMostrar.Split(',');
        if (partes.Length < 3) return "CO";
        var pais = clsTexto.Normalizar(partes[^1]);
        return pais switch
        {
            "VENEZUELA" => "VE",
            "ECUADOR" => "EC",
            _ => "CO",
        };
    }

    /// <summary>
    /// ¿Esta fila de <c>tblSAPDANE</c> es un municipio real (no un centro poblado
    /// menor — corregimiento/vereda — dentro de un municipio)? Solo aplica a Colombia:
    /// verificado que las filas de municipio real terminan en "000" (1.122 filas,
    /// exactas al total oficial de municipios de Colombia); Venezuela/Ecuador no
    /// tienen ese patrón de sufijo (no se les aplica este filtro).
    /// </summary>
    public static bool EsMunicipioColombiano(string codigoCentroPoblado, string pais) =>
        pais != "CO" || codigoCentroPoblado.EndsWith("000", StringComparison.Ordinal);

    /// <summary>
    /// Las 4 excepciones de corregimiento verificadas con código DANE propio (existen
    /// como filas reales, no-"000", en tblSAPDANE) que sí tienen movimiento comercial
    /// real en el tarifario (10, 5, 3 y 12 apariciones respectivamente en
    /// Origen/Destino de tblSAPTarifas+tblSAPTarifasLog) — se agregan como filas
    /// propias además de los 1.122 municipios, no como alias silencioso.
    /// </summary>
    public static readonly IReadOnlyList<(string CodigoDane, string Municipio, string Departamento, string ZonaZc)> ExcepcionesCorregimiento =
    [
        ("05591004", "Doradal", "Antioquia", "ZC_DORADAL"),
        ("52001001", "Catambuco", "Nariño", "ZC_CATAMBUCO (NAR)"),
        ("05861002", "Bolombolo", "Antioquia", "ZC_BOLOMBOLO"),
        ("44430007", "Paraguachón", "La Guajira", "ZC_PARAGUACHON"),
    ];

    /// <summary>
    /// Quinta excepción: "Jerusalén", corregimiento de Sonsón (Antioquia) confirmado
    /// por el negocio — 257 apariciones en el tarifario real (Origen/Destino,
    /// tblSAPTarifas+tblSAPTarifasLog), la de mayor volumen de las 5. tblSAPDANE NO
    /// tiene una fila propia con el nombre "Jerusalén" bajo Sonsón (existen 4 códigos
    /// de centro poblado sin nombre bajo 05756xxx, pero ninguno identificable) — se le
    /// asigna un código DANE inventado (con guion, nunca numérico puro) a propósito:
    /// así nunca puede matchear por accidente contra un código real de
    /// DWSice.tblRutasSicetac (siempre 8 dígitos numéricos), evitando un piso SICE-TAC
    /// equivocado. Consecuencia aceptada: esta fila no tendrá piso SICE-TAC propio,
    /// solo costeo por Tarifario (que sí matchea por ZC_JERUSALEN SONSON).
    /// </summary>
    public const string CodigoDaneJerusalen = "05756000-1";
    public const string MunicipioJerusalen = "Jerusalén";
    public const string DepartamentoSonson = "Antioquia";
    public const string ZonaZcJerusalen = "ZC_JERUSALEN SONSON";

    /// <summary>
    /// Casos donde el código de zona real (con prefijo, tal como aparece en el
    /// tarifario) omite una palabra conectora ("DE"/"DEL") que sí tiene el nombre
    /// oficial de DANE — la comparación por sub-cadena de
    /// <see cref="clsResolucionZonaSap.Resuelve"/> no cubre esto (la palabra en medio
    /// rompe la continuidad de la sub-cadena). Verificado contra datos reales: solo 1
    /// caso conocido en las 3.804 filas del catálogo (los otros ya resuelven solos).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AliasCodigoZcAMunicipio = new Dictionary<string, string>
    {
        ["ZC_GUAYABAL SIQUIMA"] = "GUAYABAL DE SIQUIMA",
    };

    /// <summary>
    /// Busca, entre los códigos ZC_ realmente usados en el tarifario (ya normalizados
    /// y sin el prefijo "ZC_"/"ZTC_" — ver <see cref="clsResolucionZonaSap"/>), el que
    /// corresponde a este municipio. Reutiliza la misma regla de abreviaturas y
    /// comparación por sub-cadena que ya usa el motor real de costeo
    /// (<c>clsTarifarioSapAdapter.BuscarConZonasAsync</c>), no una nueva — más los
    /// alias puntuales de <see cref="AliasCodigoZcAMunicipio"/> para los casos que esa
    /// regla no cubre. Devuelve el código ORIGINAL (con prefijo) del primer match, o
    /// null si ninguno resuelve.
    /// </summary>
    public static string? BuscarCodigoZc(string municipioNormalizado, IReadOnlyDictionary<string, IReadOnlyList<string>> variantesPorCodigoOriginal)
    {
        foreach (var (codigoOriginal, alias) in AliasCodigoZcAMunicipio)
            if (clsTexto.Normalizar(alias) == municipioNormalizado)
                return codigoOriginal;

        foreach (var (codigoOriginal, variantes) in variantesPorCodigoOriginal)
            if (clsResolucionZonaSap.Resuelve(municipioNormalizado, variantes))
                return codigoOriginal;
        return null;
    }
}
