namespace CabinaFletes.Mensajes.Solicitudes;

/// <summary>
/// Invariantes de <see cref="AutoCosteoSolicitado"/>.
///
/// Ninguna es exótica, y ese es el punto: son las que convierten el mensaje en algo
/// procesable. Un trabajo sin correlación no se puede consultar, un pliego sin
/// identificador no se puede leer, y un mensaje con cero líneas manda a un consumidor
/// a costear un pliego vacío.
/// </summary>
public sealed class AutoCosteoSolicitadoValidador : IValidadorDeMensaje<AutoCosteoSolicitado>
{
    public IReadOnlyList<string> Validar(AutoCosteoSolicitado mensaje)
    {
        var motivos = new List<string>();

        if (mensaje.TrabajoId == Guid.Empty)
            motivos.Add("TrabajoId no puede ser vacío: es la correlación con la que el cliente consulta el avance.");

        if (mensaje.SolicitudId == Guid.Empty)
            motivos.Add("SolicitudId no puede ser vacío: el consumidor lee las líneas de la base por ese id.");

        if (mensaje.TotalLineas <= 0)
            motivos.Add("TotalLineas debe ser mayor que cero: no hay nada que costear en un pliego vacío.");

        if (mensaje.SolicitadoPor == Guid.Empty)
            motivos.Add("SolicitadoPor no puede ser vacío: la auditoría quedaría sin autor.");

        if (mensaje.SolicitadoEn == default)
            motivos.Add("SolicitadoEn no puede ser la fecha por defecto.");

        return motivos;
    }
}
