using Nexo.Domain.Cotizaciones.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>Reglas de cálculo puras del agregado Cotización, compartidas por varios casos de uso.</summary>
internal static class clsCotizacionCalculos
{
    /// <summary>El total suma el flete, el aprovisionamiento y el retorno del contenedor de
    /// cada línea (conceptos separados) más los servicios conexos.</summary>
    public static void Recalcular(clsCotizacion c) =>
        c.Total = c.Lineas.Sum(l => l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0))
                + c.Conexos.Sum(x => x.ValorAplicado);
}
