using Moq;
using Nexo.Application.CasosDeUso.DiccionarioCliente.Interfaces;
using Nexo.Application.CasosDeUso.MapeoExcel;
using Nexo.Application.CasosDeUso.MapeoExcel.DTOs;
using Nexo.Application.CasosDeUso.PlantillasMapeo;
using Nexo.Application.CasosDeUso.PlantillasMapeo.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.Tests.MapeoExcel;

/// <summary>
/// Las plantillas de mapeo por cliente: la firma que reconoce los mismos encabezados, guardar o reemplazar, y reusar la plantilla en
/// <c>sugerir-mapeo</c> sin llamar a la IA mientras los encabezados no cambien.
/// </summary>
public class clsPlantillasMapeoTests
{
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid Usuario = Guid.NewGuid();

    private readonly Mock<IPlantillasMapeoRepository> _repo = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAsistenteIaPort> _ia = new();
    private readonly Mock<IDiccionarioClienteRepository> _dic = new();
    private readonly List<clsPlantillaMapeoCliente> _agregadas = [];

    public clsPlantillasMapeoTests()
    {
        _usuario.SetupGet(u => u.Id).Returns(Usuario);
        _repo.Setup(r => r.ClienteExisteAsync(Cliente, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.Agregar(It.IsAny<clsPlantillaMapeoCliente>())).Callback<clsPlantillaMapeoCliente>(_agregadas.Add);
        _ia.Setup(i => i.Habilitada(clsFuncionIa.ImportacionExcel)).Returns(true);
        _dic.Setup(d => d.ListarAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private clsGuardarPlantillaMapeoCommandHandler Guardador() => new(_repo.Object, _auditoria.Object, _usuario.Object, _uow.Object, TimeProvider.System,
        new clsClienteDeImportacion(new Mock<IClientesPort>().Object, new Mock<IClientesDwPort>().Object, new Mock<IProspectosDwPort>().Object));

    private static clsColumnaAAplicar Col(int indice, string campo, string? transformacion = null) => new(indice, campo, transformacion);

    private static readonly string[] Encabezados = ["Ciudad origen", "Ciudad destino", "Peso (TON)", "NIT"];

    private static clsColumnaAAplicar[] Mapeo => [Col(0, "origen"), Col(1, "destino"), Col(2, "peso", "peso_toneladas_a_kg"), Col(3, "ignorar")];

    private static clsGuardarPlantillaMapeoCommand Guardar(
        string?[]? encabezados = null, clsColumnaAAplicar[]? columnas = null, Guid? cliente = null, string? nombre = null) =>
        new(cliente ?? Cliente, nombre, encabezados ?? Encabezados, columnas ?? Mapeo);

    // ---------------------------------------------------------------- la firma

    [Fact]
    public void La_firma_es_un_SHA256_en_hexadecimal()
    {
        var firma = clsFirmaDeEncabezados.Calcular(Encabezados);

        Assert.Equal(64, firma.Length);
        Assert.Matches("^[0-9a-f]{64}$", firma);
        Assert.Equal(firma, clsFirmaDeEncabezados.Calcular(Encabezados)); // determinista
    }

    [Fact]
    public void Mayusculas_tildes_y_espacios_de_mas_no_cambian_la_firma()
    {
        var a = clsFirmaDeEncabezados.Calcular(["Ciudad Origen", "Peso (TON)", "Observación"]);
        var b = clsFirmaDeEncabezados.Calcular(["  CIUDAD   ORIGEN ", "peso (ton)", "OBSERVACION"]);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Cambiar_agregar_quitar_o_reordenar_una_columna_cambia_la_firma()
    {
        var original = clsFirmaDeEncabezados.Calcular(["Origen", "Destino", "Peso"]);

        Assert.NotEqual(original, clsFirmaDeEncabezados.Calcular(["Origen", "Destino", "Peso kg"]));
        Assert.NotEqual(original, clsFirmaDeEncabezados.Calcular(["Origen", "Destino", "Peso", "Notas"]));
        Assert.NotEqual(original, clsFirmaDeEncabezados.Calcular(["Origen", "Destino"]));
        Assert.NotEqual(original, clsFirmaDeEncabezados.Calcular(["Destino", "Origen", "Peso"]));
        Assert.NotEqual(clsFirmaDeEncabezados.Calcular(["A", "B"]), clsFirmaDeEncabezados.Calcular(["A|B"])); // el separador no confunde
        Assert.NotEqual(clsFirmaDeEncabezados.Calcular(["A", "", "B"]), clsFirmaDeEncabezados.Calcular(["A", "B"])); // una columna sin encabezado en el medio cuenta
        Assert.NotEqual(clsFirmaDeEncabezados.Calcular(["Peso(kg)"]), clsFirmaDeEncabezados.Calcular(["Peso (kg)"])); // la puntuación distingue
    }

    [Fact]
    public void Los_espacios_raros_los_invisibles_y_las_columnas_vacias_del_final_no_cambian_la_firma()
    {
        var limpia = clsFirmaDeEncabezados.Calcular(["Peso (kg)", "Ciudad origen"]);

        Assert.Equal(limpia, clsFirmaDeEncabezados.Calcular(["Peso\u00A0(kg)", "Ciudad\torigen"]));              // espacio de no separación y tabulador
        Assert.Equal(limpia, clsFirmaDeEncabezados.Calcular(["Peso\n(kg)", "Ciudad \r\n origen"]));            // saltos de línea dentro de la celda
        Assert.Equal(limpia, clsFirmaDeEncabezados.Calcular(["\uFEFFPeso (kg)", "Ciudad\u200B origen"]));       // marca de orden de bytes y ancho cero
        Assert.Equal(limpia, clsFirmaDeEncabezados.Calcular(["Peso (kg)", "Ciudad origen", "", null, "   "]));   // columnas de más al final
        Assert.Equal(limpia, clsFirmaDeEncabezados.Calcular(["peso_(KG)", "CIUDAD-ORIGEN"]));                    // _ y - cuentan como espacio
        Assert.Equal(2, clsFirmaDeEncabezados.Cantidad(["Peso (kg)", "Ciudad origen", "", null]));
        Assert.Equal(3, clsFirmaDeEncabezados.Cantidad(["Peso (kg)", "", "Ciudad origen"]));
    }

    [Fact]
    public async Task Guardar_y_buscar_calculan_la_misma_firma_aunque_el_archivo_traiga_columnas_vacias_de_mas()
    {
        var guardada = PlantillaGuardada(Encabezados, ColumnasGuardadas);
        ConPlantilla(guardada);

        var r = await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(Cliente, [.. Encabezados, "", " ", ""], null));

        Assert.Equal(guardada.Id, r.PlantillaId);
        Assert.Equal(7, r.Columnas.Count);
        Assert.All(r.Columnas.Skip(4), c => Assert.Equal("ignorar", c.Campo));
    }

    // ---------------------------------------------------------------- guardar

    [Fact]
    public async Task La_primera_vez_crea_la_plantilla_con_su_firma_y_el_mapeo_canonico()
    {
        var r = await Guardador().Handle(Guardar(columnas: [Col(2, "PESO", "PESO_TONELADAS_A_KG"), Col(0, "Origen"), Col(1, "destino"), Col(3, "ignorar")], nombre: "  Plantilla  BASF "));

        Assert.True(r.Creada);
        var p = Assert.Single(_agregadas);
        Assert.Equal((Cliente, "Plantilla BASF", clsFirmaDeEncabezados.Calcular(Encabezados)), (p.ClienteId, p.Nombre, p.FirmaEncabezados));
        Assert.Equal(
            [(0, "origen", "ninguna"), (1, "destino", "ninguna"), (2, "peso", "peso_toneladas_a_kg"), (3, "ignorar", "ninguna")],
            r.Plantilla.Columnas.Select(c => (c.Indice, c.Campo, c.Transformacion)));
        Assert.Equal(Encabezados, r.Plantilla.Encabezados);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditoria.Verify(a => a.Registrar(Usuario, "plantilla_mapeo_cliente", p.Id, "crear_plantilla", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Guardar_con_los_mismos_encabezados_reemplaza_la_plantilla_y_no_crea_otra()
    {
        var existente = new clsPlantillaMapeoCliente { ClienteId = Cliente, FirmaEncabezados = clsFirmaDeEncabezados.Calcular(Encabezados), Encabezados = "[]", Columnas = """{"version":1,"columnas":[]}""" };
        _repo.Setup(r => r.ObtenerParaActualizarAsync(Cliente, existente.FirmaEncabezados, It.IsAny<CancellationToken>())).ReturnsAsync(existente);

        var r = await Guardador().Handle(Guardar(columnas: [Col(0, "origen"), Col(1, "destino"), Col(2, "peso")]));

        Assert.False(r.Creada);
        Assert.Empty(_agregadas);
        Assert.NotNull(existente.UpdatedAt);
        Assert.Equal(3, r.Plantilla.Columnas.Count);
        _auditoria.Verify(a => a.Registrar(Usuario, "plantilla_mapeo_cliente", existente.Id, "reemplazar_plantilla", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Los_encabezados_con_otra_mayuscula_o_tilde_encuentran_la_misma_plantilla()
    {
        string? firmaBuscada = null;
        _repo.Setup(r => r.ObtenerParaActualizarAsync(Cliente, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, CancellationToken>((_, f, _) => firmaBuscada = f).ReturnsAsync((clsPlantillaMapeoCliente?)null);

        await Guardador().Handle(Guardar(encabezados: ["CIUDAD ORIGEN", "ciudad destino", "peso (ton)", "nit"]));

        Assert.Equal(clsFirmaDeEncabezados.Calcular(Encabezados), firmaBuscada);
    }

    [Fact]
    public async Task Una_entrada_invalida_se_rechaza_con_su_codigo_y_no_guarda_nada()
    {
        async Task Esperar(clsGuardarPlantillaMapeoCommand comando, string codigo) =>
            Assert.Equal(codigo, (await Assert.ThrowsAsync<clsValidationFailedException>(() => Guardador().Handle(comando))).Code);

        await Esperar(new(null, null, Encabezados, Mapeo), "CLIENTE_REQUERIDO");
        await Esperar(Guardar(cliente: Guid.NewGuid()), "CLIENTE_NO_ENCONTRADO");
        await Esperar(Guardar(nombre: new string('x', 101)), "NOMBRE_DEMASIADO_LARGO");
        await Esperar(new(Cliente, null, null, Mapeo), "ENCABEZADOS_REQUERIDOS");
        await Esperar(new(Cliente, null, [], Mapeo), "ENCABEZADOS_REQUERIDOS");
        await Esperar(Guardar(encabezados: Enumerable.Range(0, 101).Select(i => $"C{i}").ToArray()), "DEMASIADAS_COLUMNAS");
        await Esperar(Guardar(encabezados: [new string('x', 201), "B", "C", "D"]), "ENCABEZADO_DEMASIADO_LARGO");
        await Esperar(new(Cliente, null, Encabezados, null), "MAPEO_REQUERIDO");
        await Esperar(Guardar(columnas: [Col(0, "origen"), Col(1, "inventado")]), "CAMPO_INVALIDO");
        await Esperar(Guardar(columnas: [Col(0, "origen"), Col(1, "destino", "peso_toneladas_a_kg")]), "TRANSFORMACION_NO_APLICA");
        await Esperar(Guardar(columnas: [Col(0, "origen"), Col(1, "origen")]), "CAMPO_DUPLICADO");
        await Esperar(Guardar(columnas: [Col(0, "origen")]), "CAMPO_REQUERIDO");
        await Esperar(Guardar(columnas: [Col(0, "origen"), Col(1, "destino"), Col(4, "peso")]), "COLUMNA_FUERA_DE_RANGO");

        Assert.Empty(_agregadas);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- listar y eliminar

    [Fact]
    public async Task Listar_devuelve_las_plantillas_con_sus_encabezados_y_columnas()
    {
        var p = new clsPlantillaMapeoCliente
        {
            ClienteId = Cliente, Nombre = "BASF", FirmaEncabezados = "f", Cliente = new clsCliente { Nombre = "BASF SA" },
            Encabezados = """["A","B"]""", Columnas = """{"version":1,"columnas":[{"indice":0,"campo":"origen","transformacion":"ninguna"}]}""",
        };
        _repo.Setup(r => r.ListarAsync(Cliente, It.IsAny<CancellationToken>())).ReturnsAsync([p]);

        var lista = await new clsListarPlantillasMapeoQueryHandler(_repo.Object).Handle(new(Cliente));

        var dto = Assert.Single(lista);
        Assert.Equal(("BASF SA", "BASF"), (dto.ClienteNombre, dto.Nombre));
        Assert.Equal(["A", "B"], dto.Encabezados);
        Assert.Equal([(0, "origen", "ninguna")], dto.Columnas.Select(c => (c.Indice, c.Campo, c.Transformacion)));
    }

    [Fact]
    public async Task Eliminar_borra_la_plantilla_y_si_no_existe_es_404()
    {
        var p = new clsPlantillaMapeoCliente { ClienteId = Cliente, FirmaEncabezados = "f", Encabezados = "[]", Columnas = """{"version":1,"columnas":[]}""" };
        _repo.Setup(r => r.ObtenerAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);
        var manejador = new clsEliminarPlantillaMapeoCommandHandler(_repo.Object, _auditoria.Object, _usuario.Object, _uow.Object);

        await manejador.Handle(new(p.Id));

        _repo.Verify(r => r.Eliminar(p), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => manejador.Handle(new(Guid.NewGuid())));
        Assert.Equal("PLANTILLA_NO_ENCONTRADA", ex.Code);
    }

    // ---------------------------------------------------------------- reusar en sugerir-mapeo

    private clsSugerirMapeoExcelQueryHandler Sugeridor() => new(_ia.Object, _dic.Object, _usuario.Object, _repo.Object);

    private clsPlantillaMapeoCliente PlantillaGuardada(string[] encabezados, string columnas) => new()
    {
        ClienteId = Cliente, FirmaEncabezados = clsFirmaDeEncabezados.Calcular(encabezados),
        Encabezados = System.Text.Json.JsonSerializer.Serialize(encabezados), Columnas = columnas,
    };

    private const string ColumnasGuardadas = """
        {"version":1,"columnas":[{"indice":0,"campo":"origen","transformacion":"ninguna"},{"indice":1,"campo":"destino","transformacion":"ninguna"},
         {"indice":2,"campo":"peso","transformacion":"peso_toneladas_a_kg"},{"indice":3,"campo":"ignorar","transformacion":"ninguna"}]}
        """;

    private void ConPlantilla(clsPlantillaMapeoCliente p) =>
        _repo.Setup(r => r.BuscarPorFirmaAsync(Cliente, p.FirmaEncabezados, It.IsAny<CancellationToken>())).ReturnsAsync(p);

    [Fact]
    public async Task Con_los_mismos_encabezados_se_usa_la_plantilla_y_no_se_llama_a_la_IA()
    {
        var p = PlantillaGuardada(Encabezados, ColumnasGuardadas);
        ConPlantilla(p);

        var r = await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(Cliente, ["CIUDAD ORIGEN", "ciudad destino", "Peso (ton)", "Nit"], null));

        Assert.False(r.IaUsada);
        Assert.Equal(("PLANTILLA_RECONOCIDA", p.Id), (r.MotivoSinIa, r.PlantillaId));
        Assert.Equal(
            [("origen", "ninguna"), ("destino", "ninguna"), ("peso", "peso_toneladas_a_kg"), ("ignorar", "ninguna")],
            r.Columnas.Select(c => (c.Campo, c.Transformacion)));
        Assert.All(r.Columnas, c => Assert.Equal(("plantilla", 1.0), (c.Origen, c.Confianza)));
        Assert.Equal(["CIUDAD ORIGEN", "ciudad destino", "Peso (ton)", "Nit"], r.Columnas.Select(c => c.Columna)); // los encabezados del archivo que llegó
        Assert.Empty(r.Dudas);
        _ia.Verify(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()), Times.Never);
        _dic.Verify(d => d.ListarAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Peso kg")]            // cambia un encabezado
    [InlineData("Peso (TON) total")]
    public async Task Si_cambia_un_encabezado_no_hay_plantilla_y_se_vuelve_a_sugerir(string pesoNuevo)
    {
        ConPlantilla(PlantillaGuardada(Encabezados, ColumnasGuardadas));
        _ia.Setup(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsRespuestaIa("{}", """{"columnas":[],"dudas":[]}""", "modelo", 1, 1, TimeSpan.Zero));

        var r = await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(Cliente, ["Ciudad origen", "Ciudad destino", pesoNuevo, "NIT"], null));

        Assert.True(r.IaUsada);
        Assert.Null(r.PlantillaId);
        _ia.Verify(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Agregar_quitar_o_reordenar_columnas_tambien_vuelve_a_sugerir()
    {
        ConPlantilla(PlantillaGuardada(Encabezados, ColumnasGuardadas));
        _ia.Setup(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsRespuestaIa("{}", """{"columnas":[],"dudas":[]}""", "modelo", 1, 1, TimeSpan.Zero));

        foreach (var otros in new[] { new[] { "Ciudad origen", "Ciudad destino", "Peso (TON)" }, new[] { "Ciudad destino", "Ciudad origen", "Peso (TON)", "NIT" }, [.. Encabezados, "Notas"] })
            Assert.Null((await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(Cliente, otros, null))).PlantillaId);

        _ia.Verify(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Sin_cliente_no_se_busca_plantilla()
    {
        _ia.Setup(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsRespuestaIa("{}", """{"columnas":[],"dudas":[]}""", "modelo", 1, 1, TimeSpan.Zero));

        await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(null, Encabezados, null));

        _repo.Verify(r => r.BuscarPorFirmaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_plantilla_con_el_JSON_danado_se_ignora_y_se_sugiere_de_nuevo()
    {
        ConPlantilla(PlantillaGuardada(Encabezados, "esto no es json"));
        _ia.Setup(i => i.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsRespuestaIa("{}", """{"columnas":[],"dudas":[]}""", "modelo", 1, 1, TimeSpan.Zero));

        var r = await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(Cliente, Encabezados, null));

        Assert.True(r.IaUsada);
        Assert.Null(r.PlantillaId);
    }

    private async Task<clsSugerenciaMapeoDto> SugerirConIaVacia(string[] encabezados)
    {
        _ia.Setup(x => x.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsRespuestaIa("{}", """{"columnas":[],"dudas":[]}""", "modelo", 1, 1, TimeSpan.Zero));
        return await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(Cliente, encabezados, null));
    }

    [Fact]
    public async Task Las_columnas_que_la_plantilla_no_menciona_se_ignoran()
    {
        ConPlantilla(PlantillaGuardada(Encabezados, """
            {"version":1,"columnas":[{"indice":0,"campo":"origen","transformacion":"ninguna"},{"indice":1,"campo":"destino","transformacion":"ninguna"}]}
            """));

        var r = await Sugeridor().Handle(new clsSugerirMapeoExcelQuery(Cliente, Encabezados, null));

        Assert.Equal("PLANTILLA_RECONOCIDA", r.MotivoSinIa);
        Assert.Equal(["origen", "destino", "ignorar", "ignorar"], r.Columnas.Select(c => c.Campo));
    }

    [Theory]
    // Un campo que ya no existe en NEXO.
    [InlineData("""{"version":1,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"campo_retirado"},{"indice":2,"campo":"destino"}]}""")]
    // Una conversión que ya no existe, o que no corresponde al campo.
    [InlineData("""{"version":1,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino","transformacion":"conversion_retirada"}]}""")]
    [InlineData("""{"version":1,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino"},{"indice":2,"campo":"peso","transformacion":"dividir_origen_destino"}]}""")]
    // Ya no cumple las reglas del mapeo: sin destino, o dos columnas en el mismo campo.
    [InlineData("""{"version":1,"columnas":[{"indice":0,"campo":"origen"}]}""")]
    [InlineData("""{"version":1,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"origen"},{"indice":2,"campo":"destino"}]}""")]
    // Una columna que se usa y ya no cabe en el archivo.
    [InlineData("""{"version":1,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino"},{"indice":9,"campo":"peso"}]}""")]
    // Una forma que este código no conoce: otra versión, sin versión, o que no es el objeto esperado.
    [InlineData("""{"version":2,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino"}]}""")]
    [InlineData("""[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino"}]""")]
    [InlineData("""{"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino"}]}""")]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"columnas":[null]}""")]
    [InlineData("""{"version":1,"columnas":"x"}""")]
    [InlineData("esto no es json")]
    public async Task Una_plantilla_que_ya_no_sirve_no_se_usa_se_sugiere_de_nuevo_y_se_avisa(string columnas)
    {
        ConPlantilla(PlantillaGuardada(Encabezados, columnas));

        var r = await SugerirConIaVacia(Encabezados);

        Assert.True(r.IaUsada);
        Assert.Null(r.PlantillaId);
        Assert.NotEqual("PLANTILLA_RECONOCIDA", r.MotivoSinIa);
        Assert.Contains(clsSugerirMapeoExcelQueryHandler.DudaPlantillaObsoleta, r.Dudas);
        _ia.Verify(x => x.CompletarAsync(It.IsAny<clsPeticionIa>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_plantilla_con_los_encabezados_guardados_danados_tambien_se_trata_como_vieja()
    {
        var p = PlantillaGuardada(Encabezados, ColumnasGuardadas);
        p.Encabezados = "no es json";
        ConPlantilla(p);

        var r = await SugerirConIaVacia(Encabezados);

        Assert.Null(r.PlantillaId);
        Assert.Contains(clsSugerirMapeoExcelQueryHandler.DudaPlantillaObsoleta, r.Dudas);
    }

    [Fact]
    public async Task Si_no_hay_plantilla_no_se_avisa_nada()
    {
        var r = await SugerirConIaVacia(Encabezados);

        Assert.DoesNotContain(clsSugerirMapeoExcelQueryHandler.DudaPlantillaObsoleta, r.Dudas);
    }

    [Fact]
    public async Task Las_columnas_se_guardan_con_su_version()
    {
        var r = await Guardador().Handle(Guardar());

        Assert.StartsWith("""{"version":1,"columnas":[""", _agregadas.Single().Columnas);
        Assert.Equal(4, r.Plantilla.Columnas.Count);
    }

    // ---------------------------------------------------------------- guardar sin duplicar ni romper

    [Fact]
    public async Task Un_archivo_sin_ningun_encabezado_con_texto_no_se_guarda()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Guardador().Handle(Guardar(encabezados: ["", " ", ""], columnas: [Col(0, "origen"), Col(1, "destino")])));
        Assert.Equal("ENCABEZADOS_REQUERIDOS", ex.Code);
    }

    [Fact]
    public async Task Guardar_con_columnas_vacias_de_mas_al_final_reemplaza_la_plantilla_y_no_crea_otra()
    {
        var existente = new clsPlantillaMapeoCliente { ClienteId = Cliente, FirmaEncabezados = clsFirmaDeEncabezados.Calcular(Encabezados), Encabezados = "[]", Columnas = "{}" };
        _repo.Setup(r => r.ObtenerParaActualizarAsync(Cliente, existente.FirmaEncabezados, It.IsAny<CancellationToken>())).ReturnsAsync(existente);

        var r = await Guardador().Handle(Guardar(encabezados: [.. Encabezados, "", ""]));

        Assert.False(r.Creada);
        Assert.Empty(_agregadas);
    }

    [Fact]
    public async Task Dos_confirmaciones_a_la_vez_la_segunda_reemplaza_y_no_falla()
    {
        // La otra confirmación insertó entre mi búsqueda y mi inserción: el primer intento choca con el índice único.
        var existente = new clsPlantillaMapeoCliente { ClienteId = Cliente, FirmaEncabezados = clsFirmaDeEncabezados.Calcular(Encabezados), Encabezados = "[]", Columnas = "{}" };
        var busquedas = 0;
        _repo.Setup(r => r.ObtenerParaActualizarAsync(Cliente, existente.FirmaEncabezados, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++busquedas == 1 ? null : existente);
        _repo.Setup(r => r.EsChoqueDeFirma(It.IsAny<Exception>())).Returns(true);
        _uow.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("choque")).ReturnsAsync(1);

        var r = await Guardador().Handle(Guardar());

        Assert.False(r.Creada);
        Assert.Equal(2, busquedas);
        _repo.Verify(x => x.DescartarCambiosPendientes(), Times.Once);
        _auditoria.Verify(a => a.Registrar(Usuario, "plantilla_mapeo_cliente", existente.Id, "reemplazar_plantilla", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Un_error_que_no_es_el_choque_de_firma_no_se_reintenta()
    {
        _repo.Setup(r => r.EsChoqueDeFirma(It.IsAny<Exception>())).Returns(false);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("otro error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Guardador().Handle(Guardar()));

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------- quién la confirmó

    [Fact]
    public async Task Borrar_queda_auditado_con_el_usuario_que_lo_hizo()
    {
        var p = new clsPlantillaMapeoCliente { ClienteId = Cliente, FirmaEncabezados = "f", Encabezados = "[]", Columnas = "{}" };
        _repo.Setup(r => r.ObtenerAsync(p.Id, It.IsAny<CancellationToken>())).ReturnsAsync(p);

        await new clsEliminarPlantillaMapeoCommandHandler(_repo.Object, _auditoria.Object, _usuario.Object, _uow.Object).Handle(new(p.Id));

        _auditoria.Verify(a => a.Registrar(Usuario, "plantilla_mapeo_cliente", p.Id, "eliminar_plantilla", It.IsAny<string>()), Times.Once);
    }
}
