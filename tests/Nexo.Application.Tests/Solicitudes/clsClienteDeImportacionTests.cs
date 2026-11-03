using Moq;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.Tests.Solicitudes;

public class clsClienteDeImportacionTests
{
    private readonly Mock<IClientesPort> _clientes = new();
    private readonly Mock<IClientesDwPort> _dw = new();
    private readonly Mock<IProspectosDwPort> _prospectos = new();
    private readonly List<clsCliente> _agregados = [];

    public clsClienteDeImportacionTests()
    {
        _clientes.Setup(c => c.Agregar(It.IsAny<clsCliente>())).Callback<clsCliente>(_agregados.Add);
        _dw.SetupGet(d => d.Configurado).Returns(true);
        _dw.Setup(d => d.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _prospectos.SetupGet(p => p.Configurado).Returns(true);
        _prospectos.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private clsClienteDeImportacion Servicio() => new(_clientes.Object, _dw.Object, _prospectos.Object);

    private void Local(string nit, Guid id) =>
        _clientes.Setup(c => c.ObtenerPorNitAsync(nit, It.IsAny<CancellationToken>())).ReturnsAsync(new clsCliente { Id = id, Nombre = "Local", Nit = nit });

    [Fact]
    public async Task Buscar_encuentra_el_cliente_local_con_el_NIT_normalizado_y_nunca_crea()
    {
        var id = Guid.NewGuid();
        Local("900123456", id);

        Assert.Equal(id, await Servicio().BuscarLocalAsync("900.123.456-7", default));
        Assert.Null(await Servicio().BuscarLocalAsync("111222333", default));
        Assert.Null(await Servicio().BuscarLocalAsync("  ", default));
        Assert.Empty(_agregados);
    }

    [Fact]
    public async Task Con_cliente_local_no_consulta_el_DW_ni_crea()
    {
        var id = Guid.NewGuid();
        Local("900123456", id);

        Assert.Equal(id, await Servicio().ObtenerOCrearAsync("900.123.456-7", default));
        Assert.Empty(_agregados);
        _dw.Verify(d => d.ListarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_cliente_local_lo_crea_con_los_datos_del_catalogo_de_clientes()
    {
        _dw.Setup(d => d.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new clsClienteDwDto("BP1", "900123456", "Real SAS")]);

        var id = await Servicio().ObtenerOCrearAsync("900.123.456-7", default);

        var creado = Assert.Single(_agregados);
        Assert.Equal((id, "900123456", "Real SAS"), (creado.Id, creado.Nit, creado.Nombre));
        _prospectos.Verify(p => p.ListarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_cliente_local_ni_cliente_del_DW_lo_crea_desde_el_prospecto()
    {
        _prospectos.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new clsProspectoDwDto("900123456", "Prospecto SAS")]);

        await Servicio().ObtenerOCrearAsync("900123456", default);

        var creado = Assert.Single(_agregados);
        Assert.Equal(("900123456", "Prospecto SAS"), (creado.Nit, creado.Nombre));
    }

    [Theory]
    [InlineData("900123456")]
    [InlineData("---")]
    public async Task Un_NIT_que_no_es_de_nadie_es_CLIENTE_NIT_INVALIDO(string nit)
    {
        var error = await Assert.ThrowsAsync<clsValidationFailedException>(() => Servicio().ObtenerOCrearAsync(nit, default));

        Assert.Equal("CLIENTE_NIT_INVALIDO", error.Code);
        Assert.Empty(_agregados);
    }

    [Fact]
    public async Task Con_el_DW_sin_configurar_solo_existe_lo_local()
    {
        _dw.SetupGet(d => d.Configurado).Returns(false);
        _prospectos.SetupGet(p => p.Configurado).Returns(false);

        await Assert.ThrowsAsync<clsValidationFailedException>(() => Servicio().ObtenerOCrearAsync("900123456", default));
    }
}
