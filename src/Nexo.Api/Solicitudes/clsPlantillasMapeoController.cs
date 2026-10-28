using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
using Nexo.Application.CasosDeUso.PlantillasMapeo;
using Nexo.Application.CasosDeUso.PlantillasMapeo.DTOs;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Api.Solicitudes;

/// <summary>Guarda el mapeo de columnas que el usuario confirmó como la plantilla de un cliente.</summary>
public class clsGuardarPlantillaMapeoRequest
{
    [Required]
    public Guid? ClienteId { get; set; }

    /// <summary>Un nombre para reconocerla en una lista (opcional).</summary>
    [MaxLength(100)]
    public string? Nombre { get; set; }

    /// <summary>Los encabezados del archivo, en su orden: con ellos se reconoce otro archivo igual.</summary>
    [Required, MinLength(1), MaxLength(100)]
    public List<string?> Encabezados { get; set; } = null!;

    /// <summary>El mapeo confirmado, una entrada por columna (las que no se usan van como <c>ignorar</c>).</summary>
    [Required, MinLength(1), MaxLength(100)]
    public List<clsColumnaAplicarRequest> Columnas { get; set; } = null!;
}

/// <summary>
/// Plantillas de mapeo de columnas por cliente: al llegar un Excel con los mismos encabezados que uno anterior del cliente,
/// <c>sugerir-mapeo</c> devuelve el mapeo ya confirmado en vez de llamar a la IA. Lo usa quien importa archivos: Comercial y Admin.
/// </summary>
[ApiController]
[Authorize]
[Route("api/captura-solicitudes/plantillas-mapeo")]
public class clsPlantillasMapeoController(
    ICommandHandler<clsGuardarPlantillaMapeoCommand, clsPlantillaGuardadaDto> guardar,
    IQueryHandler<clsListarPlantillasMapeoQuery, IReadOnlyList<clsPlantillaMapeoDto>> listar,
    ICommandHandler<clsEliminarPlantillaMapeoCommand, Unit> eliminar)
    : ControllerBase
{
    /// <summary>
    /// Guarda (o reemplaza) la plantilla del cliente para esos encabezados: 201 la primera vez, 200 si reemplazó a la que ya tenía. El mapeo se
    /// valida como al aplicarlo (400 con el código del problema). Solo los encabezados idénticos (sin importar mayúsculas ni tildes) la reconocen.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsPlantillaMapeoDto>> Guardar([FromBody] clsGuardarPlantillaMapeoRequest req, CancellationToken ct = default)
    {
        var resultado = await guardar.Handle(new clsGuardarPlantillaMapeoCommand(
            req.ClienteId, req.Nombre, req.Encabezados,
            req.Columnas.Select(c => new clsColumnaAAplicar(c.Indice, c.Campo, c.Transformacion)).ToList()), ct);
        return resultado.Creada
            ? Created($"/api/captura-solicitudes/plantillas-mapeo?clienteId={resultado.Plantilla.ClienteId}", resultado.Plantilla)
            : Ok(resultado.Plantilla);
    }

    /// <summary>Las plantillas guardadas: con <c>clienteId</c> las de ese cliente; sin él, las de todos.</summary>
    [HttpGet]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsPlantillaMapeoDto>>> Listar([FromQuery] Guid? clienteId, CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarPlantillasMapeoQuery(clienteId), ct));

    /// <summary>Borra una plantilla: el siguiente archivo con esos encabezados se vuelve a analizar. 204; 404 si no existe.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct = default)
    {
        await eliminar.Handle(new clsEliminarPlantillaMapeoCommand(id), ct);
        return NoContent();
    }
}
