using System.Runtime.CompilerServices;
using System.Text.Json;
using Nexo.Application.CasosDeUso.Asistente;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.CasosDeUso.Asistente.Herramientas;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Domain.Enums;
using Moq;

namespace Nexo.Application.Tests.Asistente;

/// <summary>
/// Los cambios que propone el asistente: el modelo solo propone (nada se escribe), la ejecución exige la confirmación de quien tiene la
/// conversación, las reglas del dominio mandan y sus rechazos se explican, y cada acción queda auditada con su resultado.
/// </summary>
public class clsAccionesAsistenteTests
{
    private static readonly Guid Yo = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Otro = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Cotizacion = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Linea = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Servicio = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private readonly clsRepositorioConversacionesEnMemoria _repo = new();
    private readonly clsRepositorioPreferenciasEnMemoria _preferencias = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto>> _cotizaciones = new();
    private readonly clsContextoConversacionAsistente _contexto = new();
    private readonly clsLimitesAsistente _limites = new();
    private readonly clsRelojPrueba _reloj = new(new DateTimeOffset(2026, 10, 27, 15, 0, 0, TimeSpan.Zero));
    private readonly Guid _conversacionId;
    private readonly Guid _respuestaId;

    private readonly Mock<ICommandHandler<clsAgregarLineaCotizacionCommand, clsCotizacionDto>> _agregarFlete = new();
    private readonly Mock<ICommandHandler<clsEditarLineaCotizacionCommand, clsCotizacionDto>> _editarFlete = new();
    private readonly Mock<ICommandHandler<clsAgregarConexosCotizacionCommand, clsCotizacionDto>> _agregarConexos = new();
    private readonly Mock<ICommandHandler<clsRechazarLineaCotizacionCommand, clsLineaRechazoDto>> _rechazar = new();
    private readonly Mock<ICommandHandler<clsCambiarVehiculoLineaCommand, clsLineaVehiculoDto>> _cambiarVehiculo = new();

