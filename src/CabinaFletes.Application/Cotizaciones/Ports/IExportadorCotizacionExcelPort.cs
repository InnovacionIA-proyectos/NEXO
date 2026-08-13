using CabinaFletes.Domain.Entities;

namespace CabinaFletes.Application.Cotizaciones.Ports;

/// <summary>Genera el Excel de detalle de una cotización (fletes + servicios).</summary>
public interface IExportadorCotizacionExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(Cotizacion cotizacion);
}
