namespace Nexo.Mensajes.Solicitudes;

/// <summary>
/// Se cargaron las líneas de un pliego y hay que costearlas.
///
/// Lo publica la carga masiva; lo consume la saga del auto-costeo (issue #15), que
/// hace el reparto por línea. El <see cref="TrabajoId"/> es el mismo identificador
/// que el endpoint devolvió al cliente con el 202: es la forma de que quien preguntó
/// y quien procesa hablen del mismo trabajo.
///
/// El mensaje NO lleva las líneas. Un pliego de 300 no cabe cómodamente en la cola y
/// además ya están persistidas: el consumidor las lee de la base por SolicitudId. Lo
/// que viaja es el aviso, no los datos.
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
