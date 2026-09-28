using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// Issue #206: el buscador de clientes nunca traía un clienteId ya resuelto para un
/// cliente real del catálogo DW (DimBPSolicitante) — clsResolucionDeCliente es el
/// mecanismo compartido (extraído de clsCapturarSolicitudCommand, donde ya corría en
/// producción) que resuelve por clienteId, o busca-o-crea el espejo local por
/// clienteBp/NIT. Sin tests previos: esta es la primera cobertura de esta lógica.
/// </summary>
public class clsResolucionDeClienteTests
{
    private readonly Mock<IClientesPort> _clientes = new();
    private readonly Mock<IClientesDwPort> _clientesDw = new();

    private Task<Guid> Resolver(Guid? clienteId, string? clienteBp) =>
        clsResolucionDeCliente.ResolverAsync(clienteId, clienteBp, _clientes.Object, _clientesDw.Object, default);

    [Fact]
    public async Task Con_clienteId_existente_lo_devuelve_sin_tocar_el_DW()
    {
        var id = Guid.NewGuid();
        _clientes.Setup(c => c.ExisteAsync(id, default)).ReturnsAsync(true);

        var resultado = await Resolver(id, null);

        Assert.Equal(id, resultado);
        _clientesDw.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Con_clienteId_inexistente_lanza_CLIENTE_INVALIDO()
    {
        var id = Guid.NewGuid();
        _clientes.Setup(c => c.ExisteAsync(id, default)).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Resolver(id, null));

        Assert.Equal("CLIENTE_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task Sin_clienteId_ni_clienteBp_lanza_CLIENTE_REQUERIDO()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Resolver(null, null));

        Assert.Equal("CLIENTE_REQUERIDO", ex.Code);
    }

    [Fact]
    public async Task Con_clienteBp_y_DW_no_configurado_lanza_DWCUBOS_NO_CONFIGURADO()
    {
        _clientesDw.SetupGet(d => d.Configurado).Returns(false);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Resolver(null, "BP-1"));

        Assert.Equal("DWCUBOS_NO_CONFIGURADO", ex.Code);
    }

    [Fact]
    public async Task Con_clienteBp_que_no_existe_en_el_catalogo_DW_lanza_CLIENTE_BP_INVALIDO()
    {
        _clientesDw.SetupGet(d => d.Configurado).Returns(true);
        _clientesDw.Setup(d => d.ListarAsync(default)).ReturnsAsync([]);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Resolver(null, "BP-NO-EXISTE"));

        Assert.Equal("CLIENTE_BP_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task Con_clienteBp_valido_y_cliente_local_ya_existente_por_NIT_reusa_su_Id_sin_crear_otro()
    {
        var nit = "900123456";
        var existente = new clsCliente { Nombre = "Cliente Real S.A.S.", Nit = nit };
        _clientesDw.SetupGet(d => d.Configurado).Returns(true);
        _clientesDw.Setup(d => d.ListarAsync(default)).ReturnsAsync([new clsClienteDwDto("BP-1", nit, "Cliente Real S.A.S.")]);
        _clientes.Setup(c => c.ObtenerPorNitAsync(nit, default)).ReturnsAsync(existente);

        var resultado = await Resolver(null, "BP-1");

        Assert.Equal(existente.Id, resultado);
        _clientes.Verify(c => c.Agregar(It.IsAny<clsCliente>()), Times.Never);
    }

    [Fact]
    public async Task Con_clienteBp_valido_y_sin_cliente_local_lo_crea_por_NIT()
    {
        var nit = "900123456";
        _clientesDw.SetupGet(d => d.Configurado).Returns(true);
        _clientesDw.Setup(d => d.ListarAsync(default)).ReturnsAsync([new clsClienteDwDto("BP-1", nit, "Cliente Real S.A.S.")]);
        _clientes.Setup(c => c.ObtenerPorNitAsync(nit, default)).ReturnsAsync((clsCliente?)null);
        clsCliente? creado = null;
        _clientes.Setup(c => c.Agregar(It.IsAny<clsCliente>())).Callback<clsCliente>(c => creado = c);

        var resultado = await Resolver(null, "BP-1");

        Assert.NotNull(creado);
        Assert.Equal(nit, creado!.Nit);
        Assert.Equal("Cliente Real S.A.S.", creado.Nombre);
        Assert.Equal(creado.Id, resultado);
    }

    [Fact]
    public async Task El_BP_se_recorta_antes_de_comparar_contra_el_catalogo()
    {
        _clientesDw.SetupGet(d => d.Configurado).Returns(true);
        _clientesDw.Setup(d => d.ListarAsync(default)).ReturnsAsync([new clsClienteDwDto("BP-1", "900123456", "Cliente Real S.A.S.")]);
        _clientes.Setup(c => c.ObtenerPorNitAsync("900123456", default)).ReturnsAsync(new clsCliente { Nombre = "x", Nit = "900123456" });

        await Resolver(null, "  BP-1  ");
        // No lanza CLIENTE_BP_INVALIDO: si llega hasta acá sin excepción, el trim funcionó.
    }
}
