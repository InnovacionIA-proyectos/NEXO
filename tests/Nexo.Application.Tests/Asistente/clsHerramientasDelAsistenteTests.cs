using System.Text.Json;
using Nexo.Application.CasosDeUso.Asistente;
using Nexo.Application.CasosDeUso.Asistente.Herramientas;
using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza;
using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Asistente;

/// <summary>
/// S6-HU-36.1/#371: herramientas de lectura del asistente. Cubre los tres criterios de la issue: cada herramienta devuelve valor y
/// fuente (o dice que no hay), el rol del usuario limita lo que ve y las reglas de acceso de cada consulta se mantienen.
/// </summary>
public class clsHerramientasDelAsistenteTests
{
    private static readonly Guid UsuarioId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtroUsuarioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SolicitudId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LineaId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<IPropuestaTarifaPort> _propuestas = new();
    private readonly Mock<IQueryHandler<clsBuscarSapPlazaQuery, clsSapHistoricoResultDto>> _plazaSap = new();
    private readonly Mock<IQueryHandler<clsBuscarSapHistoricoQuery, clsSapHistoricoResultDto>> _historicoSap = new();
    private readonly Mock<IQueryHandler<clsListarTarifasPlazaQuery, IReadOnlyList<clsTarifaPlazaDto>>> _plazaNexo = new();
    private readonly Mock<IQueryHandler<clsProponerPropuestaTarifaQuery, clsPropuestaSolicitudDto>> _propuestaSolicitud = new();
    private readonly Mock<IQueryHandler<clsListarServiciosConexosQuery, clsPaginaServiciosDto>> _conexos = new();
    private readonly Mock<IQueryHandler<clsBuscarCiudadesQuery, IReadOnlyList<clsCiudadBusquedaDto>>> _ciudades = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();

