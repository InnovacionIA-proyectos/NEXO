using System.Text.Json;
using Moq;
using Nexo.Application.CasosDeUso.LecturaExcel;
using Nexo.Application.CasosDeUso.LecturaExcel.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using static Nexo.Application.Tests.LecturaExcel.clsHojasDePrueba;

namespace Nexo.Application.Tests.LecturaExcel;

/// <summary>
/// Leer la estructura de un Excel: cuándo se reutiliza el formato guardado sin IA, cuándo se le pregunta a la IA (con la receta anterior y lo que cambió),
/// qué pasa si la IA falla, y guardar / listar / eliminar los formatos de un cliente.
/// </summary>
public class clsLecturaEstructuraTests
{
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid Usuario = Guid.NewGuid();

    private readonly Mock<ILectorDeHojasPort> _lector = new();
    private readonly Mock<IRecetasLecturaRepository> _repo = new();
    private readonly Mock<IAsistenteIaPort> _ia = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly List<clsRecetaLecturaCliente> _guardadas = [];
    private readonly List<clsPeticionIa> _peticiones = [];
    private readonly Queue<string> _respuestas = new();

    public clsLecturaEstructuraTests()
    {
        _usuario.SetupGet(u => u.Id).Returns(Usuario);
        _repo.Setup(r => r.ListarAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _guardadas.ToList());
        _repo.Setup(r => r.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _guardadas.FirstOrDefault(g => g.Id == id));
        _repo.Setup(r => r.ClienteExisteAsync(Cliente, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.Agregar(It.IsAny<clsRecetaLecturaCliente>())).Callback<clsRecetaLecturaCliente>(_guardadas.Add);
        _repo.Setup(r => r.Eliminar(It.IsAny<clsRecetaLecturaCliente>())).Callback<clsRecetaLecturaCliente>(g => _guardadas.Remove(g));
        _ia.Setup(i => i.Habilitada(clsFuncionIa.ImportacionExcel)).Returns(true);
        _ia.Setup(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsPeticionIa p, CancellationToken _) =>
            {
                _peticiones.Add(p);
                var texto = _respuestas.Dequeue();
                return new clsRespuestaIa(texto, texto, "modelo-de-prueba", 10, 5, TimeSpan.FromMilliseconds(1));
            });
    }

    private static clsClienteDeImportacion ClientesDeImportacion() =>
        new(new Mock<IClientesPort>().Object, new Mock<IClientesDwPort>().Object, new Mock<IProspectosDwPort>().Object);

    private clsLeerEstructuraCommandHandler Lector(IReadOnlyList<clsHojaCruda> hojas)
    {
        _lector.Setup(l => l.LeerAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(hojas);
        return new(_lector.Object, _repo.Object, ClientesDeImportacion(), _ia.Object, _usuario.Object, _uow.Object, TimeProvider.System);
    }

    private clsGuardarRecetaLecturaCommandHandler Guardador(IReadOnlyList<clsHojaCruda> hojas)
    {
        _lector.Setup(l => l.LeerAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(hojas);
        return new(_lector.Object, _repo.Object, ClientesDeImportacion(), _auditoria.Object, _usuario.Object, _uow.Object, TimeProvider.System);
    }

    // ---------------------------------------------------------------- los archivos de prueba

    private static IReadOnlyList<clsHojaCruda> Formato(params string[] titulos) =>
    [
        Hoja("Tarifas", [
            "Formulario de oferta " + string.Join(" ", titulos.Take(1)),
            "Razón social|ACME",
            "",
            string.Join("|", titulos),
            "Bello|Neiva|Sencillo|120",
            "Cali|Bogota|Tractomula|300",
        ]),
    ];

    private static readonly string[] TitulosA = ["Origen", "Destino", "Vehículo", "Tarifa", "Peso", "Observaciones"];
    private static readonly string[] TitulosB = ["Origen", "Destino", "Vehículo", "Valor flete", "Tonelaje", "Comentarios"];

    private static clsRecetaLectura RecetaA() => new(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [4])]);

