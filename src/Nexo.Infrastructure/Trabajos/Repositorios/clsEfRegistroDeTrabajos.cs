using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Trabajos.Repositorios;

/// <summary>Crea la fila del trabajo por adelantado con el estado "Initial", el de arranque de toda máquina de estados de MassTransit:
/// la saga encuentra la instancia ya creada y le aplica la transición. La consulta lo traduce a "Pendiente".</summary>
public sealed class clsEfRegistroDeTrabajos(clsNexoDbContext db) : IRegistroDeTrabajos
{
    public const string EstadoInicial = "Initial";

    public void Registrar(Guid trabajoId, Guid solicitudId, Guid solicitadoPor, DateTimeOffset solicitadoEn) =>
        db.Set<clsEstadoAutoCosteo>().Add(new clsEstadoAutoCosteo
        {
            CorrelationId = trabajoId,
            CurrentState = EstadoInicial,
            SolicitudId = solicitudId,
            SolicitadoPor = solicitadoPor,
            SolicitadoEn = solicitadoEn,
        });
}
