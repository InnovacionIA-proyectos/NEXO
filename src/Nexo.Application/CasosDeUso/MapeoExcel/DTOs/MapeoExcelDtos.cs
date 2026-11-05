namespace Nexo.Application.CasosDeUso.MapeoExcel.DTOs;

/// <summary>Qué propone NEXO para una columna del Excel de un cliente.</summary>
/// <param name="Indice">Posición de la columna en los encabezados recibidos (base 0).</param>
/// <param name="Campo">Campo de NEXO al que corresponde, o <c>ignorar</c> si no se usa o no se supo.</param>
/// <param name="Confianza">De 0 a 1. Con <c>diccionario</c> siempre es 1; con <c>ninguno</c>, 0.</param>
/// <param name="Origen"><c>plantilla</c> (el mapeo ya confirmado de esos encabezados), <c>diccionario</c> (lo guardado del cliente), <c>ia</c> (lo propuso el modelo) o <c>ninguno</c> (sin propuesta).</param>
public sealed record clsColumnaMapeadaDto(
    int Indice,
    string Columna,
    string Campo,
    string Transformacion,
    double Confianza,
    string Origen,
    string? Nota);

/// <summary>
/// Propuesta de mapeo de columnas: una entrada por columna, en orden; es solo una sugerencia que el usuario confirma. Sin IA, igual
/// llega lo resuelto por el diccionario.
/// </summary>
/// <param name="MotivoSinIa">Por qué no participó la IA: <c>DICCIONARIO_COMPLETO</c>, <c>IA_NO_DISPONIBLE</c> o el código <c>IA_*</c> del proveedor; null si participó.</param>
/// <param name="Dudas">Lo que el usuario debería mirar: columnas en conflicto, campos obligatorios sin columna, observaciones del modelo.</param>
/// <param name="PlantillaId">La plantilla guardada que se reconoció (los mismos encabezados que un archivo anterior del cliente); null si no hubo.</param>
public sealed record clsSugerenciaMapeoDto(
    bool IaUsada,
    string? MotivoSinIa,
    IReadOnlyList<clsColumnaMapeadaDto> Columnas,
    IReadOnlyList<string> Dudas,
    Guid? PlantillaId = null);

/// <summary>Una columna del archivo y lo que se hace con ella, tal como la confirmó el usuario.</summary>
/// <param name="Campo">Campo de NEXO al que va, o <c>ignorar</c>.</param>
/// <param name="Transformacion">Una de <see cref="clsTransformacionesMapeo.Todas"/>; vacía equivale a <c>ninguna</c>.</param>
public sealed record clsColumnaAAplicar(int Indice, string? Campo, string? Transformacion);

/// <summary>
/// Una fila del Excel convertida a línea de captura (el cuerpo de <c>lineas</c> de <c>POST /api/captura-solicitudes</c>).
/// <paramref name="Referencia"/> y <paramref name="Deadline"/> son de la solicitud, no de la línea.
/// </summary>
/// <param name="Fila">El número de fila en el archivo (el de Excel).</param>
public sealed record clsLineaImportadaDto(
    int Fila,
    string Origen,
    string Destino,
    string? TipoVehiculo,
    string? Carroceria,
    string? Tipologia,
    string? LineaNegocio,
    string? ClaseViaje,
    string? ClaseContenedor,
    bool? Exclusivo,
    bool? Consolidado,
    bool? RetornaContenedor,
    string? CiudadDevolucionContenedor,
    bool? LevanteContenedorDiferente,
    string? CiudadLevanteContenedor,
    decimal? HorasCargue,
    decimal? HorasDescargue,
    decimal? Peso,
    decimal? ValorMercancia,
    IReadOnlyList<Nexo.Application.CasosDeUso.Solicitudes.DTOs.clsPosicionCargaDto>? Posiciones,
    string? Referencia,
    DateTimeOffset? Deadline);

/// <param name="Codigo">Estable, para que el front pueda reaccionar (<c>NUMERO_INVALIDO</c>, <c>PESO_REQUERIDO</c>…).</param>
public sealed record clsErrorDeFilaDto(int Fila, string? Campo, string Codigo, string Mensaje);

/// <summary>Resultado de aplicar un mapeo: una fila con error no entra en <paramref name="Lineas"/>, se reporta en <paramref name="Errores"/>. Nada se guarda.</summary>
/// <param name="FilasVacias">Filas sin ningún dato en las columnas usadas; se saltan sin error.</param>
/// <param name="TotalErrores">Cuántos errores hay en total; <paramref name="Errores"/> trae solo los primeros si son demasiados.</param>
public sealed record clsResultadoAplicarMapeoDto(
    int TotalFilas,
    int FilasVacias,
    int FilasConErrores,
    IReadOnlyList<clsLineaImportadaDto> Lineas,
    IReadOnlyList<clsErrorDeFilaDto> Errores,
    int TotalErrores);
