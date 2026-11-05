using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexo.Api.Common.Errors;
using Nexo.Application.CasosDeUso.LecturaExcel;
using Nexo.Application.CasosDeUso.LecturaExcel.DTOs;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Api.Solicitudes;

/// <summary>Un Excel para leer su estructura, con lo que ayuda a leerlo. Se envía como <c>multipart/form-data</c>.</summary>
public class clsLeerEstructuraRequest
{
    /// <summary>El Excel (.xlsx o .xlsm, máximo 10 MB).</summary>
    [Required]
    public IFormFile Archivo { get; set; } = null!;

    /// <summary>El cliente del archivo, para reutilizar sus formatos guardados. Alternativa: <c>clienteNit</c>; si vienen los dos manda este. Sin ninguno no se busca formato guardado.</summary>
    public Guid? ClienteId { get; set; }

    /// <summary>El NIT del cliente (con o sin puntos y dígito de verificación). Solo busca: no crea el cliente.</summary>
    [MaxLength(30)]
    public string? ClienteNit { get; set; }

    /// <summary>Lo que la persona explica o corrige sobre cómo está armado el archivo, en lenguaje natural (máximo 4000 caracteres). Con instrucciones siempre se consulta a la IA.</summary>
    [MaxLength(4000)]
    public string? Instrucciones { get; set; }

    /// <summary>La receta (JSON) de una lectura anterior. Sola, se ejecuta tal cual sin IA; con <c>instrucciones</c>, la IA la ajusta.</summary>
    public string? Receta { get; set; }
}

/// <summary>Guarda como formato del cliente la receta confirmada. Se envía como <c>multipart/form-data</c>.</summary>
public class clsGuardarRecetaLecturaRequest
{
    /// <summary>El mismo Excel con el que se confirmó la receta: sirve para comprobarla y para reconocer después el mismo formato.</summary>
    [Required]
    public IFormFile Archivo { get; set; } = null!;

    /// <summary>El cliente dueño del formato. Alternativa: <c>clienteNit</c>; si vienen los dos manda este.</summary>
    public Guid? ClienteId { get; set; }

    /// <summary>El NIT del cliente o prospecto. Si aún no tiene cliente local se crea con los datos del DW; 400 <c>CLIENTE_NIT_INVALIDO</c> si no es de ninguno.</summary>
    [MaxLength(30)]
    public string? ClienteNit { get; set; }

    /// <summary>Un nombre para reconocerlo en una lista (opcional).</summary>
    [MaxLength(100)]
    public string? Nombre { get; set; }

    /// <summary>La receta confirmada (JSON), la que devolvió <c>leer-estructura</c>.</summary>
    [Required]
    public string Receta { get; set; } = null!;

    /// <summary>Lo que la persona explicó sobre el archivo: se le da de contexto a la IA cuando el formato cambie (opcional).</summary>
    [MaxLength(4000)]
    public string? Instrucciones { get; set; }
}

/// <summary>
/// Lectura de Excel de formatos muy distintos: la IA propone una «receta» (dónde están encabezados y datos, tablas cruzadas, hojas) y la persona la confirma
/// sobre una vista previa. La receta confirmada se guarda por cliente y los siguientes archivos con el mismo formato se leen con ella sin llamar a la IA.
/// Las tablas que salen alimentan <c>sugerir-mapeo</c>. Lo usa quien importa archivos: Comercial y Admin.
/// </summary>
[ApiController]
[Authorize]
[Route("api/captura-solicitudes")]
public class clsRecetasLecturaController(
    ICommandHandler<clsLeerEstructuraCommand, clsLecturaEstructuraDto> leer,
    ICommandHandler<clsGuardarRecetaLecturaCommand, clsRecetaGuardadaResultadoDto> guardar,
    IQueryHandler<clsListarRecetasLecturaQuery, IReadOnlyList<clsRecetaGuardadaDto>> listar,
    ICommandHandler<clsEliminarRecetaLecturaCommand, Unit> eliminar)
    : ControllerBase
{
    /// <summary>
    /// Lee la estructura del Excel y devuelve sus tablas con la receta usada (<c>origenReceta</c>: guardada, ia, manual o automatica). Sin <c>instrucciones</c>
    /// reutiliza el formato guardado del cliente si se parece (sin IA); si el formato cambió, o la persona corrige con una instrucción, la IA propone otra receta
    /// partiendo de la anterior. Si la IA no está disponible responde igual (<c>motivoSinIa</c>). No guarda nada.
    /// </summary>
    [HttpPost("leer-estructura")]
    [Authorize(Roles = "Comercial,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsLecturaEstructuraDto>> LeerEstructura([FromForm] clsLeerEstructuraRequest req, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(req.Archivo);
        return Ok(await leer.Handle(new clsLeerEstructuraCommand(
            stream, req.Archivo.FileName, req.ClienteId, req.ClienteNit, req.Instrucciones, req.Receta), ct));
    }

    /// <summary>
    /// Guarda la receta confirmada como formato del cliente: 201 la primera vez, 200 si reemplazó a uno parecido (≥ 85 %) que ya tenía. Antes la ejecuta contra
    /// el archivo y exige que dé filas (400 con el código del problema). Cada cliente conserva hasta 20 formatos: al pasar el límite se descarta el que lleva más tiempo sin usarse.
    /// </summary>
    [HttpPut("recetas-lectura")]
    [Authorize(Roles = "Comercial,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsRecetaGuardadaDto>> Guardar([FromForm] clsGuardarRecetaLecturaRequest req, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(req.Archivo);
        var resultado = await guardar.Handle(new clsGuardarRecetaLecturaCommand(
            stream, req.ClienteId, req.ClienteNit, req.Nombre, req.Receta, req.Instrucciones), ct);
        return resultado.Creada
            ? Created($"/api/captura-solicitudes/recetas-lectura?clienteId={resultado.Receta.ClienteId}", resultado.Receta)
            : Ok(resultado.Receta);
    }

    /// <summary>
    /// Los formatos guardados: con <c>clienteId</c> o <c>clienteNit</c> los de ese cliente; sin ninguno, los de todos. Con un NIT que aún no tiene
    /// cliente local no hay formatos: responde lista vacía y no crea nada.
    /// </summary>
    [HttpGet("recetas-lectura")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsRecetaGuardadaDto>>> Listar(
        [FromQuery] Guid? clienteId, [FromQuery] string? clienteNit, CancellationToken ct = default) =>
        Ok(await listar.Handle(new clsListarRecetasLecturaQuery(clienteId, clienteNit), ct));

    /// <summary>Borra un formato guardado: el siguiente archivo de ese cliente vuelve a pasar por la IA. 204; 404 si no existe.</summary>
    [HttpDelete("recetas-lectura/{id:guid}")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct = default)
    {
        await eliminar.Handle(new clsEliminarRecetaLecturaCommand(id), ct);
        return NoContent();
    }
}
