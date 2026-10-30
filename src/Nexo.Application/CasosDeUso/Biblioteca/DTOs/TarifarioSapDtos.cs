using Nexo.Application.Common.Dtos;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Biblioteca.DTOs;

/// <summary>Entrada del motor tarifario SAP TM: los datos de una línea de solicitud.</summary>
public record clsConsultaTarifarioSap(
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
    string? CiudadDevolucion = null,
    /// <summary>Horas de cargue+descargue REALES de la solicitud (S4-HU-14.1/#221) — para
    /// recalcular el piso SICE-TAC oficial en vez de asumir las horas pactadas. Null cuando
    /// no hay contexto de solicitud.</summary>
    decimal? HorasSolicitud = null,
    /// <summary>Código DANE del origen y del destino (tbl_municipios.CodigoDane) cuando la ruta de la línea ya resolvió
    /// contra el catálogo de municipios. Con los dos, el piso SICE-TAC oficial compara la ruta exacta por código; si falta
    /// alguno, por el texto del nombre de la ruta.</summary>
    string? OrigenDane = null,
    string? DestinoDane = null);

/// <summary>
/// Resultado del motor: la tarifa de plaza seleccionada por las reglas (si está
/// vencida NUNCA se propone, solo se informa) y el candidato SICETAC oficial
/// cuando la condición de comparación aplica. #300: el retorno del contenedor
/// vacío YA NO sale de aquí (era la tarifa de plaza de la línea 6): se calcula
/// como viaje vacío SICE-TAC con 1h+1h en <c>clsPropuestaTarifaAdapter</c>.
/// </summary>
public record clsResultadoTarifarioSap(
    clsCandidatoTarifaDto? Tarifa,
    bool Vencida,
    clsCandidatoTarifaDto? SicetacOficial);

/// <summary>Ítem de la matriz oficial SICE-TAC (DWSice), espejo 1:1 de la fila del Data Warehouse.</summary>
public record clsRutaSicetacItemDto(
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

public record clsMatrizSicetacResultDto(int Periodo, int Total, IReadOnlyList<clsRutaSicetacItemDto> Items);

public record clsSicetacFiltrosDto(
    IReadOnlyList<int> Periodos,
    IReadOnlyList<string> Configuraciones,
    IReadOnlyList<string> TiposCarga,
    IReadOnlyList<string> UnidadesTransporte);
