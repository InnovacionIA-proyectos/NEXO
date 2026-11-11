using Nexo.Api.Common.Errors;
using Nexo.Application.CasosDeUso.MapeoExcel;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
using Nexo.Application.CasosDeUso.MapeoExcel.Normalizacion;
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
/// Captura de necesidades del equipo comercial: solicitudes spot o pliegos de licitación, a mano o importados desde el Excel del cliente.
/// La clase solo exige estar autenticado; cada endpoint declara sus roles (un [Authorize(Roles)] de método se combina con AND con el de la clase).
/// </summary>
[ApiController]
[Authorize]
[Route("api/captura-solicitudes")]
public class clsCapturaSolicitudesController(
    IQueryHandler<clsAnalizarExcelQuery, clsAnalisisExcelDto> analizarExcel,
    IQueryHandler<clsSugerirMapeoExcelQuery, clsSugerenciaMapeoDto> sugerirMapeo,
    IQueryHandler<clsAplicarMapeoExcelQuery, clsResultadoAplicarMapeoDto> aplicarMapeo,
    IQueryHandler<clsNormalizarValoresQuery, clsResultadoNormalizacionDto> normalizarValores,
    ICommandHandler<clsCapturarSolicitudCommand, clsCapturaResultDto> capturar,
    IQueryHandler<clsBuscarClientesQuery, IReadOnlyList<clsClienteBusquedaDto>> buscarClientes,
    IQueryHandler<clsListarDepartamentosQuery, IReadOnlyList<string>> listarDepartamentos,
    IQueryHandler<clsBuscarCiudadesQuery, IReadOnlyList<clsCiudadBusquedaDto>> buscarCiudades,
    IPropuestaTarifaPort propuestas,
    IQueryHandler<clsListarMunicipiosQuery, IReadOnlyList<clsMunicipioDto>> listarMunicipios,
    clsClienteDeImportacion clienteDeImportacion)
    : ControllerBase
{
    private const long MaxTamanoArchivo = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// El cliente del archivo: <c>clienteId</c> si viene (manda sobre el NIT) o el cliente local con ese NIT. Solo lee: si el NIT no tiene cliente
    /// local devuelve null y el análisis sigue como sin cliente (diccionario general, sin plantilla).
    /// </summary>
    private async Task<Guid?> ClienteDelArchivoAsync(Guid? clienteId, string? clienteNit, CancellationToken ct) =>
        clienteId ?? await clienteDeImportacion.BuscarLocalAsync(clienteNit, ct);

    /// <summary>Analiza un Excel de licitación (cualquier plantilla de cliente) y devuelve las hojas con encabezados y filas para mapear columnas. No persiste nada.</summary>
    [HttpPost("analizar-excel")]
    [Authorize(Roles = "Comercial,Admin")]
    [RequestSizeLimit(MaxTamanoArchivo)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAnalisisExcelDto>> AnalizarExcel(IFormFile? archivo, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(archivo);
        return Ok(await analizarExcel.Handle(new clsAnalizarExcelQuery(stream, archivo!.FileName), ct));
    }

    /// <summary>
    /// Propone a qué campo de NEXO corresponde cada columna del Excel de un cliente, a partir de lo que devolvió <c>analizar-excel</c> (una hoja, primeras 20 filas, con los datos sensibles ocultos).
    /// Es una sugerencia que no persiste. Con <c>clienteId</c> (o <c>clienteNit</c>) el diccionario del cliente manda sobre la IA; si la IA falla responde con lo del diccionario (<c>iaUsada=false</c>). 429 <c>IA_TOPE_DIARIO</c> si superó su tope.
    /// </summary>
    [HttpPost("sugerir-mapeo")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<clsSugerenciaMapeoDto>> SugerirMapeo([FromBody] clsSugerirMapeoRequest req, CancellationToken ct = default)
        => Ok(await sugerirMapeo.Handle(new clsSugerirMapeoExcelQuery(
            await ClienteDelArchivoAsync(req.ClienteId, req.ClienteNit, ct), req.Encabezados, req.Filas?.Select(f => (IReadOnlyList<string>)(f ?? [])).ToList()), ct));

    /// <summary>
    /// Aplica el mapeo confirmado a todas las filas del Excel y devuelve las líneas listas para <c>POST /api/captura-solicitudes</c> más los errores por fila (una fila con errores no sale como línea).
    /// Convierte unidades y formatos, traduce valores con el diccionario del cliente y valida con las mismas reglas de la captura. No usa la IA ni persiste nada.
    /// </summary>
    [HttpPost("aplicar-mapeo")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsResultadoAplicarMapeoDto>> AplicarMapeo([FromBody] clsAplicarMapeoRequest req, CancellationToken ct = default)
        => Ok(await aplicarMapeo.Handle(new clsAplicarMapeoExcelQuery(
            await ClienteDelArchivoAsync(req.ClienteId, req.ClienteNit, ct),
            req.Columnas.Select(c => new clsColumnaAAplicar(c.Indice, c.Campo, c.Transformacion)).ToList(),
            req.Filas.Select(f => (IReadOnlyList<string>)(f ?? [])).ToList(),
            req.PrimeraFila), ct));

    /// <summary>
    /// Lleva los textos libres distintos de un Excel (vehículo, carrocería, línea de negocio, ciudad) a los valores oficiales de NEXO: primero reglas exactas (diccionario del cliente, alias, catálogo, DANE) y, con lo que no resuelven, una sola llamada a la IA con listas cerradas.
    /// No persiste; lo que siga sin resolverse va a cuarentena al capturar. Si la IA falla responde con lo de las reglas (<c>iaUsada=false</c>). 429 <c>IA_TOPE_DIARIO</c> si superó su tope.
    /// </summary>
    [HttpPost("normalizar-valores")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<clsResultadoNormalizacionDto>> NormalizarValores([FromBody] clsNormalizarValoresRequest req, CancellationToken ct = default)
        => Ok(await normalizarValores.Handle(new clsNormalizarValoresQuery(
            await ClienteDelArchivoAsync(req.ClienteId, req.ClienteNit, ct), req.Valores.Select(v => new clsValorANormalizar(v.Campo, v.Valor)).ToList()), ct));

    /// <summary>Crea la solicitud (cabecera + líneas) resolviendo o creando rutas y configuraciones de vehículo desde el texto libre de las líneas.</summary>
    [HttpPost]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsCapturaResultDto>> Capturar([FromBody] clsCapturaSolicitudRequest req, CancellationToken ct = default)
    {
        var lineas = req.Lineas.Select(lr => new clsCapturaLineaInput(
            lr.Origen, lr.Destino, lr.TipoVehiculo, lr.Carroceria, lr.Tipologia, lr.LineaNegocio, lr.ClaseViaje,
            lr.ClaseContenedor, lr.Exclusivo, lr.Consolidado, lr.RetornaContenedor, lr.CiudadDevolucionContenedor,
            lr.LevanteContenedorDiferente, lr.CiudadLevanteContenedor, lr.ValorAprovisionamiento, lr.Peso, lr.ValorMercancia,
            lr.Posiciones?.Select(p => new clsPosicionCargaDto(
                p.Descripcion, p.Cantidad, p.LargoCm, p.AnchoCm, p.AltoCm, p.PesoKg, p.CodigoTipoMercancia)).ToList(),
            lr.HorasCargue, lr.HorasDescargue, lr.OrigenMunicipioId, lr.DestinoMunicipioId, lr.GrupoRutaId,
            lr.IdRecomendacionVehiculo
        )).ToList();

        var command = new clsCapturarSolicitudCommand(
            req.ClienteId, req.ClienteBp, req.Tipo, req.Modalidad, req.Deadline, req.ValorEstimado,
            req.Referencia, req.HorasCargue, req.HorasDescargue, lineas, req.Notas);
        var result = await capturar.Handle(command, ct);
        return Created($"/api/solicitudes/{result.Id}", result);
    }

    // "string?" a propósito: con Nullable Reference Types, [ApiController] trata un [FromQuery] no-nullable como [Required] y respondería un 400 genérico antes del 400 PARAMETROS_REQUERIDOS de este método.
    /// <summary>
    /// Estima el aprovisionamiento del contenedor: piso SICE-TAC del viaje vacío desde la ciudad de levante hasta el origen del cargue (1 h + 1 h). Con <c>carroceria</c> usa la fila de esa unidad; sin ella devuelve la menor y avisa en <c>advertencia</c>.
    /// Candidato nulo si la matriz no trae la ruta. Es solo una estimación para el formulario: al costear, la línea calcula su propio aprovisionamiento.
    /// </summary>
    [HttpGet("aprovisionamiento")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAprovisionamientoDto>> Aprovisionamiento(
        [FromQuery] string? levante, [FromQuery] string? origen, [FromQuery] string? configuracion,
        [FromQuery] string? carroceria, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(levante) || string.IsNullOrWhiteSpace(origen))
            throw new clsValidationFailedException("PARAMETROS_REQUERIDOS", "Indica la ciudad de levante y el origen del cargue.");

        var candidato = await propuestas.EstimarAprovisionamientoAsync(
            levante.Trim(), origen.Trim(), configuracion?.Trim(), ct, carroceria?.Trim());
        return Ok(new clsAprovisionamientoDto(candidato));
    }

    /// <summary>Búsqueda de clientes para el formulario: con el DW configurado, sobre los clientes reales (DimBPSolicitante) por nombre, NIT o BP; si no, sobre la tabla local.</summary>
    [HttpGet("clientes")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsClienteBusquedaDto>>> Clientes([FromQuery] string? q, CancellationToken ct = default)
        => Ok(await buscarClientes.Handle(new clsBuscarClientesQuery(q), ct));

    /// <summary>Departamentos de Colombia con municipio en tbl_municipios.</summary>
    [HttpGet("departamentos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Departamentos(CancellationToken ct = default)
        => Ok(await listarDepartamentos.Handle(new clsListarDepartamentosQuery(), ct));

    /// <summary>Búsqueda de ciudades para Origen/Destino, filtrable por departamento y/o texto. El frontend debe enviar el "id" elegido como origenMunicipioId/destinoMunicipioId al crear la solicitud.</summary>
    [HttpGet("ciudades")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsCiudadBusquedaDto>>> Ciudades(
        [FromQuery] string? departamento, [FromQuery] string? q, CancellationToken ct = default)
        => Ok(await buscarCiudades.Handle(new clsBuscarCiudadesQuery(departamento, q), ct));

    /// <summary>Catálogo completo de municipios (~3.800 filas, sin paginar ni filtrar) para cargarse una vez en el cliente. La tabla se crea y puebla por script (database/esquema/ESQUEMA_NEXO.sql), no por migración.</summary>
    [HttpGet("municipios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsMunicipioDto>>> Municipios(CancellationToken ct = default)
        => Ok(await listarMunicipios.Handle(new clsListarMunicipiosQuery(), ct));
}
