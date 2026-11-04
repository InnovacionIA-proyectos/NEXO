using Nexo.Application.Common.Dtos;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.DTOs;

public record clsLineaSolicitudDto(
    Guid Id, Guid? RutaId, Guid? ConfigVehiculoId, TipologiaCarga? Tipologia,
    decimal? Peso, decimal? ValorMercancia, EstadoLinea EstadoLinea, string? MotivoPendiente);

public record clsSolicitudDto(
    Guid Id, Guid ClienteId, string? ClienteNombre, TipoSolicitud Tipo, ModalidadSolicitud? Modalidad,
    DateTimeOffset? Deadline, decimal? ProbabilidadCierre, decimal? ValorEstimado, decimal ScorePrioridad,
    EstadoSolicitud Estado, DateTimeOffset CreatedAt, IReadOnlyList<clsLineaSolicitudDto> Lineas,
    string? Notas = null);

/// <summary>Fila de la Bandeja Priorizada (anti-FIFO).</summary>
public record clsBandejaItemDto(
    Guid Id, string clsCliente, string Tipo, string Estado, DateTimeOffset? Deadline, decimal? ValorEstimado,
    decimal ScorePrioridad, int Lineas, decimal? FleteTotal, int LineasSinFlete, DateTimeOffset FechaCaptura,
    string Atencion, string Vigencia,
    /// <summary>"SinDefinir" | "Ganada" | "Perdida"; null si no hay cotización. Con varias: la de resultado distinto de SinDefinir, si no la más reciente.</summary>
    string? Resultado = null);

/// <summary>Filtros de la Bandeja; <c>resultado</c>: esperando | ganada | perdida (ver <c>clsBandejaResultado</c>).</summary>
public record clsBandejaFiltro(
    string? Estado, string? Atencion, DateTimeOffset? Desde, DateTimeOffset? Hasta, string? Vigencia = null,
    string? Resultado = null);

public record clsDesgloseScoreDto(double Total, double Urgencia, double Valor, double Probabilidad, double Tipo);

/// <summary>Piso SICE-TAC resuelto para una línea, con su semáforo de vigencia.</summary>
public record clsPisoSicetacDto(
    Guid Id, decimal ValorPiso, decimal? ValorHora, decimal? HorasPactadas, int Periodo, string Vigencia,
    string Configuracion, string TipoCarga, string UnidadTransporte, string CondicionCarga);

public record clsHistoricaSugeridaDto(Guid Id, decimal Valor, DateOnly Fecha, TipologiaCarga? Tipologia, string Vigencia);

