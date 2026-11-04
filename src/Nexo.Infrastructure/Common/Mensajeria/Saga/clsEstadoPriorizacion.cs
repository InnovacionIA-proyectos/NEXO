using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// Estado de la única cadena de recálculo. La fila es el candado: el CorrelationId es constante, así que la clave primaria impide dos cadenas
/// (con un reloj por proceso, dos réplicas del API harían el mismo UPDATE a la vez). Los contadores permiten saber si corre sin leer logs.
/// </summary>
public class clsEstadoPriorizacion : SagaStateMachineInstance, ISagaVersion
{
    /// <summary>Siempre <c>clsCadenaDePriorizacion.Unica</c>. Es lo que hace que no haya dos.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Concurrencia optimista: un tic y un cambio de parámetros pueden coincidir.</summary>
    public int Version { get; set; }

    public string CurrentState { get; set; } = null!;

    /// <summary>Token del temporizador vigente: al reprogramar se descarta el anterior, así cada arranque del API no suma temporizadores.</summary>
    public Guid? TokenTic { get; set; }

    public DateTimeOffset ArmadaEn { get; set; }

    /// <summary>Último rearmado. Cambia en cada arranque del API.</summary>
    public DateTimeOffset RearmadaEn { get; set; }

    /// <summary>Último recálculo pedido, por cualquiera de los dos disparadores.</summary>
    public DateTimeOffset? UltimoRecalculoEn { get; set; }

    public int RecalculosPorReloj { get; set; }

    /// <summary>Recálculos pedidos porque el Admin recalibró los pesos.</summary>
    public int RecalculosPorParametros { get; set; }
}
