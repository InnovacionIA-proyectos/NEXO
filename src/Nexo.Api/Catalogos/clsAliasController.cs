using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.Catalogos;
using Nexo.Application.CasosDeUso.Catalogos.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Catalogos;

/// <summary>S6-HU-27.2/#321: agregar un alias al diccionario. El texto y el código son obligatorios.</summary>
public class clsCrearAliasRequest
{
    /// <summary>"vehiculo" o "contenedor".</summary>
    [Required(AllowEmptyStrings = false)]
    public string Tipo { get; set; } = null!;

    [Required(AllowEmptyStrings = false), MaxLength(100)]
    public string Alias { get; set; } = null!;

    [Required(AllowEmptyStrings = false), MaxLength(20)]
    public string Codigo { get; set; } = null!;
}

/// <summary>S6-HU-27.3/#322: mapear una configuración en cuarentena a un código oficial.</summary>
public class clsMapearVehiculoRequest
{
    [Required(AllowEmptyStrings = false), MaxLength(20)]
    public string Codigo { get; set; } = null!;
}

/// <summary>
/// Diccionario de alias de vehículos y de clases de contenedor (S6-HU-27.2/#321) y bandeja de normalización de vehículos en cuarentena
/// (S6-HU-27.3/#322). Leer el diccionario y "resolver" un texto lo puede cualquier usuario autenticado (Cotifly lo usa al importar un
/// Excel); agregar alias y toda la bandeja son solo de AdminTarifas y Admin.
/// </summary>
[ApiController]
[Authorize]
[Route("api/catalogos")]
public class clsAliasController(
    IQueryHandler<clsListarAliasQuery, IReadOnlyList<clsAliasDto>> listarAlias,
    IQueryHandler<clsResolverAliasQuery, clsResolverAliasDto> resolverAlias,
    ICommandHandler<clsCrearAliasCommand, clsAliasDto> crearAlias,
    IQueryHandler<clsListarVehiculosCuarentenaQuery, IReadOnlyList<clsVehiculoCuarentenaDto>> listarCuarentena,
    ICommandHandler<clsMapearVehiculoCuarentenaCommand, clsMapeoVehiculoDto> mapearVehiculo,
    ICommandHandler<clsDescartarVehiculoCuarentenaCommand, Unit> descartarVehiculo)
    : ControllerBase
{
    /// <summary>El diccionario completo. <c>tipo</c> = vehiculo | contenedor (sin él, los dos). Otro valor → 400 TIPO_ALIAS_INVALIDO.</summary>
    [HttpGet("alias")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsAliasDto>>> Listar([FromQuery] string? tipo, CancellationToken ct = default)
        => Ok(await listarAlias.Handle(new clsListarAliasQuery(tipo), ct));

    /// <summary>
    /// ¿A qué código corresponde este texto suelto? (<c>tipo</c> = vehiculo por defecto | contenedor). Responde <c>reconocido: false</c> para un
    /// texto que el diccionario no conoce — en la captura caería en cuarentena (vehículo) o quedaría como texto libre (contenedor).
    /// </summary>
    [HttpGet("alias/resolver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<clsResolverAliasDto>> Resolver(
        [FromQuery] string? tipo, [FromQuery] string? texto, CancellationToken ct = default)
        => Ok(await resolverAlias.Handle(new clsResolverAliasQuery(tipo, texto), ct));

    /// <summary>
    /// Agrega un alias. Solo AdminTarifas y Admin. 400 si el tipo o el código no son oficiales; 409 ALIAS_DUPLICADO si el texto (normalizado) ya
    /// está en el diccionario, en cualquier tipo.
    /// </summary>
    [HttpPost("alias")]
    [Authorize(Roles = "AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsAliasDto>> Crear([FromBody] clsCrearAliasRequest req, CancellationToken ct = default)
    {
        var dto = await crearAlias.Handle(new clsCrearAliasCommand(req.Tipo, req.Alias, req.Codigo), ct);
        return Created($"/api/catalogos/alias?tipo={dto.TipoElemento.ToLowerInvariant()}", dto);
    }

    /// <summary>
    /// Bandeja de normalización: las configuraciones de vehículo en cuarentena (<c>EsCatalogoOficial = false</c>) que nadie ha mapeado ni descartado.
    /// Solo AdminTarifas y Admin.
    /// </summary>
    [HttpGet("vehiculos-cuarentena")]
    [Authorize(Roles = "AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsVehiculoCuarentenaDto>>> ListarCuarentena(CancellationToken ct = default)
        => Ok(await listarCuarentena.Handle(new clsListarVehiculosCuarentenaQuery(), ct));

    /// <summary>
    /// Mapea una configuración en cuarentena a un código oficial: crea el alias de su tipo de vehículo, así que el mismo texto no vuelve a caer en
    /// cuarentena, y la saca de la bandeja. 404 si no existe; 409 si ya es oficial o ya se descartó; 400 si el código no es oficial; 409
    /// ALIAS_DUPLICADO si ese texto ya apunta a otro código.
    /// </summary>
    [HttpPost("vehiculos-cuarentena/{id:guid}/mapear")]
    [Authorize(Roles = "AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsMapeoVehiculoDto>> Mapear(Guid id, [FromBody] clsMapearVehiculoRequest req, CancellationToken ct = default)
        => Ok(await mapearVehiculo.Handle(new clsMapearVehiculoCuarentenaCommand(id, req.Codigo), ct));

    /// <summary>Descarta una configuración en cuarentena: sale de la bandeja y no se borra. 204; 404 si no existe; 409 si ya es oficial o ya se descartó.</summary>
    [HttpPost("vehiculos-cuarentena/{id:guid}/descartar")]
    [Authorize(Roles = "AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Descartar(Guid id, CancellationToken ct = default)
    {
        await descartarVehiculo.Handle(new clsDescartarVehiculoCuarentenaCommand(id), ct);
        return NoContent();
    }
}
