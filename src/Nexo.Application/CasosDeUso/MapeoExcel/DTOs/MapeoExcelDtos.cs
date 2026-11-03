namespace Nexo.Application.CasosDeUso.MapeoExcel.DTOs;

/// <summary>
/// Qué propone NEXO para una columna del Excel de un cliente.
/// </summary>
/// <param name="Indice">Posición de la columna en los encabezados recibidos (base 0).</param>
/// <param name="Columna">El encabezado tal como llegó.</param>
/// <param name="Campo">Campo de NEXO al que corresponde, o <c>ignorar</c> si no se usa o no se supo.</param>
/// <param name="Transformacion">Cómo se convierte su valor antes de usarlo (ver <see cref="clsTransformacionesMapeo"/>).</param>
/// <param name="Confianza">De 0 a 1. Con <c>diccionario</c> siempre es 1; con <c>ninguno</c>, 0.</param>
/// <param name="Origen"><c>plantilla</c> (el mapeo ya confirmado de esos encabezados), <c>diccionario</c> (lo guardado del cliente), <c>ia</c> (lo propuso el modelo) o <c>ninguno</c> (sin propuesta).</param>
/// <param name="Nota">Una aclaración corta de por qué; puede venir vacía.</param>
public sealed record clsColumnaMapeadaDto(
    int Indice,
    string Columna,
    string Campo,
    string Transformacion,
    double Confianza,
    string Origen,
    string? Nota);

/// <summary>
/// La propuesta de mapeo de columnas de un Excel de cliente. Siempre trae una entrada por columna, en el orden del archivo, y es solo
/// una sugerencia: el usuario la revisa y la confirma. Si la IA no está disponible, lo que se pudo resolver con el diccionario del
/// cliente sigue llegando y el resto queda para mapear a mano.
/// </summary>
/// <param name="IaUsada">True si el modelo participó en esta propuesta.</param>
/// <param name="MotivoSinIa">
/// Por qué no participó: <c>DICCIONARIO_COMPLETO</c> (el diccionario resolvió todas las columnas), <c>IA_NO_DISPONIBLE</c> (sin llave o con la
/// función apagada) o el código <c>IA_*</c> del error del proveedor. Null cuando sí participó.
/// </param>
/// <param name="Dudas">Lo que el usuario debería mirar: columnas en conflicto, campos obligatorios sin columna, observaciones del modelo.</param>
/// <param name="PlantillaId">La plantilla guardada que se reconoció (los mismos encabezados que un archivo anterior del cliente); null si no hubo.</param>
public sealed record clsSugerenciaMapeoDto(
    bool IaUsada,
    string? MotivoSinIa,
    IReadOnlyList<clsColumnaMapeadaDto> Columnas,
    IReadOnlyList<string> Dudas,
    Guid? PlantillaId = null);

// ---------------------------------------------------------------- aplicar el mapeo

/// <summary>Una columna del archivo y lo que se hace con ella, tal como la confirmó el usuario.</summary>
/// <param name="Indice">Posición de la columna en las filas (base 0).</param>
/// <param name="Campo">Campo de NEXO al que va, o <c>ignorar</c>.</param>
/// <param name="Transformacion">Una de <see cref="clsTransformacionesMapeo.Todas"/>; vacía equivale a <c>ninguna</c>.</param>
public sealed record clsColumnaAAplicar(int Indice, string? Campo, string? Transformacion);

/// <summary>
/// Una fila del Excel ya convertida a la forma de una línea de captura: el mismo cuerpo que <c>POST /api/captura-solicitudes</c> recibe en
/// <c>lineas</c>. <paramref name="Referencia"/> y <paramref name="Deadline"/> son datos de la solicitud completa, no de la línea: llegan por fila
/// porque así vienen en el archivo, y quien captura decide cuál usar.
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

/// <param name="Fila">El número de fila en el archivo.</param>
/// <param name="Campo">El campo de NEXO al que se refiere el error, si es de uno solo.</param>
/// <param name="Codigo">Estable, para que el front pueda reaccionar (<c>NUMERO_INVALIDO</c>, <c>PESO_REQUERIDO</c>…).</param>
public sealed record clsErrorDeFilaDto(int Fila, string? Campo, string Codigo, string Mensaje);

/// <summary>
/// El resultado de aplicar un mapeo a las filas de un Excel. Una fila con algún error no entra en <paramref name="Lineas"/>: se reporta en
/// <paramref name="Errores"/> (todos los de la fila) para que el usuario la corrija y repita. Nada se guarda.
/// </summary>
/// <param name="TotalFilas">Las filas recibidas.</param>
/// <param name="FilasVacias">Filas sin ningún dato en las columnas usadas; se saltan sin error.</param>
/// <param name="FilasConErrores">Filas que no pudieron convertirse.</param>
/// <param name="TotalErrores">Cuántos errores hay en total; <paramref name="Errores"/> trae solo los primeros si son demasiados.</param>
public sealed record clsResultadoAplicarMapeoDto(
    int TotalFilas,
    int FilasVacias,
    int FilasConErrores,
    IReadOnlyList<clsLineaImportadaDto> Lineas,
    IReadOnlyList<clsErrorDeFilaDto> Errores,
    int TotalErrores);
