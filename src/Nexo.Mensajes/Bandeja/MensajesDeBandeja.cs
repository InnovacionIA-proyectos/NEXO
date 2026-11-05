namespace Nexo.Mensajes.Bandeja;

/// <summary>Por qué cambió la bandeja. Viaja en el aviso y en el log.</summary>
public static class clsMotivoDeCambio
{
    /// <summary>Entró una solicitud.</summary>
    public const string SolicitudNueva = "solicitud-nueva";

    /// <summary>Se rechazó una solicitud; deja de estar en la cola de trabajo.</summary>
    public const string SolicitudRechazada = "solicitud-rechazada";

    /// <summary>Terminó el auto-costeo de un pliego; la fila puede salir del filtro de pendientes.</summary>
    public const string TrabajoTerminado = "trabajo-terminado";

    /// <summary>El Admin recalibró los parámetros del score. La bandeja calcula el puntaje al leer, así que el orden cambia en la siguiente consulta.</summary>
    public const string ParametrosRecalibrados = "parametros-recalibrados";

    /// <summary>
    /// Latido del recálculo periódico: la urgencia depende del ahora, así que el orden se mueve con el
    /// tiempo sin que ningún evento lo represente.
    /// </summary>
    public const string RefrescoPeriodico = "refresco-periodico";
}

/// <summary>
/// La bandeja cambió: el cliente debe reconsultarla con sus filtros. No lleva filas (no existe "la bandeja":
/// cada cliente mira una porción distinta) ni secuencia (reconsultar es idempotente, un aviso duplicado
/// o desordenado da el mismo resultado).
/// </summary>
/// <param name="Motivo">Uno de <see cref="clsMotivoDeCambio"/>.</param>
/// <param name="En">Cuándo se produjo el cambio, en UTC.</param>
public sealed record clsBandejaCambio(string Motivo, DateTimeOffset En);
