namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// S4-HU-15.5/#251: resuelve las horas de cargue/descargue EFECTIVAS de una línea, para
/// que <see cref="clsPisoConHoras"/> las use en vez del único valor por solicitud
/// (clsCondicionesServicioDto.TiemposLogisticos) que aplicaba antes a todas las líneas
/// por igual. Función pura: no consulta nada, solo decide entre lo que capturó el
/// Comercial, el caso especial de devolución de contenedor vacío, y el default de la
/// solicitud completa.
/// </summary>
public static class clsResolucionHorasLinea
{
    /// <summary>
    /// Devolución de contenedor vacío (RetornaContenedor): 2h en total, no las 8h
    /// (4+4) por defecto de una línea con cargue y descargue completos — ya existía
    /// como caso especial reconocido (RetornaContenedor en clsCapturaLineaInput), solo
    /// que hasta ahora compartía las mismas horas que el resto de la solicitud.
    /// </summary>
    public const decimal HorasTotalesRetornoContenedor = 2m;

    /// <summary>
    /// El Comercial puede sobreescribir el default explícitamente por línea — basta con
    /// mandar AMBAS horas (cargue y descargue) en la línea; si falta cualquiera de las
    /// dos, se trata como "no especificado" y se aplica el default completo.
    /// </summary>
    public static (decimal HorasCargue, decimal HorasDescargue) Resolver(
        decimal? horasCargueLinea, decimal? horasDescargueLinea, bool retornaContenedor,
        decimal horasCargueSolicitud, decimal horasDescargueSolicitud)
    {
        if (horasCargueLinea is { } hc && horasDescargueLinea is { } hd) return (hc, hd);

        if (retornaContenedor) return (HorasTotalesRetornoContenedor / 2m, HorasTotalesRetornoContenedor / 2m);

        return (horasCargueSolicitud, horasDescargueSolicitud);
    }
}
