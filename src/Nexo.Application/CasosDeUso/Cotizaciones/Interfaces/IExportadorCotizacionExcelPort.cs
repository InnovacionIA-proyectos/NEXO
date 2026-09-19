using Nexo.Domain.Cotizaciones.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

/// <summary>Genera el Excel de detalle de una cotización (fletes + servicios).</summary>
public interface IExportadorCotizacionExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(clsCotizacion cotizacion);
}
