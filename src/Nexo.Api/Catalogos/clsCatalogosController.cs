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

/// <summary>Catálogos oficiales de solo lectura para desplegables: tipos de vehículo, línea de negocio, clase de viaje, clase de contenedor y municipios.</summary>
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
    /// <summary>Catálogo de tipos de vehículo (tblSAPTiposVehiculoTarifario, tal cual).</summary>
    [HttpGet("tipos-vehiculo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsTipoVehiculoDto>>> TiposVehiculo(CancellationToken ct = default)
        => Ok(await tiposVehiculo.Handle(new clsListarTiposVehiculoQuery(), ct));

    /// <summary>Catálogo de línea de negocio (tblSAPLineaNegocio).</summary>
    [HttpGet("lineas-negocio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsLineaNegocioDto>>> LineasNegocio(CancellationToken ct = default)
        => Ok(await lineasNegocio.Handle(new clsListarLineasNegocioQuery(), ct));

    /// <summary>Catálogo de clase de viaje (tblSAPClaseViaje).</summary>
    [HttpGet("clases-viaje")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsClaseViajeDto>>> ClasesViaje(CancellationToken ct = default)
        => Ok(await clasesViaje.Handle(new clsListarClasesViajeQuery(), ct));

    /// <summary>Catálogo de clase de contenedor (tblSAPClaseContenedor).</summary>
    [HttpGet("clases-contenedor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsClaseContenedorDto>>> ClasesContenedor(CancellationToken ct = default)
        => Ok(await clasesContenedor.Handle(new clsListarClasesContenedorQuery(), ct));

    /// <summary>
    /// Catálogo completo de municipios (Id, Municipio, Departamento, Zona), ~3.800 filas sin paginar ni filtrar: para cargarse una vez en el front.
    /// Mismo query que GET /api/captura-solicitudes/municipios, expuesto aquí porque ese controller es solo Comercial/Admin y AdminTarifas también lo necesita.
    /// </summary>
    [HttpGet("municipios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsMunicipioDto>>> Municipios(CancellationToken ct = default)
        => Ok(await municipios.Handle(new clsListarMunicipiosQuery(), ct));
}
