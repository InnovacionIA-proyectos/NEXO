using CabinaFletes.Application.CasosDeUso.PlanDeCargue;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Mensajes.Cubicacion;
using Moq;

namespace CabinaFletes.Application.Tests.PlanDeCargue;

/// <summary>
/// S3-HU-02: crear un plan deja la fila espejo de NEXO en "procesando" hasta que llegue
/// la confirmación de Cubicador.
///
/// El test que importa es <c>La_fila_espejo_nace_procesando_para_bloquear_un_optimizar_inmediato</c>:
/// fija la corrección de una carrera real. Con la fila en "borrador" desde el POST, un
/// <c>POST /{id}/optimizar</c> inmediato pasaba la guarda de
/// <see cref="SolicitarOptimizacionCommand"/> y encolaba "optimizar" mientras "crear_plan"
/// seguía sin procesarse — misma cola, prefetch 2, así que el worker podía atender
/// "optimizar" primero y fallar con PLAN_NO_ENCONTRADO. Si alguien devuelve el estado a
/// "borrador", este test falla y explica por qué.
/// </summary>
public class CrearPlanCommandTests
{
    private readonly Mock<IPlanCargueRepository> _planes = new();
    private readonly Mock<ICubicacionPort> _cubicacion = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IParidadCubicadorPort> _paridad = new();

    private CrearPlanCommandHandler Handler() => new(_planes.Object, _cubicacion.Object, _uow.Object);

    private static CrearPlanCommand Comando() => new("Plan de prueba", Guid.NewGuid());

    /// <summary>Captura la entidad que el handler mandó a persistir.</summary>
    private PlanCargue CapturarPlanCreado()
    {
        PlanCargue? capturado = null;
        _planes.Setup(r => r.PrepararCreacionAsync(It.IsAny<PlanCargue>(), It.IsAny<CancellationToken>()))
            .Callback<PlanCargue, CancellationToken>((p, _) => capturado = p)
            .Returns(Task.CompletedTask);
        return capturado!;
    }

    [Fact]
    public async Task La_fila_espejo_nace_procesando_para_bloquear_un_optimizar_inmediato()
    {
        PlanCargue? creado = null;
        _planes.Setup(r => r.PrepararCreacionAsync(It.IsAny<PlanCargue>(), It.IsAny<CancellationToken>()))
            .Callback<PlanCargue, CancellationToken>((p, _) => creado = p)
            .Returns(Task.CompletedTask);

        var r = await Handler().Handle(Comando());

        Assert.NotNull(creado);
        Assert.Equal("procesando", creado!.Estado);
        Assert.Equal("procesando", r.Estado);

        // Y la consecuencia que justifica el estado: con esa fila, un optimizar
        // inmediato choca contra la guarda que YA existía, sin código nuevo.
        _paridad.SetupGet(p => p.Habilitada).Returns(true);
        _planes.Setup(x => x.ObtenerAsync(creado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(creado);
        var optimizar = new SolicitarOptimizacionCommandHandler(
            _paridad.Object, _planes.Object, _cubicacion.Object, _uow.Object);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => optimizar.Handle(new SolicitarOptimizacionCommand(creado.Id, null, null, null, null)));

        Assert.Equal("PLAN_CARGUE_EN_PROCESO", ex.Code);
    }

    [Fact]
    public async Task Publica_crear_plan_con_el_id_que_genero_nexo()
    {
        PlanCargue? creado = null;
        _planes.Setup(r => r.PrepararCreacionAsync(It.IsAny<PlanCargue>(), It.IsAny<CancellationToken>()))
            .Callback<PlanCargue, CancellationToken>((p, _) => creado = p)
            .Returns(Task.CompletedTask);

        var r = await Handler().Handle(Comando());

        Assert.Equal(creado!.Id, r.PlanCargueId);
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            "crear_plan", creado.Id.ToString(), It.IsAny<CrearPlanInput>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publica_antes_de_guardar()
    {
        // El Outbox exige este orden — ver docs/mensajeria/CUBICACION.md #5.
        _ = CapturarPlanCreado();
        var orden = new List<string>();
        _cubicacion.Setup(c => c.PublicarComandoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Handler().Handle(Comando());

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }

    [Fact]
    public async Task Un_nombre_vacio_no_publica_nada()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(
            () => Handler().Handle(new CrearPlanCommand("   ", Guid.NewGuid())));

        _cubicacion.Verify(c => c.PublicarComandoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
