using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Biblioteca.Dtos;

/// <summary>Entrada del motor tarifario SAP TM: los datos de una línea de solicitud.</summary>
public record ConsultaTarifarioSap(
    string Origen,
    string Destino,
    LineaNegocio? LineaNegocio,
    ClaseViaje? ClaseViaje,
    string? ClaseContenedor,   // texto local: CNT20.IT, CNT40.HC…
    string? ConfigVehiculo,    // config SICE-TAC local: 2, 2L3, 2S2, 3S3…
    string? Carroceria,        // texto local: FURGON, ESTACAS…
    decimal? Peso,             // kg
    bool Exclusivo,
    bool Consolidado,
    bool RetornaContenedor = false,
    string? CiudadDevolucion = null);

/// <summary>
/// Resultado del motor: la tarifa de plaza seleccionada por las reglas (si está
/// vencida NUNCA se propone, solo se informa), el candidato SICETAC oficial
/// cuando la condición de comparación aplica, y la tarifa del retorno del
/// contenedor vacío cuando el viaje lo incluye.
/// </summary>
public record ResultadoTarifarioSap(
    CandidatoTarifaDto? Tarifa,
    bool Vencida,
    CandidatoTarifaDto? SicetacOficial,
    CandidatoTarifaDto? Retorno = null);

/// <summary>Ítem de la matriz oficial SICE-TAC (DWSice), espejo 1:1 de la fila del Data Warehouse.</summary>
public record RutaSicetacItemDto(
    int Id,
    int Periodo,
    string NombreRuta,
    string? Origen,
    string? Destino,
    string Configuracion,
    string NombreTipoCarga,
    string NombreUnidadTransporte,
    string? CondicionCarga,
    decimal Valor,
    decimal? ValorHora,
    decimal? ValorSiceConHora,
    decimal? HorasRecorrido,
    int? HorasPactadas,
    string? ViaEstandar,
    DateTime FechaCreacion,
    DateTime FechaModificacion);

public record MatrizSicetacResultDto(int Periodo, int Total, IReadOnlyList<RutaSicetacItemDto> Items);

public record SicetacFiltrosDto(
    IReadOnlyList<int> Periodos,
    IReadOnlyList<string> Configuraciones,
    IReadOnlyList<string> TiposCarga,
    IReadOnlyList<string> UnidadesTransporte);
