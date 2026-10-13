using Nexo.Application.Common.Dtos;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.DTOs;

// DTOs de salida del módulo Solicitudes/Captura. Portados de
// Nexo.Api/Dtos/{Responses.cs,SolicitudDtos.cs,CosteoDtos.cs,CapturaDtos.cs}
// del proyecto original.

public record clsLineaSolicitudDto(
    Guid Id, Guid? RutaId, Guid? ConfigVehiculoId, TipologiaCarga? Tipologia,
    decimal? Peso, decimal? ValorMercancia, EstadoLinea EstadoLinea, string? MotivoPendiente);

public record clsSolicitudDto(
    Guid Id, Guid ClienteId, string? ClienteNombre, TipoSolicitud Tipo, ModalidadSolicitud? Modalidad,
    DateTimeOffset? Deadline, decimal? ProbabilidadCierre, decimal? ValorEstimado, decimal ScorePrioridad,
    EstadoSolicitud Estado, DateTimeOffset CreatedAt, IReadOnlyList<clsLineaSolicitudDto> Lineas,
    /// <summary>Notas u observaciones comerciales (S4-HU-18.10/#227).</summary>
    string? Notas = null);

/// <summary>Fila de la Bandeja Priorizada (anti-FIFO).</summary>
public record clsBandejaItemDto(
    Guid Id, string clsCliente, string Tipo, string Estado, DateTimeOffset? Deadline, decimal? ValorEstimado,
    decimal ScorePrioridad, int Lineas, decimal? FleteTotal, int LineasSinFlete, DateTimeOffset FechaCaptura,
    string Atencion, string Vigencia);

/// <summary>Filtros de la Bandeja: estado, atención, vigencia y rango de fecha de captura.</summary>
public record clsBandejaFiltro(
    string? Estado, string? Atencion, DateTimeOffset? Desde, DateTimeOffset? Hasta, string? Vigencia = null);

public record clsDesgloseScoreDto(double Total, double Urgencia, double Valor, double Probabilidad, double Tipo);

/// <summary>Piso SICE-TAC resuelto para una línea, con su semáforo de vigencia.</summary>
public record clsPisoSicetacDto(
    Guid Id, decimal ValorPiso, decimal? ValorHora, decimal? HorasPactadas, int Periodo, string Vigencia,
    string Configuracion, string TipoCarga, string UnidadTransporte, string CondicionCarga);

public record clsHistoricaSugeridaDto(Guid Id, decimal Valor, DateOnly Fecha, TipologiaCarga? Tipologia, string Vigencia);

