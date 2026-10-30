using Nexo.Application.CasosDeUso.RangosPeso;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Costeo.Entidades;
using Moq;

namespace Nexo.Application.Tests.RangosPeso;

public class clsRangosPesoCommandsTests
{
    private readonly Mock<IRangosPesoRepository> _rangos = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsCrearRangoPesoCommandHandler CrearHandler() => new(_rangos.Object, _uow.Object);
    private clsEditarRangoPesoCommandHandler EditarHandler() => new(_rangos.Object, _uow.Object);
    private clsEliminarRangoPesoCommandHandler EliminarHandler() => new(_rangos.Object);
    private clsListarRangosPesoQueryHandler ListarHandler() => new(_rangos.Object);

    [Fact]
    public async Task Crea_una_banda_valida()
    {
        _rangos.Setup(r => r.ExisteAsync(3, 15000, null, default)).ReturnsAsync(false);
        clsRangoPeso? agregado = null;
        _rangos.Setup(r => r.Agregar(It.IsAny<clsRangoPeso>())).Callback<clsRangoPeso>(x => agregado = x);

        var dto = await CrearHandler().Handle(new clsCrearRangoPesoCommand(3, 15000));

        Assert.NotNull(agregado);
        Assert.Equal(3, dto.PesoMinimo);
        Assert.Equal(15000, dto.PesoMaximo);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Crea_una_banda_abierta_sin_peso_maximo()
    {
        _rangos.Setup(r => r.ExisteAsync(10500, null, null, default)).ReturnsAsync(false);

        var dto = await CrearHandler().Handle(new clsCrearRangoPesoCommand(10500, null));

        Assert.Null(dto.PesoMaximo);
    }

    [Fact]
    public async Task Peso_maximo_menor_o_igual_al_minimo_lanza_RANGO_PESO_INVALIDO()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            CrearHandler().Handle(new clsCrearRangoPesoCommand(15000, 15000)));

        Assert.Equal("RANGO_PESO_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task Peso_minimo_negativo_lanza_RANGO_PESO_INVALIDO()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            CrearHandler().Handle(new clsCrearRangoPesoCommand(-1, 100)));

        Assert.Equal("RANGO_PESO_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task Banda_duplicada_lanza_RANGO_PESO_DUPLICADO()
    {
        _rangos.Setup(r => r.ExisteAsync(3, 15000, null, default)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            CrearHandler().Handle(new clsCrearRangoPesoCommand(3, 15000)));

        Assert.Equal("RANGO_PESO_DUPLICADO", ex.Code);
    }

    [Fact]
    public async Task Editar_banda_inexistente_lanza_RANGO_PESO_NO_ENCONTRADO()
    {
        _rangos.Setup(r => r.ObtenerAsync(99, default)).ReturnsAsync((clsRangoPeso?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            EditarHandler().Handle(new clsEditarRangoPesoCommand(99, 3, 15000)));

        Assert.Equal("RANGO_PESO_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Editar_actualiza_los_valores_y_excluye_su_propio_id_del_chequeo_de_duplicado()
    {
        var existente = new clsRangoPeso { Id = 5, PesoMinimo = 3, PesoMaximo = 15000 };
        _rangos.Setup(r => r.ObtenerAsync(5, default)).ReturnsAsync(existente);
        _rangos.Setup(r => r.ExisteAsync(18001, 23000, 5, default)).ReturnsAsync(false);

        var dto = await EditarHandler().Handle(new clsEditarRangoPesoCommand(5, 18001, 23000));

        Assert.Equal(18001, dto.PesoMinimo);
        Assert.Equal(23000, dto.PesoMaximo);
        _rangos.Verify(r => r.ExisteAsync(18001, 23000, 5, default), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Eliminar_banda_inexistente_devuelve_false()
    {
        _rangos.Setup(r => r.EliminarAsync(99, default)).ReturnsAsync(0);

        Assert.False(await EliminarHandler().Handle(new clsEliminarRangoPesoCommand(99)));
    }

    [Fact]
    public async Task Listar_ordena_por_peso_minimo()
    {
        _rangos.Setup(r => r.ListarAsync(default)).ReturnsAsync(
        [
            new clsRangoPeso { Id = 2, PesoMinimo = 15001, PesoMaximo = 18000 },
            new clsRangoPeso { Id = 1, PesoMinimo = 3, PesoMaximo = 15000 },
        ]);

        var lista = await ListarHandler().Handle(new clsListarRangosPesoQuery());

        Assert.Equal([3, 15001], lista.Select(r => r.PesoMinimo));
    }
}
