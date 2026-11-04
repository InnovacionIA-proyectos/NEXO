using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.DTOs;

public record clsCotizacionLineaDto(
    Guid Id, Guid? LineaSolicitudId, decimal ValorFlete, FuenteFlete Fuente,
    decimal? ValorAprovisionamiento = null, decimal? ValorRetornoContenedor = null);

/// <summary>
/// <see cref="Origen"/> ("individual" | "general"): si el conexo se agregó a ESTA línea o a todas de una vez (igual crea una fila por línea).
/// <see cref="Servicio"/> trae el servicio completo para que el front no baje el catálogo.
/// </summary>
public record clsCotizacionConexoDto(
    Guid Id, Guid CotizacionLineaId, Guid? ServicioConexoId, string? Nombre, string Categoria, decimal Cantidad, decimal ValorAplicado,
    string Origen = "individual", clsServicioConexoDto? Servicio = null, decimal PrecioVentaUnitario = 0, decimal ValorVenta = 0);

public record clsCotizacionDto(
    Guid Id,
    Guid SolicitudId,
    EstadoCotizacion Estado,
    ResultadoCotizacion Resultado,
    decimal Total,
    DateTimeOffset? EntregadaAt,
    IReadOnlyList<clsCotizacionLineaDto> Lineas,
    IReadOnlyList<clsCotizacionConexoDto> Conexos,
    /// <summary>Una auto-costeada nunca pasa por /entregar: el front no debe ofrecer ese botón.</summary>
    bool EsAutoCosteo = false,
    /// <summary>Última vez que se cambiaron conexos tras la entrega (null si nunca); no altera Estado ni EntregadaAt.</summary>
    DateTimeOffset? ModificadaTrasEntregaAt = null);

/// <summary>Resultado de aplicar las propuestas de la Biblioteca como fletes por defecto.</summary>
public record clsAplicarPropuestasResultDto(clsCotizacionDto Cotizacion, int Agregadas);

/// <summary>
/// Resultado de entregar la cotización. No dice si el correo se envió (lo hace un consumidor y al responder no ha salido nada): solo que la
/// notificación quedó encolada, garantizado porque el evento se publica en la misma transacción. El destinatario tampoco viaja aquí.
/// </summary>
/// <param name="EstadoNotificacion">Hoy siempre "encolada".</param>
public record clsEntregarCotizacionResultDto(
    clsCotizacionDto Cotizacion,
    string EstadoNotificacion,
    string Mensaje);

/// <summary>Archivo binario de salida (usado por la exportación a Excel).</summary>
public record clsArchivoDto(byte[] Contenido, string NombreArchivo, string ContentType);
