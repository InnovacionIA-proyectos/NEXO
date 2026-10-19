namespace Nexo.Domain.PlanDeCargue.Entidades;

/// <summary>
/// #304: por qué el worker del Cubicador rechazó "crear_desde_linea" para una línea. Una fila por línea (la clave ES la
/// línea): un rechazo nuevo reemplaza al anterior, y pedir el plan otra vez la borra (empieza de cero).
///
/// Existe porque <c>POST /api/planes-cargue/desde-linea</c> responde 202 y el rechazo vuelve por cola DESPUÉS: como en ese
/// flujo no hay ningún <c>clsPlanCargue</c> que marcar (el Id del mensaje es el de la línea), el motivo solo se registraba
/// en el log y el front sondeaba para siempre sin poder distinguir "todavía procesando" de "falló". Es una tabla aparte, no
/// columnas de la línea, para no tocar una tabla que lee todo el sistema.
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
