using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Domain.Cotizaciones.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

/// <summary>Genera el Excel de detalle de una cotización (fletes + servicios).</summary>
public interface IExportadorCotizacionExcelPort
{
    /// <param name="lineasRechazadas">Las líneas rechazadas de la solicitud: salen como «Sin costeo», sin precio y SIN motivo (el motivo va en el
    /// correo, no en el Excel ni el PDF).</param>
    (byte[] Contenido, string NombreArchivo) Generar(clsCotizacion cotizacion, IReadOnlyList<clsLineaRechazadaInfo>? lineasRechazadas = null);
}
