using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Asistente;

/// <summary>
/// El contexto de pantalla (solicitud, cotización y línea abiertas) se valida contra los permisos de la persona antes de llegar al modelo,
/// y llega como dato al final de las instrucciones, después de las reglas.
/// </summary>
public class clsContextoPantallaTests
{
    private static readonly Guid Yo = Guid.NewGuid();
    private static readonly Guid SolicitudId = Guid.NewGuid();
    private static readonly Guid CotizacionId = Guid.NewGuid();
    private static readonly Guid LineaId = Guid.NewGuid();

    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<ILineasSolicitudPort> _lineas = new();
    private readonly Mock<IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto>> _cotizaciones = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();

    public clsContextoPantallaTests()
    {
        _usuario.SetupGet(u => u.Id).Returns(Yo);
        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.AdminTarifas);
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = SolicitudId, CreadoPor = Guid.NewGuid() });
        _cotizaciones.Setup(c => c.Handle(It.IsAny<clsObtenerCotizacionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsCotizacionDto(CotizacionId, SolicitudId, EstadoCotizacion.Borrador, ResultadoCotizacion.SinDefinir, 0, null, [], []));
        _lineas.Setup(l => l.ObtenerConRutaYVehiculoAsync(LineaId, SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaSolicitud
            {
                Id = LineaId,
                clsRuta = new clsRuta { Origen = "Cali", Destino = "Barranquilla" },
                clsConfiguracionVehiculo = new clsConfiguracionVehiculo { TipoVehiculo = "Turbo" },
            });
    }

    private clsResolutorContextoPantalla Resolutor() => new(_solicitudes.Object, _lineas.Object, _cotizaciones.Object, _usuario.Object);

    [Fact]
    public async Task El_contexto_trae_los_identificadores_y_la_descripcion_de_la_linea()
    {
        var texto = await Resolutor().ResolverAsync(new clsContextoPantalla(SolicitudId, CotizacionId, LineaId), default);

        Assert.StartsWith("CONTEXTO DE PANTALLA", texto);
        Assert.Contains($"- solicitudId: {SolicitudId}", texto);
        Assert.Contains($"- cotizacionId: {CotizacionId}", texto);
        Assert.Contains($"- lineaSolicitudId: {LineaId} (Cali → Barranquilla · Turbo)", texto);
    }

    [Fact]
    public async Task Solo_con_la_solicitud_no_menciona_cotizacion_ni_linea()
    {
        var texto = await Resolutor().ResolverAsync(new clsContextoPantalla(SolicitudId), default);

        Assert.DoesNotContain("cotizacionId", texto);
        Assert.DoesNotContain("lineaSolicitudId", texto);
    }

    [Fact]
    public async Task Una_solicitud_que_no_existe_es_404_y_sin_identificador_es_400()
    {
        var noExiste = await Assert.ThrowsAsync<clsNotFoundException>(
            () => Resolutor().ResolverAsync(new clsContextoPantalla(Guid.NewGuid()), default));
        Assert.Equal("SOLICITUD_NO_ENCONTRADA", noExiste.Code);

        var vacio = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Resolutor().ResolverAsync(new clsContextoPantalla(Guid.Empty), default));
        Assert.Equal("CONTEXTO_INVALIDO", vacio.Code);
    }

    [Fact]
    public async Task Un_comercial_no_puede_poner_de_contexto_una_solicitud_ajena()
    {
        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.Comercial);

        var ex = await Assert.ThrowsAsync<clsForbiddenException>(
            () => Resolutor().ResolverAsync(new clsContextoPantalla(SolicitudId), default));

        Assert.Equal("SOLICITUD_AJENA", ex.Code);
    }

    [Fact]
    public async Task Un_comercial_si_puede_con_una_solicitud_que_creo()
    {
        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.Comercial);
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = SolicitudId, CreadoPor = Yo });

        var texto = await Resolutor().ResolverAsync(new clsContextoPantalla(SolicitudId), default);

        Assert.Contains(SolicitudId.ToString(), texto);
    }

    [Fact]
    public async Task Una_cotizacion_de_otra_solicitud_es_contexto_invalido()
    {
        _cotizaciones.Setup(c => c.Handle(It.IsAny<clsObtenerCotizacionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsCotizacionDto(CotizacionId, Guid.NewGuid(), EstadoCotizacion.Borrador, ResultadoCotizacion.SinDefinir, 0, null, [], []));

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Resolutor().ResolverAsync(new clsContextoPantalla(SolicitudId, CotizacionId), default));

        Assert.Equal("CONTEXTO_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task Una_linea_de_otra_solicitud_es_contexto_invalido()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Resolutor().ResolverAsync(new clsContextoPantalla(SolicitudId, null, Guid.NewGuid()), default));

        Assert.Equal("CONTEXTO_INVALIDO", ex.Code);
    }

    [Fact]
    public void Las_reglas_piden_nombrar_la_configuracion_al_dar_un_piso_SICETAC()
    {
        Assert.Contains("con qué configuración vehicular se calculó", clsInstruccionesAsistente.Texto);
    }
}
