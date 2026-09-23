using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Common;

namespace Nexo.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Réplica, en miniatura, de la regla de resolución ciudad→zona que ya usa
/// <c>clsTarifarioSapAdapter.BuscarConZonasAsync</c> (motor real de tarifas): un municipio de
/// `tblSAPDANE` se considera "costeable" si su nombre aparece —en cualquier dirección—
/// dentro de algún código de `tblSAPTarifas.ORIGEN`/`DESTINO` o de
/// `tblZonasTarificacionRelacion.IDZONATRANSPORTE` (los códigos granulares tipo
/// `ZC_MEDELLIN`; la zona agregada real, tipo `ZTC_VABURRA`, sale de `IDZONATARIFICACION`
/// cuando el directo falla). Verificado contra datos reales: de 577 códigos de zona
/// distintos, esta regla —con las abreviaturas de arriba— resuelve 564; los 13 restantes
/// quedan en <see cref="AliasZonasAgregadas"/>.
/// </summary>
public static class clsResolucionZonaSap
{
    /// <summary>
    /// Casos que la regla automática no resuelve, curados a mano una sola vez (verificado
    /// contra tblSAPDANE) — no es mantenimiento recurrente. Dos motivos distintos, ambos
    /// reales:
    /// (a) zonas agregadas de verdad, sin una sola ciudad que las represente
    /// (VABURRA/URABA_ANT/ORIENTE_ANT), y
    /// (b) municipios reales que sí existen en tblSAPDANE pero con un formato que la
    /// regla de abreviaturas no cubre (ej. "DONMATÍAS" sin espacio, "CARTAGENA DE INDIAS"
    /// vs. el sufijo de departamento "(BOL)" del código de zona, o abreviaturas de una
    /// sola letra como "A"→"Andrés"/"J"→"José" que son demasiado ambiguas para expandir
    /// en general sin arriesgar falsos positivos en otros nombres).
    /// "CHIBOLO" queda con el departamento (Magdalena) sin confirmar dentro de
    /// tblSAPDANE — no apareció en la búsqueda directa, se deja con el departamento real
    /// conocido del municipio, a confirmar.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, clsCiudadDwDto> AliasZonasAgregadas = new Dictionary<string, clsCiudadDwDto>
    {
        ["VABURRA"] = new clsCiudadDwDto("Valle de Aburrá", "Antioquia"),
        ["URABA_ANT"] = new clsCiudadDwDto("Urabá", "Antioquia"),
        ["ORIENTE_ANT"] = new clsCiudadDwDto("Oriente antioqueño", "Antioquia"),
        ["DON MATIAS"] = new clsCiudadDwDto("Don Matías", "Antioquia"),
        ["CARTAGENA (BOL)"] = new clsCiudadDwDto("Cartagena de Indias", "Bolívar"),
        ["CATAMBUCO (NAR)"] = new clsCiudadDwDto("Catambuco (Pasto)", "Nariño"),
        ["S J DE LA MONTANA"] = new clsCiudadDwDto("San José de la Montaña", "Antioquia"),
        ["SAN A DE CUERQUIA"] = new clsCiudadDwDto("San Andrés de Cuerquía", "Antioquia"),
        ["DORADAL"] = new clsCiudadDwDto("Doradal (Puerto Triunfo)", "Antioquia"),
        ["SAN B DEL VIENTO"] = new clsCiudadDwDto("San Bernardo del Viento", "Córdoba"),
        ["S J DEL GUAVIARE"] = new clsCiudadDwDto("San José del Guaviare", "Guaviare"),
        ["BOLOMBOLO"] = new clsCiudadDwDto("Bolombolo (Venecia)", "Antioquia"),
        ["CHIBOLO"] = new clsCiudadDwDto("Chibolo", "Magdalena"),
    };

    /// <summary>Quita el prefijo de zona (`ZC_`/`ZTC_`) de un código ya normalizado.</summary>
    public static string SinPrefijoZona(string codigoNormalizado)
    {
        if (codigoNormalizado.StartsWith("ZTC_", StringComparison.Ordinal)) return codigoNormalizado["ZTC_".Length..];
        if (codigoNormalizado.StartsWith("ZC_", StringComparison.Ordinal)) return codigoNormalizado["ZC_".Length..];
        return codigoNormalizado;
    }

    /// <summary>
    /// Variantes de un código de zona ya normalizado y sin prefijo (ver
    /// <see cref="SinPrefijoZona"/>), expandiendo las abreviaturas que SAP usa en esos
    /// códigos (nunca en el nombre completo de DANE, que es lo que se compara). "S" es
    /// ambiguo (San/Santa), se prueban ambas.
    /// </summary>
    public static IReadOnlyList<string> ExpandirAbreviaturas(string zonaNormalizadaSinPrefijo)
    {
        var variantes = new HashSet<string> { zonaNormalizadaSinPrefijo };
        void Agregar(string patron, string reemplazo) =>
            variantes.Add(Regex.Replace(zonaNormalizadaSinPrefijo, patron, reemplazo));

        Agregar(@"\bPTO\b", "PUERTO");
        Agregar(@"\bSTFE\b", "SANTA FE");
        Agregar(@"\bSANT\b", "SANTANDER");
        Agregar(@"\bBCA\b", "BARRANCA");
        Agregar(@"\bS\b", "SAN");
        Agregar(@"\bS\b", "SANTA");
        return [.. variantes];
    }

    /// <summary>
    /// ¿Este municipio (nombre normalizado, completo) resuelve contra el conjunto de
    /// zonas válidas (ya normalizadas, sin prefijo y con abreviaturas expandidas — ver
    /// <see cref="ExpandirAbreviaturas"/>)? Compara en ambas direcciones porque el nombre
    /// del municipio y el de la zona no siempre tienen la misma longitud (ej. "BOGOTA"
    /// dentro de zona "BOGOTA", o zona "CARTAGENA (BOL)" conteniendo municipio "CARTAGENA").
    /// </summary>
    public static bool Resuelve(string municipioNormalizado, IReadOnlyCollection<string> zonasValidasExpandidas)
    {
        foreach (var zona in zonasValidasExpandidas)
            if (zona.Contains(municipioNormalizado, StringComparison.Ordinal) || municipioNormalizado.Contains(zona, StringComparison.Ordinal))
                return true;
        return false;
    }
}
