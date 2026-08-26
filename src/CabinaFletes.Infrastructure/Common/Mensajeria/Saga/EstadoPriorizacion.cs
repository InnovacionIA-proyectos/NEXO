using MassTransit;

namespace CabinaFletes.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// Estado persistido de la única cadena de recálculo.
///
/// ESTA FILA ES EL CANDADO. El CorrelationId es una constante, así que solo puede
/// existir una fila; y como la instancia se crea al recibir el primer arranque, un
/// segundo arranque cae sobre la que ya está. Por eso no hace falta ningún chequeo de
/// "¿ya hay una cadena?": la restricción de clave primaria lo garantiza.
///
/// Ese era el problema real del PeriodicTimer que esto reemplaza. El reloj vivía en el
/// proceso, así que con dos réplicas del API había dos relojes haciendo el mismo UPDATE
/// sobre las mismas filas, y con concurrencia optimista uno de los dos perdía. Con una
/// sola instancia no se nota, y el día que se escale se nota tarde.
///
/// Los contadores no son adorno: son la única forma de responder "¿esto está corriendo?"
/// sin ponerse a leer logs.
/// </summary>
public class EstadoPriorizacion : SagaStateMachineInstance, ISagaVersion
{
    /// <summary>Siempre <c>CadenaDePriorizacion.Unica</c>. Es lo que hace que no haya dos.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Concurrencia optimista: un tic y un cambio de parámetros pueden coincidir.</summary>
    public int Version { get; set; }

    public string CurrentState { get; set; } = null!;

    /// <summary>Token del temporizador vigente.</summary>
    ///
    /// <remarks>
    /// Es lo que hace idempotente el rearmado. Al reprogramar, MassTransit guarda aquí el
    /// token nuevo y descarta el mensaje del anterior cuando llegue. Sin esto, cada
    /// arranque del API sumaría un temporizador y la frecuencia se multiplicaría sola.
    /// </remarks>
    public Guid? TokenTic { get; set; }

    /// <summary>Cuándo se armó la cadena por primera vez.</summary>
    public DateTimeOffset ArmadaEn { get; set; }

    /// <summary>Último rearmado. Cambia en cada arranque del API.</summary>
    public DateTimeOffset RearmadaEn { get; set; }

    /// <summary>Último recálculo pedido, por cualquiera de los dos disparadores.</summary>
    public DateTimeOffset? UltimoRecalculoEn { get; set; }

    /// <summary>Recálculos pedidos por el reloj.</summary>
    public int RecalculosPorReloj { get; set; }

    /// <summary>Recálculos pedidos porque el Admin recalibró los pesos.</summary>
    public int RecalculosPorParametros { get; set; }
}
