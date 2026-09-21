using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Domain.Catalogos.Entidades;
using Moq;

namespace CabinaFletes.Application.Tests.Solicitudes;

/// <summary>
/// HU-005 parte 2: la búsqueda de clientes del formulario de captura ahora combina
/// DimBPSolicitante (clientes reales) con tblProspectos (prospectos comerciales, todavía
/// sin BP). La regla acordada: primero DimBPSolicitante; solo si esa búsqueda no
/// encuentra nada se consulta tblProspectos — así nunca se cargan/combinan los dos
/// catálogos completos en la misma búsqueda, y no hace falta deduplicar.
/// </summary>
public class BuscarClientesQueryTests
{
    private readonly Mock<IClientesDwPort> _clientesDw = new();
    private readonly Mock<IProspectosDwPort> _prospectosDw = new();
    private readonly Mock<IClientesPort> _clientesLocal = new();

    private BuscarClientesQueryHandler Handler() => new(_clientesDw.Object, _prospectosDw.Object, _clientesLocal.Object);

    [Fact]
    public async Task Cliente_que_solo_esta_en_DimBP_aparece_con_origen_cliente_sin_consultar_prospectos()
    {
        _clientesDw.SetupGet(p => p.Configurado).Returns(true);
        _clientesDw.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ClienteDwDto("BP001", "900123456", "Distribuciones Ana SAS")]);
        _prospectosDw.SetupGet(p => p.Configurado).Returns(true);

        var resultado = await Handler().Handle(new BuscarClientesQuery("Ana"));

        var cliente = Assert.Single(resultado);
        Assert.Equal("cliente", cliente.Origen);
        Assert.Equal("BP001", cliente.Bp);
        Assert.Null(cliente.ClienteId);
        _prospectosDw.Verify(p => p.ListarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Prospecto_que_solo_esta_en_tblProspectos_aparece_con_origen_prospecto()
    {
        _clientesDw.SetupGet(p => p.Configurado).Returns(true);
        _clientesDw.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ClienteDwDto("BP002", "800999888", "Otro Cliente SAS")]);
        _prospectosDw.SetupGet(p => p.Configurado).Returns(true);
        _prospectosDw.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProspectoDwDto("900555444", "Textiles del Norte")]);

        var resultado = await Handler().Handle(new BuscarClientesQuery("Textiles"));

        var prospecto = Assert.Single(resultado);
        Assert.Equal("prospecto", prospecto.Origen);
        Assert.Null(prospecto.Bp);
        Assert.Null(prospecto.ClienteId);
        Assert.Equal("900555444", prospecto.Nit);
    }

    [Fact]
    public async Task Sin_coincidencias_en_ninguna_fuente_devuelve_vacio()
    {
        _clientesDw.SetupGet(p => p.Configurado).Returns(true);
        _clientesDw.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _prospectosDw.SetupGet(p => p.Configurado).Returns(true);
        _prospectosDw.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var resultado = await Handler().Handle(new BuscarClientesQuery("NoExiste"));

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Sin_prospectos_configurado_no_falla_cuando_DimBP_no_tiene_coincidencias()
    {
        _clientesDw.SetupGet(p => p.Configurado).Returns(true);
        _clientesDw.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _prospectosDw.SetupGet(p => p.Configurado).Returns(false);

        var resultado = await Handler().Handle(new BuscarClientesQuery("Cualquiera"));

        Assert.Empty(resultado);
        _prospectosDw.Verify(p => p.ListarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_DW_configurado_sigue_usando_la_tabla_local_con_origen_cliente()
    {
        _clientesDw.SetupGet(p => p.Configurado).Returns(false);
        var clienteLocal = new Cliente { Nit = "900111222", Nombre = "Cliente Local SAS" };
        _clientesLocal.Setup(p => p.BuscarAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([clienteLocal]);

        var resultado = await Handler().Handle(new BuscarClientesQuery("Local"));

        var cliente = Assert.Single(resultado);
        Assert.Equal("cliente", cliente.Origen);
        Assert.Equal(clienteLocal.Id, cliente.ClienteId);
        _prospectosDw.Verify(p => p.ListarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
