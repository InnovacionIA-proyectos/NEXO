namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// Resuelve las horas de cargue/descargue efectivas de una línea para <see cref="clsPisoConHoras"/>: lo que
/// capturó el Comercial, el caso de la devolución de contenedor vacío, o el default de la solicitud.
/// Los tramos vacíos (levante y retorno del contenedor) siempre usan <see cref="HorasTramoVacio"/> (1+1),
/// una constante de negocio que no se guarda; marcar "retorna contenedor" no cambia las horas del viaje cargado.
/// </summary>
public static class clsResolucionHorasLinea
{
    /// <summary>Horas de cargue y de descargue de todo tramo vacío (devolución, levante, retorno): 1 + 1.</summary>
    public const decimal HorasCargueTramoVacio = 1m;
    public const decimal HorasDescargueTramoVacio = 1m;

    /// <summary>2 h en total (1+1): lo que recibe <see cref="clsPisoConHoras.Calcular"/> para un tramo vacío.</summary>
    public const decimal HorasTotalesTramoVacio = HorasCargueTramoVacio + HorasDescargueTramoVacio;

    /// <summary>(1, 1): horas por defecto de una línea que es un tramo vacío (devolución de contenedor).</summary>
    public static (decimal HorasCargue, decimal HorasDescargue) HorasTramoVacio => (HorasCargueTramoVacio, HorasDescargueTramoVacio);

    /// <summary>
    /// El Comercial sobrescribe el default solo si manda AMBAS horas; si falta alguna se aplica el default completo.
    /// <paramref name="esDevolucionContenedor"/>: la línea entera es una devolución de contenedor vacío (default 1+1),
    /// no el "retorna contenedor" de un viaje cargado.
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
