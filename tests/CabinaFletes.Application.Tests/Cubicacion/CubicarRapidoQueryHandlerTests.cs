using CabinaFletes.Application.CasosDeUso.Cubicacion;
using CabinaFletes.Application.CasosDeUso.Cubicacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Cubicacion.Interfaces;
using Moq;

namespace CabinaFletes.Application.Tests.Cubicacion;

/// <summary>
/// [S3-HU-04.3, issue #78] El handler es un passthrough deliberado: cálculo puro sin
/// persistencia, nada que este caso de uso deba validar o transformar — toda la lógica
/// (y la excepción HTTP declarada) vive en <see cref="ICubicacionRapidaPort"/>.
/// </summary>
public class CubicarRapidoQueryHandlerTests
{
    private readonly Mock<ICubicacionRapidaPort> _puerto = new();

    private static CubicacionRapidaSolicitud Solicitud() => new(
        [new ItemCubicacionRapidaDto("Caja", 1, 100, 80, 60, 50, null)], ["furgon-3-5"], "cupo");

    [Fact]
    public async Task Delega_la_solicitud_al_puerto_sin_modificarla()
    {
        var solicitud = Solicitud();
        var resultado = new CubicacionRapidaResultado([], null, [], null, null);
        _puerto.Setup(p => p.EvaluarAsync(solicitud, It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

        var handler = new CubicarRapidoQueryHandler(_puerto.Object);
        var r = await handler.Handle(new CubicarRapidoQuery(solicitud));

        Assert.Same(resultado, r);
        _puerto.Verify(p => p.EvaluarAsync(solicitud, It.IsAny<CancellationToken>()), Times.Once);
    }
}
