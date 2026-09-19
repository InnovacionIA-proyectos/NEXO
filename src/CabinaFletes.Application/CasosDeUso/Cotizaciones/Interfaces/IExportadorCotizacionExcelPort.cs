using CabinaFletes.Domain.Cotizaciones.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;

/// <summary>Genera el Excel de detalle de una cotización (fletes + servicios).</summary>
public interface IExportadorCotizacionExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(Cotizacion cotizacion);
}
