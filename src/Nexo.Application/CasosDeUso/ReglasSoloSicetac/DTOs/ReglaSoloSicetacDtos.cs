namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;

/// <summary>
/// Regla "solo SICE-TAC". Origen/Destino son el valor crudo (código DANE o texto libre); OrigenMunicipio/DestinoMunicipio son el nombre
/// resuelto, null si no matchea ningún código DANE (el frontend muestra entonces el crudo).
/// </summary>
public record clsReglaSoloSicetacDto(
    Guid Id, string Origen, string Destino, string? OrigenMunicipio, string? DestinoMunicipio,
    string TipoVehiculo, DateTimeOffset CreatedAt);

/// <summary>Una fila leída de un Excel de carga masiva (ya con las 3 columnas resueltas, sin normalizar).</summary>
public record clsFilaReglaSoloSicetacDto(string Origen, string Destino, string TipoVehiculo);

/// <summary>Resultado de una carga masiva de reglas desde Excel. <see cref="Errores"/> trae
/// el motivo puntual de cada fila inválida o duplicada (tope 20 mensajes).</summary>
public record clsImportarReglasResultDto(int Filas, int Creadas, int Duplicadas, int Invalidas, IReadOnlyList<string> Errores);

/// <summary>Un archivo generado para descarga (plantilla, exportación).</summary>
public record clsArchivoDto(byte[] Contenido, string NombreArchivo, string ContentType);
