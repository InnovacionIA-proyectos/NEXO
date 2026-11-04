namespace Nexo.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>
/// Registra el trabajo en el MISMO momento en que se acepta, dentro de la transacción: así el identificador que recibe el cliente (202)
/// es consultable de inmediato, sin ventana de 404 hasta que la orquestación arranque tras el commit. No confirma nada por su cuenta.
/// </summary>
public interface IRegistroDeTrabajos
{
    void Registrar(Guid trabajoId, Guid solicitudId, Guid solicitadoPor, DateTimeOffset solicitadoEn);
}
