namespace Nexo.Domain.PlanDeCargue.Entidades;

/// <summary>
/// Por qué el Cubicador rechazó "crear_desde_linea" para una línea. Una fila por línea: un rechazo nuevo reemplaza al
/// anterior y pedir el plan otra vez la borra. Como el endpoint responde 202 y el rechazo llega después por cola, no hay un
/// plan donde marcarlo; esta tabla permite distinguir "procesando" de "falló".
/// </summary>
public class clsRechazoCubicacionLinea
{
    /// <summary>Clave y a la vez FK a la línea de solicitud (borrado en cascada con ella).</summary>
    public Guid LineaSolicitudId { get; set; }

    /// <summary>Código del Cubicador tal cual (<c>FLOTA_INSUFICIENTE</c>, <c>VEHICULO_SIN_PRESET</c>, <c>ERROR_INTERNO</c>...).</summary>
    public string Codigo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public DateTimeOffset RechazadoAt { get; set; } = DateTimeOffset.UtcNow;
}
