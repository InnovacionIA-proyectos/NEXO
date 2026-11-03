using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.ServiciosConexos;

public class clsCrearServicioConexoCommandTests
{
    private readonly Mock<IServiciosConexosRepository> _repo = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsCrearServicioConexoCommandHandler Handler() => new(_repo.Object, _usuarioActual.Object, _uow.Object);

    private static clsCrearServicioConexoCommand Comando(
        string codigo = "PRE_BSAT", string tipo = "Precinto", string? origen = null, string? destino = null,
        string? puerto = null, string? ruta = null, string? tipoMaquina = null, decimal costo = 5000m,
        string unidad = "Por unidad", string? categoria = "adicional") =>
        new(codigo, tipo, origen, destino, puerto, ruta, tipoMaquina, costo, unidad, new DateOnly(2026, 12, 31), categoria, true);

    private void SinDuplicados() =>
        _repo.Setup(r => r.ExisteDimensionesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), default))
            .ReturnsAsync(false);

    [Fact]
    public async Task Combinacion_de_dimensiones_duplicada_lanza_ConflictException()
    {
        _repo.Setup(r => r.ExisteDimensionesAsync(
                "PRE_BSAT", clsServicioConexo.NoAplica, clsServicioConexo.NoAplica, clsServicioConexo.NoAplica,
                "BSAT", clsServicioConexo.NoAplica, null, default))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(Comando(ruta: "BSAT")));

        _repo.Verify(r => r.Agregar(It.IsAny<clsServicioConexo>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "logistico")]
    [InlineData("", "logistico")]
    [InlineData("LOGISTICO", "logistico")]
    [InlineData("adicional", "adicional")]
    [InlineData("ADICIONAL", "adicional")]
    public async Task Categoria_se_normaliza_o_usa_el_valor_por_defecto(string? entrada, string esperada)
    {
        SinDuplicados();
        clsServicioConexo? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsServicioConexo>())).Callback<clsServicioConexo>(s => guardado = s);

        await Handler().Handle(Comando(categoria: entrada));

        Assert.Equal(esperada, guardado!.Categoria);
    }

    [Fact]
    public async Task Categoria_no_reconocida_lanza_ValidationFailedException()
    {
        SinDuplicados();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(Comando(categoria: "opcional")));

        Assert.Equal("CATEGORIA_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Dimensiones_vacias_se_normalizan_a_NO_APLICA()
    {
        SinDuplicados();
        clsServicioConexo? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsServicioConexo>())).Callback<clsServicioConexo>(s => guardado = s);

        await Handler().Handle(Comando(origen: null, destino: "  "));

        Assert.Equal(clsServicioConexo.NoAplica, guardado!.Origen);
        Assert.Equal(clsServicioConexo.NoAplica, guardado.Destino);
    }

    [Fact]
    public async Task Datos_validos_crea_el_servicio_y_guarda()
    {
        SinDuplicados();

        var dto = await Handler().Handle(Comando(tipo: "  Precinto de seguridad  "));

        Assert.Equal("Precinto de seguridad", dto.TipoServicio); // se recorta
        _repo.Verify(r => r.Agregar(It.IsAny<clsServicioConexo>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
