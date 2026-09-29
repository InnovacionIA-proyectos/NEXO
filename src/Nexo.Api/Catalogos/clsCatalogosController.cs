using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.Catalogos;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Catalogos;

/// <summary>
/// Catálogos oficiales de solo lectura para desplegables (S4-HU-09/S4-HU-10): tipos de
/// vehículo, línea de negocio, clase de viaje, clase de contenedor y municipios —
/// reemplazan el texto libre en Reglas Solo-SICETAC y Tarifas de Plaza.
/// </summary>
[ApiController]
[Authorize]
[Route("api/catalogos")]
public class clsCatalogosController(
    IQueryHandler<clsListarTiposVehiculoQuery, IReadOnlyList<clsTipoVehiculoDto>> tiposVehiculo,
    IQueryHandler<clsListarLineasNegocioQuery, IReadOnlyList<clsLineaNegocioDto>> lineasNegocio,
    IQueryHandler<clsListarClasesViajeQuery, IReadOnlyList<clsClaseViajeDto>> clasesViaje,
    IQueryHandler<clsListarClasesContenedorQuery, IReadOnlyList<clsClaseContenedorDto>> clasesContenedor,
    IQueryHandler<clsListarMunicipiosQuery, IReadOnlyList<clsMunicipioDto>> municipios)
    : ControllerBase
{
    /// <summary>Catálogo de tipos de vehículo (tblSAPTiposVehiculoTarifario + casos especiales sin código SAP).</summary>
    [HttpGet("tipos-vehiculo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsTipoVehiculoDto>>> TiposVehiculo(CancellationToken ct = default)
        => Ok(await tiposVehiculo.Handle(new clsListarTiposVehiculoQuery(), ct));

    /// <summary>Catálogo de línea de negocio (tblSAPLineaNegocio) para Tarifas de Plaza (S4-HU-10.1).</summary>
    [HttpGet("lineas-negocio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsLineaNegocioDto>>> LineasNegocio(CancellationToken ct = default)
        => Ok(await lineasNegocio.Handle(new clsListarLineasNegocioQuery(), ct));

    /// <summary>Catálogo de clase de viaje (tblSAPClaseViaje) para Tarifas de Plaza (S4-HU-10.1).</summary>
    [HttpGet("clases-viaje")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsClaseViajeDto>>> ClasesViaje(CancellationToken ct = default)
        => Ok(await clasesViaje.Handle(new clsListarClasesViajeQuery(), ct));

    /// <summary>Catálogo de clase de contenedor (tblSAPClaseContenedor) para Tarifas de Plaza (S4-HU-10.1).</summary>
    [HttpGet("clases-contenedor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsClaseContenedorDto>>> ClasesContenedor(CancellationToken ct = default)
        => Ok(await clasesContenedor.Handle(new clsListarClasesContenedorQuery(), ct));

    /// <summary>
    /// Catálogo completo de municipios (Id, Municipio, Departamento, Zona) — S4-HU-10.2/#242:
    /// resuelve OrigenMunicipioId/DestinoMunicipioId para el formulario de Tarifas de Plaza.
    /// Mismo query que GET /api/captura-solicitudes/municipios (S4-HU-09.1), pero expuesto
    /// acá porque ese controller es solo Comercial/Admin y AdminTarifas también lo necesita.
    /// ~3.800 filas, sin paginar ni filtrar: pensado para cargarse una vez en el front.
    /// </summary>
    [HttpGet("municipios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsMunicipioDto>>> Municipios(CancellationToken ct = default)
        => Ok(await municipios.Handle(new clsListarMunicipiosQuery(), ct));
}
