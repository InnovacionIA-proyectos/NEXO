namespace Nexo.Domain.Asistente.Entidades;

/// <summary>
/// Preferencia de una persona para el asistente ("avísame si plaza e histórico difieren más de 20 %"), que recibe al
/// inicio de cada una de sus conversaciones. Nunca reemplaza ni salta las reglas del sistema (piso SICE-TAC, precios de una consulta, confirmación de cambios).
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
