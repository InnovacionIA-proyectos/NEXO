using System.Linq.Expressions;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S5-HU-20.1/#286: GET por solicitud devuelve la cotización existente o null, y NUNCA crea
/// nada (el handler ni siquiera tiene un puerto con el que crear). Elige igual que
/// clsCrearCotizacionCommand: Entregada si la solicitud ya se entregó, si no el borrador.
/// </summary>
public class clsObtenerCotizacionPorSolicitudQueryTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();

    private clsObtenerCotizacionPorSolicitudQueryHandler Handler() => new(_cotizaciones.Object, _solicitudes.Object);

    private static bool Cumple(Expression<Func<clsCotizacion, bool>> filtro, clsCotizacion c) => filtro.Compile()(c);

    private void Existe(Guid solicitudId, EstadoSolicitud estado, params clsCotizacion[] cotizaciones)
    {
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = estado });
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(It.IsAny<Expression<Func<clsCotizacion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<clsCotizacion, bool>> f, CancellationToken _) => cotizaciones.FirstOrDefault(c => Cumple(f, c)));
    }

    [Fact]
    public async Task Sin_cotizacion_devuelve_null_y_no_crea_nada()
    {
        var id = Guid.NewGuid();
        Existe(id, EstadoSolicitud.Pendiente);

        var dto = await Handler().Handle(new clsObtenerCotizacionPorSolicitudQuery(id));

        Assert.Null(dto);
        _cotizaciones.Verify(r => r.Agregar(It.IsAny<clsCotizacion>()), Times.Never);
    }

    [Fact]
    public async Task Solicitud_inexistente_devuelve_null_en_vez_de_lanzar()
    {
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsSolicitud?)null);

        Assert.Null(await Handler().Handle(new clsObtenerCotizacionPorSolicitudQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task Con_un_borrador_en_curso_lo_devuelve()
    {
        var id = Guid.NewGuid();
        var borrador = new clsCotizacion { SolicitudId = id, Estado = EstadoCotizacion.Borrador, EsAutoCosteo = true, Total = 1_800_000m };
        Existe(id, EstadoSolicitud.EnCosteo, borrador);

        var dto = await Handler().Handle(new clsObtenerCotizacionPorSolicitudQuery(id));

        Assert.NotNull(dto);
        Assert.Equal(borrador.Id, dto!.Id);
        Assert.True(dto.EsAutoCosteo);
    }

    [Fact]
    public async Task Solicitud_entregada_devuelve_la_Entregada_aunque_exista_tambien_un_borrador()
    {
        var id = Guid.NewGuid();
        var borrador = new clsCotizacion { SolicitudId = id, Estado = EstadoCotizacion.Borrador };
        var entregada = new clsCotizacion { SolicitudId = id, Estado = EstadoCotizacion.Entregada };
        Existe(id, EstadoSolicitud.Entregada, borrador, entregada);

        var dto = await Handler().Handle(new clsObtenerCotizacionPorSolicitudQuery(id));

        Assert.Equal(entregada.Id, dto!.Id);
    }
}
