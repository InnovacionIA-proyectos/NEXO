using CabinaFletes.Domain.Entities;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Cotizaciones.Dtos;

// DTOs de salida del caso de uso: qué campos de las entidades se exponen. El
// controller de Api los serializa directamente como respuesta HTTP (no hay un
// segundo mapeo redundante en Api para esto).

public record CotizacionLineaDto(
    Guid Id, Guid? LineaSolicitudId, decimal ValorFlete, FuenteFlete Fuente,
    decimal? ValorAprovisionamiento = null, decimal? ValorRetornoContenedor = null);

public record CotizacionConexoDto(
    Guid Id, Guid? ServicioConexoId, string? Nombre, string Categoria, decimal Cantidad, decimal ValorAplicado);

public record CotizacionDto(
    Guid Id,
    Guid SolicitudId,
    EstadoCotizacion Estado,
    ResultadoCotizacion Resultado,
    decimal Total,
    DateTimeOffset? EntregadaAt,
    IReadOnlyList<CotizacionLineaDto> Lineas,
    IReadOnlyList<CotizacionConexoDto> Conexos);

/// <summary>Resultado de aplicar las propuestas de la Biblioteca como fletes por defecto.</summary>
public record AplicarPropuestasResultDto(CotizacionDto Cotizacion, int Agregadas);

/// <summary>
/// Resultado de entregar la cotización: la cotización entregada + el estado del
/// envío del formato por email al comercial responsable del cliente.
/// </summary>
public record EntregarCotizacionResultDto(
    CotizacionDto Cotizacion,
    bool EmailEnviado,
    string? NombreResponsable,
    string? CorreoResponsable,
    string Mensaje);

/// <summary>Archivo binario de salida (usado por la exportación a Excel).</summary>
public record ArchivoDto(byte[] Contenido, string NombreArchivo, string ContentType);

/// <summary>Mapeadores entidad -> DTO. Único punto donde se decide qué se serializa.</summary>
public static class CotizacionDtoMappers
{
    public static CotizacionDto ToDto(this Cotizacion c) =>
        new(
            c.Id,
            c.SolicitudId,
            c.Estado,
            c.Resultado,
            c.Total,
            c.EntregadaAt,
            c.Lineas.Select(l => new CotizacionLineaDto(
                l.Id, l.LineaSolicitudId, l.ValorFlete, l.Fuente,
                l.ValorAprovisionamiento, l.ValorRetornoContenedor)).ToList(),
            c.Conexos.Select(x => new CotizacionConexoDto(
                x.Id, x.ServicioConexoId, x.ServicioConexo?.Nombre,
                x.ServicioConexo?.Categoria ?? "logistico", x.Cantidad, x.ValorAplicado)).ToList());
}
