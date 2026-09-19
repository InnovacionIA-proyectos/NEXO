namespace CabinaFletes.Application.CasosDeUso.Cubicacion.DTOs;

/// <summary>
/// [S3-HU-04.3, issue #78, sección 8.2] Espejo 1:1 del contrato HTTP de
/// <c>POST /api/cubicacion</c> de Cubicador (<c>cubicador/api/dtos/cubicacion_rapida.py</c>).
/// A propósito NO hay mapeo de dominio: es cálculo puro sin persistencia, NEXO solo
/// reenvía la solicitud y devuelve la respuesta tal cual — ver
/// <see cref="Interfaces.ICubicacionRapidaPort"/> para por qué esto es una excepción
/// declarada al patrón "solo colas" del resto de Cubicación.
/// </summary>
public sealed record ItemCubicacionRapidaDto(
    string? Descripcion, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm,
    decimal? PesoKg, string? TipoMercancia);

public sealed record CubicacionRapidaSolicitud(
    IReadOnlyList<ItemCubicacionRapidaDto> Items, IReadOnlyList<string>? UnidadIds, string? ClaseViaje);

public sealed record UnidadCubicacionDto(
    string Id, string Nombre, string Geometria, string ControlAmbiental,
    decimal LargoUtilM, decimal AnchoUtilM, decimal AltoUtilM,
    decimal PesoBrutoPermitidoKg, decimal PesoVacioKg, IReadOnlyList<decimal> CapacidadPorEjeKg,
    string? ConfigVehiculo, string? Carroceria, string? ClaseViaje, string? ClaseContenedor);

public sealed record CriterioCubicacionDto(string Criterio, string Resultado, string Detalle, string Criticidad);

public sealed record ColocacionDisposicionDto(
    string Descripcion, decimal X, decimal Y, decimal Z,
    decimal LargoM, decimal AnchoM, decimal AltoM, decimal PesoKg);

public sealed record DisposicionDto(
    decimal PesoTotalKg, IReadOnlyList<decimal> CentroGravedadM, bool MargenEstabilidadOk,
    decimal FactorUtilizacionVolumen, IReadOnlyList<string> ItemsSinUbicar,
    IReadOnlyList<ColocacionDisposicionDto> Colocaciones);

public sealed record MatrizCubicacionDto(
    UnidadCubicacionDto Unidad, IReadOnlyList<CriterioCubicacionDto> Criterios, string Banda,
    IReadOnlyList<string> Advertencias, DisposicionDto? Disposicion);

public sealed record SugerenciaVehiculoDto(
    string UnidadId, string UnidadNombre, string Banda, decimal FactorUtilizacion,
    string? ConfigVehiculo, string? Carroceria, string? ClaseViaje, string? ClaseContenedor);

public sealed record SugerenciaFlotaDto(
    string UnidadId, string UnidadNombre, int CantidadVehiculos, decimal UtilizacionPromedio,
    string? ConfigVehiculo, string? Carroceria, string? ClaseViaje, string? ClaseContenedor,
    IReadOnlyList<DisposicionDto> Disposiciones);

public sealed record MedidasEstandarDto(decimal LargoMaxCm, decimal AnchoMaxCm, decimal AltoMaxCm, decimal PesoMaxKg);

public sealed record PiezaExcedidaDto(
    string Descripcion, IReadOnlyList<string> Excesos, decimal LargoCm, decimal AnchoCm, decimal AltoCm, decimal? PesoKg);

public sealed record DeteccionExtradimensionDto(
    bool HayExtradimension, MedidasEstandarDto MedidasEstandar, IReadOnlyList<PiezaExcedidaDto> Piezas, string? Sugerencia);

public sealed record CubicacionRapidaResultado(
    IReadOnlyList<MatrizCubicacionDto> Matrices, SugerenciaVehiculoDto? Sugerencia, IReadOnlyList<string> Advertencias,
    SugerenciaFlotaDto? SugerenciaFlota, DeteccionExtradimensionDto? Extradimension);
