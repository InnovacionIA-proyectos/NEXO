namespace Nexo.Mensajes.Notificaciones;

/// <summary>
/// Se entregó una cotización y hay que notificarlo por correo. Se publica por el outbox, en la misma
/// transacción que la entrega. La notificación es interna: va al correo corporativo de quien pidió la
/// consulta de flete, nunca al cliente.
/// </summary>
/// <param name="CotizacionId">La cotización entregada. El consumidor lee sus datos de la base.</param>
/// <param name="SolicitudId">Su solicitud, para llegar al cliente.</param>
/// <param name="EntregadaPor">Quién la entregó. Viaja para la auditoría.</param>
/// <param name="EntregadaEn">Momento de la entrega, en UTC.</param>
/// <param name="FormatoPdf">El formato que se adjunta; no se persiste, por eso viaja en el mensaje (sobrevive a los reintentos). Nulo si la entrega no trajo archivo.</param>
/// <param name="NombrePdf">Nombre con el que se adjunta.</param>
public sealed record clsCotizacionEntregada(
    Guid CotizacionId,
    Guid SolicitudId,
    Guid EntregadaPor,
    DateTimeOffset EntregadaEn,
    byte[]? FormatoPdf,
    string? NombrePdf);
