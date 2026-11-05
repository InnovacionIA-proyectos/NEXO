namespace Nexo.Mensajes.Solicitudes;

/// <summary>
/// Se cargaron las líneas de un pliego y hay que costearlas. Lo publica la carga masiva y lo consume la saga
/// del auto-costeo. No lleva las líneas: ya están persistidas y el consumidor las lee por SolicitudId.
/// </summary>
/// <param name="TrabajoId">Correlación del trabajo, devuelta al cliente en el 202.</param>
/// <param name="SolicitudId">Pliego cuyas líneas hay que costear.</param>
/// <param name="TotalLineas">Cuántas líneas se cargaron. Para el avance, no para procesar.</param>
/// <param name="SolicitadoPor">clsUsuario que disparó la carga; viaja para la auditoría.</param>
/// <param name="SolicitadoEn">Momento de la solicitud, en UTC.</param>
public sealed record clsAutoCosteoSolicitado(
    Guid TrabajoId,
    Guid SolicitudId,
    int TotalLineas,
    Guid SolicitadoPor,
    DateTimeOffset SolicitadoEn);
