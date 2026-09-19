namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;

/// <summary>Regla "solo SICE-TAC" (origen + destino + tipo de vehículo) demarcada por el Administrador.</summary>
public record clsReglaSoloSicetacDto(Guid Id, string Origen, string Destino, string TipoVehiculo, DateTimeOffset CreatedAt);

/// <summary>Una fila leída de un Excel de carga masiva (ya con las 3 columnas resueltas, sin normalizar).</summary>
public record clsFilaReglaSoloSicetacDto(string Origen, string Destino, string TipoVehiculo);

/// <summary>Resultado de una carga masiva de reglas desde Excel.</summary>
public record clsImportarReglasResultDto(int Filas, int Creadas, int Duplicadas, int Invalidas);
