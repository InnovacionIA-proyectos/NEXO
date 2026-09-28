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
    IQueryHandler<clsListarDepartamentosQuery, IReadOnlyList<string>> listarDepartamentos,
    IQueryHandler<clsBuscarCiudadesQuery, IReadOnlyList<clsCiudadBusquedaDto>> buscarCiudades,
    IPropuestaTarifaPort propuestas,
    IQueryHandler<clsListarMunicipiosQuery, IReadOnlyList<clsMunicipioDto>> listarMunicipios)
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
    /// obligatorios (400 PARAMETROS_REQUERIDOS si faltan).
    ///
    /// A propósito "string?" y no "string": con Nullable Reference Types habilitado,
    /// [ApiController] trata un parámetro [FromQuery] no-nullable como implícitamente
    /// [Required] y devuelve su propio 400 VALIDATION_ERROR genérico (en inglés, sin pasar
    /// por este método) antes de llegar aquí — se probó en producción (2026-09-22) y
    /// reemplazaba el código/mensaje de negocio de abajo. Swagger los marca como
    /// opcionales por este mismo motivo (limitación conocida, no corregida): preferible a
    /// cambiar el contrato de error que ya consume el frontend.
    /// </summary>
    [HttpGet("aprovisionamiento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAprovisionamientoDto>> Aprovisionamiento(
        [FromQuery] string? levante, [FromQuery] string? origen, [FromQuery] string? configuracion, CancellationToken ct = default)
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

    /// <summary>Departamentos del catálogo de ciudades costeables.</summary>
    [HttpGet("departamentos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Departamentos(CancellationToken ct = default)
        => Ok(await listarDepartamentos.Handle(new clsListarDepartamentosQuery(), ct));

    /// <summary>
    /// Búsqueda de ciudades para Origen/Destino, filtrable por departamento y/o texto —
    /// mismo catálogo "costeable" en ambos casos.
    /// </summary>
    [HttpGet("ciudades")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsCiudadBusquedaDto>>> Ciudades(
        [FromQuery] string? departamento, [FromQuery] string? q, CancellationToken ct = default)
        => Ok(await buscarCiudades.Handle(new clsBuscarCiudadesQuery(departamento, q), ct));

    /// <summary>
    /// Catálogo completo de municipios (S4-HU-09.1), todas las columnas — para poblar
    /// el desplegable de Origen/Destino de una sola vez en el cliente (~3.800 filas,
    /// sin paginar ni filtrar: pensado para cargarse una vez y quedar en memoria del
    /// front, no para consultarse en cada tecleo). Tabla creada y poblada por script
    /// (ver docs/Database/ESQUEMA_NEXO.sql y el script de poblado), no por migración.
    /// </summary>
    [HttpGet("municipios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsMunicipioDto>>> Municipios(CancellationToken ct = default)
        => Ok(await listarMunicipios.Handle(new clsListarMunicipiosQuery(), ct));
}
