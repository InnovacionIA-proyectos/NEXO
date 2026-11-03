namespace Nexo.Domain.Asistente.Entidades;

/// <summary>
/// Una regla o preferencia propia de una persona para el asistente ("avísame si plaza e histórico difieren más de 20 %"). El asistente la
/// recibe al inicio de cada conversación de esa persona. Es una PREFERENCIA: nunca reemplaza ni salta las reglas del sistema (el piso
/// SICE-TAC, que todo precio venga de una consulta, la confirmación de los cambios).
/// </summary>
public class clsPreferenciaAsistente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>De quién es; solo esa persona la ve, la usa y la borra.</summary>
    public Guid UsuarioId { get; set; }

    public string Texto { get; set; } = null!;

    /// <summary>La conversación en la que la dijo, para saber de dónde salió.</summary>
    public Guid? ConversacionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