    private sealed class clsRelojPrueba(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    public clsAccionesAsistenteTests()
    {
        _usuario.SetupGet(u => u.Id).Returns(Yo);
        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.AdminTarifas);
        _cotizaciones.Setup(h => h.Handle(It.IsAny<clsObtenerCotizacionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsCotizacionDto(Cotizacion, Guid.NewGuid(), EstadoCotizacion.Borrador, ResultadoCotizacion.SinDefinir, 0, null, [], []));

        // Una conversación con una respuesta en curso, que es donde se propone.
        var conversacion = _repo.CrearAsync(Yo, "titulo").Result;
        _conversacionId = conversacion.Id;
        _respuestaId = _repo.IniciarTurnoAsync(conversacion.Id, "Pon el flete de la línea en $4.500.000").Result;
        _contexto.ConversacionId = _conversacionId;
        _contexto.MensajeId = _respuestaId;
    }

    // ------------------------------------------------------------------ armado

    private IHerramientaAsistente[] Herramientas() =>
    [
        new clsProponerAgregarFleteHerramienta(_repo, _contexto, _usuario.Object, _cotizaciones.Object, _limites, _reloj),
        new clsProponerEditarFleteHerramienta(_repo, _contexto, _usuario.Object, _cotizaciones.Object, _limites, _reloj),
        new clsProponerAgregarConexosHerramienta(_repo, _contexto, _usuario.Object, _cotizaciones.Object, _limites, _reloj),
        new clsProponerRechazarLineaHerramienta(_repo, _contexto, _usuario.Object, _cotizaciones.Object, _limites, _reloj),
        new clsProponerCambiarVehiculoHerramienta(_repo, _contexto, _usuario.Object, _cotizaciones.Object, _limites, _reloj),
    ];

    private clsHerramientasDelAsistente Catalogo() => new(Herramientas(), _usuario.Object);

    private static JsonElement Entrada(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private Task<clsResultadoHerramienta> Proponer(string herramienta, string json) => Catalogo().EjecutarAsync(herramienta, Entrada(json));

    private clsConfirmarAccionAsistenteCommandHandler Confirmador() => new(
        _repo, _usuario.Object, _reloj, _agregarFlete.Object, _editarFlete.Object, _agregarConexos.Object, _rechazar.Object, _cambiarVehiculo.Object, GuardarPreferencia());

    private clsCancelarAccionAsistenteCommandHandler Cancelador() => new(_repo, _usuario.Object, _reloj);

    private clsGuardarPreferenciaAsistenteCommandHandler GuardarPreferencia() => new(_preferencias, _usuario.Object, _limites);

    private void ValorPermitido(string texto) =>
        _repo.Mensajes.First(m => m.Rol == clsMensajeAsistente.RolUsuario).Contenido = texto;

    private async Task<clsAccionAsistente> ProponerYObtener(string herramienta, string json)
    {
        var r = await Proponer(herramienta, json);
        return _repo.Acciones.Single(a => a.Id == ((clsAccionPropuestaDto)r.Valor!).AccionId);
    }

    private static string Fleten(decimal valor = 4_500_000m) =>
        $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","valorFlete":{{valor}},"fuente":"Manual"}""";

    // ------------------------------------------------------------------ proponer: el modelo solo propone

    [Fact]
    public async Task Proponer_un_flete_deja_una_accion_pendiente_con_parametros_fijos_y_no_ejecuta_nada()
    {
        var r = await Proponer("proponer_agregar_flete", Fleten());

        Assert.True(r.Encontrado);
        Assert.Contains("sin aplicar", r.Fuente);
        Assert.Contains("Nada se ha modificado", r.Nota);
        var propuesta = Assert.IsType<clsAccionPropuestaDto>(r.Valor);
        Assert.Equal(clsAccionAsistente.EstadoPendiente, propuesta.Estado);
        Assert.Equal(_reloj.Ahora + TimeSpan.FromMinutes(30), propuesta.VenceAt);

        var accion = Assert.Single(_repo.Acciones);
        Assert.Equal((_conversacionId, _respuestaId, Yo), (accion.ConversacionId, accion.MensajeId, accion.UsuarioId));
        Assert.Equal(clsAccionAsistente.TipoAgregarFlete, accion.Tipo);
        Assert.Contains("$4.500.000", accion.Resumen);
        var p = JsonDocument.Parse(accion.Parametros).RootElement;
        Assert.Equal(4_500_000m, p.GetProperty("valorFlete").GetDecimal());
        Assert.Equal("Manual", p.GetProperty("fuente").GetString());

        _agregarFlete.VerifyNoOtherCalls();
        _editarFlete.VerifyNoOtherCalls();
        _agregarConexos.VerifyNoOtherCalls();
        _rechazar.VerifyNoOtherCalls();
        _cambiarVehiculo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cada_tipo_de_propuesta_queda_guardado_con_su_tipo()
    {
        ValorPermitido("Pon el flete de la línea en $4.500.000 y el conexo en 3 unidades");
        await Proponer("proponer_editar_flete", $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","valorFlete":4500000}""");
        await Proponer("proponer_agregar_conexos", $$"""{"cotizacionId":"{{Cotizacion}}","items":[{"lineaId":"{{Linea}}","servicioConexoId":"{{Servicio}}","cantidad":3}]}""");
        await Proponer("proponer_rechazar_linea", $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","motivo":"Sin tarifa vigente"}""");

        Assert.Equal(
            [clsAccionAsistente.TipoEditarFlete, clsAccionAsistente.TipoAgregarConexos, clsAccionAsistente.TipoRechazarLinea],
            _repo.Acciones.Select(a => a.Tipo));
    }

    [Fact]
    public async Task Los_parametros_guardados_son_los_validados_no_lo_que_mando_el_modelo()
    {
        var r = await ProponerYObtener("proponer_rechazar_linea",
            $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","motivo":"  Sin tarifa  ","extra":"ignorado"}""");

        var p = JsonDocument.Parse(r.Parametros).RootElement;
        Assert.False(p.TryGetProperty("extra", out _));
        Assert.Equal("Sin tarifa", p.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task Un_valor_que_no_salio_de_una_consulta_ni_de_la_persona_no_se_propone()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Proponer("proponer_agregar_flete", Fleten(9_999_999m)));

        Assert.Equal("VALOR_SIN_ORIGEN", ex.Code);
        Assert.Contains("no lo calcules", ex.Message);
        Assert.Empty(_repo.Acciones);
    }

    [Fact]
    public async Task Un_valor_que_salio_de_una_herramienta_de_esta_conversacion_si_se_propone()
    {
        _repo.Herramientas.Add(new clsUsoHerramientaAsistente
        {
            ConversacionId = _conversacionId, MensajeId = _respuestaId, Herramienta = "consultar_propuesta_linea", Entrada = "{}",
            Salida = """{"encontrado":true,"valor":{"valorPropuesto":3720000}}""", Resultado = "ok",
        });

        var r = await Proponer("proponer_agregar_flete", Fleten(3_720_000m));

        Assert.True(r.Encontrado);
        Assert.Single(_repo.Acciones);
    }

    [Fact]
    public async Task Un_valor_de_otra_conversacion_no_sirve_de_origen()
    {
        var ajena = await _repo.CrearAsync(Yo, "otra");
        _repo.Herramientas.Add(new clsUsoHerramientaAsistente
        {
            ConversacionId = ajena.Id, MensajeId = Guid.NewGuid(), Herramienta = "x", Entrada = "{}", Salida = """{"valor":3720000}""", Resultado = "ok",
        });

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Proponer("proponer_agregar_flete", Fleten(3_720_000m)));

        Assert.Equal("VALOR_SIN_ORIGEN", ex.Code);
    }

    [Fact]
    public async Task Fuera_de_una_conversacion_no_se_puede_proponer()
    {
        _contexto.ConversacionId = null;
        _contexto.MensajeId = null;

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Proponer("proponer_rechazar_linea",
            $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","motivo":"x"}"""));

        Assert.Equal("ASISTENTE_SIN_CONVERSACION", ex.Code);
    }

    [Fact]
    public async Task Una_cotizacion_inexistente_es_un_error_para_el_modelo_y_no_deja_nada_pendiente()
    {
        _cotizaciones.Setup(h => h.Handle(It.IsAny<clsObtenerCotizacionQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada."));

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Proponer("proponer_agregar_flete", Fleten()));

        Assert.Equal("COTIZACION_NO_ENCONTRADA", ex.Code);
        Assert.Empty(_repo.Acciones);
    }

    [Fact]
    public async Task No_se_pueden_proponer_mas_cambios_por_respuesta_que_el_limite()
    {
        _limites.MaxAccionesPorRespuesta = 2;
        var rechazar = $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","motivo":"x"}""";
        await Proponer("proponer_rechazar_linea", rechazar);
        await Proponer("proponer_rechazar_linea", rechazar);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Proponer("proponer_rechazar_linea", rechazar));

        Assert.Equal("DEMASIADAS_ACCIONES", ex.Code);
        Assert.Equal(2, _repo.Acciones.Count);
    }

    [Theory]
    [InlineData("proponer_agregar_flete", """{"cotizacionId":"no-es-guid","lineaSolicitudId":"bbbbbbbb-0000-0000-0000-000000000001","valorFlete":1,"fuente":"Manual"}""")]
    [InlineData("proponer_agregar_flete", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","lineaSolicitudId":"bbbbbbbb-0000-0000-0000-000000000001","valorFlete":1,"fuente":"Inventada"}""")]
    [InlineData("proponer_agregar_flete", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","lineaSolicitudId":"bbbbbbbb-0000-0000-0000-000000000001","fuente":"Manual"}""")]
    [InlineData("proponer_agregar_flete", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","lineaSolicitudId":"bbbbbbbb-0000-0000-0000-000000000001","valorFlete":-5,"fuente":"Manual"}""")]
    [InlineData("proponer_editar_flete", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","lineaSolicitudId":"bbbbbbbb-0000-0000-0000-000000000001"}""")]
    [InlineData("proponer_agregar_conexos", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","items":[]}""")]
    [InlineData("proponer_agregar_conexos", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","items":[{"lineaId":"bbbbbbbb-0000-0000-0000-000000000001"}]}""")]
    [InlineData("proponer_rechazar_linea", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","lineaSolicitudId":"bbbbbbbb-0000-0000-0000-000000000001"}""")]
    [InlineData("proponer_cambiar_vehiculo", """{"cotizacionId":"aaaaaaaa-0000-0000-0000-000000000001","lineaSolicitudId":"bbbbbbbb-0000-0000-0000-000000000001"}""")]
    public async Task Una_propuesta_mal_formada_se_rechaza_sin_dejar_nada(string herramienta, string json)
    {
        await Assert.ThrowsAsync<clsValidationFailedException>(() => Proponer(herramienta, json));

        Assert.Empty(_repo.Acciones);
    }

    [Theory]
    [InlineData(RolUsuario.Comercial)]
    [InlineData(RolUsuario.AdminBiblioteca)]
    [InlineData(RolUsuario.ServiciosLogisticos)]
    [InlineData(RolUsuario.LiderProceso)]
    public async Task Solo_AdminTarifas_y_Admin_tienen_las_herramientas_de_cambio(RolUsuario rol)
    {
        _usuario.SetupGet(u => u.Rol).Returns(rol);

        Assert.DoesNotContain(Catalogo().Disponibles(), d => d.Nombre.StartsWith("proponer_"));
        var ex = await Assert.ThrowsAsync<clsForbiddenException>(() => Proponer("proponer_agregar_flete", Fleten()));
        Assert.Equal("HERRAMIENTA_NO_PERMITIDA", ex.Code);
    }

    [Theory]
    [InlineData(RolUsuario.AdminTarifas)]
    [InlineData(RolUsuario.Admin)]
    public void AdminTarifas_y_Admin_ven_las_cinco_herramientas_de_cambio(RolUsuario rol)
    {
        _usuario.SetupGet(u => u.Rol).Returns(rol);

        Assert.Equal(
            ["proponer_agregar_conexos", "proponer_agregar_flete", "proponer_cambiar_vehiculo", "proponer_editar_flete", "proponer_rechazar_linea"],
            Catalogo().Disponibles().Select(d => d.Nombre));
    }

    // ------------------------------------------------------------------ confirmar: se aplica con los casos de uso de siempre

    [Fact]
    public async Task Confirmar_aplica_el_flete_con_los_parametros_fijos_y_deja_la_accion_ejecutada_y_la_conversacion_al_tanto()
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten(4_500_000m));

