namespace Nexo.Mensajes.Notificaciones;

/// <summary>
/// Se agregó o se quitó un servicio conexo de una cotización que YA estaba entregada, y quien
/// la pidió tiene que enterarse (S5-HU-20.1/#286, HU-B23/B24).
///
/// Lo publica el caso de uso POR EL OUTBOX —misma transacción que el cambio— igual que
/// <see cref="clsCotizacionEntregada"/>: no puede quedar un cambio firme sin aviso pendiente,
/// ni un aviso de un cambio que se revirtió. Solo se publica cuando el cambio lo hace OTRA
/// persona distinta de quien creó la solicitud (a uno mismo no se le avisa lo que acaba de
/// hacer) y nunca sobre una cotización Borrador (todavía no hay nada entregado que corregir);
/// esa decisión es del caso de uso, no de este contrato.
///
/// LA NOTIFICACIÓN ES INTERNA: va al correo corporativo de quien creó la solicitud
/// (<c>clsSolicitud.Creador</c>), nunca al cliente. El destinatario no viaja aquí: lo resuelve
/// el consumidor desde la base, igual que en la entrega.
///
/// Una petición = un mensaje: "agregar a todas las líneas" ya llega como UNA petición con N
/// ítems y produce UN aviso. Agrupar peticiones sucesivas en un solo correo queda por decidir.
/// </summary>
/// <param name="CotizacionId">La cotización modificada.</param>
/// <param name="SolicitudId">Su solicitud, para llegar a quien la creó.</param>
/// <param name="Accion">"agregado" o "quitado".</param>
/// <param name="Items">Qué servicios cambiaron (nombre, cantidad y valor).</param>
/// <param name="NuevoTotal">Total de la cotización DESPUÉS del cambio.</param>
/// <param name="ModificadoPor">Quién hizo el cambio.</param>
/// <param name="ModificadoEn">Momento del cambio, en UTC.</param>
public sealed record clsConexosCotizacionCambiados(
    Guid CotizacionId,
    Guid SolicitudId,
    string Accion,
    IReadOnlyList<clsConexoCambiado> Items,
    decimal NuevoTotal,
    Guid ModificadoPor,
    DateTimeOffset ModificadoEn);

/// <summary>Un servicio conexo que se agregó o se quitó.</summary>
public sealed record clsConexoCambiado(string Servicio, decimal Cantidad, decimal Valor);
