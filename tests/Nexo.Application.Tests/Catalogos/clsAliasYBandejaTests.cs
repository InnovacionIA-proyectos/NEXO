using Nexo.Application.CasosDeUso.Catalogos;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.Catalogos;

/// <summary>
/// S6-HU-27.2/#321 (diccionario de alias: listar, resolver, crear) y S6-HU-27.3/#322 (bandeja de normalización: listar, mapear, descartar).
/// </summary>
public class clsAliasYBandejaTests
{
    private readonly Mock<IAliasCatalogoPort> _alias = new();
    private readonly Mock<ITiposVehiculoPort> _tiposVehiculo = new();
    private readonly Mock<IConfiguracionesVehiculoPort> _vehiculos = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<Nexo.Application.Common.ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    public clsAliasYBandejaTests()
    {
        _tiposVehiculo.Setup(t => t.ListarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new clsTipoVehiculoDto("2S2", "Minimula", true), new clsTipoVehiculoDto("C2", "Sencillo", true)]);
        _alias.Setup(a => a.ListarCodigosAsync(clsAliasElemento.TipoVehiculo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["2L2", "3S3"]);
        _alias.Setup(a => a.MapaActivoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
    }

    private static clsAliasElemento Alias(string texto, string codigo, string tipo = clsAliasElemento.TipoVehiculo, bool activo = true) =>
        new() { Alias = texto, AliasNormalizado = Nexo.Application.CasosDeUso.Solicitudes.clsResolucionDeCatalogos.Normalizar(texto), TipoElemento = tipo, Codigo = codigo, Activo = activo };

    private clsCrearAliasCommandHandler Crear() => new(_alias.Object, _tiposVehiculo.Object, _auditoria.Object, _usuario.Object, _uow.Object);
    private clsMapearVehiculoCuarentenaCommandHandler Mapear() =>
        new(_vehiculos.Object, _alias.Object, _tiposVehiculo.Object, _auditoria.Object, _usuario.Object, _uow.Object);

    // ---------------------------------------------------------------- listar y resolver

    [Fact]
    public async Task Listar_con_tipo_invalido_falla_400()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            new clsListarAliasQueryHandler(_alias.Object).Handle(new clsListarAliasQuery("camion")));

