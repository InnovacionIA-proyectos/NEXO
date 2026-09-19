namespace Nexo.Mensajes;

/// <summary>
/// Invariantes de un mensaje: qué tiene que cumplir para poder salir al broker.
///
/// Vive junto a los contratos y no en la capa que publica, porque son reglas DEL
/// mensaje, no de quien lo manda. Eso le sirve también a quien consume: puede
/// revalidar al recibir sin duplicar las reglas.
///
/// Devuelve los motivos en vez de lanzar para que este proyecto siga sin depender
/// de nada. Quién convierte esos motivos en un error, y de qué tipo, es decisión de
/// la capa que publica.
/// </summary>
/// <typeparam name="T">Tipo de mensaje que valida.</typeparam>
public interface IValidadorDeMensaje<in T> where T : class
{
    /// <summary>Motivos por los que el mensaje no puede publicarse. Vacío = válido.</summary>
    IReadOnlyList<string> Validar(T mensaje);
}
