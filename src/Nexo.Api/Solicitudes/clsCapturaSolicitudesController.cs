using Nexo.Api.Common.Errors;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Solicitudes;

/// <summary>
/// Captura de necesidades del equipo comercial: solicitudes puntuales (spot) o
/// pliegos de licitación, ingresados a mano o importados desde el Excel del cliente.
/// Solo Comercial/Admin — es la puerta de entrada de nuevas solicitudes.
/// </summary>
[ApiController]
[Authorize(Roles = "Comercial,Admin")]
[Route("api/captura-solicitudes")]
public class clsCapturaSolicitudesController(
    IQueryHandler<clsAnalizarExcelQuery, clsAnalisisExcelDto> analizarExcel,
    ICommandHandler<clsCapturarSolicitudCommand, clsCapturaResultDto> capturar,
    IQueryHandler<clsBuscarClientesQuery, IReadOnlyList<clsClienteBusquedaDto>> buscarClientes,
    IPropuestaTarifaPort propuestas)
    : ControllerBase
{
    private const long MaxTamanoArchivo = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Analiza un Excel de licitación (cualquier plantilla de cliente) y devuelve
    /// las hojas con encabezados y filas para que el usuario mapee las columnas.
    /// No persiste nada.
    /// </summary>
    [HttpPost("analizar-excel")]
    [RequestSizeLimit(MaxTamanoArchivo)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAnalisisExcelDto>> AnalizarExcel(IFormFile? archivo, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(archivo);
        return Ok(await analizarExcel.Handle(new clsAnalizarExcelQuery(stream, archivo!.FileName), ct));
    }

    /// <summary>
    /// Crea la solicitud (cabecera + líneas) resolviendo o creando rutas y
    /// configuraciones de vehículo a partir del texto libre de las líneas.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsCapturaResultDto>> Capturar([FromBody] clsCapturaSolicitudRequest req, CancellationToken ct = default)
    {
        var lineas = req.Lineas.Select(lr => new clsCapturaLineaInput(
            lr.Origen, lr.Destino, lr.TipoVehiculo, lr.Carroceria, lr.Tipologia, lr.LineaNegocio, lr.ClaseViaje,
            lr.ClaseContenedor, lr.Exclusivo, lr.Consolidado, lr.RetornaContenedor, lr.CiudadDevolucionContenedor,
            lr.LevanteContenedorDiferente, lr.CiudadLevanteContenedor, lr.ValorAprovisionamiento, lr.Peso, lr.ValorMercancia,
            lr.Posiciones?.Select(p => new clsPosicionCargaDto(p.Descripcion, p.Cantidad, p.LargoCm, p.AnchoCm, p.AltoCm, p.PesoKg)).ToList()
        )).ToList();

        var command = new clsCapturarSolicitudCommand(
            req.ClienteId, req.ClienteBp, req.Tipo, req.Modalidad, req.Deadline, req.ValorEstimado,
            req.Referencia, req.HorasCargue, req.HorasDescargue, lineas);
        var result = await capturar.Handle(command, ct);
        return Created($"/api/solicitudes/{result.Id}", result);
    }

    /// <summary>
    /// Estima el valor del aprovisionamiento del contenedor: piso SICE-TAC del viaje
    /// VACÍO (Res. 16075) desde la ciudad de levante hasta el origen del cargue.
    /// Devuelve candidato nulo si la matriz no trae la ruta. levante/origen son
    /// obligatorios (400 PARAMETROS_REQUERIDOS si faltan) — tipados sin "?" para que
    /// Swagger los refleje como requeridos; el binder igual puede dejarlos en null si
    /// el query string no los trae, por eso la validación explícita de abajo se mantiene.
    /// </summary>
    [HttpGet("aprovisionamiento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAprovisionamientoDto>> Aprovisionamiento(
        [FromQuery] string levante, [FromQuery] string origen, [FromQuery] string? configuracion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(levante) || string.IsNullOrWhiteSpace(origen))
            throw new clsValidationFailedException("PARAMETROS_REQUERIDOS", "Indica la ciudad de levante y el origen del cargue.");

        var candidato = await propuestas.EstimarAprovisionamientoAsync(levante.Trim(), origen.Trim(), configuracion?.Trim(), ct);
        return Ok(new clsAprovisionamientoDto(candidato));
    }

    /// <summary>
    /// Búsqueda de clientes para el formulario. Con el DW configurado busca sobre los
    /// clientes REALES (DimBPSolicitante) por nombre, NIT o BP; sin él, sobre la tabla local.
    /// </summary>
    [HttpGet("clientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsClienteBusquedaDto>>> Clientes([FromQuery] string? q, CancellationToken ct = default)
        => Ok(await buscarClientes.Handle(new clsBuscarClientesQuery(q), ct));
}