        Assert.Equal("TIPO_ALIAS_INVALIDO", ex.Code);
    }

    [Theory]
    [InlineData("vehiculo", clsAliasElemento.TipoVehiculo)]
    [InlineData("Vehículo", clsAliasElemento.TipoVehiculo)]
    [InlineData("CONTENEDOR", clsAliasElemento.TipoContenedor)]
    public async Task Listar_filtra_por_el_tipo_sin_importar_mayusculas_ni_tildes(string tipo, string esperado)
    {
        _alias.Setup(a => a.ListarAsync(esperado, It.IsAny<CancellationToken>())).ReturnsAsync([Alias("TB", "2L2")]);

        var r = await new clsListarAliasQueryHandler(_alias.Object).Handle(new clsListarAliasQuery(tipo));

        Assert.Single(r);
    }

    [Theory]
    [InlineData("TB")]
    [InlineData("t.b")]
    [InlineData("  Turbo ")]
    public async Task Resolver_un_texto_conocido_devuelve_su_codigo(string texto)
    {
        _alias.Setup(a => a.BuscarPorNormalizadoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => n is "TB" or "T.B" or "TURBO" ? Alias(n, "2L2") : null);

        var r = await new clsResolverAliasQueryHandler(_alias.Object).Handle(new clsResolverAliasQuery(null, texto));

        Assert.True(r.Reconocido);
        Assert.Equal("2L2", r.Codigo);
        Assert.Equal(clsAliasElemento.TipoVehiculo, r.TipoElemento);   // por defecto: vehículo
    }

    [Fact]
    public async Task Resolver_un_texto_inventado_dice_que_no_se_reconoce()
    {
        _alias.Setup(a => a.BuscarPorNormalizadoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAliasElemento?)null);

        var r = await new clsResolverAliasQueryHandler(_alias.Object).Handle(new clsResolverAliasQuery("vehiculo", "ZZZ inventado"));

        Assert.False(r.Reconocido);
        Assert.Null(r.Codigo);
    }

    [Fact]
    public async Task Resolver_un_alias_de_otro_tipo_o_inactivo_no_cuenta()
    {
        _alias.Setup(a => a.BuscarPorNormalizadoAsync("40 HC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Alias("40 HC", "CNT40.HC", clsAliasElemento.TipoContenedor));
        _alias.Setup(a => a.BuscarPorNormalizadoAsync("VIEJO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Alias("Viejo", "2L2", activo: false));
        var h = new clsResolverAliasQueryHandler(_alias.Object);

        Assert.False((await h.Handle(new clsResolverAliasQuery("vehiculo", "40 HC"))).Reconocido);
        Assert.True((await h.Handle(new clsResolverAliasQuery("contenedor", "40 HC"))).Reconocido);
        Assert.False((await h.Handle(new clsResolverAliasQuery("vehiculo", "Viejo"))).Reconocido);
    }

    [Fact]
    public async Task Resolver_sin_texto_falla_400()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            new clsResolverAliasQueryHandler(_alias.Object).Handle(new clsResolverAliasQuery("vehiculo", "   ")));

        Assert.Equal("TEXTO_REQUERIDO", ex.Code);
    }

    // ---------------------------------------------------------------- crear

    [Fact]
    public async Task Crear_guarda_el_alias_con_su_normalizado_y_el_codigo_canonico()
    {
        clsAliasElemento? guardado = null;
        _alias.Setup(a => a.BuscarPorNormalizadoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAliasElemento?)null);
        _alias.Setup(a => a.Agregar(It.IsAny<clsAliasElemento>())).Callback<clsAliasElemento>(a => guardado = a);

        var dto = await Crear().Handle(new clsCrearAliasCommand("vehiculo", "  Mini  Mula-X ", "2s2"));   // código en minúscula

        Assert.Equal("2S2", dto.Codigo);                       // el del catálogo, no el que tecleó la persona
        Assert.Equal("Mini Mula-X", dto.Alias);
        Assert.Equal("MINI MULA X", guardado!.AliasNormalizado);
        Assert.Equal(clsAliasElemento.TipoVehiculo, guardado.TipoElemento);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Crear_acepta_los_codigos_de_contenedor_de_SAP()
    {
        _alias.Setup(a => a.ListarCodigosAsync(clsAliasElemento.TipoContenedor, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _alias.Setup(a => a.BuscarPorNormalizadoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAliasElemento?)null);

        var dto = await Crear().Handle(new clsCrearAliasCommand("contenedor", "40 high", "CNT40.HC"));

        Assert.Equal("CNT40.HC", dto.Codigo);
    }

    [Fact]
    public async Task Crear_con_un_codigo_que_no_es_oficial_falla_400_y_no_guarda()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Crear().Handle(new clsCrearAliasCommand("vehiculo", "Camioncito", "XYZ")));

        Assert.Equal("CODIGO_NO_RECONOCIDO", ex.Code);
        _alias.Verify(a => a.Agregar(It.IsAny<clsAliasElemento>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_un_alias_que_ya_existe_falla_409_aunque_sea_de_otro_tipo()
    {
        _alias.Setup(a => a.BuscarPorNormalizadoAsync("TB", It.IsAny<CancellationToken>())).ReturnsAsync(Alias("TB", "2L2"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Crear().Handle(new clsCrearAliasCommand("vehiculo", " tb ", "C2")));

        Assert.Equal("ALIAS_DUPLICADO", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "TB", "2L2", "TIPO_ALIAS_INVALIDO")]
    [InlineData("vehiculo", "  ", "2L2", "ALIAS_REQUERIDO")]
    [InlineData("vehiculo", "TB", " ", "CODIGO_NO_RECONOCIDO")]
    public async Task Crear_valida_los_tres_datos(string? tipo, string? alias, string? codigo, string esperado)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Crear().Handle(new clsCrearAliasCommand(tipo, alias, codigo)));

        Assert.Equal(esperado, ex.Code);
    }

    // ---------------------------------------------------------------- bandeja: listar

    [Fact]
    public async Task La_bandeja_lista_las_filas_en_cuarentena_y_deja_fuera_las_de_un_tipo_ya_mapeado()
    {
        var nueva = new clsConfiguracionVehiculo { TipoVehiculo = "Furgon 2 ejes", Carroceria = "Seca" };
        var yaMapeada = new clsConfiguracionVehiculo { TipoVehiculo = "T.MULA", Carroceria = null };
        _vehiculos.Setup(v => v.ListarEnCuarentenaAsync(It.IsAny<CancellationToken>())).ReturnsAsync([nueva, yaMapeada]);
        _alias.Setup(a => a.MapaActivoAsync(clsAliasElemento.TipoVehiculo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["T.MULA"] = "3S3" });

        var r = await new clsListarVehiculosCuarentenaQueryHandler(_vehiculos.Object, _alias.Object).Handle(new clsListarVehiculosCuarentenaQuery());

        var fila = Assert.Single(r);
        Assert.Equal(nueva.Id, fila.Id);
        Assert.Equal("Furgon 2 ejes", fila.TipoVehiculo);
        Assert.Equal("Seca", fila.Carroceria);
    }

    // ---------------------------------------------------------------- bandeja: mapear

    [Fact]
    public async Task Mapear_crea_el_alias_del_tipo_de_vehiculo_con_el_codigo_elegido()
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = "Tracto-mula  carrozada", Carroceria = "Furgon" };
        _vehiculos.Setup(x => x.ObtenerAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);
        _alias.Setup(a => a.BuscarPorNormalizadoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAliasElemento?)null);
        clsAliasElemento? guardado = null;
        _alias.Setup(a => a.Agregar(It.IsAny<clsAliasElemento>())).Callback<clsAliasElemento>(a => guardado = a);

        var r = await Mapear().Handle(new clsMapearVehiculoCuarentenaCommand(v.Id, "3S3"));

        Assert.True(r.AliasCreado);
        Assert.Equal("3S3", r.Codigo);
        Assert.Equal("TRACTO MULA CARROZADA", guardado!.AliasNormalizado);
        Assert.Equal(clsAliasElemento.TipoVehiculo, guardado.TipoElemento);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Mapear_a_un_alias_que_ya_apunta_al_mismo_codigo_no_crea_otro()
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = "TB" };
        _vehiculos.Setup(x => x.ObtenerAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);
        _alias.Setup(a => a.BuscarPorNormalizadoAsync("TB", It.IsAny<CancellationToken>())).ReturnsAsync(Alias("TB", "2L2"));

        var r = await Mapear().Handle(new clsMapearVehiculoCuarentenaCommand(v.Id, "2L2"));

        Assert.False(r.AliasCreado);
        _alias.Verify(a => a.Agregar(It.IsAny<clsAliasElemento>()), Times.Never);
    }

    [Fact]
    public async Task Mapear_a_un_codigo_distinto_del_que_ya_tiene_el_alias_falla_409()
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = "TB" };
        _vehiculos.Setup(x => x.ObtenerAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);
        _alias.Setup(a => a.BuscarPorNormalizadoAsync("TB", It.IsAny<CancellationToken>())).ReturnsAsync(Alias("TB", "2L2"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Mapear().Handle(new clsMapearVehiculoCuarentenaCommand(v.Id, "C2")));

        Assert.Equal("ALIAS_DUPLICADO", ex.Code);
    }

    [Fact]
    public async Task Mapear_con_un_codigo_que_no_es_oficial_falla_400()
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = "Algo raro" };
        _vehiculos.Setup(x => x.ObtenerAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Mapear().Handle(new clsMapearVehiculoCuarentenaCommand(v.Id, "NOEXISTE")));

        Assert.Equal("CODIGO_NO_RECONOCIDO", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mapear_o_descartar_una_configuracion_inexistente_falla_404()
    {
        _vehiculos.Setup(x => x.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsConfiguracionVehiculo?)null);

        var m = await Assert.ThrowsAsync<clsNotFoundException>(() => Mapear().Handle(new clsMapearVehiculoCuarentenaCommand(Guid.NewGuid(), "2L2")));
        var d = await Assert.ThrowsAsync<clsNotFoundException>(() => Descartar().Handle(new clsDescartarVehiculoCuarentenaCommand(Guid.NewGuid())));

        Assert.Equal("VEHICULO_NO_ENCONTRADO", m.Code);
        Assert.Equal("VEHICULO_NO_ENCONTRADO", d.Code);
    }

    [Fact]
    public async Task Una_configuracion_oficial_no_se_mapea_ni_se_descarta_409()
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = "C2", EsCatalogoOficial = true };
        _vehiculos.Setup(x => x.ObtenerAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);

        var m = await Assert.ThrowsAsync<clsConflictException>(() => Mapear().Handle(new clsMapearVehiculoCuarentenaCommand(v.Id, "C2")));
        var d = await Assert.ThrowsAsync<clsConflictException>(() => Descartar().Handle(new clsDescartarVehiculoCuarentenaCommand(v.Id)));

        Assert.Equal("VEHICULO_NO_ESTA_EN_CUARENTENA", m.Code);
        Assert.Equal("VEHICULO_NO_ESTA_EN_CUARENTENA", d.Code);
        Assert.False(v.Descartada);
    }

    // ---------------------------------------------------------------- bandeja: descartar

    private clsDescartarVehiculoCuarentenaCommandHandler Descartar() =>
        new(_vehiculos.Object, _auditoria.Object, _usuario.Object, _uow.Object);

    [Fact]
    public async Task Descartar_marca_la_fila_y_no_la_borra()
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = "Basura" };
        _vehiculos.Setup(x => x.ObtenerAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);

        await Descartar().Handle(new clsDescartarVehiculoCuarentenaCommand(v.Id));

        Assert.True(v.Descartada);
        Assert.False(v.EsCatalogoOficial);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Descartar_dos_veces_o_mapear_una_descartada_falla_409()
    {
        var v = new clsConfiguracionVehiculo { TipoVehiculo = "Basura", Descartada = true };
        _vehiculos.Setup(x => x.ObtenerAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);

        var d = await Assert.ThrowsAsync<clsConflictException>(() => Descartar().Handle(new clsDescartarVehiculoCuarentenaCommand(v.Id)));
        var m = await Assert.ThrowsAsync<clsConflictException>(() => Mapear().Handle(new clsMapearVehiculoCuarentenaCommand(v.Id, "2L2")));

        Assert.Equal("VEHICULO_YA_DESCARTADO", d.Code);
        Assert.Equal("VEHICULO_YA_DESCARTADO", m.Code);
    }
}
