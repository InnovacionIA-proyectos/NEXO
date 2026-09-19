namespace Nexo.Mensajes.Notificaciones;

/// <summary>
/// Se entregó una cotización y hay que notificarlo por correo.
///
/// Lo publica el caso de uso de entrega POR EL OUTBOX, o sea en la misma transacción que
/// la entrega. Esa es la garantía que importa: no puede quedar una cotización entregada
/// sin notificación pendiente, ni una notificación de una entrega que se revirtió.
///
/// LA NOTIFICACIÓN ES INTERNA. Va al correo corporativo de quien pidió la consulta de
/// flete; nunca al cliente. Está dicho aquí porque este mensaje es el contrato: quien
/// escriba otro consumidor lo lee antes que cualquier documento.
///
/// POR QUÉ EL PDF VIAJA EN EL MENSAJE. Es la única pieza que no está en la base: llega
/// como archivo en la petición de entrega y hoy no se persiste en ningún lado. Si no
/// viajara, el consumidor no tendría de dónde sacarlo y el correo saldría sin el formato
/// que es justo lo que el comercial necesita.
///
/// Y viajando en el mensaje sobrevive a los reintentos: si el relay está caído tres
/// horas, el mensaje espera en la cola con su adjunto. El precio es un mensaje grande
/// —un formato ronda los cientos de kilobytes— que además pasa por la tabla de salida.
/// A este volumen no es problema; si algún día los formatos crecen a varios megas o las
/// entregas pasan de unos cientos al día, toca moverlo a un almacén aparte y dejar aquí
/// solo la referencia.
/// </summary>
/// <param name="CotizacionId">La cotización entregada. El consumidor lee sus datos de la base.</param>
/// <param name="SolicitudId">Su solicitud, para llegar al cliente.</param>
/// <param name="EntregadaPor">Quién la entregó. Viaja para la auditoría.</param>
/// <param name="EntregadaEn">Momento de la entrega, en UTC.</param>
/// <param name="FormatoPdf">El formato que se adjunta. Nulo si la entrega no trajo archivo.</param>
/// <param name="NombrePdf">Nombre con el que se adjunta.</param>
public sealed record clsCotizacionEntregada(
    Guid CotizacionId,
    Guid SolicitudId,
    Guid EntregadaPor,
    DateTimeOffset EntregadaEn,
    byte[]? FormatoPdf,
    string? NombrePdf);
