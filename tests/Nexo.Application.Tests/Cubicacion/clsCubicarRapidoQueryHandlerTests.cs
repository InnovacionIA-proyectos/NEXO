using Nexo.Application.CasosDeUso.Cubicacion;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Moq;

namespace Nexo.Application.Tests.Cubicacion;

/// <summary>
/// S5-HU-18.1/#273: los dos handlers son passthrough deliberado hacia
/// <see cref="ICubicacionRapidaPort"/> — nada que este caso de uso deba validar o
/// transformar, toda la lógica (y la traducción de errores) vive en el adaptador.
/// </summary>
public class clsCubicarRapidoQueryHandlerTests
{
    private readonly Mock<ICubicacionRapidaPort> _puerto = new();

    private static clsCrearRecomendacionInput clsSolicitud() => new(
        "LIN-001", [new clsMercanciaSolicitadaInput("tipo-1", 1, 100, 80, 60, 50)], "cupo", "usuario-1");

    private static clsRecomendacionDto clsRespuesta() => new(
        "REC-1", "LIN-001", "NEXO", "ASIGNADO", null, false, [], [], [], null);

    [Fact]
    public async Task Crear_delega_la_solicitud_al_puerto_sin_modificarla()
    {
        var solicitud = clsSolicitud();
        var respuesta = clsRespuesta();
        _puerto.Setup(p => p.CrearAsync(solicitud, It.IsAny<CancellationToken>())).ReturnsAsync(respuesta);

        var handler = new clsCrearRecomendacionCommandHandler(_puerto.Object);
        var r = await handler.Handle(new clsCrearRecomendacionCommand(solicitud));

        Assert.Same(respuesta, r);
        _puerto.Verify(p => p.CrearAsync(solicitud, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consultar_delega_idLineaSolicitud_y_origenSistema_al_puerto()
    {
        var respuesta = clsRespuesta();
        _puerto.Setup(p => p.ConsultarAsync("LIN-001", "COTIFLY", It.IsAny<CancellationToken>())).ReturnsAsync(respuesta);

        var handler = new clsConsultarRecomendacionQueryHandler(_puerto.Object);
        var r = await handler.Handle(new clsConsultarRecomendacionQuery("LIN-001", "COTIFLY"));

        Assert.Same(respuesta, r);
        _puerto.Verify(p => p.ConsultarAsync("LIN-001", "COTIFLY", It.IsAny<CancellationToken>()), Times.Once);
    }
}
