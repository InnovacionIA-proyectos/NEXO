namespace Nexo.Application.CasosDeUso.PlantillasMapeo.DTOs;

/// <summary>Lo que se hace con una columna de la plantilla.</summary>
/// <param name="Indice">Posición de la columna en el archivo (base 0).</param>
/// <param name="Campo">Campo de NEXO al que va, o <c>ignorar</c>.</param>
/// <param name="Transformacion">Una de las conversiones del mapeo (<c>ninguna</c> si no hay).</param>
public sealed record clsColumnaPlantillaDto(int Indice, string Campo, string Transformacion);

public sealed record clsPlantillaMapeoDto(
    Guid Id, Guid ClienteId, string? ClienteNombre, string? Nombre, IReadOnlyList<string> Encabezados,
    IReadOnlyList<clsColumnaPlantillaDto> Columnas, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <param name="Creada">True si era la primera plantilla de esos encabezados para el cliente; false si reemplazó a la que ya tenía.</param>
public sealed record clsPlantillaGuardadaDto(clsPlantillaMapeoDto Plantilla, bool Creada);
