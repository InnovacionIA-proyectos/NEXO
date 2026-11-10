using Nexo.Application.CasosDeUso.DiccionarioCliente;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.DiccionarioCliente;

/// <summary>
/// El diccionario de términos por cliente: qué se acepta al crear y editar, qué se rechaza y con qué código, y que lo guardado queda
/// en su forma canónica y auditado. Sin base: el repositorio es un doble.
/// </summary>
public class clsDiccionarioClienteTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly Mock<IDiccionarioClienteRepository> _repo = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IClientesPort> _clientes = new();
    private readonly Mock<IClientesDwPort> _dw = new();
    private readonly Mock<IProspectosDwPort> _prospectos = new();
    private readonly List<clsCliente> _clientesAgregados = [];

    public clsDiccionarioClienteTests()
    {
        _usuario.SetupGet(u => u.Id).Returns(Guid.NewGuid());
        _repo.Setup(r => r.ClienteExisteAsync(ClienteId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.BuscarDuplicadoAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsTerminoCliente?)null);
        _clientes.Setup(c => c.Agregar(It.IsAny<clsCliente>())).Callback<clsCliente>(_clientesAgregados.Add);
        _dw.SetupGet(d => d.Configurado).Returns(true);
        _dw.Setup(d => d.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _prospectos.SetupGet(p => p.Configurado).Returns(true);
        _prospectos.Setup(p => p.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repo.Setup(r => r.EsChoqueUnico(It.IsAny<Exception>())).Returns(false);
    }

    private clsClienteDeImportacion Importacion() => new(_clientes.Object, _dw.Object, _prospectos.Object);
    private clsCrearTerminoClienteCommandHandler Crear() => new(_repo.Object, _auditoria.Object, _usuario.Object, _uow.Object, Importacion());
    private clsEditarTerminoClienteCommandHandler Editar() =>
        new(_repo.Object, _auditoria.Object, _usuario.Object, _uow.Object, TimeProvider.System);

    private void ClienteLocal(string nit, Guid id) =>
        _clientes.Setup(c => c.ObtenerPorNitAsync(nit, It.IsAny<CancellationToken>())).ReturnsAsync(new clsCliente { Id = id, Nombre = "Local", Nit = nit });

    private static readonly Exception ChoqueUnico = new InvalidOperationException("índice único");

    private static clsCrearTerminoClienteCommand Comando(
        string? termino = "CIUDAD ORIGEN", string? tipo = "columna", string? concepto = "origen", string? contexto = null,
        string? ejemplo = null, Guid? cliente = null, bool activo = true) =>
        new(cliente ?? ClienteId, termino, tipo, concepto, contexto, ejemplo, activo);

    // ---------------------------------------------------------------- crear: lo que se acepta

    [Fact]
    public async Task Una_columna_se_guarda_con_su_forma_canonica_y_queda_auditada()
    {
        clsTerminoCliente? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(t => guardado = t);

        await Crear().Handle(Comando(termino: "  Ciudad   Origén ", concepto: "ORIGEN"));

        Assert.NotNull(guardado);
        Assert.Equal("Ciudad Origén", guardado!.Termino);
        Assert.Equal("CIUDAD ORIGEN", guardado.TerminoNormalizado);
        Assert.Equal("columna", guardado.Tipo);
        Assert.Equal("origen", guardado.Concepto);
        Assert.Null(guardado.Contexto);
        Assert.Equal(ClienteId, guardado.ClienteId);
        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid?>(), "diccionario_cliente", guardado.Id, "crear_termino", It.IsAny<string>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_valor_de_vehiculo_acepta_un_concepto_que_el_catalogo_oficial_no_tiene()
    {
        clsTerminoCliente? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(t => guardado = t);

        await Crear().Handle(Comando(termino: "TURBO (4,5 TON)", tipo: "VALOR", concepto: "2_7", contexto: "tipovehiculo"));

        Assert.Equal(("valor", "2_7", "tipoVehiculo"), (guardado!.Tipo, guardado.Concepto, guardado.Contexto));
        Assert.Equal("TURBO (4,5 TON)", guardado.TerminoNormalizado);
    }

    [Fact]
    public async Task Una_unidad_se_guarda_en_minuscula_canonica()
    {
        clsTerminoCliente? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(t => guardado = t);

        await Crear().Handle(Comando(termino: "TON", tipo: "unidad", concepto: "T"));

        Assert.Equal("t", guardado!.Concepto);
    }

    [Fact]
    public async Task Un_termino_general_no_necesita_cliente_ni_consulta_si_existe()
    {
        clsTerminoCliente? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(t => guardado = t);
        var general = new clsCrearTerminoClienteCommand(null, "KG", "columna", "peso", null, null, true);

        await Crear().Handle(general);

        Assert.Null(guardado!.ClienteId);
        _repo.Verify(r => r.ClienteExisteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- crear: lo que se rechaza

    [Theory]
    [InlineData(null, "columna", "origen", null, "TERMINO_REQUERIDO")]
    [InlineData("   ", "columna", "origen", null, "TERMINO_REQUERIDO")]
    [InlineData("X", "otro", "origen", null, "TIPO_TERMINO_INVALIDO")]
    [InlineData("X", null, "origen", null, "TIPO_TERMINO_INVALIDO")]
    [InlineData("X", "columna", null, null, "CONCEPTO_REQUERIDO")]
    [InlineData("X", "columna", "campo-que-no-existe", null, "CAMPO_DESTINO_INVALIDO")]
    [InlineData("X", "unidad", "libras", null, "UNIDAD_INVALIDA")]
    [InlineData("X", "valor", "2_7", null, "CONTEXTO_INVALIDO")]
    [InlineData("X", "valor", "2_7", "campo-raro", "CONTEXTO_INVALIDO")]
    [InlineData("X", "columna", "origen", "tipoVehiculo", "CONTEXTO_NO_APLICA")]
    [InlineData("X", "unidad", "kg", "tipoVehiculo", "CONTEXTO_NO_APLICA")]
    public async Task Los_datos_invalidos_se_rechazan_con_su_codigo(string? termino, string? tipo, string? concepto, string? contexto, string codigo)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Crear().Handle(Comando(termino, tipo, concepto, contexto)));

        Assert.Equal(codigo, ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_termino_o_un_ejemplo_demasiado_largos_se_rechazan()
    {
        var largo = await Assert.ThrowsAsync<clsValidationFailedException>(() => Crear().Handle(Comando(termino: new string('a', 201))));
        Assert.Equal("TERMINO_REQUERIDO", largo.Code);

        var ejemplo = await Assert.ThrowsAsync<clsValidationFailedException>(() => Crear().Handle(Comando(ejemplo: new string('a', 501))));
        Assert.Equal("EJEMPLO_MUY_LARGO", ejemplo.Code);
    }

    [Fact]
    public async Task Un_cliente_que_no_existe_se_rechaza()
    {
        var otro = Guid.NewGuid();
        _repo.Setup(r => r.ClienteExisteAsync(otro, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Crear().Handle(Comando(cliente: otro)));

        Assert.Equal("CLIENTE_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Un_termino_repetido_para_el_mismo_cliente_tipo_y_contexto_es_un_409_que_lo_describe()
    {
        _repo.Setup(r => r.BuscarDuplicadoAsync(ClienteId, "CIUDAD ORIGEN", "columna", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsTerminoCliente { ClienteId = ClienteId, Termino = "Ciudad Origen", Tipo = "columna", Concepto = "destino" });

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Crear().Handle(Comando(termino: "ciudad  origén")));

        Assert.Equal("TERMINO_DUPLICADO", ex.Code);
        Assert.Contains("destino", ex.Message);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- crear: el cliente por NIT

    [Fact]
    public async Task Con_el_NIT_de_un_cliente_local_el_termino_queda_de_ese_cliente()
    {
        var local = Guid.NewGuid();
        ClienteLocal("900123456", local);
        clsTerminoCliente? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(t => guardado = t);

        await Crear().Handle(Comando() with { ClienteId = null, ClienteNit = "900.123.456-7" });

        Assert.Equal(local, guardado!.ClienteId);
        Assert.Empty(_clientesAgregados);
        _repo.Verify(r => r.ClienteExisteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Con_el_NIT_de_un_cliente_del_DW_sin_cliente_local_lo_crea_y_el_termino_queda_de_ese_cliente()
    {
        _dw.Setup(d => d.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new clsClienteDwDto("BP1", "900123456", "Real SAS")]);
        clsTerminoCliente? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(t => guardado = t);

        await Crear().Handle(Comando() with { ClienteId = null, ClienteNit = "900123456" });

        var creado = Assert.Single(_clientesAgregados);
        Assert.Equal(("Real SAS", "900123456"), (creado.Nombre, creado.Nit));
        Assert.Equal(creado.Id, guardado!.ClienteId);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_NIT_que_no_es_de_ningun_cliente_ni_prospecto_se_rechaza_sin_guardar()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Crear().Handle(Comando() with { ClienteId = null, ClienteNit = "999888777" }));

        Assert.Equal("CLIENTE_NIT_INVALIDO", ex.Code);
        Assert.Empty(_clientesAgregados);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mandar_el_id_y_el_NIT_a_la_vez_es_CLIENTE_AMBIGUO()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Crear().Handle(Comando() with { ClienteNit = "900123456" }));

        Assert.Equal("CLIENTE_AMBIGUO", ex.Code);
        _clientes.Verify(c => c.ObtenerPorNitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Un_NIT_en_blanco_cuenta_como_no_enviado_y_el_termino_es_general(string nit)
    {
        clsTerminoCliente? guardado = null;
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(t => guardado = t);

        await Crear().Handle(Comando() with { ClienteId = null, ClienteNit = nit });

        Assert.Null(guardado!.ClienteId);
        _clientes.Verify(c => c.ObtenerPorNitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- crear: dos peticiones a la vez

    [Fact]
    public async Task Si_otra_peticion_guardo_primero_el_mismo_termino_es_409_y_no_un_error_del_servidor()
    {
        var yaGuardado = new clsTerminoCliente { ClienteId = ClienteId, Termino = "Ciudad Origen", Tipo = "columna", Concepto = "origen" };
        _repo.SetupSequence(r => r.BuscarDuplicadoAsync(ClienteId, "CIUDAD ORIGEN", "columna", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsTerminoCliente?)null)
            .ReturnsAsync(yaGuardado);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ChoqueUnico);
        _repo.Setup(r => r.EsChoqueUnico(ChoqueUnico)).Returns(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Crear().Handle(Comando()));

        Assert.Equal("TERMINO_DUPLICADO", ex.Code);
        _repo.Verify(r => r.DescartarCambiosPendientes(), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_el_choque_fue_por_el_mismo_cliente_creado_a_la_vez_se_reintenta_con_el_cliente_ya_guardado()
    {
        _dw.Setup(d => d.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new clsClienteDwDto("BP1", "900123456", "Real SAS")]);
        var local = Guid.NewGuid();
        _clientes.SetupSequence(c => c.ObtenerPorNitAsync("900123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsCliente?)null)
            .ReturnsAsync(new clsCliente { Id = local, Nombre = "Real SAS", Nit = "900123456" });
        _uow.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ChoqueUnico).ReturnsAsync(1);
        _repo.Setup(r => r.EsChoqueUnico(ChoqueUnico)).Returns(true);
        var guardados = new List<clsTerminoCliente>();
        _repo.Setup(r => r.Agregar(It.IsAny<clsTerminoCliente>())).Callback<clsTerminoCliente>(guardados.Add);

        await Crear().Handle(Comando() with { ClienteId = null, ClienteNit = "900123456" });

        Assert.Equal(local, guardados[^1].ClienteId);
        _repo.Verify(r => r.DescartarCambiosPendientes(), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Un_choque_que_no_se_explica_ni_con_el_reintento_se_propaga()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ChoqueUnico);
        _repo.Setup(r => r.EsChoqueUnico(ChoqueUnico)).Returns(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Crear().Handle(Comando()));

        Assert.Same(ChoqueUnico, ex);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Un_error_de_guardado_que_no_es_un_choque_unico_no_se_traduce()
    {
        var otro = new InvalidOperationException("otro");
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(otro);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Crear().Handle(Comando()));

        Assert.Same(otro, ex);
        _repo.Verify(r => r.DescartarCambiosPendientes(), Times.Never);
    }

    // ---------------------------------------------------------------- editar

    private static clsTerminoCliente Existente() => new()
    {
        Id = Guid.NewGuid(), ClienteId = ClienteId, Termino = "KG", TerminoNormalizado = "KG", Tipo = "columna", Concepto = "peso",
    };

    [Fact]
    public async Task Editar_un_termino_inexistente_es_404()
    {
        _repo.Setup(r => r.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsTerminoCliente?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(
            () => Editar().Handle(new clsEditarTerminoClienteCommand(Guid.NewGuid(), "x", "columna", "origen", null, null, true)));

        Assert.Equal("TERMINO_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Editar_reemplaza_los_campos_marca_la_fecha_y_no_cambia_el_cliente()
    {
        var actual = Existente();
        _repo.Setup(r => r.ObtenerAsync(actual.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actual);

        var dto = await Editar().Handle(new clsEditarTerminoClienteCommand(actual.Id, "Kilos", "unidad", "KG", null, "ej", false));

        Assert.Equal(("Kilos", "KILOS", "unidad", "kg", false), (actual.Termino, actual.TerminoNormalizado, actual.Tipo, actual.Concepto, actual.Activo));
        Assert.Equal(ClienteId, actual.ClienteId);
        Assert.NotNull(actual.UpdatedAt);
        Assert.Equal(actual.Id, dto.Id);
        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid?>(), "diccionario_cliente", actual.Id, "editar_termino", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Editar_no_choca_consigo_mismo_pero_si_con_otro_termino()
    {
        var actual = Existente();
        _repo.Setup(r => r.ObtenerAsync(actual.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actual);

        await Editar().Handle(new clsEditarTerminoClienteCommand(actual.Id, "KG", "columna", "peso", null, "nuevo ejemplo", true));
        _repo.Verify(r => r.BuscarDuplicadoAsync(ClienteId, "KG", "columna", null, actual.Id, It.IsAny<CancellationToken>()), Times.Once);

        _repo.Setup(r => r.BuscarDuplicadoAsync(ClienteId, "TON", "columna", null, actual.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsTerminoCliente { ClienteId = ClienteId, Termino = "TON", Tipo = "columna", Concepto = "peso" });
        var ex = await Assert.ThrowsAsync<clsConflictException>(
            () => Editar().Handle(new clsEditarTerminoClienteCommand(actual.Id, "TON", "columna", "peso", null, null, true)));
        Assert.Equal("TERMINO_DUPLICADO", ex.Code);
    }

    [Fact]
    public async Task Editar_con_un_choque_de_otra_peticion_es_409_TERMINO_DUPLICADO()
    {
        var actual = Existente();
        _repo.Setup(r => r.ObtenerAsync(actual.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actual);
        var otro = new clsTerminoCliente { ClienteId = ClienteId, Termino = "TON", Tipo = "columna", Concepto = "peso" };
        _repo.SetupSequence(r => r.BuscarDuplicadoAsync(ClienteId, "TON", "columna", null, actual.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsTerminoCliente?)null)
            .ReturnsAsync(otro);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ChoqueUnico);
        _repo.Setup(r => r.EsChoqueUnico(ChoqueUnico)).Returns(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(
            () => Editar().Handle(new clsEditarTerminoClienteCommand(actual.Id, "TON", "columna", "peso", null, null, true)));

        Assert.Equal("TERMINO_DUPLICADO", ex.Code);
        _repo.Verify(r => r.DescartarCambiosPendientes(), Times.Once);
    }

    // ---------------------------------------------------------------- eliminar y listar

    [Fact]
    public async Task Eliminar_borra_y_audita_y_uno_inexistente_es_404()
    {
        var actual = Existente();
        _repo.Setup(r => r.ObtenerAsync(actual.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actual);
        var manejador = new clsEliminarTerminoClienteCommandHandler(_repo.Object, _auditoria.Object, _usuario.Object, _uow.Object);

        await manejador.Handle(new clsEliminarTerminoClienteCommand(actual.Id));

        _repo.Verify(r => r.Eliminar(actual), Times.Once);
        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid?>(), "diccionario_cliente", actual.Id, "eliminar_termino", It.IsAny<string>()), Times.Once);

        _repo.Setup(r => r.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsTerminoCliente?)null);
        await Assert.ThrowsAsync<clsNotFoundException>(() => manejador.Handle(new clsEliminarTerminoClienteCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Listar_con_un_tipo_invalido_es_400_y_con_uno_valido_lo_normaliza()
    {
        var manejador = new clsListarDiccionarioClienteQueryHandler(_repo.Object, Importacion());
        _repo.Setup(r => r.ListarAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => manejador.Handle(new clsListarDiccionarioClienteQuery(null, true, "raro", null, false)));
        Assert.Equal("TIPO_TERMINO_INVALIDO", ex.Code);

        await manejador.Handle(new clsListarDiccionarioClienteQuery(ClienteId, true, "COLUMNA", "  peso ", true));
        _repo.Verify(r => r.ListarAsync(ClienteId, true, "columna", "peso", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_por_NIT_filtra_por_el_cliente_local_y_el_id_manda_sobre_el_NIT()
    {
        var manejador = new clsListarDiccionarioClienteQueryHandler(_repo.Object, Importacion());
        var local = Guid.NewGuid();
        ClienteLocal("900123456", local);
        _repo.Setup(r => r.ListarAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await manejador.Handle(new clsListarDiccionarioClienteQuery(null, true, null, null, false, "900.123.456-7"));
        _repo.Verify(r => r.ListarAsync(local, true, null, null, false, It.IsAny<CancellationToken>()), Times.Once);

        await manejador.Handle(new clsListarDiccionarioClienteQuery(ClienteId, true, null, null, false, "900123456"));
        _repo.Verify(r => r.ListarAsync(ClienteId, true, null, null, false, It.IsAny<CancellationToken>()), Times.Once);
        _clientes.Verify(c => c.ObtenerPorNitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_por_un_NIT_sin_cliente_local_no_crea_nada_y_no_trae_terminos_de_ningun_cliente()
    {
        var manejador = new clsListarDiccionarioClienteQueryHandler(_repo.Object, Importacion());
        _repo.Setup(r => r.ListarAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await manejador.Handle(new clsListarDiccionarioClienteQuery(null, true, null, null, false, "111222333"));

        // El id vacío no pertenece a ningún cliente: quedan solo los generales (los de todos los clientes no se piden).
        _repo.Verify(r => r.ListarAsync(Guid.Empty, true, null, null, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_clientesAgregados);
    }

    [Fact]
    public async Task Las_opciones_traen_los_valores_que_acepta_el_diccionario()
    {
        var o = await new clsObtenerOpcionesDiccionarioClienteQueryHandler().Handle(new clsObtenerOpcionesDiccionarioClienteQuery());

        Assert.Equal(["columna", "unidad", "valor"], o.Tipos);
        Assert.Contains("origen", o.CamposDestino);
        Assert.Contains("ignorar", o.CamposDestino);
        Assert.Equal(["kg", "t"], o.Unidades);
        Assert.Contains("tipoVehiculo", o.Contextos);
    }
}
