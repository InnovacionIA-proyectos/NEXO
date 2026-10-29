namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>Lo que se transmite al cliente mientras el asistente responde, en orden.</summary>
public abstract record clsEventoAsistente
{
    /// <summary>Primer evento: a qué conversación y respuesta pertenece lo que sigue (la conversación se crea con el primer mensaje).</summary>
    public sealed record Conversacion(Guid ConversacionId, Guid MensajeId) : clsEventoAsistente;

    public sealed record Fragmento(string Delta) : clsEventoAsistente;

    /// <summary><paramref name="Estado"/>: <c>ejecutando</c>, <c>ok</c> o <c>error</c>.</summary>
    public sealed record Herramienta(string Nombre, string Estado, string? Fuente) : clsEventoAsistente;

    /// <summary>
    /// La respuesta traía cifras de dinero que no salen de ninguna consulta: lo mostrado hasta ahora debe sustituirse por
    /// <paramref name="Texto"/>, que es también lo que queda guardado.
    /// </summary>
    public sealed record Reemplazo(string Texto, IReadOnlyList<string> CifrasSinOrigen) : clsEventoAsistente;

    /// <summary><paramref name="Estado"/>: <c>completo</c>, <c>incompleto</c> o <c>descartado</c>.</summary>
    public sealed record Fin(Guid ConversacionId, Guid MensajeId, string Estado) : clsEventoAsistente;

    /// <summary>La respuesta se cortó a mitad de la transmisión; lo ya mostrado queda como respuesta incompleta.</summary>
    public sealed record Error(string Codigo, string Mensaje) : clsEventoAsistente;
}

public sealed record clsConversacionResumenDto(Guid Id, string Titulo, int Turnos, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record clsMensajeAsistenteDto(Guid Id, int Orden, string Rol, string Contenido, string Estado, DateTimeOffset CreatedAt);

/// <summary>Una herramienta que usó el asistente en una respuesta. La salida completa queda en la base para auditoría y no se expone.</summary>
public sealed record clsUsoHerramientaDto(
    Guid MensajeId, string Herramienta, string Entrada, bool? Encontrado, string? Fuente, string Resultado, int LatenciaMs, DateTimeOffset CreatedAt);

public sealed record clsConversacionDetalleDto(
    Guid Id, string Titulo, int Turnos, Guid UsuarioId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<clsMensajeAsistenteDto> Mensajes, IReadOnlyList<clsUsoHerramientaDto> Herramientas);