    public clsHerramientasDelAsistenteTests()
    {
        _usuario.SetupGet(u => u.Id).Returns(UsuarioId);
        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.Admin);
        ConDatos();
    }

    // ------------------------------------------------------------------ armado

    private IHerramientaAsistente[] Herramientas() =>
    [
        new clsConsultarPisoSicetacHerramienta(_propuestas.Object),
        new clsConsultarTarifaPlazaSapHerramienta(_plazaSap.Object),
        new clsConsultarTarifaPlazaNexoHerramienta(_plazaNexo.Object),
        new clsConsultarHistoricoSapHerramienta(_historicoSap.Object),
        new clsConsultarAprovisionamientoHerramienta(_propuestas.Object),
        new clsConsultarPropuestaLineaHerramienta(_propuestaSolicitud.Object, _solicitudes.Object, _usuario.Object),
        new clsConsultarServiciosConexosHerramienta(_conexos.Object),
        new clsBuscarCiudadesDaneHerramienta(_ciudades.Object),
    ];

    private clsHerramientasDelAsistente Catalogo() => new(Herramientas(), _usuario.Object);

    private void ComoRol(RolUsuario? rol) => _usuario.SetupGet(u => u.Rol).Returns(rol);

    private static JsonElement Entrada(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private Task<clsResultadoHerramienta> Ejecutar(string nombre, string json) => Catalogo().EjecutarAsync(nombre, Entrada(json));

    private static clsSapTarifaHistoricoItemDto FilaSap(decimal valor = 3_500_000m, DateTime? vigenciaAjustada = null) => new(
        Id: 1, LineaNegocio: 1, LineaNegocioNombre: "Nacional", Origen: "05001000", OrigenNombre: "Medellín", Destino: "11001000",
        DestinoNombre: "Bogotá", Valor: valor, ValorTexto: valor.ToString("0"), Vigencia: new DateTime(2026, 1, 1), ClaseContenedor: null,
        TamanoContenedor: null, RangoPesoInf: 0, RangoPesoSup: 34000, TipoVehiculo: "10", TipoVehiculoNombre: "3S3 · Tractomula",
        TipoCarroseria: "F", TipoCarroseriaNombre: "FURGON", PesoLimite: null, ClaseViaje: 1, ClaseViajeNombre: "Cupo", Devolucion: false,
        Consolidado: null, Exclusivo: null, DetallesTarifa: "tarifa pactada", FechaCreacion: new DateTime(2026, 1, 1),
        FechaModificacion: new DateTime(2026, 1, 2), VigenciaAjustada: vigenciaAjustada);

    private static clsCandidatoTarifaDto Candidato(string fuente = "SICE-TAC", decimal valor = 3_000_000m) =>
        new(fuente, valor, "detalle", "criterio", 1);

    private static clsPropuestaLineaDto Linea(string? motivoPendiente = null) => new(
        LineaId, "Medellín → Bogotá", "3S3", 30000m, Candidato(), null, null, 3_000_000m, "SICE-TAC", "Gobierna el piso SICE-TAC.",
        MotivoPendiente: motivoPendiente);

    private static clsSolicitud Solicitud(Guid creadoPor) => new() { Id = SolicitudId, CreadoPor = creadoPor };

    /// <summary>Todas las fuentes devuelven un dato.</summary>
    private void ConDatos()
    {
        _propuestas.Setup(p => p.PisoSicetacVigenteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TipologiaCarga?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPisoSicetacVigenteDto(202610, 2_900_000m, 12_000m, 8m, "3S3", "General", "Tractocamión", "CARGADO"));
        _propuestas.Setup(p => p.EstimarAprovisionamientoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(Candidato());
        _propuestas.Setup(p => p.MotivoSicetacNoUsadoAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        _plazaSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSapHistoricoResultDto(1, [FilaSap()]));
        _historicoSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapHistoricoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSapHistoricoResultDto(1, [FilaSap()]));
        _plazaNexo.Setup(h => h.Handle(It.IsAny<clsListarTarifasPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([TarifaNexo("Medellín", "Bogotá")]);

        _solicitudes.Setup(s => s.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(Solicitud(UsuarioId));
        _propuestaSolicitud.Setup(h => h.Handle(It.IsAny<clsProponerPropuestaTarifaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPropuestaSolicitudDto(SolicitudId, [Linea()], []));

        _conexos.Setup(h => h.Handle(It.IsAny<clsListarServiciosConexosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPaginaServiciosDto([Conexo()], 1));
        _ciudades.Setup(h => h.Handle(It.IsAny<clsBuscarCiudadesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new clsCiudadBusquedaDto(1, "05001000", "Medellín", "Antioquia", null, "Zona 1")]);
    }

    /// <summary>Ninguna fuente tiene dato.</summary>
    private void SinDatos()
    {
        _propuestas.Setup(p => p.PisoSicetacVigenteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TipologiaCarga?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsPisoSicetacVigenteDto?)null);
        _propuestas.Setup(p => p.EstimarAprovisionamientoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync((clsCandidatoTarifaDto?)null);

        _plazaSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSapHistoricoResultDto(0, []));
        _historicoSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapHistoricoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSapHistoricoResultDto(0, []));
        _plazaNexo.Setup(h => h.Handle(It.IsAny<clsListarTarifasPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _conexos.Setup(h => h.Handle(It.IsAny<clsListarServiciosConexosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPaginaServiciosDto([], 0));
        _ciudades.Setup(h => h.Handle(It.IsAny<clsBuscarCiudadesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private static clsTarifaPlazaDto TarifaNexo(string origen, string destino, string? tipoVehiculo = "3S3") => new(
        Guid.NewGuid(), "Nacional", "Cupo", 1, origen, "Zona 1", 2, destino, "Zona 2", null, null, null, tipoVehiculo, 3_200_000m,
        new DateOnly(2026, 6, 1), "detalle", DateTimeOffset.UtcNow);

    private static clsServicioConexoDto Conexo() => new(
        Guid.NewGuid(), "CON-1", "Escolta", "Medellín", "Bogotá", "", "", "", 450_000m, "viaje", new DateOnly(2026, 6, 1), "Seguridad", true);

    // ------------------------------------------------------------------ catálogo y roles

    public static IEnumerable<object[]> HerramientasPorRol()
    {
        string[] todas = ["buscar_ciudades_dane", "consultar_historico_sap", "consultar_piso_sicetac", "consultar_servicios_conexos", "consultar_tarifa_plaza_sap"];
        string[] Con(params string[] extra) => [.. todas, .. extra];

        yield return [RolUsuario.Admin, Con("consultar_aprovisionamiento", "consultar_propuesta_linea", "consultar_tarifa_plaza_nexo")];
        yield return [RolUsuario.Comercial, Con("consultar_aprovisionamiento", "consultar_propuesta_linea")];
        yield return [RolUsuario.AdminTarifas, Con("consultar_propuesta_linea", "consultar_tarifa_plaza_nexo")];
        yield return [RolUsuario.ServiciosLogisticos, Con("consultar_propuesta_linea")];
        yield return [RolUsuario.LiderProceso, Con("consultar_propuesta_linea")];
        yield return [RolUsuario.AdminBiblioteca, Con("consultar_tarifa_plaza_nexo")];
    }

    [Theory]
    [MemberData(nameof(HerramientasPorRol))]
    public void Cada_rol_ve_solo_las_herramientas_que_ya_puede_usar_en_los_endpoints(RolUsuario rol, string[] esperadas)
    {
        ComoRol(rol);

        var nombres = Catalogo().Disponibles().Select(d => d.Nombre).Order().ToArray();

        Assert.Equal(esperadas.Order().ToArray(), nombres);
    }

    [Fact]
    public void Sin_rol_no_se_ofrece_ninguna_herramienta()
    {
        ComoRol(null);

        Assert.Empty(Catalogo().Disponibles());
    }

    [Fact]
    public async Task Pedir_una_herramienta_que_el_rol_no_puede_usar_se_rechaza_sin_ejecutarla()
    {
        ComoRol(RolUsuario.Comercial);

        var ex = await Assert.ThrowsAsync<clsForbiddenException>(() =>
            Ejecutar("consultar_tarifa_plaza_nexo", """{"q":"Medellin"}"""));

        Assert.Equal("HERRAMIENTA_NO_PERMITIDA", ex.Code);
        _plazaNexo.Verify(h => h.Handle(It.IsAny<clsListarTarifasPlazaQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_rol_tampoco_se_puede_ejecutar_nada()
    {
        ComoRol(null);

        var ex = await Assert.ThrowsAsync<clsForbiddenException>(() => Ejecutar("buscar_ciudades_dane", """{"q":"Med"}"""));

        Assert.Equal("HERRAMIENTA_NO_PERMITIDA", ex.Code);
    }

    [Theory]
    [InlineData("no_existe")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Una_herramienta_desconocida_es_un_error_de_validacion(string nombre)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Ejecutar(nombre, "{}"));

        Assert.Equal("HERRAMIENTA_DESCONOCIDA", ex.Code);
    }

    [Fact]
    public void Los_nombres_son_unicos_estables_y_cada_esquema_de_entrada_es_un_objeto_JSON_valido()
    {
        var herramientas = Herramientas();

        Assert.Equal(herramientas.Length, herramientas.Select(h => h.Nombre).Distinct().Count());
        foreach (var h in herramientas)
        {
            Assert.Matches("^[a-z]+(_[a-z]+)+$", h.Nombre);
            Assert.False(string.IsNullOrWhiteSpace(h.Descripcion));
            var esquema = JsonDocument.Parse(h.EsquemaEntradaJson).RootElement;
            Assert.Equal("object", esquema.GetProperty("type").GetString());
            Assert.False(esquema.GetProperty("additionalProperties").GetBoolean());
        }
    }

    // ------------------------------------------------------------------ valor y fuente

    /// <summary>Una entrada válida por herramienta, la mínima que acepta.</summary>
    public static IEnumerable<object[]> EntradasValidas()
    {
        yield return ["consultar_piso_sicetac", """{"origen":"Medellín","destino":"Bogotá"}"""];
        yield return ["consultar_tarifa_plaza_sap", """{"q":"Medellín"}"""];
        yield return ["consultar_tarifa_plaza_nexo", """{"q":"Medellín"}"""];
        yield return ["consultar_historico_sap", """{"q":"Medellín"}"""];
        yield return ["consultar_aprovisionamiento", """{"ciudadLevante":"Cartagena","origen":"Medellín"}"""];
        yield return ["consultar_propuesta_linea", $$"""{"solicitudId":"{{SolicitudId}}","lineaId":"{{LineaId}}"}"""];
        yield return ["consultar_servicios_conexos", """{"q":"escolta"}"""];
        yield return ["buscar_ciudades_dane", """{"q":"Med"}"""];
    }

    [Theory]
    [MemberData(nameof(EntradasValidas))]
    public async Task Cada_herramienta_con_datos_devuelve_valor_y_fuente(string nombre, string json)
    {
        var r = await Ejecutar(nombre, json);

        Assert.True(r.Encontrado);
        Assert.NotNull(r.Valor);
        Assert.False(string.IsNullOrWhiteSpace(r.Fuente));
    }

    [Theory]
    [MemberData(nameof(EntradasValidas))]
    public async Task Cada_herramienta_sin_datos_lo_dice_y_conserva_la_fuente_en_lugar_de_inventar(string nombre, string json)
    {
        SinDatos();
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(Solicitud(UsuarioId));
        _propuestaSolicitud.Setup(h => h.Handle(It.IsAny<clsProponerPropuestaTarifaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPropuestaSolicitudDto(SolicitudId, [Linea() with { ValorPropuesto = null, Sicetac = null }], []));

        var r = await Ejecutar(nombre, json);

        Assert.False(string.IsNullOrWhiteSpace(r.Fuente));
        if (nombre == "consultar_propuesta_linea")
        {
            // La propuesta siempre existe como respuesta; lo que falta es el valor, y se ve en el propio resultado.
            Assert.Null(((clsPropuestaLineaDto)r.Valor!).ValorPropuesto);
            return;
        }
        Assert.False(r.Encontrado);
        Assert.Null(r.Valor);
        Assert.False(string.IsNullOrWhiteSpace(r.Nota));
    }

    [Theory]
    [InlineData("consultar_piso_sicetac", """{"origen":"Medellín","destino":"Bogotá"}""")]
    [InlineData("consultar_tarifa_plaza_sap", """{"q":"Medellín"}""")]
    [InlineData("consultar_historico_sap", """{"q":"Medellín"}""")]
    [InlineData("consultar_aprovisionamiento", """{"ciudadLevante":"Cartagena","origen":"Medellín"}""")]
    public async Task Si_la_fuente_externa_no_responde_la_herramienta_informa_que_no_hay_valor_en_lugar_de_fallar(string nombre, string json)
    {
        var caida = new clsServiceUnavailableException("DW_NO_CONFIGURADO", "sin conexión");
        _propuestas.Setup(p => p.PisoSicetacVigenteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TipologiaCarga?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(caida);
        _propuestas.Setup(p => p.EstimarAprovisionamientoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ThrowsAsync(caida);
        _plazaSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapPlazaQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(caida);
        _historicoSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapHistoricoQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(caida);

        var r = await Ejecutar(nombre, json);

        Assert.False(r.Encontrado);
        Assert.Null(r.Valor);
        Assert.False(string.IsNullOrWhiteSpace(r.Fuente));
        Assert.Contains("no está disponible", r.Nota);
    }

    // ------------------------------------------------------------------ entradas

    [Theory]
    [InlineData("[]")]
    [InlineData("\"texto\"")]
    [InlineData("null")]
    public async Task La_entrada_debe_ser_un_objeto(string json)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Ejecutar("buscar_ciudades_dane", json));

        Assert.Equal("HERRAMIENTA_ENTRADA_INVALIDA", ex.Code);
    }

    [Theory]
    [InlineData("consultar_piso_sicetac", """{"origen":"Medellín"}""")]                            // falta destino
    [InlineData("consultar_piso_sicetac", """{"origen":"Medellín","destino":"Bogotá","tipologia":"Camion"}""")]
    [InlineData("consultar_piso_sicetac", """{"origen":"Medellín","destino":7}""")]                // destino debe ser texto
    [InlineData("consultar_tarifa_plaza_sap", """{}""")]                                           // sin ningún filtro
    [InlineData("consultar_tarifa_plaza_sap", """{"q":"Medellín","top":26}""")]                    // top fuera de rango
    [InlineData("consultar_tarifa_plaza_sap", """{"q":"Medellín","top":0}""")]
    [InlineData("consultar_tarifa_plaza_sap", """{"claseViaje":"cupo"}""")]                        // debe ser número
    [InlineData("consultar_historico_sap", """{}""")]
    [InlineData("consultar_tarifa_plaza_nexo", """{}""")]
    [InlineData("consultar_aprovisionamiento", """{"origen":"Medellín"}""")]
    [InlineData("consultar_propuesta_linea", """{"solicitudId":"no-es-guid","lineaId":"44444444-4444-4444-4444-444444444444"}""")]
    [InlineData("consultar_propuesta_linea", """{"solicitudId":"33333333-3333-3333-3333-333333333333"}""")]
    [InlineData("buscar_ciudades_dane", """{}""")]
    [InlineData("buscar_ciudades_dane", """{"q":"   "}""")]                                       // solo espacios = no enviado
    public async Task Una_entrada_que_no_cumple_el_esquema_se_rechaza_sin_consultar_nada(string nombre, string json)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Ejecutar(nombre, json));

        Assert.Equal("HERRAMIENTA_ENTRADA_INVALIDA", ex.Code);
        Assert.StartsWith(nombre, ex.Message);
        _plazaSap.VerifyNoOtherCalls();
        _historicoSap.VerifyNoOtherCalls();
        _plazaNexo.VerifyNoOtherCalls();
        _ciudades.VerifyNoOtherCalls();
        _propuestaSolicitud.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_numero_enviado_como_texto_se_acepta_y_el_tope_por_defecto_es_10()
    {
        await Ejecutar("consultar_tarifa_plaza_sap", """{"claseViaje":"3"}""");

        _plazaSap.Verify(h => h.Handle(
            It.Is<clsBuscarSapPlazaQuery>(q => q.ClaseViaje == 3 && q.Top == 10 && q.Page == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------ piso SICE-TAC

    [Fact]
    public async Task El_piso_sicetac_cita_el_periodo_en_la_fuente_y_reenvia_los_filtros()
    {
        var r = await Ejecutar("consultar_piso_sicetac",
            """{"origen":" Medellín ","destino":"Bogotá","vehiculo":"3S3","carroceria":"FURGON","tipologia":"contenedor"}""");

        Assert.Equal("SICE-TAC (DWSice), periodo 202610", r.Fuente);
        Assert.Equal(2_900_000m, ((clsPisoSicetacVigenteDto)r.Valor!).ValorPiso);
        _propuestas.Verify(p => p.PisoSicetacVigenteAsync(
            "Medellín", "Bogotá", "3S3", TipologiaCarga.Contenedor, "FURGON", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_piso_se_informa_el_motivo_del_periodo_cuando_el_puerto_lo_da()
    {
        SinDatos();
        _propuestas.Setup(p => p.MotivoSicetacNoUsadoAsync(It.IsAny<CancellationToken>())).ReturnsAsync("El periodo cargado es 202609.");

        var r = await Ejecutar("consultar_piso_sicetac", """{"origen":"Medellín","destino":"Bogotá"}""");

        Assert.False(r.Encontrado);
        Assert.Equal("El periodo cargado es 202609.", r.Nota);
    }

    // ------------------------------------------------------------------ SAP (plaza e histórico)

    [Fact]
    public async Task La_plaza_sap_y_el_historico_consultan_cada_uno_su_propia_fuente()
    {
        var plaza = await Ejecutar("consultar_tarifa_plaza_sap", """{"q":"Medellín"}""");
        var historico = await Ejecutar("consultar_historico_sap", """{"q":"Medellín"}""");

        Assert.Contains("tblSAPTarifas", plaza.Fuente);
        Assert.Contains("tblSAPTarifasLog", historico.Fuente);
        _plazaSap.Verify(h => h.Handle(It.IsAny<clsBuscarSapPlazaQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _historicoSap.Verify(h => h.Handle(It.IsAny<clsBuscarSapHistoricoQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task La_plaza_sap_entrega_una_fila_resumida_con_nombres_y_la_vigencia_ajustada_si_existe()
    {
        var ajustada = new DateTime(2026, 9, 1);
        _plazaSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSapHistoricoResultDto(1, [FilaSap(vigenciaAjustada: ajustada)]));

        var r = await Ejecutar("consultar_tarifa_plaza_sap", """{"q":"Medellín"}""");

        var fila = Assert.Single(((clsListadoAsistente<clsTarifaSapAsistenteDto>)r.Valor!).Items);
        Assert.Equal("Medellín", fila.Origen);
        Assert.Equal("Bogotá", fila.Destino);
        Assert.Equal("3S3 · Tractomula", fila.TipoVehiculo);
        Assert.Equal(3_500_000m, fila.Valor);
        Assert.Equal(ajustada, fila.Vigencia);
        Assert.Null(r.Nota);
    }

    [Fact]
    public async Task Cuando_hay_mas_filas_que_el_tope_se_avisa_que_es_una_muestra()
    {
        _plazaSap.Setup(h => h.Handle(It.IsAny<clsBuscarSapPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSapHistoricoResultDto(87, [FilaSap(), FilaSap()]));

        var r = await Ejecutar("consultar_tarifa_plaza_sap", """{"q":"Medellín","top":2}""");

        var listado = (clsListadoAsistente<clsTarifaSapAsistenteDto>)r.Valor!;
        Assert.Equal(87, listado.Total);
        Assert.Contains("2 de 87", r.Nota);
    }

    // ------------------------------------------------------------------ plaza propia de NEXO

    [Fact]
    public async Task La_plaza_de_nexo_filtra_sin_tildes_ni_mayusculas_y_recorta_al_tope()
    {
        _plazaNexo.Setup(h => h.Handle(It.IsAny<clsListarTarifasPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([TarifaNexo("Medellín", "Bogotá"), TarifaNexo("Medellín", "Cali"), TarifaNexo("Cartagena", "Bogotá")]);

        var r = await Ejecutar("consultar_tarifa_plaza_nexo", """{"q":"MEDELLIN","top":1}""");

        var listado = (clsListadoAsistente<clsTarifaPlazaNexoAsistenteDto>)r.Valor!;
        Assert.Equal(2, listado.Total);
        Assert.Single(listado.Items);
        Assert.Contains("1 de 2", r.Nota);
        Assert.Contains("tbl_tarifas_plaza", r.Fuente);
    }

    [Fact]
    public async Task La_plaza_de_nexo_combina_los_filtros()
    {
        _plazaNexo.Setup(h => h.Handle(It.IsAny<clsListarTarifasPlazaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([TarifaNexo("Medellín", "Bogotá", "3S3"), TarifaNexo("Medellín", "Bogotá", "C2")]);

        var r = await Ejecutar("consultar_tarifa_plaza_nexo", """{"q":"Bogota","tipoVehiculo":"c2"}""");

        var fila = Assert.Single(((clsListadoAsistente<clsTarifaPlazaNexoAsistenteDto>)r.Valor!).Items);
        Assert.Equal("C2", fila.TipoVehiculo);
    }

    // ------------------------------------------------------------------ aprovisionamiento

    [Fact]
    public async Task El_aprovisionamiento_reenvia_levante_origen_configuracion_y_carroceria()
    {
        await Ejecutar("consultar_aprovisionamiento", """{"ciudadLevante":"Cartagena","origen":"Medellín","configuracion":"3S3","carroceria":"FURGON"}""");

        _propuestas.Verify(p => p.EstimarAprovisionamientoAsync(
            "Cartagena", "Medellín", "3S3", It.IsAny<CancellationToken>(), "FURGON"), Times.Once);
    }

    // ------------------------------------------------------------------ propuesta de una línea

    [Fact]
    public async Task La_propuesta_trae_el_valor_la_fuente_propuesta_y_el_motivo_de_pendiente()
    {
        _propuestaSolicitud.Setup(h => h.Handle(It.IsAny<clsProponerPropuestaTarifaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPropuestaSolicitudDto(SolicitudId, [Linea("Falta el vehículo")], ["El periodo SICE-TAC no es el actual."]));

        var r = await Ejecutar("consultar_propuesta_linea", $$"""{"solicitudId":"{{SolicitudId}}","lineaId":"{{LineaId}}"}""");

        var linea = (clsPropuestaLineaDto)r.Valor!;
        Assert.Equal(3_000_000m, linea.ValorPropuesto);
        Assert.Equal("SICE-TAC", linea.FuentePropuesta);
        Assert.Equal("Falta el vehículo", linea.MotivoPendiente);
        Assert.Equal("El periodo SICE-TAC no es el actual.", r.Nota);
    }

    [Fact]
    public async Task Una_linea_que_no_esta_en_la_solicitud_es_404_y_no_se_filtra_la_de_otra()
    {
        _propuestaSolicitud.Setup(h => h.Handle(It.IsAny<clsProponerPropuestaTarifaQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPropuestaSolicitudDto(SolicitudId, [Linea()], []));
        var otra = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Ejecutar("consultar_propuesta_linea", $$"""{"solicitudId":"{{SolicitudId}}","lineaId":"{{otra}}"}"""));

        Assert.Equal("LINEA_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Una_solicitud_inexistente_es_404_y_no_calcula_la_propuesta()
    {
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>())).ReturnsAsync((clsSolicitud?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Ejecutar("consultar_propuesta_linea", $$"""{"solicitudId":"{{SolicitudId}}","lineaId":"{{LineaId}}"}"""));

        Assert.Equal("SOLICITUD_NO_ENCONTRADA", ex.Code);
        _propuestaSolicitud.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_comercial_no_ve_la_propuesta_de_una_solicitud_que_creo_otra_persona()
    {
        ComoRol(RolUsuario.Comercial);
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(Solicitud(OtroUsuarioId));

        var ex = await Assert.ThrowsAsync<clsForbiddenException>(() =>
            Ejecutar("consultar_propuesta_linea", $$"""{"solicitudId":"{{SolicitudId}}","lineaId":"{{LineaId}}"}"""));

        Assert.Equal("SOLICITUD_AJENA", ex.Code);
        _propuestaSolicitud.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_comercial_si_ve_la_propuesta_de_su_propia_solicitud()
    {
        ComoRol(RolUsuario.Comercial);

        var r = await Ejecutar("consultar_propuesta_linea", $$"""{"solicitudId":"{{SolicitudId}}","lineaId":"{{LineaId}}"}""");

        Assert.True(r.Encontrado);
    }

    [Theory]
    [InlineData(RolUsuario.AdminTarifas)]
    [InlineData(RolUsuario.ServiciosLogisticos)]
    [InlineData(RolUsuario.LiderProceso)]
    [InlineData(RolUsuario.Admin)]
    public async Task Los_demas_roles_de_la_mesa_ven_la_propuesta_de_cualquier_solicitud(RolUsuario rol)
    {
        ComoRol(rol);
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(Solicitud(OtroUsuarioId));

        var r = await Ejecutar("consultar_propuesta_linea", $$"""{"solicitudId":"{{SolicitudId}}","lineaId":"{{LineaId}}"}""");

        Assert.True(r.Encontrado);
    }

    // ------------------------------------------------------------------ conexos y ciudades

    [Fact]
    public async Task Los_conexos_solo_traen_servicios_activos_y_la_pagina_es_el_tope()
    {
        await Ejecutar("consultar_servicios_conexos", """{"q":"escolta","categoria":"Seguridad","top":5}""");

        _conexos.Verify(h => h.Handle(
            new clsListarServiciosConexosQuery(true, "escolta", "Seguridad", 1, 5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Los_conexos_sin_filtros_son_validos_y_avisan_cuando_hay_mas_de_los_mostrados()
    {
        _conexos.Setup(h => h.Handle(It.IsAny<clsListarServiciosConexosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPaginaServiciosDto([Conexo()], 40));

        var r = await Ejecutar("consultar_servicios_conexos", "{}");

        Assert.True(r.Encontrado);
        Assert.Contains("1 de 40", r.Nota);
    }

    [Fact]
    public async Task Las_ciudades_traen_el_codigo_dane_y_se_recortan_al_tope()
    {
        _ciudades.Setup(h => h.Handle(It.IsAny<clsBuscarCiudadesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 30).Select(i => new clsCiudadBusquedaDto(i, $"0500{i}000", $"Ciudad {i}", "Antioquia", null, null)).ToList());

        var r = await Ejecutar("buscar_ciudades_dane", """{"departamento":"Antioquia"}""");

        var listado = (clsListadoAsistente<clsCiudadBusquedaDto>)r.Valor!;
        Assert.Equal(30, listado.Total);
        Assert.Equal(10, listado.Items.Count);
        Assert.Equal("05001000", listado.Items[0].CodigoDane);
        _ciudades.Verify(h => h.Handle(new clsBuscarCiudadesQuery("Antioquia", null), It.IsAny<CancellationToken>()), Times.Once);
    }
}