        var resultado = await Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id));

        _agregarFlete.Verify(h => h.Handle(
            new clsAgregarLineaCotizacionCommand(Cotizacion, Linea, 4_500_000m, null, "Manual"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(clsAccionAsistente.EstadoEjecutada, resultado.Estado);
        Assert.Equal("ok", resultado.ResultadoCodigo);
        Assert.NotNull(resultado.ResueltaAt);
        Assert.StartsWith("Listo, apliqué el cambio", _repo.Mensajes.Last().Contenido);
        Assert.Equal(clsMensajeAsistente.RolAsistente, _repo.Mensajes.Last().Rol);
    }

    [Fact]
    public async Task Editar_un_flete_nunca_activa_la_confirmacion_sin_piso()
    {
        var accion = await ProponerYObtener("proponer_editar_flete",
            $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","valorFlete":4500000,"valorRetornoContenedor":4500000}""");

        await Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id));

        _editarFlete.Verify(h => h.Handle(
            new clsEditarLineaCotizacionCommand(Cotizacion, Linea, 4_500_000m, 4_500_000m, false), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirmar_conexos_rechazo_y_vehiculo_llama_a_cada_caso_de_uso_con_lo_propuesto()
    {
        ValorPermitido("Agrega el conexo con 12 unidades a $50.000");
        var conexos = await ProponerYObtener("proponer_agregar_conexos",
            $$"""{"cotizacionId":"{{Cotizacion}}","items":[{"lineaId":"{{Linea}}","servicioConexoId":"{{Servicio}}","cantidad":12,"valorAplicado":50000}]}""");
        var rechazo = await ProponerYObtener("proponer_rechazar_linea",
            $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","motivo":"Sin tarifa vigente"}""");
        var vehiculo = await ProponerYObtener("proponer_cambiar_vehiculo",
            $$"""{"cotizacionId":"{{Cotizacion}}","lineaSolicitudId":"{{Linea}}","tipoVehiculo":"C3","carroceria":"FURGON"}""");

        await Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(conexos.Id));
        await Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(rechazo.Id));
        await Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(vehiculo.Id));

        _agregarConexos.Verify(h => h.Handle(
            It.Is<clsAgregarConexosCotizacionCommand>(c => c.CotizacionId == Cotizacion && c.Items.Count == 1
                && c.Items[0] == new clsConexoItem(Linea, Servicio, 12m, 50_000m, "individual")), It.IsAny<CancellationToken>()), Times.Once);
        _rechazar.Verify(h => h.Handle(
            new clsRechazarLineaCotizacionCommand(Cotizacion, Linea, "Sin tarifa vigente"), It.IsAny<CancellationToken>()), Times.Once);
        _cambiarVehiculo.Verify(h => h.Handle(
            new clsCambiarVehiculoLineaCommand(Cotizacion, Linea, null, "C3", "FURGON"), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------ confirmar: reglas del dominio y reglas de la confirmación

    [Theory]
    [InlineData("FLETE_SOLO_SICETAC", "El flete de esta ruta lo gobierna una regla solo SICE-TAC.")]
    [InlineData("COTIZACION_NO_EDITABLE", "La cotización ya se entregó.")]
    [InlineData("FLETE_BAJO_PISO_SICETAC", "El valor está por debajo del piso SICE-TAC.")]
    public async Task Un_rechazo_del_dominio_se_responde_igual_queda_fallida_y_la_conversacion_lo_explica(string codigo, string motivo)
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten());
        _agregarFlete.Setup(h => h.Handle(It.IsAny<clsAgregarLineaCotizacionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException(codigo, motivo));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id)));

        Assert.Equal(codigo, ex.Code);
        var guardada = _repo.Acciones.Single();
        Assert.Equal(clsAccionAsistente.EstadoFallida, guardada.Estado);
        Assert.Equal(codigo, guardada.ResultadoCodigo);
        Assert.Equal(motivo, guardada.ResultadoMensaje);
        var aviso = _repo.Mensajes.Last();
        Assert.Contains(motivo, aviso.Contenido);
        Assert.Contains(codigo, aviso.Contenido);
        Assert.Contains("No se modificó nada", aviso.Contenido);
        // No se vuelve a intentar por otro camino: el caso de uso se llamó una sola vez.
        _agregarFlete.Verify(h => h.Handle(It.IsAny<clsAgregarLineaCotizacionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _editarFlete.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_fallo_inesperado_queda_fallido_sin_filtrar_su_detalle_a_la_conversacion()
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten());
        _agregarFlete.Setup(h => h.Handle(It.IsAny<clsAgregarLineaCotizacionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cadena de conexión secreta"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id)));

        var guardada = _repo.Acciones.Single();
        Assert.Equal(("fallida", "ERROR_INTERNO"), (guardada.Estado, guardada.ResultadoCodigo));
        Assert.DoesNotContain("secreta", _repo.Mensajes.Last().Contenido + guardada.ResultadoMensaje);
    }

    [Fact]
    public async Task Una_accion_se_confirma_una_sola_vez()
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten());
        await Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id)));

        Assert.Equal("ACCION_YA_RESUELTA", ex.Code);
        _agregarFlete.Verify(h => h.Handle(It.IsAny<clsAgregarLineaCotizacionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_accion_vencida_no_se_ejecuta_y_queda_vencida()
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten());
        _reloj.Ahora = _reloj.Ahora.AddMinutes(31);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id)));

        Assert.Equal("ACCION_VENCIDA", ex.Code);
        Assert.Equal(clsAccionAsistente.EstadoVencida, _repo.Acciones.Single().Estado);
        _agregarFlete.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Solo_quien_tiene_la_conversacion_confirma_o_cancela_y_una_inexistente_es_404()
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten());
        var otro = new Mock<ICurrentUserPort>();
        otro.SetupGet(u => u.Id).Returns(Otro);
        otro.SetupGet(u => u.Rol).Returns(RolUsuario.Admin);

        var confirmar = await Assert.ThrowsAsync<clsForbiddenException>(() =>
            new clsConfirmarAccionAsistenteCommandHandler(_repo, otro.Object, _reloj, _agregarFlete.Object, _editarFlete.Object, _agregarConexos.Object, _rechazar.Object, _cambiarVehiculo.Object, GuardarPreferencia())
                .Handle(new clsConfirmarAccionAsistenteCommand(accion.Id)));
        var cancelar = await Assert.ThrowsAsync<clsForbiddenException>(() =>
            new clsCancelarAccionAsistenteCommandHandler(_repo, otro.Object, _reloj).Handle(new clsCancelarAccionAsistenteCommand(accion.Id)));
        var noExiste = await Assert.ThrowsAsync<clsNotFoundException>(() => Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(Guid.NewGuid())));

        Assert.Equal(("ACCION_AJENA", "ACCION_AJENA", "ACCION_NO_ENCONTRADA"), (confirmar.Code, cancelar.Code, noExiste.Code));
        Assert.Equal(clsAccionAsistente.EstadoPendiente, _repo.Acciones.Single().Estado);
        _agregarFlete.VerifyNoOtherCalls();
    }

    // ------------------------------------------------------------------ cancelar

    [Fact]
    public async Task Cancelar_descarta_la_accion_sin_aplicarla_y_ya_no_se_puede_confirmar()
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten());

        var cancelada = await Cancelador().Handle(new clsCancelarAccionAsistenteCommand(accion.Id));

        Assert.Equal(clsAccionAsistente.EstadoCancelada, cancelada.Estado);
        Assert.StartsWith("Se canceló el cambio propuesto", _repo.Mensajes.Last().Contenido);
        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id)));
        Assert.Equal("ACCION_YA_RESUELTA", ex.Code);
        _agregarFlete.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task No_se_cancela_lo_que_ya_se_aplico()
    {
        var accion = await ProponerYObtener("proponer_agregar_flete", Fleten());
        await Confirmador().Handle(new clsConfirmarAccionAsistenteCommand(accion.Id));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Cancelador().Handle(new clsCancelarAccionAsistenteCommand(accion.Id)));

        Assert.Equal("ACCION_YA_RESUELTA", ex.Code);
        Assert.Equal(clsAccionAsistente.EstadoEjecutada, _repo.Acciones.Single().Estado);
    }

    // ------------------------------------------------------------------ la conversación avisa de la propuesta

    private static async IAsyncEnumerable<clsEventoConversacionIa> ProponeYDice(
        string herramienta, string entrada, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, [EnumeratorCancellation] CancellationToken ct)
    {
        var llamada = new clsLlamadaHerramientaIa("toolu_1", herramienta, Entrada(entrada));
        yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
        yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, await ejecutar(llamada, ct));
        yield return new clsEventoConversacionIa.Texto("Propuse el cambio; confírmalo en pantalla.");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    [Fact]
    public async Task La_conversacion_avisa_al_cliente_de_la_accion_propuesta_con_su_resumen_y_parametros()
    {
        var modelo = new Mock<IConversacionIaPort>();
        modelo.Setup(m => m.ConversarAsync(It.IsAny<clsPeticionConversacionIa>(), It.IsAny<Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>>>(), It.IsAny<CancellationToken>()))
            .Returns((clsPeticionConversacionIa _, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, CancellationToken ct) =>
                ProponeYDice("proponer_agregar_flete", Fleten(4_500_000m), ejecutar, ct));
        var contexto = new clsContextoConversacionAsistente();
        var catalogo = new clsHerramientasDelAsistente(
        [
            new clsProponerAgregarFleteHerramienta(_repo, contexto, _usuario.Object, _cotizaciones.Object, _limites, _reloj),
        ], _usuario.Object);
        var handler = new clsConversarAsistenteCommandHandler(modelo.Object, _repo, catalogo, _usuario.Object, _limites, contexto, _preferencias);

        List<clsEventoAsistente> eventos = [];
        await foreach (var e in await handler.Handle(new clsConversarAsistenteCommand(null, "Pon el flete en $4.500.000"))) eventos.Add(e);

        var accion = Assert.Single(eventos.OfType<clsEventoAsistente.Accion>()).Propuesta;
        Assert.Equal(clsAccionAsistente.TipoAgregarFlete, accion.Tipo);
        Assert.Equal(clsAccionAsistente.EstadoPendiente, accion.Estado);
        Assert.Contains("$4.500.000", accion.Resumen);
        Assert.Equal(4_500_000m, accion.Parametros.GetProperty("valorFlete").GetDecimal());
        Assert.Equal(Yo, _repo.Acciones.Single().UsuarioId);
        // el aviso llega justo después de que la herramienta termina, antes del texto de la respuesta
        var nombres = eventos.Select(e => e.GetType().Name).ToList();
        Assert.True(nombres.IndexOf(nameof(clsEventoAsistente.Accion)) < nombres.IndexOf(nameof(clsEventoAsistente.Fragmento)));
        Assert.Equal(clsMensajeAsistente.EstadoCompleto, Assert.IsType<clsEventoAsistente.Fin>(eventos[^1]).Estado);
    }
}
