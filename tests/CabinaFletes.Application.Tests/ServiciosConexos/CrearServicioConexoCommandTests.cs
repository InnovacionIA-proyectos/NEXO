using CabinaFletes.Application.CasosDeUso.ServiciosConexos;
using CabinaFletes.Application.CasosDeUso.ServiciosConexos.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Domain.Catalogos.Entidades;
using Moq;

namespace CabinaFletes.Application.Tests.ServiciosConexos;

public class CrearServicioConexoCommandTests
{
    private readonly Mock<IServiciosConexosRepository> _repo = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CrearServicioConexoCommandHandler Handler() => new(_repo.Object, _usuarioActual.Object, _uow.Object);

    [Fact]
    public async Task Nombre_duplicado_lanza_ConflictException()
    {
        _repo.Setup(r => r.ExisteNombreAsync("Flete adicional", default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Handler().Handle(new CrearServicioConexoCommand("Flete adicional", null, null, null, null, true)));

        _repo.Verify(r => r.Agregar(It.IsAny<Domain.Catalogos.Entidades.ServicioConexo>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "logistico")]
    [InlineData("", "logistico")]
    [InlineData("LOGISTICO", "logistico")]
    [InlineData("adicional", "adicional")]
    [InlineData("ADICIONAL", "adicional")]
    public async Task Categoria_se_normaliza_o_usa_el_valor_por_defecto(string? entrada, string esperada)
    {
        _repo.Setup(r => r.ExisteNombreAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        Domain.Catalogos.Entidades.ServicioConexo? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<Domain.Catalogos.Entidades.ServicioConexo>()))
            .Callback<Domain.Catalogos.Entidades.ServicioConexo>(s => guardado = s);

        await Handler().Handle(new CrearServicioConexoCommand("Servicio X", entrada, null, null, null, true));

        Assert.Equal(esperada, guardado!.Categoria);
    }

    [Fact]
    public async Task Categoria_no_reconocida_lanza_ValidationFailedException()
    {
        _repo.Setup(r => r.ExisteNombreAsync(It.IsAny<string>(), default)).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Handler().Handle(new CrearServicioConexoCommand("Servicio X", "opcional", null, null, null, true)));

        Assert.Equal("CATEGORIA_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Nombre_valido_crea_el_servicio_y_guarda()
    {
        _repo.Setup(r => r.ExisteNombreAsync(It.IsAny<string>(), default)).ReturnsAsync(false);

        var dto = await Handler().Handle(new CrearServicioConexoCommand("  Servicio Y  ", "adicional", null, 5000m, null, true));

        Assert.Equal("Servicio Y", dto.Nombre); // se recorta
        _repo.Verify(r => r.Agregar(It.IsAny<Domain.Catalogos.Entidades.ServicioConexo>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
