using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.DiccionarioCliente;
using Nexo.Application.CasosDeUso.DiccionarioCliente.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Catalogos;

/// <summary>Un término del diccionario de clientes. <c>clienteId</c> null = aplica a todos los clientes.</summary>
public class clsCrearTerminoClienteRequest
{
    public Guid? ClienteId { get; set; }

    [Required(AllowEmptyStrings = false), MaxLength(200)]
    public string Termino { get; set; } = null!;

    /// <summary>"columna", "unidad" o "valor".</summary>
    [Required(AllowEmptyStrings = false), MaxLength(20)]
    public string Tipo { get; set; } = null!;

    /// <summary>Columna: campo de NEXO · unidad: kg o t · valor: valor de NEXO.</summary>
    [Required(AllowEmptyStrings = false), MaxLength(100)]
    public string Concepto { get; set; } = null!;

    /// <summary>Solo para tipo "valor": el campo al que pertenece el valor (tipoVehiculo, carroceria…).</summary>
    [MaxLength(50)]
    public string? Contexto { get; set; }

    [MaxLength(500)]
    public string? Ejemplo { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>Reemplaza los campos editables de un término; el cliente no cambia.</summary>
public class clsEditarTerminoClienteRequest
{
    [Required(AllowEmptyStrings = false), MaxLength(200)]
    public string Termino { get; set; } = null!;

    [Required(AllowEmptyStrings = false), MaxLength(20)]
    public string Tipo { get; set; } = null!;

    [Required(AllowEmptyStrings = false), MaxLength(100)]
    public string Concepto { get; set; } = null!;

    [MaxLength(50)]
    public string? Contexto { get; set; }

    [MaxLength(500)]
    public string? Ejemplo { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>
/// Términos y conceptos que usa cada cliente en sus archivos (cómo se traduce cada palabra suya a lo que entiende NEXO). Leer lo puede
/// cualquier rol válido (la importación lo consulta); crear, editar y borrar es de Comercial, AdminBiblioteca, AdminTarifas y Admin.
/// </summary>
[ApiController]
[Authorize]
[Route("api/diccionario-cliente")]
public class clsDiccionarioClienteController(
    IQueryHandler<clsListarDiccionarioClienteQuery, IReadOnlyList<clsTerminoClienteDto>> listar,
    IQueryHandler<clsObtenerOpcionesDiccionarioClienteQuery, clsOpcionesDiccionarioClienteDto> opciones,
    ICommandHandler<clsCrearTerminoClienteCommand, clsTerminoClienteDto> crear,
    ICommandHandler<clsEditarTerminoClienteCommand, clsTerminoClienteDto> editar,
    ICommandHandler<clsEliminarTerminoClienteCommand, Unit> eliminar)
    : ControllerBase
{
    // El Comercial también lo mantiene: es quien importa los Excel de sus clientes y enseña qué significa cada término.
    private const string RolesEscritura = "Comercial,AdminBiblioteca,AdminTarifas,Admin";

    /// <summary>
    /// Los términos del diccionario. Con <c>clienteId</c> trae los de ese cliente y, por defecto, también los que aplican a todos
    /// (<c>incluirGenerales=false</c> los omite); sin él, los de todos los clientes. <c>tipo</c> = columna | unidad | valor,
    /// <c>q</c> busca en el término y el concepto y <c>soloActivos</c> omite los inactivos. Otro tipo → 400 TIPO_TERMINO_INVALIDO.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsTerminoClienteDto>>> Listar(
        [FromQuery] Guid? clienteId, [FromQuery] bool incluirGenerales = true, [FromQuery] string? tipo = null,
        [FromQuery] string? q = null, [FromQuery] bool soloActivos = false, CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarDiccionarioClienteQuery(clienteId, incluirGenerales, tipo, q, soloActivos), ct));

    /// <summary>Los valores que acepta el diccionario (tipos, campos de NEXO, unidades y contextos) para armar los formularios.</summary>
    [HttpGet("opciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsOpcionesDiccionarioClienteDto>> Opciones(CancellationToken ct = default)
        => Ok(await opciones.Handle(new clsObtenerOpcionesDiccionarioClienteQuery(), ct));

    /// <summary>
    /// Agrega un término. 400 si el tipo, el concepto o el contexto no son válidos o el cliente no existe; 409 TERMINO_DUPLICADO si el
    /// cliente ya tiene ese término (sin distinguir mayúsculas ni tildes) con el mismo tipo y contexto.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RolesEscritura)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsTerminoClienteDto>> Crear([FromBody] clsCrearTerminoClienteRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(
            new clsCrearTerminoClienteCommand(req.ClienteId, req.Termino, req.Tipo, req.Concepto, req.Contexto, req.Ejemplo, req.Activo), ct);
        return Created($"/api/diccionario-cliente?clienteId={dto.ClienteId}", dto);
    }

    /// <summary>Edita un término (reemplaza término, tipo, concepto, contexto, ejemplo y estado). 404 si no existe; 400 y 409 como al crear.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RolesEscritura)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsTerminoClienteDto>> Editar(Guid id, [FromBody] clsEditarTerminoClienteRequest req, CancellationToken ct = default)
        => Ok(await editar.Handle(
            new clsEditarTerminoClienteCommand(id, req.Termino, req.Tipo, req.Concepto, req.Contexto, req.Ejemplo, req.Activo), ct));

    /// <summary>Borra un término. 204; 404 si no existe.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RolesEscritura)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct = default)
    {
        await eliminar.Handle(new clsEliminarTerminoClienteCommand(id), ct);
        return NoContent();
    }
}