    private clsRecetaLecturaCliente GuardadaDe(IReadOnlyList<clsHojaCruda> hojas, clsRecetaLectura receta, string? instrucciones = null, int usos = 0)
    {
        var g = new clsRecetaLecturaCliente
        {
            ClienteId = Cliente, Receta = receta.ASerializado(), Instrucciones = instrucciones, Usos = usos,
            Huella = JsonSerializer.Serialize(clsHuellaDeEstructura.Tokens(hojas, receta), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };
        _guardadas.Add(g);
        return g;
    }

    private static string RespuestaIa(clsRecetaLectura receta, string resumen = "Una tabla simple.", string[]? dudas = null) =>
        JsonSerializer.Serialize(new { receta = JsonDocument.Parse(receta.ASerializado()).RootElement, resumen, dudas = dudas ?? [] });

    private static clsLeerEstructuraCommand Leer(Guid? cliente = null, string? instrucciones = null, string? receta = null) =>
        new(new MemoryStream([1]), "archivo.xlsx", cliente, null, instrucciones, receta);

    // ---------------------------------------------------------------- formato guardado

    [Fact]
    public async Task Un_archivo_con_el_mismo_formato_guardado_se_lee_sin_IA_y_suma_un_uso()
    {
        var hojas = Formato(TitulosA);
        var guardada = GuardadaDe(hojas, RecetaA(), usos: 2);

        var r = await Lector(hojas).Handle(Leer(Cliente));

        Assert.Equal("guardada", r.OrigenReceta);
        Assert.False(r.IaUsada);
        Assert.Equal(guardada.Id, r.RecetaGuardadaId);
        Assert.True(r.Similitud >= 0.85);
        Assert.Equal(2, Assert.Single(r.Tablas).TotalFilas);
        Assert.Empty(_peticiones);
        Assert.Equal(3, guardada.Usos);
        Assert.NotNull(guardada.UltimoUsoAt);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_cliente_no_se_busca_formato_guardado_y_se_consulta_a_la_IA()
    {
        var hojas = Formato(TitulosA);
        GuardadaDe(hojas, RecetaA());
        _respuestas.Enqueue(RespuestaIa(RecetaA(), "La tabla empieza en la fila 4.", ["¿Peso en kg o toneladas?"]));

        var r = await Lector(hojas).Handle(Leer());

        Assert.Equal("ia", r.OrigenReceta);
        Assert.True(r.IaUsada);
        Assert.Null(r.RecetaGuardadaId);
        Assert.Equal("La tabla empieza en la fila 4.", r.Resumen);
        Assert.Equal(["¿Peso en kg o toneladas?"], r.Dudas);
        Assert.Single(_peticiones);
        Assert.Equal(clsFuncionIa.ImportacionExcel, _peticiones[0].Funcion);
        Assert.Equal(Usuario, _peticiones[0].UsuarioId);
    }

    [Fact]
    public async Task Con_una_instruccion_siempre_se_consulta_a_la_IA_aunque_haya_formato_guardado()
    {
        var hojas = Formato(TitulosA);
        GuardadaDe(hojas, RecetaA());
        _respuestas.Enqueue(RespuestaIa(RecetaA()));

        var r = await Lector(hojas).Handle(Leer(Cliente, instrucciones: "La columna Peso está en toneladas"));

        Assert.Equal("ia", r.OrigenReceta);
        Assert.Contains("La columna Peso está en toneladas", _peticiones.Single().Mensajes.Single().Contenido);
    }

    [Fact]
    public async Task Si_el_formato_cambio_la_IA_recibe_la_receta_anterior_y_lo_que_cambio()
    {
        var anterior = Formato(TitulosA);
        GuardadaDe(anterior, RecetaA(), instrucciones: "El encabezado está en la fila 4");
        var nuevo = Formato(TitulosB);
        var nueva = new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [4])]);
        _respuestas.Enqueue(RespuestaIa(nueva));

        var r = await Lector(nuevo).Handle(Leer(Cliente));

        Assert.Equal("ia", r.OrigenReceta);
        Assert.NotNull(r.Cambios);
        Assert.Contains(r.Cambios!.Faltan, t => t.Contains("tarifa", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Cambios.Nuevos, t => t.Contains("valor flete", StringComparison.OrdinalIgnoreCase));
        var mensaje = _peticiones.Single().Mensajes.Single().Contenido;
        Assert.Contains("El encabezado está en la fila 4", mensaje);
        Assert.Contains("ya no aparece", mensaje);
    }

