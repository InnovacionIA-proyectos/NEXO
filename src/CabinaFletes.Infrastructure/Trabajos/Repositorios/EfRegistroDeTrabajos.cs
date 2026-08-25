using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Infrastructure.Persistence;

namespace CabinaFletes.Infrastructure.Trabajos.Repositorios;

/// <summary>
/// Crea la fila del trabajo por adelantado, con el estado inicial que MassTransit
/// reconoce.
///
/// "Initial" no es un nombre inventado: es el estado de arranque de toda máquina de
/// estados de MassTransit. Al consumir el mensaje, la saga encuentra esta instancia ya
/// creada y le aplica la transición, en vez de crear otra. Es lo que permite que el
/// trabajo exista antes de que la orquestación arranque.
///
/// El adaptador de consulta traduce "Initial" a "Pendiente" para el cliente: el nombre
/// interno de la máquina de estados no es asunto de quien consume el API.
/// </summary>
public sealed class EfRegistroDeTrabajos(CabinaFletesDbContext db) : IRegistroDeTrabajos
{
    public const string EstadoInicial = "Initial";

    public void Registrar(Guid trabajoId, Guid solicitudId, Guid solicitadoPor, DateTimeOffset solicitadoEn) =>
        db.Set<EstadoAutoCosteo>().Add(new EstadoAutoCosteo
        {
            CorrelationId = trabajoId,
            CurrentState = EstadoInicial,
            SolicitudId = solicitudId,
            SolicitadoPor = solicitadoPor,
            SolicitadoEn = solicitadoEn,
        });
}
