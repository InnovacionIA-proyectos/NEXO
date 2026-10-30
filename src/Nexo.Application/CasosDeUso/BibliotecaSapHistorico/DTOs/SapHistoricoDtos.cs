namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;

/// <summary>
/// Tarifa SAP TM (log de cambios tblSAPTarifasLog o tarifas de plaza tblSAPTarifas) expuesta
/// en la Biblioteca. Espejo de la estructura de la tabla del datacenter.
/// </summary>
public record clsSapTarifaHistoricoItemDto(
    int Id,
    int LineaNegocio,
    string? LineaNegocioNombre,    // tblSAPLineaNegocio; null si el ID no está en el catálogo
    string Origen,                 // código DIVIPOLA
    string? OrigenNombre,          // nombre DANE (tblsapdane); null si el código no está en el catálogo
    string Destino,                // código DIVIPOLA
    string? DestinoNombre,         // nombre DANE (tblsapdane); null si el código no está en el catálogo
    decimal? Valor,                // parseado del texto original; null si no es numérico
    string ValorTexto,             // valor tal cual viene de SAP
    DateTime Vigencia,
    int? ClaseContenedor,
    string? TamanoContenedor,
    int? RangoPesoInf,
    int? RangoPesoSup,
    string? TipoVehiculo,
    string? TipoVehiculoNombre,    // tblSAPTiposVehiculoTarifario (por ID): "3S3 · Tractomula"
    string? TipoCarroseria,
    string? TipoCarroseriaNombre,  // tblSAPTiposCarroseria (por CODIGO): "FURGON"
    string? PesoLimite,
    int ClaseViaje,
    string? ClaseViajeNombre,      // tblSAPClaseViaje (por ID): "Contenedor", "Cupo", …
    bool? Devolucion,
    bool? Consolidado,
    bool? Exclusivo,
    string? DetallesTarifa,
    DateTime FechaCreacion,
    DateTime FechaModificacion,
    // Ajuste local de vigencia (override de Nexo). Null = sin ajuste; la vigencia
    // efectiva de la tarifa es VigenciaAjustada si viene, o Vigencia (SAP) si no.
    DateTime? VigenciaAjustada = null);

/// <summary>Resultado paginado (por corte superior) del histórico/plaza SAP.</summary>
public record clsSapHistoricoResultDto(
    int Total,                 // filas que cumplen el filtro
    IReadOnlyList<clsSapTarifaHistoricoItemDto> Items);

/// <summary>Opción de filtro genérica: valor que viaja en el query string + etiqueta legible.</summary>
public record clsOpcionFiltroDto(string Valor, string Etiqueta);

/// <summary>Valores disponibles para los filtros del histórico/plaza SAP (solo los usados en la tabla).</summary>
public record clsSapHistoricoFiltrosDto(
    IReadOnlyList<clsOpcionFiltroDto> LineasNegocio,
    IReadOnlyList<clsOpcionFiltroDto> TiposVehiculo,
    IReadOnlyList<clsOpcionFiltroDto> TiposCarroseria,
    IReadOnlyList<clsOpcionFiltroDto> ClasesViaje);
