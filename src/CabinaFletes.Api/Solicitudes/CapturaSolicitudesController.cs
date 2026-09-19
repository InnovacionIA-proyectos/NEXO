using CabinaFletes.Api.Common.Errors;
using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Solicitudes;

/// <summary>
/// Captura de necesidades del equipo comercial: solicitudes puntuales (spot) o
/// pliegos de licitación, ingresados a mano o importados desde el Excel del cliente.
/// </summary>
[ApiController]
[Authorize(Roles = "Comercial,Admin")]
[Route("api/captura-solicitudes")]
public class CapturaSolicitudesController(
    IQueryHandler<AnalizarExcelQuery, AnalisisExcelDto> analizarExcel,
    ICommandHandler<CapturarSolicitudCommand, CapturaResultDto> capturar,
    IQueryHandler<BuscarClientesQuery, IReadOnlyList<ClienteBusquedaDto>> buscarClientes,
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
    public async Task<ActionResult<AnalisisExcelDto>> AnalizarExcel(IFormFile? archivo, CancellationToken ct = default)
    {
        await using var stream = ExcelUpload.AbrirValidado(archivo);
        return Ok(await analizarExcel.Handle(new AnalizarExcelQuery(stream, archivo!.FileName), ct));
    }

    /// <summary>
    /// Crea la solicitud (cabecera + líneas) resolviendo o creando rutas y
    /// configuraciones de vehículo a partir del texto libre de las líneas.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CapturaResultDto>> Capturar([FromBody] CapturaSolicitudRequest req, CancellationToken ct = default)
    {
        var lineas = req.Lineas.Select(lr => new CapturaLineaInput(
            lr.Origen, lr.Destino, lr.TipoVehiculo, lr.Carroceria, lr.Tipologia, lr.LineaNegocio, lr.ClaseViaje,
            lr.ClaseContenedor, lr.Exclusivo, lr.Consolidado, lr.RetornaContenedor, lr.CiudadDevolucionContenedor,
            lr.LevanteContenedorDiferente, lr.CiudadLevanteContenedor, lr.ValorAprovisionamiento, lr.Peso, lr.ValorMercancia,
            lr.Posiciones?.Select(p => new PosicionCargaDto(p.Descripcion, p.Cantidad, p.LargoCm, p.AnchoCm, p.AltoCm, p.PesoKg)).ToList()
        )).ToList();

        var command = new CapturarSolicitudCommand(
            req.ClienteId, req.ClienteBp, req.Tipo, req.Modalidad, req.Deadline, req.ValorEstimado,
            req.Referencia, req.HorasCargue, req.HorasDescargue, lineas);
        var result = await capturar.Handle(command, ct);
        return Created($"/api/solicitudes/{result.Id}", result);
    }

    /// <summary>
    /// Estima el valor del aprovisionamiento del contenedor: piso SICE-TAC del viaje
    /// VACÍO (Res. 16075) desde la ciudad de levante hasta el origen del cargue.
    /// Devuelve candidato nulo si la matriz no trae la ruta.
    /// </summary>
    [HttpGet("aprovisionamiento")]
    public async Task<ActionResult<AprovisionamientoDto>> Aprovisionamiento(
        [FromQuery] string? levante, [FromQuery] string? origen, [FromQuery] string? configuracion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(levante) || string.IsNullOrWhiteSpace(origen))
            throw new ValidationFailedException("PARAMETROS_REQUERIDOS", "Indica la ciudad de levante y el origen del cargue.");

        var candidato = await propuestas.EstimarAprovisionamientoAsync(levante.Trim(), origen.Trim(), configuracion?.Trim(), ct);
        return Ok(new AprovisionamientoDto(candidato));
    }

    /// <summary>
    /// Búsqueda de clientes para el formulario. Con el DW configurado busca sobre los
    /// clientes REALES (DimBPSolicitante) por nombre, NIT o BP; sin él, sobre la tabla local.
    /// </summary>
    [HttpGet("clientes")]
    public async Task<ActionResult<IReadOnlyList<ClienteBusquedaDto>>> Clientes([FromQuery] string? q, CancellationToken ct = default)
        => Ok(await buscarClientes.Handle(new BuscarClientesQuery(q), ct));
}
