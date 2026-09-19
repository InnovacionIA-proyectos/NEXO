namespace Nexo.Mensajes.Bandeja;

/// <summary>
/// Por qué cambió la bandeja. Viaja en el aviso para que el cliente pueda decidir —y
/// para que en el log se entienda qué provocó cada refresco.
/// </summary>
public static class MotivoDeCambio
{
    /// <summary>Entró una solicitud. La bandeja tiene una fila más, ya priorizada.</summary>
    public const string SolicitudNueva = "solicitud-nueva";

    /// <summary>Se rechazó una solicitud. Deja de estar en la cola de trabajo.</summary>
    public const string SolicitudRechazada = "solicitud-rechazada";

    /// <summary>
    /// Terminó el auto-costeo de un pliego. Es el momento en que sus líneas dejan de
    /// estar sin flete y la fila puede salir del filtro de pendientes.
    /// </summary>
    public const string TrabajoTerminado = "trabajo-terminado";

    /// <summary>
    /// El Admin recalibró los parámetros del score. Este SÍ reordena de verdad: la
    /// bandeja calcula el puntaje al leer, así que un horizonte distinto cambia el orden
    /// en la siguiente consulta.
    /// </summary>
    public const string ParametrosRecalibrados = "parametros-recalibrados";

    /// <summary>
    /// Latido del recálculo periódico.
    ///
    /// No lo manda porque haya cambiado el puntaje persistido —eso no afecta a la
    /// bandeja, que recalcula al leer— sino porque HA PASADO EL TIEMPO. La urgencia
    /// depende del ahora, así que el orden se mueve solo, de forma continua, sin que
    /// nadie toque nada. Ese cambio no tiene ningún evento que lo represente, y este
    /// latido es lo único que lo cubre.
    /// </summary>
    public const string RefrescoPeriodico = "refresco-periodico";
}

/// <summary>
/// La bandeja cambió. Vuelve a consultarla.
///
/// NO LLEVA LA BANDEJA, y no es por peso: es que no existe "la bandeja". La consulta
/// admite cinco filtros —estado, atención, desde, hasta, vigencia— y cada cliente mira
/// una porción distinta. El servidor no sabe qué filtros usa cada uno, así que empujar
/// filas obligaría a mandarlas todas y filtrar en el navegador.
///
/// Por eso el aviso es una señal y el cliente reconsulta CON SUS FILTROS. La pantalla se
/// actualiza sola, que es lo que pide la HU; lo que no hace es duplicar la consulta
/// dentro del canal.
///
/// TAMPOCO LLEVA SECUENCIA, al contrario que el aviso de avance de un trabajo. Allá el
/// aviso pintaba un valor y uno rezagado hacía retroceder la barra. Aquí solo dice
/// "reconsulta", y eso es idempotente: dos avisos duplicados o desordenados producen dos
/// consultas y el mismo resultado. Agregar un contador sería complejidad sin problema
/// que resolver.
/// </summary>
/// <param name="Motivo">Uno de <see cref="MotivoDeCambio"/>.</param>
/// <param name="En">Cuándo se produjo el cambio, en UTC.</param>
public sealed record BandejaCambio(string Motivo, DateTimeOffset En);
