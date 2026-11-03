using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Domain.Cotizaciones.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

/// <summary>Genera el Excel de detalle de una cotización (fletes + servicios).</summary>
public interface IExportadorCotizacionExcelPort
{
    /// <param name="lineasRechazadas">S6-HU-24.3/#320: las líneas de la solicitud que se rechazaron. Salen en el Excel como «Sin costeo»,
    /// sin precio y SIN motivo (decisión: el motivo va en el correo al comercial, no en el Excel ni en el PDF).</param>
    (byte[] Contenido, string NombreArchivo) Generar(clsCotizacion cotizacion, IReadOnlyList<clsLineaRechazadaInfo>? lineasRechazadas = null);
}
