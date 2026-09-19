using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Mensajes.Costeo;
using Moq;

namespace Nexo.Application.Tests.AjustesVigenciaPlaza;

public class clsAjustarVigenciaPlazaCommandTests
{
    private readonly Mock<IAjustesVigenciaPlazaRepository> _ajustes = new();
    private readonly Mock<IVigenciaSapPlazaPort> _vigenciaSap = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly clsFakeTimeProvider _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsAjustarVigenciaPlazaCommandHandler Handler() => new(
        _ajustes.Object, _vigenciaSap.Object, _auditoria.Object, _usuarioActual.Object,
        _publicador.Object, _clock, _uow.Object);

    [Fact]
    public async Task Ajuste_nuevo_consulta_SAP_para_guardar_la_vigencia_original_de_referencia()
    {
        _ajustes.Setup(a => a.ObtenerPorTarifaAsync(42, default)).ReturnsAsync((clsAjusteVigenciaPlaza?)null);
        var vigenciaSapOriginal = new DateTime(2026, 1, 1);
        _vigenciaSap.Setup(v => v.VigenciasAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 42 })), default))
            .ReturnsAsync(new Dictionary<int, DateTime> { [42] = vigenciaSapOriginal });

        clsAjusteVigenciaPlaza? agregado = null;
        _ajustes.Setup(a => a.Agregar(It.IsAny<clsAjusteVigenciaPlaza>())).Callback<clsAjusteVigenciaPlaza>(a => agregado = a);

        await Handler().Handle(new clsAjustarVigenciaPlazaCommand(42, new DateTime(2026, 6, 1), "porque si"));

        Assert.NotNull(agregado);
        Assert.Equal(vigenciaSapOriginal, agregado!.VigenciaOriginal);
        Assert.Equal(new DateTime(2026, 6, 1), agregado.VigenciaAjustada);
        _ajustes.Verify(a => a.InvalidarCache(), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Ajuste_existente_se_actualiza_sin_volver_a_consultar_SAP()
    {
        var existente = new clsAjusteVigenciaPlaza { TarifaPlazaId = 7, VigenciaAjustada = new DateTime(2025, 1, 1) };
        _ajustes.Setup(a => a.ObtenerPorTarifaAsync(7, default)).ReturnsAsync(existente);

        await Handler().Handle(new clsAjustarVigenciaPlazaCommand(7, new DateTime(2026, 3, 15), null));

        Assert.Equal(new DateTime(2026, 3, 15), existente.VigenciaAjustada);
        _vigenciaSap.Verify(v => v.VigenciasAsync(It.IsAny<IReadOnlyCollection<int>>(), default), Times.Never);
        _ajustes.Verify(a => a.Agregar(It.IsAny<clsAjusteVigenciaPlaza>()), Times.Never);
    }

    [Fact]
    public async Task Motivo_vacio_no_reemplaza_el_motivo_ya_guardado()
    {
        var existente = new clsAjusteVigenciaPlaza { TarifaPlazaId = 7, VigenciaAjustada = new DateTime(2025, 1, 1), Motivo = "original" };
        _ajustes.Setup(a => a.ObtenerPorTarifaAsync(7, default)).ReturnsAsync(existente);

        await Handler().Handle(new clsAjustarVigenciaPlazaCommand(7, new DateTime(2026, 3, 15), "   "));

        Assert.Equal("original", existente.Motivo);
    }
}

