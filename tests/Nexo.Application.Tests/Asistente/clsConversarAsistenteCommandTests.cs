using System.Runtime.CompilerServices;
using System.Text.Json;
using Nexo.Application.CasosDeUso.Asistente;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.Common;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Domain.Enums;
using Moq;

namespace Nexo.Application.Tests.Asistente;

/// <summary>
/// La conversación con el asistente: validaciones, límites, que lo guardado y lo auditado sea lo que ocurrió y que una respuesta con un
/// precio sin origen no se entregue como buena. El modelo es un guion: ninguna prueba llama a un proveedor.
/// </summary>
public class clsConversarAsistenteCommandTests
{
    private static readonly Guid Yo = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Otro = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private delegate IAsyncEnumerable<clsEventoConversacionIa> clsGuion(
        clsPeticionConversacionIa peticion, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, CancellationToken ct);

    private sealed class clsModeloGuionado(clsGuion guion) : IConversacionIaPort
    {
        public Exception? ErrorAlVerificar { get; set; }
        public int Verificaciones { get; private set; }
        public List<clsPeticionConversacionIa> Peticiones { get; } = [];

        public Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default)
        {
            Verificaciones++;
            return ErrorAlVerificar is null ? Task.CompletedTask : Task.FromException(ErrorAlVerificar);
        }

