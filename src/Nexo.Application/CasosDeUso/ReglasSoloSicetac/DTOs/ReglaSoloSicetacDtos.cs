namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;

/// <summary>
/// Regla "solo SICE-TAC" (origen + destino + tipo de vehículo) demarcada por el
/// Administrador. Origen/Destino son el valor crudo guardado (código DANE en las filas
/// cargadas por Excel — S4-HU-09.2 — o texto libre en las más viejas). OrigenMunicipio/
/// DestinoMunicipio son el nombre resuelto contra el catálogo de municipios para mostrar
/// en el listado (#242-adjacent); null si el valor crudo no matchea ningún código DANE
/// conocido, en cuyo caso el frontend debe mostrar el valor crudo tal cual.
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
