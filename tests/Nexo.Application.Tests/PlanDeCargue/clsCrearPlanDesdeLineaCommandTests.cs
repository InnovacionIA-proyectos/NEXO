using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// [S3-HU-04.3, issue #78] POST /api/planes-cargue/desde-linea/{lineaId} — el equivalente
/// que le faltaba a NEXO para que <c>crearPlanDesdeLinea()</c> dejara de hablarle directo a
/// Cubicador. Mismo patrón que CrearPlanDemoCommandTests tendría (sin fila espejo, solo
/// validar + publicar + guardar), más la validación de existencia de la línea que
/// clsCrearPlanDemoCommand no necesita.
/// </summary>
public class clsCrearPlanDesdeLineaCommandTests
{
    private readonly Mock<ILineaSolicitudCubicacionPort> _lineas = new();
    private readonly Mock<ICubicacionPort> _cubicacion = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRechazoCubicacionLineaPort> _rechazos = new();

    private static readonly Guid LineaId = Guid.Parse("dddd0000-0000-0000-0000-000000000001");

    private clsCrearPlanDesdeLineaCommandHandler Handler() => new(_lineas.Object, _cubicacion.Object, _rechazos.Object, _uow.Object);

    private void LineaExiste() =>
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaParaCubicacionDto(LineaId, [], "Bogotá", "Medellín"));

    [Fact]
    public async Task Con_la_linea_inexistente_no_publica_nada()
    {
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsLineaParaCubicacionDto?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(
            () => Handler().Handle(new clsCrearPlanDesdeLineaCommand(LineaId)));

        Assert.Equal("LINEA_SOLICITUD_NO_ENCONTRADA", ex.Code);
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Publica_crear_desde_linea_con_el_lineaId_como_correlationId()
    {
        LineaExiste();

        var r = await Handler().Handle(new clsCrearPlanDesdeLineaCommand(LineaId));

        Assert.Equal(LineaId, r.CorrelationId);
        Assert.Equal("procesando", r.Estado);
        // El tipo de cola es "crear_desde_linea" (lo que el worker de Cubicador ya
        // implementa), NO "crear_plan_desde_linea".
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            "crear_desde_linea", LineaId.ToString(),
            It.Is<clsCrearPlanDesdeLineaInput>(i => i.LineaSolicitudId == LineaId.ToString()),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // #304: el puente del Cubicador ya no inventa 1 kg: rechaza con PESO_NO_DECLARADO. Ese rechazo nacía en el worker,
    // después del 202, y NEXO solo lo registraba en el log. Ahora se valida ANTES de encolar, con el mismo código.
    [Theory]
    [InlineData(null)]
    [InlineData(0)] // el cero cuenta como no declarado
    public async Task Una_posicion_sin_peso_se_rechaza_con_PESO_NO_DECLARADO_y_no_publica_nada(int? pesoKg)
    {
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaParaCubicacionDto(LineaId,
            [
                new clsPosicionCargaDto("Vidrio", 1, 100, 100, 100, 15m, "VIDRIO_PLANO"),
                new clsPosicionCargaDto("Cajas de cartón", 5, 40, 30, 30, pesoKg, "CAJAS_CARTON"),
            ], "Bogotá", "Medellín"));

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsCrearPlanDesdeLineaCommand(LineaId)));

        Assert.Equal("PESO_NO_DECLARADO", ex.Code);
        Assert.Contains("Cajas de cartón", ex.Message);
        Assert.DoesNotContain("Vidrio", ex.Message); // solo nombra las que faltan
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Con_el_peso_de_todas_las_posiciones_publica_normal()
    {
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaParaCubicacionDto(LineaId,
                [new clsPosicionCargaDto("Cajas", 5, 40, 30, 30, 20m, "CAJAS_CARTON")], "Bogotá", "Medellín"));

        var r = await Handler().Handle(new clsCrearPlanDesdeLineaCommand(LineaId));

        Assert.Equal("procesando", r.Estado);
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            "crear_desde_linea", LineaId.ToString(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // #304: pedir el plan otra vez empieza de cero: se borra el rechazo del intento anterior, ANTES de guardar (con el
    // mismo SaveChanges que flushea el Outbox), para que el estado vuelva a "procesando".
    [Fact]
    public async Task Pedir_el_plan_otra_vez_borra_el_rechazo_anterior_antes_de_guardar()
    {
        LineaExiste();
        var orden = new List<string>();
        _rechazos.Setup(r => r.LimpiarAsync(LineaId, It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("limpiar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Handler().Handle(new clsCrearPlanDesdeLineaCommand(LineaId));

        Assert.Equal(new[] { "limpiar", "guardar" }, orden);
    }

    // Si ni siquiera se encola (peso sin declarar), no se toca un rechazo anterior.
    [Fact]
    public async Task Si_se_rechaza_antes_de_encolar_no_toca_el_rechazo_anterior()
    {
        _lineas.Setup(l => l.ObtenerAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsLineaParaCubicacionDto(LineaId,
                [new clsPosicionCargaDto("Cajas", 5, 40, 30, 30, null, "CAJAS_CARTON")], "Bogotá", "Medellín"));

        await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsCrearPlanDesdeLineaCommand(LineaId)));

        _rechazos.Verify(r => r.LimpiarAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

        await Handler().Handle(new clsCrearPlanDesdeLineaCommand(LineaId));

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }
}
