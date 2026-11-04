namespace Nexo.Mensajes.Notificaciones;

/// <summary>
/// Se agregó o quitó un servicio conexo de una cotización ya entregada. Se publica por el outbox, solo
/// cuando el cambio lo hace otra persona distinta de quien creó la solicitud y nunca sobre un Borrador.
/// La notificación es interna: el consumidor resuelve el destinatario (el creador de la solicitud) desde la base.
/// Una petición = un mensaje, aunque lleve N ítems.
/// </summary>
/// <param name="CotizacionId">La cotización modificada.</param>
/// <param name="SolicitudId">Su solicitud, para llegar a quien la creó.</param>
/// <param name="Accion">"agregado" o "quitado".</param>
/// <param name="Items">Qué servicios cambiaron (nombre, cantidad y valor).</param>
/// <param name="NuevoTotal">Total de la cotización después del cambio.</param>
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
