namespace Nexo.Application.CasosDeUso.MapeoExcel.DTOs;

/// <summary>
/// Qué propone NEXO para una columna del Excel de un cliente.
/// </summary>
/// <param name="Indice">Posición de la columna en los encabezados recibidos (base 0).</param>
/// <param name="Columna">El encabezado tal como llegó.</param>
/// <param name="Campo">Campo de NEXO al que corresponde, o <c>ignorar</c> si no se usa o no se supo.</param>
/// <param name="Transformacion">Cómo se convierte su valor antes de usarlo (ver <see cref="clsTransformacionesMapeo"/>).</param>
/// <param name="Confianza">De 0 a 1. Con <c>diccionario</c> siempre es 1; con <c>ninguno</c>, 0.</param>
/// <param name="Origen"><c>diccionario</c> (lo guardado del cliente), <c>ia</c> (lo propuso el modelo) o <c>ninguno</c> (sin propuesta).</param>
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
public sealed record clsSugerenciaMapeoDto(
    bool IaUsada,
    string? MotivoSinIa,
    IReadOnlyList<clsColumnaMapeadaDto> Columnas,
    IReadOnlyList<string> Dudas);
