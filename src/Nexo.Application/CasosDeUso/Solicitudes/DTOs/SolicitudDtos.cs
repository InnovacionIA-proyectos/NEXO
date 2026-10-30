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
    string Atencion, string Vigencia,
    /// <summary>S5-HU-23.1/#289 (HU-B25): "SinDefinir" | "Ganada" | "Perdida"; null si la solicitud no tiene
    /// cotización. Con varias: la que tenga resultado distinto de SinDefinir, si no la más reciente.</summary>
    string? Resultado = null);

/// <summary>Filtros de la Bandeja: estado, atención, vigencia, rango de fecha de captura y (S5-HU-23.1/#289)
/// resultado de la cotización: esperando | ganada | perdida (ver <c>clsBandejaResultado</c>).</summary>
public record clsBandejaFiltro(
    string? Estado, string? Atencion, DateTimeOffset? Desde, DateTimeOffset? Hasta, string? Vigencia = null,
    string? Resultado = null);

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
    decimal? ValorMercancia = null,
    /// <summary>S5-HU-20.5/#297: orden de la línea en el arreglo con que se capturó (1, 2, 3...). null en líneas
    /// capturadas antes de S5-HU-18.3, que además salen al final del GET.</summary>
    int? NumeroLinea = null,
    /// <summary>S6-HU-26.1/#30: <c>true</c> si la ruta y el vehículo de la línea tienen regla Solo-SICE. Con regla el flete lo fija el
    /// piso y NO se puede editar (PUT .../lineas/{id} responde 409 FLETE_SOLO_SICETAC): el front no debe ofrecer "Editar flete".
    /// Misma definición que el motor de propuesta y que la edición (clsReglaSoloSicetacDeLinea).</summary>
    bool TieneReglaSoloSicetac = false,
    /// <summary>S6-HU-24.1/#309: estado de la línea en el costeo — <c>porCostear</c>, <c>autoResuelta</c> o <c>revisada</c>
    /// (minúscula inicial). Antes solo vivía en la entidad: Cotifly veía una línea en consulta completa como "pendiente de
    /// costeo" y no sabía por qué.</summary>
    string? EstadoLinea = null,
    /// <summary>S6-HU-24.1/#309: por qué sigue en <c>porCostear</c> (consulta completa, línea sin vehículo, SICE-TAC que no es del
    /// mes actual...). Texto para mostrar al usuario; null cuando la línea no está pendiente.</summary>
    string? MotivoPendiente = null,
    /// <summary>S6-HU-29.1/#311: línea de negocio capturada, con minúscula inicial (<c>puertoAInterior</c>, <c>interiorAPuerto</c>,
    /// <c>nacional</c>, <c>devolucionContenedor</c>, <c>andino</c>, <c>licitaciones</c>); null si la línea no la trae.</summary>
    string? LineaNegocio = null,
    /// <summary>S6-HU-29.1/#311: clase de viaje (<c>contenedor</c>, <c>cupo</c>, <c>cargaSuelta</c>, <c>extradimensionado</c>); null si no.</summary>
    string? ClaseViaje = null,
    /// <summary>S6-HU-29.1/#311: clase de contenedor tal como se capturó (<c>CNT20.IT</c>...). <b>null salvo que la clase de viaje sea
    /// <c>contenedor</c></b>: en otras clases el dato, si llegó, no aplica.</summary>
    string? ClaseContenedor = null,
    /// <summary>S6-HU-29.1/#311: id de <c>tbl_municipios</c> del origen/destino de la ruta (el mismo de <c>GET /ciudades</c>), o null si
    /// la ruta no resolvió municipio. Evita que el front deduzca la ciudad leyendo el texto (68 nombres repetidos en el catálogo,
    /// p. ej. "Armenia", y nombres que no coinciden con el catálogo, p. ej. "Bogota" contra "BOGOTA, D.C.").</summary>
    int? OrigenMunicipioId = null, int? DestinoMunicipioId = null,
    /// <summary>S6-HU-24.3/#320: por qué se rechazó la línea (<c>estadoLinea = "rechazada"</c>); null en cualquier otro estado. Cotifly lo
    /// necesita para no dejar la cotización "pendiente" por una línea que se dejó sin costear a propósito.</summary>
    string? MotivoRechazo = null);

public record clsSolicitudCosteoDto(
    clsSolicitudDto Solicitud, clsTiemposLogisticosDto Tiempos, IReadOnlyList<clsLineaCosteoDto> Lineas,
    /// <summary>#300: aviso visible cuando el piso SICE-TAC NO se usa porque el período más reciente cargado no es
    /// el del mes actual (las líneas salen con <c>pisoSicetac</c> null). null = el piso se usa con normalidad.</summary>
    string? AvisoSicetac = null);

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
