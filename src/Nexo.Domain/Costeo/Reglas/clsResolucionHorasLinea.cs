namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// S4-HU-15.5/#251: resuelve las horas de cargue/descargue EFECTIVAS de una línea, para
/// que <see cref="clsPisoConHoras"/> las use en vez del único valor por solicitud
/// (clsCondicionesServicioDto.TiemposLogisticos) que aplicaba antes a todas las líneas
/// por igual. Función pura: no consulta nada, solo decide entre lo que capturó el
/// Comercial, el caso especial de la devolución de contenedor vacío, y el default de la
/// solicitud completa.
///
/// #300 — horas POR TRAMO. Una línea puede tener dos tramos con horas distintas: el viaje CARGADO
/// (las horas de la línea, 4+4 por defecto) y los tramos VACÍOS (levante en otra ciudad y retorno del
/// contenedor), que siempre se calculan con <see cref="HorasTramoVacio"/> (1+1). Marcar "retorna
/// contenedor" ya NO cambia las horas de toda la línea (RN-03): solo agrega el tramo vacío de retorno.
/// Las horas de los tramos vacíos son una constante de negocio, no un dato de la línea: por eso no se
/// guardan en ninguna columna.
/// </summary>
public static class clsResolucionHorasLinea
{
    /// <summary>Horas de cargue y de descargue de todo tramo vacío (devolución, levante, retorno): 1 + 1.</summary>
    public const decimal HorasCargueTramoVacio = 1m;
    public const decimal HorasDescargueTramoVacio = 1m;

    /// <summary>2 h en total (1+1) — lo que recibe <see cref="clsPisoConHoras.Calcular"/> para un tramo vacío.</summary>
    public const decimal HorasTotalesTramoVacio = HorasCargueTramoVacio + HorasDescargueTramoVacio;

    /// <summary>(1, 1): horas por defecto de una línea que ES un tramo vacío (devolución de contenedor).</summary>
    public static (decimal HorasCargue, decimal HorasDescargue) HorasTramoVacio => (HorasCargueTramoVacio, HorasDescargueTramoVacio);

    /// <summary>
    /// El Comercial puede sobreescribir el default explícitamente por línea — basta con
    /// mandar AMBAS horas (cargue y descargue) en la línea; si falta cualquiera de las
    /// dos, se trata como "no especificado" y se aplica el default completo.
    ///
    /// <paramref name="esDevolucionContenedor"/>: la línea ES una devolución de contenedor vacío (línea
    /// de negocio DevolucionContenedor), o sea toda ella es un tramo vacío — default 1+1. NO es el
    /// "retorna contenedor" de un viaje cargado: ese tramo vacío se calcula aparte con las mismas 1+1 y
    /// no toca las horas de la línea (ver el doc de la clase).
    /// </summary>
    public static (decimal HorasCargue, decimal HorasDescargue) Resolver(
        decimal? horasCargueLinea, decimal? horasDescargueLinea, bool esDevolucionContenedor,
        decimal horasCargueSolicitud, decimal horasDescargueSolicitud)
    {
        if (horasCargueLinea is { } hc && horasDescargueLinea is { } hd) return (hc, hd);

        if (esDevolucionContenedor) return HorasTramoVacio;

        return (horasCargueSolicitud, horasDescargueSolicitud);
    }
}
