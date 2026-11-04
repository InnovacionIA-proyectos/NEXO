namespace Nexo.Mensajes;

/// <summary>
/// Invariantes de un mensaje para poder salir al broker. Viven junto al contrato para que el consumidor
/// pueda revalidar sin duplicar reglas. Devuelve motivos en vez de lanzar: la capa que publica decide el error.
/// </summary>
public interface IValidadorDeMensaje<in T> where T : class
{
    /// <summary>Motivos por los que el mensaje no puede publicarse. Vacío = válido.</summary>
    IReadOnlyList<string> Validar(T mensaje);
}
