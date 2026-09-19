using CabinaFletes.Application.CasosDeUso.PlanDeCargue;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;
using Moq;

namespace CabinaFletes.Application.Tests.PlanDeCargue;

/// <summary>
/// [S3-HU-04.3, issue #78] POST /api/planes-cargue/desde-linea/{lineaId} — el equivalente
/// que le faltaba a NEXO para que <c>crearPlanDesdeLinea()</c> dejara de hablarle directo a
/// Cubicador. Mismo patrón que CrearPlanDemoCommandTests tendría (sin fila espejo, solo
/// validar + publicar + guardar), más la validación de existencia de la línea que
/// CrearPlanDemoCommand no necesita.
/// </summary>
public class CrearPlanDesdeLineaCommandTests
{
    private readonly Mock<ILineaSolicitudCubicacionPort> _lineas = new();
    private readonly Mock<ICubicacionPort> _cubicacion = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid LineaId = Guid.Parse("dddd0000-0000-0000-0000-000000000001");

    private CrearPlanDesdeLineaCommandHandler Handler() => new(_lineas.Object, _cubicacion.Object, _uow.Object);

    private void LineaExiste() =>
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LineaParaCubicacionDto(LineaId, [], "Bogotá", "Medellín"));

    [Fact]
    public async Task Con_la_linea_inexistente_no_publica_nada()
    {
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LineaParaCubicacionDto?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => Handler().Handle(new CrearPlanDesdeLineaCommand(LineaId)));

        Assert.Equal("LINEA_SOLICITUD_NO_ENCONTRADA", ex.Code);
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Publica_crear_desde_linea_con_el_lineaId_como_correlationId()
    {
        LineaExiste();

        var r = await Handler().Handle(new CrearPlanDesdeLineaCommand(LineaId));

        Assert.Equal(LineaId, r.CorrelationId);
        Assert.Equal("procesando", r.Estado);
        // El tipo de cola es "crear_desde_linea" (lo que el worker de Cubicador ya
        // implementa), NO "crear_plan_desde_linea".
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            "crear_desde_linea", LineaId.ToString(),
            It.Is<CrearPlanDesdeLineaInput>(i => i.LineaSolicitudId == LineaId.ToString()),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publica_antes_de_guardar()
    {
        // El Outbox exige este orden — ver docs/mensajeria/CUBICACION.md #5.
        LineaExiste();
        var orden = new List<string>();
        _cubicacion.Setup(c => c.PublicarComandoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Handler().Handle(new CrearPlanDesdeLineaCommand(LineaId));

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }
}
