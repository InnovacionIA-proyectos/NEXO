namespace CabinaFletes.Mensajes.Cubicacion;

/// <summary>Invariantes de <see cref="CubicacionSolicitada"/> — ver PublicadorValidado
/// (falla cerrado sin este validador registrado).</summary>
public sealed class CubicacionSolicitadaValidador : IValidadorDeMensaje<CubicacionSolicitada>
{
    public IReadOnlyList<string> Validar(CubicacionSolicitada mensaje)
    {
        var motivos = new List<string>();

        if (string.IsNullOrWhiteSpace(mensaje.PlanId) || !Guid.TryParse(mensaje.PlanId, out _))
            motivos.Add("PlanId debe ser un GUID válido: Cubicador optimiza el plan que ya tiene con ese id.");

        if (string.IsNullOrWhiteSpace(mensaje.CorrelationId))
            motivos.Add("CorrelationId no puede ser vacío: es con lo que se correlaciona la respuesta.");

        if (string.IsNullOrWhiteSpace(mensaje.Tipo))
            motivos.Add("Tipo no puede ser vacío: el worker de Cubicador lo usa para elegir el caso de uso.");

        if (string.IsNullOrWhiteSpace(mensaje.Solicitud.Estrategia))
            motivos.Add("Solicitud.Estrategia no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(mensaje.Solicitud.Algoritmo))
            motivos.Add("Solicitud.Algoritmo no puede ser vacío.");

        return motivos;
    }
}