    [Fact]
    public async Task Un_formato_completamente_distinto_no_se_usa_de_base()
    {
        GuardadaDe(Formato(TitulosA), RecetaA());
        var otro = new[] { Hoja("Hoja1", ["Cliente|Producto|Cantidad", "X|Y|1"]) };
        _respuestas.Enqueue(RespuestaIa(new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Hoja1", FilasEncabezado: [1])])));

        var r = await Lector(otro).Handle(Leer(Cliente));

        Assert.Equal("ia", r.OrigenReceta);
        Assert.Null(r.Cambios);
    }

    // ---------------------------------------------------------------- receta enviada

    [Fact]
    public async Task Una_receta_enviada_sin_instrucciones_se_ejecuta_tal_cual_sin_IA()
    {
        var hojas = Formato(TitulosA);

        var r = await Lector(hojas).Handle(Leer(receta: RecetaA().ASerializado()));

        Assert.Equal("manual", r.OrigenReceta);
        Assert.False(r.IaUsada);
        Assert.Equal(2, Assert.Single(r.Tablas).TotalFilas);
        Assert.Empty(_peticiones);
    }

    [Fact]
    public async Task Una_receta_enviada_con_errores_se_rechaza_con_RECETA_INVALIDA()
    {
        var hojas = Formato(TitulosA);
        var mala = new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [0])]);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Lector(hojas).Handle(Leer(receta: mala.ASerializado())));

        Assert.Equal("RECETA_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Una_receta_enviada_que_no_es_JSON_se_rechaza()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Lector(Formato(TitulosA)).Handle(Leer(receta: "no es json")));

        Assert.Equal("RECETA_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Una_receta_enviada_con_una_instruccion_la_usa_la_IA_de_base()
    {
        var hojas = Formato(TitulosA);
        _respuestas.Enqueue(RespuestaIa(RecetaA()));

        var r = await Lector(hojas).Handle(Leer(instrucciones: "Quita la columna Peso", receta: RecetaA().ASerializado()));

        Assert.Equal("ia", r.OrigenReceta);
        Assert.Contains("Quita la columna Peso", _peticiones.Single().Mensajes.Single().Contenido);
    }

    [Fact]
    public async Task Las_instrucciones_demasiado_largas_se_rechazan()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Lector(Formato(TitulosA)).Handle(Leer(instrucciones: new string('a', clsPromptReceta.MaxCaracteresInstrucciones + 1))));

        Assert.Equal("INSTRUCCIONES_DEMASIADO_LARGAS", ex.Code);
    }

    // ---------------------------------------------------------------- la IA falla

    [Fact]
    public async Task Sin_IA_y_sin_formato_guardado_se_hace_una_lectura_simple()
    {
        _ia.Setup(i => i.Habilitada(clsFuncionIa.ImportacionExcel)).Returns(false);

        var r = await Lector(Formato(TitulosA)).Handle(Leer(Cliente));

        Assert.Equal("automatica", r.OrigenReceta);
        Assert.False(r.IaUsada);
        Assert.Equal("IA_NO_DISPONIBLE", r.MotivoSinIa);
        Assert.NotEmpty(r.Tablas);
        Assert.Contains(r.Avisos, a => a.Contains("lectura simple"));
    }

    [Fact]
    public async Task Sin_IA_y_con_formato_parecido_se_usa_el_guardado_y_se_avisa()
    {
        var anterior = Formato(TitulosA);
        GuardadaDe(anterior, RecetaA());
        _ia.Setup(i => i.Habilitada(clsFuncionIa.ImportacionExcel)).Returns(false);

        var r = await Lector(Formato(TitulosB)).Handle(Leer(Cliente));

        Assert.Equal("guardada", r.OrigenReceta);
        Assert.Equal("IA_NO_DISPONIBLE", r.MotivoSinIa);
        Assert.Contains(r.Avisos, a => a.Contains("formato cambió"));
    }

    [Fact]
    public async Task Si_el_proveedor_de_IA_falla_se_responde_igual_con_el_motivo()
    {
        _ia.Setup(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsServiceUnavailableException("IA_TOPE_USO", "Tope superado."));

        var r = await Lector(Formato(TitulosA)).Handle(Leer(Cliente));

        Assert.Equal("automatica", r.OrigenReceta);
        Assert.Equal("IA_TOPE_USO", r.MotivoSinIa);
    }

    [Fact]
    public async Task Una_respuesta_de_la_IA_que_no_se_entiende_cae_a_la_lectura_simple()
    {
        _respuestas.Enqueue("esto no es json");

        var r = await Lector(Formato(TitulosA)).Handle(Leer(Cliente));

        Assert.Equal("automatica", r.OrigenReceta);
        Assert.Equal("IA_RESPUESTA_INVALIDA", r.MotivoSinIa);
    }

    [Fact]
    public async Task Si_la_IA_propone_una_receta_con_errores_se_le_pide_corregirla_una_vez()
    {
        var mala = new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [0])]);
        _respuestas.Enqueue(RespuestaIa(mala));
        _respuestas.Enqueue(RespuestaIa(RecetaA(), "Corregida."));

        var r = await Lector(Formato(TitulosA)).Handle(Leer(Cliente));

        Assert.Equal("ia", r.OrigenReceta);
        Assert.Equal("Corregida.", r.Resumen);
        Assert.Equal(2, _peticiones.Count);
        Assert.Contains(r.Avisos, a => a.Contains("tenía errores"));
        Assert.Equal(3, _peticiones[1].Mensajes.Count);
    }

    [Fact]
    public async Task Si_la_correccion_tambien_falla_cae_a_la_lectura_simple()
    {
        var mala = new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [0])]);
        _respuestas.Enqueue(RespuestaIa(mala));
        _respuestas.Enqueue(RespuestaIa(mala));

        var r = await Lector(Formato(TitulosA)).Handle(Leer(Cliente));

        Assert.Equal("automatica", r.OrigenReceta);
        Assert.Equal("IA_RESPUESTA_INVALIDA", r.MotivoSinIa);
        Assert.Equal(2, _peticiones.Count);
    }

    // ---------------------------------------------------------------- guardar

    private static clsGuardarRecetaLecturaCommand Guardar(clsRecetaLectura? receta = null, Guid? cliente = null, string? nit = null, string? nombre = null, string? instrucciones = null) =>
        new(new MemoryStream([1]), cliente, nit, nombre, (receta ?? RecetaA()).ASerializado(), instrucciones);

    [Fact]
    public async Task Guardar_un_formato_nuevo_lo_crea_con_su_huella_y_lo_audita()
    {
        var hojas = Formato(TitulosA);

        var r = await Guardador(hojas).Handle(Guardar(cliente: Cliente, nombre: "Tarifas 2026", instrucciones: "Encabezado en la fila 4"));

        Assert.True(r.Creada);
        var g = Assert.Single(_guardadas);
        Assert.Equal(Cliente, g.ClienteId);
        Assert.Equal("Tarifas 2026", g.Nombre);
        Assert.Equal("Encabezado en la fila 4", g.Instrucciones);
        Assert.Equal(Usuario, g.CreadoPor);
        Assert.Equal(clsHuellaDeEstructura.Tokens(hojas, RecetaA()), JsonSerializer.Deserialize<List<string>>(g.Huella, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        _auditoria.Verify(a => a.Registrar(Usuario, "receta_lectura_cliente", g.Id, "crear_receta", It.IsAny<string>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Guardar_el_mismo_formato_reemplaza_el_que_ya_tenia_el_cliente()
    {
        var hojas = Formato(TitulosA);
        var existente = GuardadaDe(hojas, RecetaA(), instrucciones: "vieja");
        var ajustada = new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [4], Columnas: ["A:D"])]);

        var r = await Guardador(hojas).Handle(Guardar(ajustada, Cliente, instrucciones: "nueva"));

        Assert.False(r.Creada);
        Assert.Same(existente, Assert.Single(_guardadas));
        Assert.Equal("nueva", existente.Instrucciones);
        Assert.Equal(ajustada.ASerializado(), existente.Receta);
        Assert.NotNull(existente.UpdatedAt);
    }

    [Fact]
    public async Task Guardar_un_formato_distinto_agrega_otro_al_cliente()
    {
        GuardadaDe(Formato(TitulosA), RecetaA());
        var otro = new[] { Hoja("Hoja1", ["Cliente|Producto|Cantidad", "X|Y|1"]) };

        var r = await Guardador(otro).Handle(Guardar(new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Hoja1", FilasEncabezado: [1])]), Cliente));

        Assert.True(r.Creada);
        Assert.Equal(2, _guardadas.Count);
    }

    [Fact]
    public async Task Una_receta_que_no_da_filas_no_se_guarda()
    {
        var hojas = new[] { Hoja("Tarifas", ["Origen|Destino", ""]) };

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Guardador(hojas).Handle(Guardar(new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [1])]), Cliente)));

        Assert.Equal("RECETA_SIN_FILAS", ex.Code);
        Assert.Empty(_guardadas);
    }

    [Fact]
    public async Task Guardar_valida_la_receta_cliente_y_tamanos()
    {
        var hojas = Formato(TitulosA);
        var mala = new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Tarifas", FilasEncabezado: [0])]);

        Assert.Equal("RECETA_INVALIDA", (await Assert.ThrowsAsync<clsValidationFailedException>(() => Guardador(hojas).Handle(Guardar(mala, Cliente)))).Code);
        Assert.Equal("CLIENTE_REQUERIDO", (await Assert.ThrowsAsync<clsValidationFailedException>(() => Guardador(hojas).Handle(Guardar()))).Code);
        Assert.Equal("CLIENTE_NO_ENCONTRADO", (await Assert.ThrowsAsync<clsValidationFailedException>(() => Guardador(hojas).Handle(Guardar(cliente: Guid.NewGuid())))).Code);
        Assert.Equal("NOMBRE_DEMASIADO_LARGO", (await Assert.ThrowsAsync<clsValidationFailedException>(() => Guardador(hojas).Handle(Guardar(cliente: Cliente, nombre: new string('n', 101))))).Code);
        Assert.Equal("RECETA_REQUERIDA", (await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Guardador(hojas).Handle(new clsGuardarRecetaLecturaCommand(new MemoryStream([1]), Cliente, null, null, " ", null)))).Code);
        Assert.Empty(_guardadas);
    }

    [Fact]
    public async Task Al_pasar_de_20_formatos_por_cliente_se_descarta_el_que_lleva_mas_tiempo_sin_usarse()
    {
        var hojas = new[] { Hoja("Nueva", ["Uno|Dos|Tres", "a|b|c"]) };
        var recetaOtra = new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("X", FilasEncabezado: [1])]);
        for (var i = 0; i < clsRecetasLecturaReglas.MaxRecetasPorCliente; i++)
        {
            var g = GuardadaDe(Formato(TitulosA), RecetaA());
            g.Huella = JsonSerializer.Serialize(new[] { $"token{i}" });
            g.UltimoUsoAt = DateTimeOffset.UtcNow.AddDays(-i);
        }
        var masAntigua = _guardadas.OrderBy(g => g.UltimoUsoAt).First();

        await Guardador(hojas).Handle(Guardar(new clsRecetaLectura(clsRecetaLectura.VersionActual, [new clsRecetaHoja("Nueva", FilasEncabezado: [1])]), Cliente));

        Assert.Equal(clsRecetasLecturaReglas.MaxRecetasPorCliente, _guardadas.Count);
        Assert.DoesNotContain(masAntigua, _guardadas);
        _ = recetaOtra;
    }

    // ---------------------------------------------------------------- listar y eliminar

    [Fact]
    public async Task Listar_devuelve_los_formatos_del_cliente_con_su_receta()
    {
        GuardadaDe(Formato(TitulosA), RecetaA());
        var handler = new clsListarRecetasLecturaQueryHandler(_repo.Object, ClientesDeImportacion());

        var lista = await handler.Handle(new clsListarRecetasLecturaQuery(Cliente, null));

        var dto = Assert.Single(lista);
        Assert.Equal(Cliente, dto.ClienteId);
        Assert.Equal("Tarifas", Assert.Single(dto.Receta.Hojas).Hoja);
    }

    [Fact]
    public async Task Eliminar_borra_el_formato_y_lo_audita_o_responde_404()
    {
        var g = GuardadaDe(Formato(TitulosA), RecetaA());
        var handler = new clsEliminarRecetaLecturaCommandHandler(_repo.Object, _auditoria.Object, _usuario.Object, _uow.Object);

        await handler.Handle(new clsEliminarRecetaLecturaCommand(g.Id));

        Assert.Empty(_guardadas);
        _auditoria.Verify(a => a.Registrar(Usuario, "receta_lectura_cliente", g.Id, "eliminar_receta", It.IsAny<string>()), Times.Once);
        await Assert.ThrowsAsync<clsNotFoundException>(() => handler.Handle(new clsEliminarRecetaLecturaCommand(Guid.NewGuid())));
    }
}
