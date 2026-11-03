namespace Nexo.Application.CasosDeUso.DiccionarioCliente.DTOs;

/// <param name="ClienteId">Null = el término aplica a todos los clientes.</param>
/// <param name="ClienteNombre">Nombre del cliente; null cuando el término es general.</param>
/// <param name="Contexto">Solo en los términos de tipo <c>valor</c>: el campo al que pertenece el valor.</param>
public sealed record clsTerminoClienteDto(
    Guid Id,
    Guid? ClienteId,
    string? ClienteNombre,
    string Termino,
    string Tipo,
    string Concepto,
    string? Contexto,
    string? Ejemplo,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Los valores que acepta el diccionario, para pintar los formularios sin copiarlos en el front.</summary>
public sealed record clsOpcionesDiccionarioClienteDto(
    IReadOnlyList<string> Tipos,
    IReadOnlyList<string> CamposDestino,
    IReadOnlyList<string> Unidades,
    IReadOnlyList<string> Contextos);
