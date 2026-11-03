namespace Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;

/// <summary>Tarifa de plaza gobernada por Nexo (S4-HU-10). <see cref="ZonaOrigen"/>/<see cref="ZonaDestino"/>
/// se resuelven desde el catálogo de municipios al elegir origen/destino (pedido explícito de la Issue #211).
/// Al menos uno de <see cref="PesoId"/>/<see cref="TipoVehiculo"/> viene lleno — no son
/// mutuamente excluyentes: las tarifas de contenedor traen ambos (verificado con datos reales).</summary>
public record clsTarifaPlazaDto(
    Guid Id, string LineaNegocio, string ClaseViaje,
    int OrigenMunicipioId, string Origen, string? ZonaOrigen,
    int DestinoMunicipioId, string Destino, string? ZonaDestino,
    string? ClaseContenedor, int? PesoId, string? RangoPeso, string? TipoVehiculo,
    decimal Valor, DateOnly Vigencia, string DetallesTarifa, DateTimeOffset CreatedAt);

/// <summary>Resultado de una carga masiva de tarifas de plaza desde Excel. <see cref="Errores"/> trae
/// el motivo puntual de cada fila rechazada (tope 20 mensajes).</summary>
public record clsImportarTarifasPlazaResultDto(int Procesadas, int Insertadas, int Actualizadas, int Rechazadas, IReadOnlyList<string> Errores);

/// <summary>Un archivo generado para descarga (exportación).</summary>
public record clsArchivoDto(byte[] Contenido, string NombreArchivo, string ContentType);
