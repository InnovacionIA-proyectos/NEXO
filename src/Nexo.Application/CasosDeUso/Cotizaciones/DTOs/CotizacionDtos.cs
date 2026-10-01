using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.DTOs;

// DTOs de salida del caso de uso: qué campos de las entidades se exponen. El
// controller de Api los serializa directamente como respuesta HTTP (no hay un
// segundo mapeo redundante en Api para esto).

public record clsCotizacionLineaDto(
    Guid Id, Guid? LineaSolicitudId, decimal ValorFlete, FuenteFlete Fuente,
    decimal? ValorAprovisionamiento = null, decimal? ValorRetornoContenedor = null);

public record clsCotizacionConexoDto(
    Guid Id, Guid CotizacionLineaId, Guid? ServicioConexoId, string? Nombre, string Categoria, decimal Cantidad, decimal ValorAplicado);

public record clsCotizacionDto(
    Guid Id,
    Guid SolicitudId,
    EstadoCotizacion Estado,
    ResultadoCotizacion Resultado,
    decimal Total,
    DateTimeOffset? EntregadaAt,
    IReadOnlyList<clsCotizacionLineaDto> Lineas,
    IReadOnlyList<clsCotizacionConexoDto> Conexos,
    /// <summary>S4-HU-15.6/#252: auto-costeada nunca pasa por /entregar — el frontend
    /// puede usar esto para no ofrecer ese botón en este caso.</summary>
    bool EsAutoCosteo = false);

/// <summary>Resultado de aplicar las propuestas de la Biblioteca como fletes por defecto.</summary>
public record clsAplicarPropuestasResultDto(clsCotizacionDto Cotizacion, int Agregadas);

/// <summary>
/// Resultado de entregar la cotización.
///
/// YA NO DICE SI EL CORREO SE ENVIÓ, y no podría: el envío pasó a un consumidor, así que
/// al responder no ha salido nada. Los campos anteriores —<c>emailEnviado</c>, el correo
/// del responsable y un mensaje con "enviada a fulano"— habrían quedado mintiendo.
///
/// Lo que sí se puede afirmar es que la notificación quedó encolada, y eso está
/// garantizado: el evento se publica en la misma transacción que la entrega. Si la entrega
/// quedó firme, la notificación está pendiente de salir.
///
/// El destinatario tampoco viaja aquí, y es a propósito: resolverlo exigía consultar el
/// Data Warehouse dentro de la petición, que es una de las esperas que esta HU quita.
/// </summary>
/// <param name="EstadoNotificacion">Hoy siempre "encolada". Es lo único cierto al responder.</param>
/// <param name="Mensaje">clsTexto para mostrar a quien entregó.</param>
public record clsEntregarCotizacionResultDto(
    clsCotizacionDto Cotizacion,
    string EstadoNotificacion,
    string Mensaje);

/// <summary>Archivo binario de salida (usado por la exportación a Excel).</summary>
public record clsArchivoDto(byte[] Contenido, string NombreArchivo, string ContentType);
