using Nexo.Api.Common.Errors;
using Nexo.Application.CasosDeUso.MapeoExcel;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
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
/// La puerta de entrada de nuevas solicitudes (analizar-excel y Capturar) es solo de
/// Comercial/Admin.
///
/// S5-HU-22.1/#288: los roles ya NO están a nivel de clase. Un [Authorize(Roles)] de método no
/// reemplaza el de la clase —se combinan con AND—, así que con "Comercial,Admin" en la clase el
/// catálogo de ciudades devolvía 403 a AdminBiblioteca (que sí gestiona servicios en
/// Biblioteca) y el frontend, que traga ese error, dejaba el campo sin sugerencias. Ahora la
/// clase exige solo estar autenticado (más la política de respaldo: un rol válido de Nexo) y
/// cada endpoint dice quién puede usarlo.
/// </summary>
[ApiController]
[Authorize]
[Route("api/captura-solicitudes")]
public class clsCapturaSolicitudesController(
    IQueryHandler<clsAnalizarExcelQuery, clsAnalisisExcelDto> analizarExcel,
    IQueryHandler<clsSugerirMapeoExcelQuery, clsSugerenciaMapeoDto> sugerirMapeo,
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
    /// Propone a qué campo de NEXO corresponde cada columna del Excel de un cliente. Recibe los encabezados y las filas que ya devolvió
    /// <c>analizar-excel</c> (para una sola hoja); solo cuentan las primeras 20 filas y los datos sensibles (NIT, nombres, correos, teléfonos y
    /// valores) se ocultan antes de salir. Es una SUGERENCIA, no persiste nada: el usuario la revisa y la confirma. Con <c>clienteId</c> usa
    /// el diccionario de ese cliente, que manda sobre la IA. Si la IA está apagada o falla responde igual con lo que resolvió el diccionario
    /// (<c>iaUsada=false</c> y <c>motivoSinIa</c>) y el resto se mapea a mano. 429 <c>IA_TOPE_DIARIO</c> si la persona ya consumió su tope.
    /// </summary>
    [HttpPost("sugerir-mapeo")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<clsSugerenciaMapeoDto>> SugerirMapeo([FromBody] clsSugerirMapeoRequest req, CancellationToken ct = default)
        => Ok(await sugerirMapeo.Handle(new clsSugerirMapeoExcelQuery(
            req.ClienteId, req.Encabezados, req.Filas?.Select(f => (IReadOnlyList<string>)(f ?? [])).ToList()), ct));

    /// <summary>
    /// Crea la solicitud (cabecera + líneas) resolviendo o creando rutas y
    /// configuraciones de vehículo a partir del texto libre de las líneas.
    /// </summary>
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

    /// <summary>
    /// Estima el valor del aprovisionamiento del contenedor: piso SICE-TAC del viaje
    /// VACÍO (Res. 16075) desde la ciudad de levante hasta el origen del cargue, con
    /// 1 h + 1 h. Con <c>carroceria</c> elige la fila de esa unidad de transporte (la
    /// misma que usa el costeo de la línea); sin ella devuelve una aproximación (la
    /// menor) y lo avisa en <c>advertencia</c>. Devuelve candidato nulo si la matriz no
    /// trae la ruta. levante/origen son obligatorios (400 PARAMETROS_REQUERIDOS si faltan).
    /// Al costear, la línea calcula su propio aprovisionamiento: este endpoint es solo una
    /// estimación para mostrar en el formulario.
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

    /// <summary>
    /// Búsqueda de clientes para el formulario. Con el DW configurado busca sobre los
    /// clientes REALES (DimBPSolicitante) por nombre, NIT o BP; sin él, sobre la tabla local.
    /// </summary>
    [HttpGet("clientes")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsClienteBusquedaDto>>> Clientes([FromQuery] string? q, CancellationToken ct = default)
        => Ok(await buscarClientes.Handle(new clsBuscarClientesQuery(q), ct));

    /// <summary>Departamentos de Colombia con municipio en tbl_municipios (S5-HU-16.1/#254).</summary>
    [HttpGet("departamentos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Departamentos(CancellationToken ct = default)
        => Ok(await listarDepartamentos.Handle(new clsListarDepartamentosQuery(), ct));

    /// <summary>
    /// Búsqueda de ciudades para Origen/Destino, filtrable por departamento y/o texto.
    /// S5-HU-16.1/#254: el frontend debe capturar el "id" de la ciudad elegida y enviarlo
    /// como origenMunicipioId/destinoMunicipioId al crear la solicitud (ver
    /// clsCapturaLineaRequest) — es lo que permite a #255/#256 dejar de comparar por texto.
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