        public IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
            clsPeticionConversacionIa peticion, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutarHerramienta,
            CancellationToken ct = default)
        {
            Peticiones.Add(peticion);
            return guion(peticion, ejecutarHerramienta, ct);
        }
    }

    private sealed class clsHerramientaDePrueba(string nombre, Func<JsonElement, Task<clsResultadoHerramienta>> ejecutar, params RolUsuario[] roles) : IHerramientaAsistente
    {
        public string Nombre => nombre;
        public string Descripcion => "Herramienta de prueba.";
        public string EsquemaEntradaJson => """{"type":"object","properties":{"origen":{"type":"string"}},"additionalProperties":false}""";
        public IReadOnlyCollection<RolUsuario> Roles => roles;
        public Task<clsResultadoHerramienta> EjecutarAsync(JsonElement entrada, CancellationToken ct = default) => ejecutar(entrada);
    }

    private readonly clsRepositorioConversacionesEnMemoria _repo = new();
    private readonly clsRepositorioPreferenciasEnMemoria _preferencias = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly clsLimitesAsistente _limites = new();
    private clsModeloGuionado _modelo = null!;

    public clsConversarAsistenteCommandTests()
    {
        _usuario.SetupGet(u => u.Id).Returns(Yo);
        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.AdminTarifas);
    }

    // ------------------------------------------------------------------ armado

    private static readonly clsResultadoHerramienta PisoPorDefecto =
        clsResultadoHerramienta.Con(new { valorPiso = 2_900_000m, periodo = 202610 }, "SICE-TAC (DWSice), periodo 202610");

    private clsConversarAsistenteCommandHandler Handler(clsGuion guion, params IHerramientaAsistente[] herramientas)
    {
        _modelo = new clsModeloGuionado(guion);
        IHerramientaAsistente[] todas = herramientas.Length > 0
            ? herramientas
            : [new clsHerramientaDePrueba("consultar_piso_sicetac", _ => Task.FromResult(PisoPorDefecto))];
        return new clsConversarAsistenteCommandHandler(
            _modelo, _repo, new clsHerramientasDelAsistente(todas, _usuario.Object), _usuario.Object, _limites, new clsContextoConversacionAsistente(), _preferencias);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> Eventos(params clsEventoConversacionIa[] eventos)
    {
        foreach (var e in eventos) yield return e;
        await Task.CompletedTask;
    }

    private static clsGuion Responde(string texto, string motivo = "end_turn") =>
        (_, _, _) => Eventos(new clsEventoConversacionIa.Texto(texto), new clsEventoConversacionIa.Fin(motivo));

    private static async Task<List<clsEventoAsistente>> Leer(IAsyncEnumerable<clsEventoAsistente> flujo)
    {
        List<clsEventoAsistente> lista = [];
        await foreach (var e in flujo) lista.Add(e);
        return lista;
    }

    private async Task<List<clsEventoAsistente>> Conversar(clsConversarAsistenteCommandHandler h, string mensaje, Guid? conversacionId = null) =>
        await Leer(await h.Handle(new clsConversarAsistenteCommand(conversacionId, mensaje)));

    // ------------------------------------------------------------------ antes de responder

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Un_mensaje_vacio_se_rechaza_sin_crear_nada(string? mensaje)
    {
        var h = Handler(Responde("hola"));

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => h.Handle(new clsConversarAsistenteCommand(null, mensaje)));

        Assert.Equal("MENSAJE_REQUERIDO", ex.Code);
        Assert.Empty(_repo.Conversaciones);
    }

    [Fact]
    public async Task Un_mensaje_mas_largo_que_el_limite_se_rechaza()
    {
        _limites.MaxCaracteresMensaje = 100;
        var h = Handler(Responde("hola"));

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => h.Handle(new clsConversarAsistenteCommand(null, new string('a', 101))));

        Assert.Equal("MENSAJE_DEMASIADO_LARGO", ex.Code);
    }

    [Fact]
    public async Task Una_conversacion_inexistente_es_404_y_una_ajena_es_403_incluso_para_el_Admin()
    {
        var h = Handler(Responde("hola"));
        _repo.Conversaciones.Add(new clsConversacionAsistente { UsuarioId = Otro, Titulo = "de otro" });

        var noExiste = await Assert.ThrowsAsync<clsNotFoundException>(() => h.Handle(new clsConversarAsistenteCommand(Guid.NewGuid(), "hola")));
        Assert.Equal("CONVERSACION_NO_ENCONTRADA", noExiste.Code);

        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.Admin);
        var ajena = await Assert.ThrowsAsync<clsForbiddenException>(() => h.Handle(new clsConversarAsistenteCommand(_repo.Conversaciones[0].Id, "hola")));
        Assert.Equal("CONVERSACION_AJENA", ajena.Code);
        Assert.Empty(_repo.Mensajes);
    }

    [Fact]
    public async Task Al_llegar_al_limite_de_turnos_se_corta_con_429_sin_llamar_al_modelo()
    {
        _limites.MaxTurnos = 2;
        var h = Handler(Responde("ok"));
        var id = (await Conversar(h, "uno")).OfType<clsEventoAsistente.Conversacion>().Single().ConversacionId;
        await Conversar(h, "dos", id);

        var ex = await Assert.ThrowsAsync<clsLimiteExcedidoException>(() => h.Handle(new clsConversarAsistenteCommand(id, "tres")));

        Assert.Equal("ASISTENTE_LIMITE_TURNOS", ex.Code);
        Assert.Equal(2, _modelo.Peticiones.Count);
        Assert.Equal(2, _repo.Conversaciones.Single().Turnos);
    }

    [Theory]
    [InlineData("IA_APAGADA")]
    [InlineData("IA_NO_CONFIGURADA")]
    [InlineData("IA_FUNCION_DESHABILITADA")]
    public async Task Con_la_funcion_apagada_responde_503_antes_de_guardar_nada(string codigo)
    {
        var h = Handler(Responde("hola"));
        _modelo.ErrorAlVerificar = new clsServiceUnavailableException(codigo, "apagada");

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => h.Handle(new clsConversarAsistenteCommand(null, "hola")));

        Assert.Equal(codigo, ex.Code);
        Assert.Empty(_repo.Conversaciones);
        Assert.Empty(_repo.Mensajes);
        Assert.Empty(_modelo.Peticiones);
    }

    [Fact]
    public async Task Con_el_tope_diario_superado_responde_429_antes_de_guardar_nada()
    {
        var h = Handler(Responde("hola"));
        _modelo.ErrorAlVerificar = new clsLimiteExcedidoException("IA_TOPE_DIARIO", "tope", new { });

        var ex = await Assert.ThrowsAsync<clsLimiteExcedidoException>(() => h.Handle(new clsConversarAsistenteCommand(null, "hola")));

        Assert.Equal("IA_TOPE_DIARIO", ex.Code);
        Assert.Empty(_repo.Conversaciones);
        Assert.Empty(_modelo.Peticiones);
    }

    // ------------------------------------------------------------------ una respuesta normal

    [Fact]
    public async Task Una_respuesta_se_transmite_en_orden_y_queda_guardada_completa()
    {
        var h = Handler((_, _, _) => Eventos(
            new clsEventoConversacionIa.Texto("Hola, "), new clsEventoConversacionIa.Texto("¿en qué te ayudo?"), new clsEventoConversacionIa.Fin("end_turn")));

        var eventos = await Conversar(h, "  Hola  ");

        Assert.IsType<clsEventoAsistente.Conversacion>(eventos[0]);
        Assert.Equal(["Hola, ", "¿en qué te ayudo?"], eventos.OfType<clsEventoAsistente.Fragmento>().Select(t => t.Delta));
        var fin = Assert.IsType<clsEventoAsistente.Fin>(eventos[^1]);
        Assert.Equal(clsMensajeAsistente.EstadoCompleto, fin.Estado);

        var conversacion = Assert.Single(_repo.Conversaciones);
        Assert.Equal(Yo, conversacion.UsuarioId);
        Assert.Equal("Hola", conversacion.Titulo);
        Assert.Equal(1, conversacion.Turnos);
        Assert.Equal(["user", "assistant"], _repo.Mensajes.Select(m => m.Rol));
        Assert.Equal("Hola", _repo.Mensajes[0].Contenido);
        Assert.Equal("Hola, ¿en qué te ayudo?", _repo.Mensajes[1].Contenido);
        Assert.Equal(clsMensajeAsistente.EstadoCompleto, _repo.Mensajes[1].Estado);
    }

    [Fact]
    public async Task Al_modelo_le_llegan_las_instrucciones_las_herramientas_del_rol_el_limite_y_el_usuario()
    {
        var h = Handler(Responde("ok"),
            new clsHerramientaDePrueba("para_todos", _ => Task.FromResult(PisoPorDefecto)),
            new clsHerramientaDePrueba("solo_admin", _ => Task.FromResult(PisoPorDefecto), RolUsuario.Admin));

        await Conversar(h, "hola");

        var p = Assert.Single(_modelo.Peticiones);
        Assert.Equal(clsFuncionIa.AsistenteCotizacion, p.Funcion);
        Assert.Equal(clsInstruccionesAsistente.Texto, p.Instrucciones);
        Assert.Equal(["para_todos"], p.Herramientas.Select(t => t.Nombre));
        Assert.Equal(_limites.MaxIteracionesEfectivo, p.MaxIteraciones);
        Assert.Equal(Yo, p.UsuarioId);
        Assert.Equal([new clsMensajeIa("user", "hola")], p.Mensajes);
    }

    [Fact]
    public async Task Una_segunda_pregunta_reutiliza_la_conversacion_y_el_historial_solo_trae_lo_entregado_completo()
    {
        var h = Handler(Responde("Primera respuesta."));
        var id = (await Conversar(h, "primera")).OfType<clsEventoAsistente.Conversacion>().Single().ConversacionId;
        _repo.Mensajes.Add(new clsMensajeAsistente { ConversacionId = id, Orden = 3, Rol = "user", Contenido = "cortada", Estado = clsMensajeAsistente.EstadoCompleto });
        _repo.Mensajes.Add(new clsMensajeAsistente { ConversacionId = id, Orden = 4, Rol = "assistant", Contenido = "a medias", Estado = clsMensajeAsistente.EstadoIncompleto });
        _repo.Mensajes.Add(new clsMensajeAsistente { ConversacionId = id, Orden = 5, Rol = "assistant", Contenido = "aviso de descarte", Estado = clsMensajeAsistente.EstadoDescartado });

        await Conversar(h, "segunda", id);

        var historial = _modelo.Peticiones[1].Mensajes;
        Assert.Equal(
            [new clsMensajeIa("user", "primera"), new clsMensajeIa("assistant", "Primera respuesta."), new clsMensajeIa("user", "cortada"), new clsMensajeIa("user", "segunda")],
            historial);
        Assert.Single(_repo.Conversaciones);
    }

    // ------------------------------------------------------------------ herramientas y auditoría

    [Fact]
    public async Task Cada_herramienta_usada_queda_auditada_con_entrada_salida_fuente_y_resultado_y_sus_eventos_se_transmiten()
    {
        var h = Handler((_, ejecutar, ct) => Eventos2(ejecutar, ct));

        var eventos = await Conversar(h, "¿cuál es el piso Medellín a Bogotá?");

        Assert.Equal(
            [("consultar_piso_sicetac", "ejecutando"), ("consultar_piso_sicetac", "ok")],
            eventos.OfType<clsEventoAsistente.Herramienta>().Select(e => (e.Nombre, e.Estado)));
        Assert.Equal("SICE-TAC (DWSice), periodo 202610", eventos.OfType<clsEventoAsistente.Herramienta>().Last().Fuente);

        var uso = Assert.Single(_repo.Herramientas);
        Assert.Equal("consultar_piso_sicetac", uso.Herramienta);
        Assert.Equal(_repo.Mensajes[1].Id, uso.MensajeId);
        Assert.Equal(_repo.Conversaciones[0].Id, uso.ConversacionId);
        Assert.Equal("""{"origen":"Medellín"}""", uso.Entrada);
        Assert.Contains("2900000", uso.Salida);
        Assert.True(uso.Encontrado);
        Assert.Equal("SICE-TAC (DWSice), periodo 202610", uso.Fuente);
        Assert.Equal(clsUsoHerramientaAsistente.ResultadoOk, uso.Resultado);
        Assert.Equal(clsMensajeAsistente.EstadoCompleto, _repo.Mensajes[1].Estado);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> Eventos2(
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, [EnumeratorCancellation] CancellationToken ct)
    {
        var llamada = new clsLlamadaHerramientaIa("toolu_1", "consultar_piso_sicetac", JsonDocument.Parse("""{"origen":"Medellín"}""").RootElement.Clone());
        yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
        var resultado = await ejecutar(llamada, ct);
        yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, resultado);
        yield return new clsEventoConversacionIa.Texto("El piso SICE-TAC es $2.900.000 (periodo 202610).");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    [Fact]
    public async Task Un_precio_que_viene_de_la_herramienta_se_entrega_y_uno_inventado_se_descarta()
    {
        var citado = Handler((_, ejecutar, ct) => Eventos2(ejecutar, ct));
        var buena = await Conversar(citado, "piso");
        Assert.Equal(clsMensajeAsistente.EstadoCompleto, Assert.IsType<clsEventoAsistente.Fin>(buena[^1]).Estado);

        var inventado = Handler((_, ejecutar, ct) => ConPrecioInventado(ejecutar, ct));
        var mala = await Conversar(inventado, "piso");

        var reemplazo = Assert.Single(mala.OfType<clsEventoAsistente.Reemplazo>());
        Assert.Equal(["$3.100.000"], reemplazo.CifrasSinOrigen);
        Assert.DoesNotContain("Te recomiendo", reemplazo.Texto);
        var fin = Assert.IsType<clsEventoAsistente.Fin>(mala[^1]);
        Assert.Equal(clsMensajeAsistente.EstadoDescartado, fin.Estado);

        var guardado = _repo.Mensajes.Last(m => m.Rol == "assistant");
        Assert.Equal(clsMensajeAsistente.EstadoDescartado, guardado.Estado);
        Assert.Equal(reemplazo.Texto, guardado.Contenido);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> ConPrecioInventado(
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, [EnumeratorCancellation] CancellationToken ct)
    {
        var llamada = new clsLlamadaHerramientaIa("toolu_1", "consultar_piso_sicetac", JsonDocument.Parse("{}").RootElement.Clone());
        yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
        yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, await ejecutar(llamada, ct));
        yield return new clsEventoConversacionIa.Texto("Te recomiendo cotizar a $3.100.000 con un margen del 8 %.");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    [Fact]
    public async Task Una_cifra_de_una_herramienta_de_un_turno_anterior_sigue_siendo_valida()
    {
        var primero = Handler((_, ejecutar, ct) => Eventos2(ejecutar, ct));
        var id = (await Conversar(primero, "piso")).OfType<clsEventoAsistente.Conversacion>().Single().ConversacionId;
        var segundo = Handler(Responde("Como te dije, el piso es $2.900.000."));

        var eventos = await Conversar(segundo, "repítelo", id);

        Assert.Equal(clsMensajeAsistente.EstadoCompleto, Assert.IsType<clsEventoAsistente.Fin>(eventos[^1]).Estado);
    }

    [Fact]
    public async Task Una_herramienta_que_el_rol_no_puede_usar_vuelve_al_modelo_como_error_y_queda_auditada()
    {
        var h = Handler(
            (_, ejecutar, ct) => LlamaA("solo_admin", ejecutar, ct),
            new clsHerramientaDePrueba("solo_admin", _ => Task.FromResult(PisoPorDefecto), RolUsuario.Admin));

        var eventos = await Conversar(h, "usa la de admin");

        Assert.Equal(["ejecutando", "error"], eventos.OfType<clsEventoAsistente.Herramienta>().Select(e => e.Estado));
        var uso = Assert.Single(_repo.Herramientas);
        Assert.Equal("HERRAMIENTA_NO_PERMITIDA", uso.Resultado);
        Assert.Null(uso.Salida);
        Assert.Null(uso.Encontrado);
        Assert.Equal(clsMensajeAsistente.EstadoCompleto, _repo.Mensajes[1].Estado);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> LlamaA(
        string herramienta, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, [EnumeratorCancellation] CancellationToken ct)
    {
        var llamada = new clsLlamadaHerramientaIa("toolu_9", herramienta, JsonDocument.Parse("{}").RootElement.Clone());
        yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
        var resultado = await ejecutar(llamada, ct);
        // El modelo recibe el motivo para explicárselo a la persona; no hay otra vía.
        Assert.True(resultado.EsError);
        Assert.Contains("HERRAMIENTA_NO_PERMITIDA", resultado.Contenido);
        yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, resultado);
        yield return new clsEventoConversacionIa.Texto("No tengo permiso para esa consulta.");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    [Fact]
    public async Task Un_rechazo_del_dominio_dentro_de_la_herramienta_se_explica_y_un_fallo_inesperado_no_filtra_su_detalle()
    {
        var h = Handler(
            (_, ejecutar, ct) => DosLlamadas(ejecutar, ct),
            new clsHerramientaDePrueba("ajena", _ => throw new clsForbiddenException("SOLICITUD_AJENA", "No tiene acceso a esta solicitud.")),
            new clsHerramientaDePrueba("rota", _ => throw new InvalidOperationException("cadena de conexión secreta")));

        await Conversar(h, "prueba");

        Assert.Equal(["SOLICITUD_AJENA", "HERRAMIENTA_ERROR"], _repo.Herramientas.Select(u => u.Resultado));
        Assert.DoesNotContain("secreta", string.Concat(_repo.Herramientas.Select(u => u.Salida)));
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> DosLlamadas(
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var nombre in new[] { "ajena", "rota" })
        {
            var llamada = new clsLlamadaHerramientaIa("t_" + nombre, nombre, JsonDocument.Parse("{}").RootElement.Clone());
            yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
            var r = await ejecutar(llamada, ct);
            Assert.True(r.EsError);
            Assert.DoesNotContain("secreta", r.Contenido);
            yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, r);
        }
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    // ------------------------------------------------------------------ cortes

    [Fact]
    public async Task Si_el_proveedor_falla_a_mitad_se_avisa_con_el_codigo_y_lo_generado_queda_incompleto()
    {
        var h = Handler((_, _, _) => ParteYFalla());

        var eventos = await Conversar(h, "hola");

        var error = Assert.IsType<clsEventoAsistente.Error>(eventos[^1]);
        Assert.Equal("IA_NO_DISPONIBLE", error.Codigo);
        Assert.DoesNotContain(eventos, e => e is clsEventoAsistente.Fin);
        var guardado = _repo.Mensajes[1];
        Assert.Equal(clsMensajeAsistente.EstadoIncompleto, guardado.Estado);
        Assert.Equal("Voy a consultar", guardado.Contenido);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> ParteYFalla()
    {
        yield return new clsEventoConversacionIa.Texto("Voy a consultar");
        await Task.Yield();
        throw new clsServiceUnavailableException("IA_NO_DISPONIBLE", "El proveedor de IA no está disponible en este momento.");
    }

    [Fact]
    public async Task Un_error_inesperado_no_filtra_su_mensaje_al_usuario()
    {
        var h = Handler((_, _, _) => Explota());

        var eventos = await Conversar(h, "hola");

        var error = Assert.IsType<clsEventoAsistente.Error>(eventos[^1]);
        Assert.Equal("ASISTENTE_ERROR", error.Codigo);
        Assert.DoesNotContain("detalle interno", error.Mensaje);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> Explota()
    {
        await Task.Yield();
        throw new InvalidOperationException("detalle interno");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    [Theory]
    [InlineData("refusal", "IA_RECHAZADA")]
    [InlineData("max_tokens", "IA_RESPUESTA_TRUNCADA")]
    [InlineData("max_iteraciones", "ASISTENTE_DEMASIADAS_CONSULTAS")]
    public async Task Los_finales_que_no_son_normales_se_informan_como_error_y_la_respuesta_queda_incompleta(string motivo, string codigo)
    {
        var h = Handler(Responde("Parte de la respuesta.", motivo));

        var eventos = await Conversar(h, "hola");

        Assert.Equal(codigo, Assert.IsType<clsEventoAsistente.Error>(eventos[^1]).Codigo);
        Assert.Equal(clsMensajeAsistente.EstadoIncompleto, _repo.Mensajes[1].Estado);
        Assert.Equal("Parte de la respuesta.", _repo.Mensajes[1].Contenido);
    }

    [Fact]
    public async Task Si_el_cliente_cierra_la_conexion_a_mitad_lo_generado_queda_como_incompleto_y_no_hay_a_quien_avisar()
    {
        using var cancelacion = new CancellationTokenSource();
        var h = Handler((_, _, ct) => HastaCancelar(cancelacion, ct));

        var eventos = new List<clsEventoAsistente>();
        var flujo = await h.Handle(new clsConversarAsistenteCommand(null, "hola"), cancelacion.Token);
        await foreach (var e in flujo.WithCancellation(cancelacion.Token)) eventos.Add(e);

        Assert.DoesNotContain(eventos, e => e is clsEventoAsistente.Error or clsEventoAsistente.Fin);
        Assert.Equal(clsMensajeAsistente.EstadoIncompleto, _repo.Mensajes[1].Estado);
        Assert.Equal("Empiezo", _repo.Mensajes[1].Contenido);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> HastaCancelar(
        CancellationTokenSource cancelacion, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new clsEventoConversacionIa.Texto("Empiezo");
        cancelacion.Cancel();
        await Task.Delay(Timeout.Infinite, ct);
    }

    [Fact]
    public async Task Si_el_consumidor_deja_de_leer_la_respuesta_no_queda_en_curso()
    {
        var h = Handler(Responde("Hola"));

        var flujo = await h.Handle(new clsConversarAsistenteCommand(null, "hola"));
        await using (var e = flujo.GetAsyncEnumerator())
        {
            await e.MoveNextAsync(); // conversacion
            await e.MoveNextAsync(); // primer trozo
        }

        Assert.NotEqual(clsMensajeAsistente.EstadoEnCurso, _repo.Mensajes[1].Estado);
        Assert.Equal(clsMensajeAsistente.EstadoIncompleto, _repo.Mensajes[1].Estado);
    }

    // ------------------------------------------------------------------ consultas

    [Fact]
    public async Task El_dueno_y_el_Admin_leen_la_conversacion_con_sus_herramientas_y_otro_no()
    {
        var h = Handler((_, ejecutar, ct) => Eventos2(ejecutar, ct));
        var id = (await Conversar(h, "piso")).OfType<clsEventoAsistente.Conversacion>().Single().ConversacionId;
        var consulta = new clsObtenerConversacionAsistenteQueryHandler(_repo, _usuario.Object);

        var propia = await consulta.Handle(new clsObtenerConversacionAsistenteQuery(id));
        Assert.Equal(2, propia.Mensajes.Count);
        Assert.Equal("consultar_piso_sicetac", Assert.Single(propia.Herramientas).Herramienta);

        var admin = new Mock<ICurrentUserPort>();
        admin.SetupGet(u => u.Id).Returns(Otro);
        admin.SetupGet(u => u.Rol).Returns(RolUsuario.Admin);
        Assert.NotNull(await new clsObtenerConversacionAsistenteQueryHandler(_repo, admin.Object).Handle(new clsObtenerConversacionAsistenteQuery(id)));

        var tarifas = new Mock<ICurrentUserPort>();
        tarifas.SetupGet(u => u.Id).Returns(Otro);
        tarifas.SetupGet(u => u.Rol).Returns(RolUsuario.AdminTarifas);
        var ex = await Assert.ThrowsAsync<clsForbiddenException>(() =>
            new clsObtenerConversacionAsistenteQueryHandler(_repo, tarifas.Object).Handle(new clsObtenerConversacionAsistenteQuery(id)));
        Assert.Equal("CONVERSACION_AJENA", ex.Code);
    }

    [Fact]
    public async Task El_listado_solo_trae_las_conversaciones_de_quien_pregunta()
    {
        _repo.Conversaciones.Add(new clsConversacionAsistente { UsuarioId = Yo, Titulo = "mía" });
        _repo.Conversaciones.Add(new clsConversacionAsistente { UsuarioId = Otro, Titulo = "ajena" });

        var lista = await new clsListarConversacionesAsistenteQueryHandler(_repo, _usuario.Object).Handle(new clsListarConversacionesAsistenteQuery());

        Assert.Equal(["mía"], lista.Select(c => c.Titulo));
    }
}
