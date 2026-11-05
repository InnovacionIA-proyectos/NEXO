using Nexo.Application.CasosDeUso.Solicitudes.DTOs;

namespace Nexo.Application.CasosDeUso.LecturaExcel.DTOs;

/// <summary>Una hoja del archivo y si la receta la usa.</summary>
public sealed record clsHojaDelArchivoDto(string Nombre, bool Oculta, int Filas, int Columnas, bool Usada);

/// <summary>Qué cambió respecto al formato guardado: títulos que ya no aparecen y títulos nuevos.</summary>
public sealed record clsCambiosDeFormatoDto(IReadOnlyList<string> Faltan, IReadOnlyList<string> Nuevos);

/// <summary>
/// La lectura de un archivo: la receta aplicada, las tablas normales que salen de ella (listas para <c>sugerir-mapeo</c>) y de dónde salió la receta.
/// </summary>
/// <param name="OrigenReceta"><c>guardada</c> (el formato ya confirmado, sin IA), <c>ia</c> (la propuso el modelo), <c>manual</c> (la receta enviada, sin IA) o <c>automatica</c> (lectura simple sin IA).</param>
/// <param name="Similitud">Parecido (0 a 1) con la receta guardada que se usó o se tomó de base; null si el cliente no tenía ninguna.</param>
/// <param name="RecetaGuardadaId">La receta guardada del cliente con la que se comparó, si la hubo.</param>
/// <param name="Resumen">Cómo entendió el archivo la IA, en lenguaje sencillo, para que la persona confirme.</param>
/// <param name="Dudas">Preguntas de la IA que la persona puede responder con una instrucción.</param>
public sealed record clsLecturaEstructuraDto(
    string Archivo, string OrigenReceta, double? Similitud, Guid? RecetaGuardadaId, bool IaUsada, string? MotivoSinIa,
    clsRecetaLectura Receta, string? Resumen, IReadOnlyList<string> Dudas, IReadOnlyList<clsHojaDelArchivoDto> HojasDelArchivo,
    IReadOnlyList<clsHojaExcelDto> Tablas, IReadOnlyList<string> Avisos, clsCambiosDeFormatoDto? Cambios);

public sealed record clsRecetaGuardadaDto(
    Guid Id, Guid ClienteId, string? ClienteNombre, string? Nombre, clsRecetaLectura Receta, string? Instrucciones,
    int Usos, DateTimeOffset? UltimoUsoAt, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <param name="Creada">True si era el primer formato de ese archivo para el cliente; false si reemplazó a uno parecido que ya tenía.</param>
public sealed record clsRecetaGuardadaResultadoDto(clsRecetaGuardadaDto Receta, bool Creada);
