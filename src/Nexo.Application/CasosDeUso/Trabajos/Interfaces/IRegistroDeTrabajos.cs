namespace Nexo.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>
/// Deja registrado un trabajo en el MISMO momento en que se acepta.
///
/// Existe para cerrar una asperaza concreta: el endpoint devuelve 202 con un
/// identificador dentro de la transacción, pero la orquestación arranca cuando el
/// mensaje se consume, o sea después del commit. En esa ventana, consultar ese
/// identificador respondía 404 — un 404 para algo que se le acaba de decir al cliente
/// que existe.
///
/// Registrando el trabajo en la misma transacción, la ventana desaparece: el
/// identificador es consultable desde el instante en que el cliente lo recibe.
///
/// No confirma nada por su cuenta: el registro viaja con el resto de la transacción.
/// Si el commit falla, el trabajo no existió — igual que las líneas.
/// </summary>
public interface IRegistroDeTrabajos
{
    void Registrar(Guid trabajoId, Guid solicitudId, Guid solicitadoPor, DateTimeOffset solicitadoEn);
}