/// <summary>
/// Posición (pieza/bulto) de la mercancía con dimensiones en cm. Formato JSON
/// persistido en clsLineaSolicitud.Dimensiones: lo escribe la captura, lo lee el costeo.
/// </summary>
public record clsPosicionCargaDto(
    string? Descripcion, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm, decimal? PesoKg,
    /// <summary>S5-HU-18.2/#274: código del tipo de mercancía (no el uuid) de esta posición —
    /// null = tipo desconocido, sin default asignado. Ver clsCapturaPosicionRequest.</summary>
    string? CodigoTipoMercancia = null)
{
    public static readonly System.Text.Json.JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<clsPosicionCargaDto> Desde(string? dimensionesJson)
    {
        if (string.IsNullOrWhiteSpace(dimensionesJson)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<clsPosicionCargaDto>>(dimensionesJson, Json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}

public record clsLineaCosteoDto(
    Guid LineaId, Guid? RutaId, string? clsRuta, TipologiaCarga? Tipologia, decimal? Peso,
    string? TipoVehiculo, string? Carroceria, IReadOnlyList<clsPosicionCargaDto> Posiciones,
    clsPisoSicetacDto? PisoSicetac, IReadOnlyList<clsHistoricaSugeridaDto> Historicas,
    /// <summary>S4-HU-15.5/#251: horas de cargue/descargue EFECTIVAS con las que se
    /// resolvió el piso SICE de esta línea — null en líneas capturadas antes de #251.</summary>
    decimal? HorasCargue = null, decimal? HorasDescargue = null,
    /// <summary>S5-HU-18.3/#275: id de ruta que Cotifly usa para agrupar las líneas de una
    /// misma carga heterogénea repartida en varios camiones. Null si NEXO no recibió el dato
    /// (línea nacida en NEXO, o capturada antes de este cambio).</summary>
    Guid? GrupoRutaId = null,
    /// <summary>S5-HU-18.2/#274: recomendación del Cubicador usada para esta línea.</summary>
    string? IdRecomendacionVehiculo = null,
    /// <summary>S5-HU-20.1/#286: valor de la mercancía capturado para la línea (clsLineaSolicitud.ValorMercancia),
    /// null si no se capturó. Mismo patrón que LineaNegocio/ClaseViaje/ClaseContenedor.</summary>
    decimal? ValorMercancia = null);

public record clsSolicitudCosteoDto(clsSolicitudDto Solicitud, clsTiemposLogisticosDto Tiempos, IReadOnlyList<clsLineaCosteoDto> Lineas);

/// <summary>Resultado de la captura, incluido el auto-costeo con la Biblioteca.</summary>
public record clsCapturaResultDto(
    Guid Id, int Lineas, int RutasCreadas, int VehiculosCreados, decimal ScorePrioridad,
    int FletesCalculados, int LineasPorCostear, bool CosteoEnProceso);

/// <summary>Contenido tabular de una hoja de Excel, listo para que el usuario mapee columnas.</summary>
public record clsHojaExcelDto(
    string Nombre, int FilaEncabezado, IReadOnlyList<string> Encabezados,
    IReadOnlyList<IReadOnlyList<string>> Filas, int TotalFilas);

public record clsAnalisisExcelDto(string Archivo, IReadOnlyList<clsHojaExcelDto> Hojas);

/// <summary>
/// clsCliente para el formulario de captura. Los reales del DW traen Bp (ClienteId nulo
/// hasta que se use en una solicitud); los locales traen ClienteId. Origen distingue
/// "cliente" (ya tiene BP real) de "prospecto" (tblProspectos, todavía sin BP asignado).
/// </summary>
public record clsClienteBusquedaDto(Guid? ClienteId, string? Bp, string Nit, string Nombre, string Origen)
{
    /// <summary>
    /// Alias de <see cref="Nit"/>: nombre de campo que espera Cotifly como identificador
    /// estable de cliente (reemplaza el uso de ClienteId/Bp, que son internos de Nexo y
    /// pueden venir nulos). Mismo valor que <see cref="Nit"/>, expuesto también con este
    /// nombre para no romper el consumidor actual de "nit".
    /// </summary>
    public string NitCliente => Nit;
}

/// <summary>
/// Municipio para Origen/Destino del formulario de captura (S5-HU-16.1/#254). Antes solo
/// traía Municipio/Departamento como texto, resuelto en vivo contra tblSAPDANE; ahora sale
/// de tbl_municipios e incluye Id/CodigoDane/NombreTarifasPlaza(ZC)/ZonaTarificacion(ZTC) —
/// la base para que SICE-TAC (#255) y plaza/histórico (#256) dejen de comparar por texto.
/// </summary>
public record clsCiudadBusquedaDto(
    int Id, string CodigoDane, string Municipio, string Departamento,
    string? NombreTarifasPlaza, string? ZonaTarificacion);

/// <summary>Estimación del aprovisionamiento del contenedor (viaje vacío levante → origen).</summary>
public record clsAprovisionamientoDto(clsCandidatoTarifaDto? Candidato);

/// <summary>Fila completa del catálogo de municipios (todas las columnas de la tabla).</summary>
public record clsMunicipioDto(
    int Id, string CodigoDane, string Municipio, string Departamento, string Pais,
    string? NombreTarifasPlaza, string? ZonaTarificacion);