/// <summary>Posición (pieza/bulto) de la mercancía con dimensiones en cm; es el JSON persistido en clsLineaSolicitud.Dimensiones.</summary>
public record clsPosicionCargaDto(
    string? Descripcion, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm, decimal? PesoKg,
    /// <summary>Código del tipo de mercancía (no el uuid); null = tipo desconocido, sin default asignado.</summary>
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
    /// <summary>Horas de cargue/descargue EFECTIVAS con las que se resolvió el piso SICE; null en líneas capturadas antes de existir este dato.</summary>
    decimal? HorasCargue = null, decimal? HorasDescargue = null,
    /// <summary>Id de ruta con que Cotifly agrupa las líneas de una misma carga heterogénea; null si NEXO no recibió el dato.</summary>
    Guid? GrupoRutaId = null,
    /// <summary>Recomendación del Cubicador usada para esta línea.</summary>
    string? IdRecomendacionVehiculo = null,
    /// <summary>Valor de la mercancía capturado para la línea; null si no se capturó.</summary>
    decimal? ValorMercancia = null,
    /// <summary>Orden de la línea en el arreglo con que se capturó (1, 2, 3...); null en líneas antiguas, que salen al final del GET.</summary>
    int? NumeroLinea = null,
    /// <summary><c>true</c> si la ruta y el vehículo tienen regla Solo-SICE: el flete lo fija el piso y NO se puede editar (PUT .../lineas/{id}
    /// responde 409 FLETE_SOLO_SICETAC); el front no debe ofrecer "Editar flete".</summary>
    bool TieneReglaSoloSicetac = false,
    /// <summary>Estado de la línea en el costeo: <c>porCostear</c>, <c>autoResuelta</c> o <c>revisada</c> (minúscula inicial).</summary>
    string? EstadoLinea = null,
    /// <summary>Por qué sigue en <c>porCostear</c> (consulta completa, línea sin vehículo, SICE-TAC que no es del mes actual...). Texto para
    /// mostrar al usuario; null cuando la línea no está pendiente.</summary>
    string? MotivoPendiente = null,
    /// <summary>Línea de negocio capturada, con minúscula inicial (<c>puertoAInterior</c>, <c>interiorAPuerto</c>, <c>nacional</c>,
    /// <c>devolucionContenedor</c>, <c>andino</c>, <c>licitaciones</c>); null si la línea no la trae.</summary>
    string? LineaNegocio = null,
    /// <summary>Clase de viaje (<c>contenedor</c>, <c>cupo</c>, <c>cargaSuelta</c>, <c>extradimensionado</c>); null si no.</summary>
    string? ClaseViaje = null,
    /// <summary>Clase de contenedor tal como se capturó (<c>CNT20.IT</c>...). <b>null salvo que la clase de viaje sea <c>contenedor</c></b>.</summary>
    string? ClaseContenedor = null,
    /// <summary>Id de <c>tbl_municipios</c> del origen/destino de la ruta (el de <c>GET /ciudades</c>); null si la ruta no resolvió municipio.
    /// Evita que el front deduzca la ciudad por el texto, que se repite o no coincide con el catálogo.</summary>
    int? OrigenMunicipioId = null, int? DestinoMunicipioId = null,
    /// <summary>Por qué se rechazó la línea (<c>estadoLinea = "rechazada"</c>); null en otro estado. Cotifly lo usa para no dejar la cotización
    /// "pendiente" por una línea que se dejó sin costear a propósito.</summary>
    string? MotivoRechazo = null);

public record clsSolicitudCosteoDto(
    clsSolicitudDto Solicitud, clsTiemposLogisticosDto Tiempos, IReadOnlyList<clsLineaCosteoDto> Lineas,
    /// <summary>Aviso cuando el piso SICE-TAC NO se usa porque el período cargado más reciente no es el del mes actual (las líneas salen con
    /// <c>pisoSicetac</c> null); null = se usa con normalidad.</summary>
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
/// clsCliente para el formulario de captura. Los reales del DW traen Bp (ClienteId nulo hasta usarse en una solicitud); los locales traen
/// ClienteId. Origen: "cliente" (con BP real) o "prospecto" (sin BP asignado).
/// </summary>
public record clsClienteBusquedaDto(Guid? ClienteId, string? Bp, string Nit, string Nombre, string Origen)
{
    /// <summary>Alias de <see cref="Nit"/> con el nombre de campo que espera Cotifly como identificador estable de cliente.</summary>
    public string NitCliente => Nit;
}

/// <summary>Municipio para Origen/Destino del formulario de captura; sale de tbl_municipios con Id, CodigoDane y zonas de tarificación.</summary>
public record clsCiudadBusquedaDto(
    int Id, string CodigoDane, string Municipio, string Departamento,
    string? NombreTarifasPlaza, string? ZonaTarificacion);

/// <summary>Estimación del aprovisionamiento del contenedor (viaje vacío levante → origen).</summary>
public record clsAprovisionamientoDto(clsCandidatoTarifaDto? Candidato);

/// <summary>Fila completa del catálogo de municipios (todas las columnas de la tabla).</summary>
public record clsMunicipioDto(
    int Id, string CodigoDane, string Municipio, string Departamento, string Pais,
    string? NombreTarifasPlaza, string? ZonaTarificacion);