public class clsAjustarVigenciaPlazaMasivoCommandTests
{
    private readonly Mock<IAjustesVigenciaPlazaRepository> _ajustes = new();
    private readonly Mock<IVigenciaSapPlazaPort> _vigenciaSap = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    public clsAjustarVigenciaPlazaMasivoCommandTests()
    {
        _ajustes.Setup(a => a.ObtenerPorTarifasAsync(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync(new Dictionary<int, clsAjusteVigenciaPlaza>());
        _vigenciaSap.Setup(v => v.VigenciasAsync(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync(new Dictionary<int, DateTime>());
    }

    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly clsFakeTimeProvider _clock = new();

    private clsAjustarVigenciaPlazaMasivoCommandHandler Handler() => new(
        _ajustes.Object, _vigenciaSap.Object, _auditoria.Object, _usuarioActual.Object,
        _publicador.Object, _clock, _uow.Object);

    [Fact]
    public async Task Modo_fecha_sin_nueva_vigencia_lanza_ValidationFailedException()
    {
        await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsAjustarVigenciaPlazaMasivoCommand([1, 2], "fecha", null, null, null, null)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Modo_extender_con_cantidad_invalida_lanza_ValidationFailedException(int cantidad)
    {
        await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsAjustarVigenciaPlazaMasivoCommand([1], "extender", null, cantidad, "dias", null)));
    }

    [Fact]
    public async Task Modo_extender_sin_unidad_lanza_ValidationFailedException()
    {
        await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsAjustarVigenciaPlazaMasivoCommand([1], "extender", null, 5, null, null)));
    }

    [Fact]
    public async Task Modo_fecha_aplica_la_misma_fecha_a_todas_las_solicitadas()
    {
        var resultado = await Handler().Handle(
            new clsAjustarVigenciaPlazaMasivoCommand([1, 2, 3], "fecha", new DateTime(2026, 12, 1), null, null, "cierre de año"));

        Assert.Equal(3, resultado.Aplicados);
        Assert.Equal(3, resultado.Solicitados);
        _ajustes.Verify(a => a.Agregar(It.Is<clsAjusteVigenciaPlaza>(x => x.VigenciaAjustada == new DateTime(2026, 12, 1))), Times.Exactly(3));
    }

    [Fact]
    public async Task Modo_extender_usa_el_override_local_como_base_sin_consultar_SAP_para_ese_id()
    {
        var existente = new clsAjusteVigenciaPlaza { TarifaPlazaId = 10, VigenciaAjustada = new DateTime(2026, 1, 1) };
        _ajustes.Setup(a => a.ObtenerPorTarifasAsync(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync(new Dictionary<int, clsAjusteVigenciaPlaza> { [10] = existente });

        var resultado = await Handler().Handle(
            new clsAjustarVigenciaPlazaMasivoCommand([10], "extender", null, 10, "dias", null));

        Assert.Equal(new DateTime(2026, 1, 11), existente.VigenciaAjustada);
        Assert.Equal(1, resultado.Aplicados);
        _vigenciaSap.Verify(v => v.VigenciasAsync(
            It.Is<IReadOnlyCollection<int>>(ids => !ids.Contains(10)), default), Times.Once);
    }

    [Fact]
    public async Task Modo_extender_usa_la_vigencia_SAP_como_base_cuando_no_hay_override()
    {
        _vigenciaSap.Setup(v => v.VigenciasAsync(It.IsAny<IReadOnlyCollection<int>>(), default))
            .ReturnsAsync(new Dictionary<int, DateTime> { [20] = new DateTime(2026, 1, 1) });

        var resultado = await Handler().Handle(
            new clsAjustarVigenciaPlazaMasivoCommand([20], "extender", null, 2, "meses", null));

        Assert.Equal(1, resultado.Aplicados);
        _ajustes.Verify(a => a.Agregar(It.Is<clsAjusteVigenciaPlaza>(x => x.VigenciaAjustada == new DateTime(2026, 3, 1))), Times.Once);
    }

    [Fact]
    public async Task Modo_extender_sin_base_conocida_omite_ese_id_y_no_cuenta_como_aplicado()
    {
        var resultado = await Handler().Handle(
            new clsAjustarVigenciaPlazaMasivoCommand([99], "extender", null, 5, "dias", null));

        Assert.Equal(0, resultado.Aplicados);
        Assert.Equal(1, resultado.Solicitados);
        _ajustes.Verify(a => a.Agregar(It.IsAny<clsAjusteVigenciaPlaza>()), Times.Never);
    }

    [Fact]
    public async Task Ids_duplicados_se_deduplican_antes_de_procesar()
    {
        var resultado = await Handler().Handle(
            new clsAjustarVigenciaPlazaMasivoCommand([5, 5, 5], "fecha", new DateTime(2026, 1, 1), null, null, null));

        Assert.Equal(1, resultado.Aplicados);
        Assert.Equal(1, resultado.Solicitados);
    }
}

public class clsQuitarVigenciaPlazaCommandTests
{
    private readonly Mock<IAjustesVigenciaPlazaRepository> _ajustes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsQuitarVigenciaPlazaCommandHandler Handler() => new(
        _ajustes.Object, _auditoria.Object, _usuarioActual.Object, _uow.Object);

    [Fact]
    public async Task Sin_ajuste_previo_retorna_false_y_no_guarda()
    {
        _ajustes.Setup(a => a.ObtenerPorTarifaAsync(1, default)).ReturnsAsync((clsAjusteVigenciaPlaza?)null);

        var resultado = await Handler().Handle(new clsQuitarVigenciaPlazaCommand(1));

        Assert.False(resultado);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Con_ajuste_previo_lo_elimina_y_retorna_true()
    {
        var existente = new clsAjusteVigenciaPlaza { TarifaPlazaId = 1 };
        _ajustes.Setup(a => a.ObtenerPorTarifaAsync(1, default)).ReturnsAsync(existente);

        var resultado = await Handler().Handle(new clsQuitarVigenciaPlazaCommand(1));

        Assert.True(resultado);
        _ajustes.Verify(a => a.Eliminar(existente), Times.Once);
        _ajustes.Verify(a => a.InvalidarCache(), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
